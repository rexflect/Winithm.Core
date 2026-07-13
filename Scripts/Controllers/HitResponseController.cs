using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

public partial class HitResponseController : Node
{
  private Control? _hitFXLayer;
  private NoteController? _noteController;

  [Export] public Vector2 PlayerAreaSize { set; get; } = new(1280, 720);
  [Export] public float HitSoundVolume { set; get; } = 0.5f;

  private float _cachedHitSoundVolume = -1f;
  private float _cachedVolumeDb = 0f;

  private readonly Dictionary<PackedScene, NodePool<HitFX>> _hitFXPools = [];
  private readonly Dictionary<HitFX, PackedScene> _sceneByInstance = [];
  private NodePool<AudioStreamPlayer>? _hitSoundPool;

  // ─────────────────────────────────────────────────────────────────────────
  public void Initialize(Control hitFXLayer, NoteController noteController)
  {
    _hitFXLayer = hitFXLayer;
    _noteController = noteController;
  }

  // ─────────────────────────────────────────────────────────────────────────

  public void RequestHitResponse(string windowId, NoteData note, HitResult result, bool withSfx)
  {
    RequestHitFX(windowId, note, result);
    if (withSfx) RequestHitSound(note);
  }

  private void RequestHitSound(NoteData note)
  {
    if (note is null) {
      GD.PushError("[HitResponseController] NoteData is null");
      return;
    }

    // Get resource pack (override by note, otherwise use active)
    var resourcePack = note.ResourcePack;

    if (resourcePack.SFX is not null && resourcePack.SFX.TryGetValue(note.Type, out var soundStream))
    {
      if (!IsInstanceValid(soundStream))
      {
        GD.PushError($"[HitResponseController] HitSound is null for note type: {note.Type}");
        return;
      }

      var player = GetHitSoundPool().Get();
      
      if (_cachedHitSoundVolume != HitSoundVolume)
      {
        _cachedHitSoundVolume = HitSoundVolume;
        _cachedVolumeDb = Mathf.LinearToDb(HitSoundVolume);
      }
      player.VolumeDb = _cachedVolumeDb;
      player.Stream = soundStream;
      player.Play();
    }
  }

  private void RequestHitFX(string windowId, NoteData note, HitResult result)
  {
    // Use IsInstanceValid() instead of `!= null` for all Godot nodes/resources.
    if (!IsInstanceValid(_noteController) || note is null || !IsInstanceValid(_hitFXLayer))
    {
      GD.PushError("[HitResponseController] HitFX request failed due to invalid note or controller.");
      return;
    }

    if (!_noteController.TryGetNoteGlobalTransformInfo(windowId, note, out var infoNullable))
    {
      GD.PushError("[HitResponseController] TryGetNoteGlobalTransformInfo failed");
      return;
    }

    var info = infoNullable!.Value;

    var resourcePack = note.ResourcePack;

    var scene = resourcePack.HitFXScene;
    if (!IsInstanceValid(scene))
    {
      GD.PushError("[HitResponseController] HitFX scene is null");
      return;
    }

    var pool = GetHitFXPool(scene);
    var fx = pool.Get();
    _sceneByInstance[fx] = scene;

    // Remove MoveChild since additive blending makes draw order irrelevant and Godot reordering is slow
    
    // NOT the canvas transform; it does not account for CanvasLayer offset.
    // Use GetScreenTransform() (Godot 4) for correct canvas-to-local conversion.
    fx.Position = _hitFXLayer.GetScreenTransform().AffineInverse() * info.Position;
    fx.Rotation = info.Rotation;
    fx.ZIndex = 0;

    fx.Play(
        result.Type,
        note.Type,
        info.NoteWidth,
        info.PlayerAreaSize,
        resourcePack.Config.HitFXAdditiveBlending,
        ReleaseHitFX
    );
  }

