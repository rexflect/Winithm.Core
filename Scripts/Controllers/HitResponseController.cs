using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

public partial class HitResponseController : Node
{
  private Control _hitFXLayer;
  private NoteController _noteController;

  [Export] public Vector2 PlayerAreaSize { set; get; } = new(1280, 720);
  [Export] public float HitSoundVolume { set; get; } = 0.5f;

  private readonly Dictionary<PackedScene, NodePool<HitFX>> _hitFXPools = [];
  private readonly Dictionary<HitFX, PackedScene> _sceneByInstance = [];
  private NodePool<AudioStreamPlayer> _hitSoundPool;

  // ─────────────────────────────────────────────────────────────────────────
  public void Initialize(Control hitFXLayer, NoteController noteController)
  {
    _hitFXLayer = hitFXLayer;
    _noteController = noteController;
  }

  // ─────────────────────────────────────────────────────────────────────────

  public void RequestHitResponse(string windowId, NoteData note, HitResult result)
  {
    RequestHitFX(windowId, note, result);
    RequestHitSound(note);
  }

  private void RequestHitSound(NoteData note)
  {
    if (note is null) return;

    // Get resource pack (override by note, otherwise use active)
    ResourcePack resourcePack = note.ResourcePack.HasValue
        ? note.ResourcePack.Value
        : ResourcePackManager.Instance.GetActiveResourcePack();

    if (resourcePack.SFX is not null && resourcePack.SFX.TryGetValue(note.Type, out var soundStream))
    {
      if (soundStream is null) return;

      AudioStreamPlayer player = GetHitSoundPool().Get();
      player.VolumeDb = Mathf.LinearToDb(HitSoundVolume);
      player.Stream = soundStream;
      player.Play();
    }
  }

  private void RequestHitFX(string windowId, NoteData note, HitResult result)
  {
    // Use IsInstanceValid() instead of `!= null` for all Godot nodes/resources.
    if (!IsInstanceValid(_noteController) || note is null || !IsInstanceValid(_hitFXLayer))
      return;

    if (!_noteController.TryGetNoteGlobalTransformInfo(windowId, note, out var info))
      return;

    ResourcePack resourcePack = note.ResourcePack.HasValue
        ? note.ResourcePack.Value
        : ResourcePackManager.Instance.GetActiveResourcePack();

    PackedScene scene = resourcePack.HitFXScene;
    if (scene is null) return;

    NodePool<HitFX> pool = GetHitFXPool(scene);
    HitFX fx = pool.Get();
    _sceneByInstance[fx] = scene;

    // Reparent to the global HitFXLayer
    // AddChild manually can emit spurious ready/exit-tree signals and is not
    // deferred-safe.  Reparent(keepGlobalTransform: false) is equivalent and safe.
    if (fx.GetParent() != _hitFXLayer)
      fx.Reparent(_hitFXLayer!, false);

    _hitFXLayer!.MoveChild(fx, -1); // -1 = move to last child (Godot 4.x shorthand)

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
    PackedScene scene = resourcePack.HitFXScene;
    if (scene is null) return;

    NodePool<HitFX> pool = GetHitFXPool(scene); // Instantiates defaultCapacity nodes

    // Force shader compilation to prevent first-hit stutter
    HitFX dummy = pool.Get();
    _sceneByInstance[dummy] = scene;

    // RequestHitFX(), so Position assignment is harmless but Play() might
    // try to manipulate the scene tree before the node is added.
    // Ensure it is added to a visible parent first.
    if (!IsInstanceValid(dummy.GetParent()))
    {
      if (IsInstanceValid(_hitFXLayer))
        _hitFXLayer!.AddChild(dummy);
      else
        AddChild(dummy);
    }

    dummy.Position = PlayerAreaSize; // Off-screen position
    dummy.Modulate = new Color(1f, 1f, 1f, 0.01f); // Nearly invisible – avoids flash

    dummy.Play(
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
  }

  // ─────────────────────────────────────────────────────────────────────────
  public override void _ExitTree()
  {
    base._ExitTree();

    foreach (NodePool<HitFX> pool in _hitFXPools.Values)
      pool.Dispose();

    _hitSoundPool?.Dispose();

    _hitFXPools.Clear();
    _sceneByInstance.Clear();
  }

  private NodePool<AudioStreamPlayer> GetHitSoundPool()
  {
    if (_hitSoundPool is not null)
      return _hitSoundPool;

    _hitSoundPool = new(
        parent: this,
        createFunc: () =>
        {
          AudioStreamPlayer player = new();
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
    if (_hitFXPools.TryGetValue(scene, out NodePool<HitFX> existing))
      return existing;

    NodePool<HitFX> pool = new(
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
                    "[HitFXController] HitFX scene root must inherit " +
                    "Winithm.Core.Behaviors.HitFX. Falling back to empty HitFX node."
                );
            // Free the wrongly-typed node that Instantiate() already added to the tree
            scene.Instantiate().QueueFree();
            fx = new HitFX();
          }

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

          // Return ownership to the pool's parent node
          if (fx.GetParent() != this)
            fx.Reparent(this, false);
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
      return;

    if (_hitFXPools.TryGetValue(scene, out var pool))
      pool.Release(fx);
  }
}
