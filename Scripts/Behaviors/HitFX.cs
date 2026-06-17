using Godot;
using System;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Behaviors;

/// <summary>
/// Generic runtime contract for resource-pack HitFX scenes.
/// Visual shapes, particles, and pack-specific animation belong entirely in the
/// scene tree or derived scripts — this base class imposes no particle policy.
/// </summary>
public partial class HitFX : Node2D, IPoolable
{
  [Export] public float DefaultDuration { get; set; } = 0.5f;

  protected HitResultType ResultType { get; private set; }
  protected NoteType NoteType { get; private set; }
  protected float NoteWidth { get; private set; }
  protected Vector2 PlayerAreaSize { get; private set; }
  protected float Elapsed => _elapsed;

  private Action<HitFX>? _onFinished;
  private float _duration;
  private float _elapsed;
  private bool _playing;

  // ──────────────────────────────────────────────────────────────────────────
  // Public API
  // ──────────────────────────────────────────────────────────────────────────

  public virtual void Play(
      HitResultType resultType,
      NoteType noteType,
      float noteWidth,
      Vector2 playerAreaSize,
      bool additiveBlending,
      Action<HitFX> onFinished)
  {
    ResultType = resultType;
    NoteType = noteType;
    NoteWidth = noteWidth;
    PlayerAreaSize = playerAreaSize;
    _onFinished = onFinished;
    _elapsed = 0f;
    _playing = true;
    _duration = DefaultDuration;

    Visible = true;
    SetProcess(true);

    ApplyBlendMode(additiveBlending);
    OnHitFXStarted();
  }

  public void Stop() => Finish();

  // ──────────────────────────────────────────────────────────────────────────
  // IPoolable
  // ──────────────────────────────────────────────────────────────────────────

  public void OnSpawn()
  {
    Visible = true;
    SetProcess(false);
  }

  public void OnDespawn()
  {
    _playing = false;
    _elapsed = 0f;
    _onFinished = null;
    OnHitFXStopped();
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Overridable hooks (subclass implements behaviour here)
  // ──────────────────────────────────────────────────────────────────────────

  /// <summary>Called once when the effect starts playing.</summary>
  protected virtual void OnHitFXStarted() { }

  /// <summary>Called every frame while the effect is playing.</summary>
  protected virtual void OnHitFXProcess(double delta) { }

  /// <summary>Called when the effect finishes or is forcibly stopped.</summary>
  protected virtual void OnHitFXStopped() { }

  // ──────────────────────────────────────────────────────────────────────────
  // Duration helper — subclasses call this inside OnHitFXStarted
  // ──────────────────────────────────────────────────────────────────────────

  protected void SetDuration(float duration) => _duration = duration;

  // ──────────────────────────────────────────────────────────────────────────
  // Godot process loop
  // ──────────────────────────────────────────────────────────────────────────

  public override void _Process(double delta)
  {
    if (!_playing) return;

    _elapsed += (float)delta;
    OnHitFXProcess(delta);

    if (_elapsed >= _duration)
      Finish();
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Internal helpers
  // ──────────────────────────────────────────────────────────────────────────

  private void Finish()
  {
    if (!_playing) return;

    _playing = false;
    SetProcess(false);
    OnHitFXStopped();

    var cb = _onFinished;
    _onFinished = null;
    cb?.Invoke(this);
  }

  private void ApplyBlendMode(bool additive)
  {
    if (this is CanvasItem ci)
    {
      var mode = additive
          ? CanvasItemMaterial.BlendModeEnum.Add
          : CanvasItemMaterial.BlendModeEnum.Mix;

      switch (ci.Material)
      {
        case null:
          ci.Material = new CanvasItemMaterial { BlendMode = mode };
          break;
        case CanvasItemMaterial m:
          m.BlendMode = mode;
          break;
      }
    }
  }
}