  // ─────────────────────────────────────────────────────────────────────────
  public void Prewarm(ResourcePack resourcePack)
  {
    var scene = resourcePack.HitFXScene;
    if (!IsInstanceValid(scene))
    {
      GD.PushError("[HitResponseController] HitFX scene is null");
      return;
    }

    var pool = GetHitFXPool(scene); // Instantiates defaultCapacity nodes

    // Force shader compilation to prevent first-hit stutter and pre-instantiate 16 nodes
    var dummies = new List<HitFX>(16);
    for (int i = 0; i < 16; i++)
    {
      var dummy = pool.Get();
      _sceneByInstance[dummy] = scene;

      if (!IsInstanceValid(dummy.GetParent()))
      {
        if (IsInstanceValid(_hitFXLayer))
          _hitFXLayer.AddChild(dummy);
        else
          AddChild(dummy);
      }

      dummy.Position = PlayerAreaSize; // Off-screen position
      dummies.Add(dummy);
    }

    _hitFXLayer?.Modulate = Colors.White with { A = 0.001f }; // Nearly invisible – avoids flash

    dummies[0].Play(
        HitResultType.Perfect,
        NoteType.Tap,
        1f,            // Dummy note width
        PlayerAreaSize,
        resourcePack.Config.HitFXAdditiveBlending,
        fx =>
        {
          fx.Modulate = Colors.White; // Reset for actual gameplay
          ReleaseHitFX(fx);
        }
    );

    // Immediately release the rest
    for (int i = 1; i < 16; i++)
    {
      ReleaseHitFX(dummies[i]);
    }

    // Prewarm Hit Sounds
    var soundPool = GetHitSoundPool();
    var soundDummies = new List<AudioStreamPlayer>(32);
    for (int i = 0; i < 32; i++)
    {
      soundDummies.Add(soundPool.Get());
    }
    for (int i = 0; i < 32; i++)
    {
      ReleaseHitSound(soundDummies[i]);
    }

    _hitFXLayer?.Modulate = Colors.White; // Nearly invisible – avoids flash
  }

  // ─────────────────────────────────────────────────────────────────────────
  public override void _ExitTree()
  {
    base._ExitTree();

    foreach (var pool in _hitFXPools.Values)
      pool.Dispose();

    _hitSoundPool?.Dispose();

    _hitFXPools.Clear();
    _sceneByInstance.Clear();
  }

  private NodePool<AudioStreamPlayer> GetHitSoundPool()
  {
    if (_hitSoundPool is not null)
      return _hitSoundPool;

    _hitSoundPool = new NodePool<AudioStreamPlayer>(
        parent: this,
        createFunc: () =>
        {
          var player = new AudioStreamPlayer();
          AddChild(player);
          player.Finished += () => ReleaseHitSound(player);
          return player;
        },
        actionOnGet: static player =>
        {
          player.ProcessMode = ProcessModeEnum.Inherit;
        },
        actionOnRelease: static player =>
        {
          player.Stop();
          player.Stream = null;
          player.ProcessMode = ProcessModeEnum.Disabled;
        },
        defaultCapacity: 32
    );

    return _hitSoundPool;
  }

  private void ReleaseHitSound(AudioStreamPlayer player)
  {
    if (!IsInstanceValid(player) || _hitSoundPool is null)
      return;

    _hitSoundPool.Release(player);
  }

  // ─────────────────────────────────────────────────────────────────────────
  private NodePool<HitFX> GetHitFXPool(PackedScene scene)
  {
    if (_hitFXPools.TryGetValue(scene, out var existing))
      return existing;

    var pool = new NodePool<HitFX>(
        parent: this,
        createFunc: () =>
        {
          // Use the generic overload and handle the InvalidCastException explicitly.
          HitFX fx;
          try
          {
            fx = scene.Instantiate<HitFX>();
          }
          catch (System.InvalidCastException)
          {
            GD.PushError(
                    "[HitResponseController] HitFX scene root must inherit " +
                    "Winithm.Core.Behaviors.HitFX. Falling back to empty HitFX node."
                );
            // Free the wrongly-typed node that Instantiate() already added to the tree
            scene.Instantiate().QueueFree();
            fx = new HitFX();
          }

          if (IsInstanceValid(_hitFXLayer))
            _hitFXLayer.AddChild(fx);
          else
            AddChild(fx);
            
          return fx;
        },
        actionOnGet: static fx =>
        {
          fx.Visible = true;
          fx.SetProcess(true);
        },
        actionOnRelease: fx =>
        {
          fx.Visible = false;
          fx.SetProcess(false);
        },
        defaultCapacity: 16
    );

    _hitFXPools[scene] = pool;
    return pool;
  }

  // ─────────────────────────────────────────────────────────────────────────
  private void ReleaseHitFX(HitFX fx)
  {
    if (!IsInstanceValid(fx) || !_sceneByInstance.Remove(fx, out var scene))
    {
      GD.PushError("[HitResponseController] Invalid HitFX node");
      return;
    }

    if (_hitFXPools.TryGetValue(scene, out var pool))
      pool.Release(fx);
  }
}
