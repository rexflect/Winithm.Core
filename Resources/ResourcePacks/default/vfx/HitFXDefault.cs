using System;
using Godot;
using Winithm.Core.Behaviors;
using Winithm.Core.Common;
using Winithm.Core.Data;

namespace Winithm.Core.ResourcePacks.Default.VFX;

public partial class HitFXDefault : HitFX
{
  // ── Tuneable constants ─────────────────────────────────────────────────────
  public const float Duration = 0.5f;
  public const float Opacity = 0.35f;
  public const float FXWidth = 0.75f;
  public const float OutlineThickness = 0.05f;

  // ── Draw state ─────────────────────────────────────────────────────────────
  private float _outlineScale;
  private float _outlineAlpha;
  private float _fillScale;
  private float _fillAlpha;
  private float _fillWidth;

  // ── Particle node references (wired in _Ready via GetNode) ─────────────────
  // The scene is free to expose zero, one, or several GPUParticles2D nodes.
  // Here we only cache the single "Burst" node that ships with the default scene.
  private GpuParticles2D _burst;

  // ──────────────────────────────────────────────────────────────────────────
  // Godot lifecycle
  // ──────────────────────────────────────────────────────────────────────────

  public override void _Ready()
  {
    // Gracefully handle scenes that don't have a Burst node.
    _burst = GetNodeOrNull<GpuParticles2D>("Burst");
  }

  // ──────────────────────────────────────────────────────────────────────────
  // HitFX hooks
  // ──────────────────────────────────────────────────────────────────────────

  protected override void OnHitFXStarted()
  {
    HitColor = ResultType switch
    {
      HitResultType.Perfect => Colors.Yellow,
      HitResultType.Good => Colors.Cyan,
      HitResultType.Bad => new Color(1f, 0f, 0.45f, 1f),
      HitResultType.Miss => new Color(0.5f, 0.5f, 0.5f, 1f),
      _ => Colors.Yellow,
    };

    SetDuration(Duration);
    Scale = Vector2.One * NoteWidth * FXWidth;

    // Tint every direct CanvasItem child with the hit colour.
    foreach (Node child in GetChildren())
    {
      if (child is CanvasItem ci)
        ci.Modulate = HitColor;
    }

    // Reset draw state.
    (_outlineScale, _outlineAlpha) = (0f, 1f);
    (_fillScale, _fillAlpha, _fillWidth) = (0f, 1f, 1f);
    QueueRedraw();

    // ── Particle ownership: this subclass decides when to emit ─────────────
    if (_burst is not null)
    {
      // Tint burst particles to match the hit colour.
      if (_burst.ProcessMaterial is ParticleProcessMaterial pm)
      {
        pm.Color = HitColor with { A = Opacity };
      }
      _burst.Restart();   // resets the one-shot emitter and begins emitting
    }
  }

  protected override void OnHitFXProcess(double delta)
  {
    // Outline: expand 0→1 over 0..0.25 s, then fade out over 0.25..0.35 s.
    float tOS = Mathf.Clamp(Elapsed / 0.25f, 0f, 1f);
    _outlineScale = (float)EasingFunctions.Evaluate(EasingType.CubicOut, tOS);

    float tOA = Mathf.Clamp((Elapsed - 0.25f) / 0.10f, 0f, 1f);
    _outlineAlpha = 1f - 0.75f * (float)EasingFunctions.Evaluate(EasingType.CubicIn, tOA);

    // Fill: expand 0→0.65 over 0..0.35 s, then shrink / fade over 0.35..0.50 s.
    float tFS = Mathf.Clamp(Elapsed / 0.35f, 0f, 1f);
    _fillScale = 0.65f * (float)EasingFunctions.Evaluate(EasingType.CubicOut, tFS);

    float tFA = Mathf.Clamp((Elapsed - 0.35f) / 0.15f, 0f, 1f);
    _fillAlpha = 1f - 0.75f * (float)EasingFunctions.Evaluate(EasingType.CubicIn, tFA);

    float tFW = Mathf.Clamp((Elapsed - 0.35f) / 0.15f, 0f, 1f);
    _fillWidth = 1f - (float)EasingFunctions.Evaluate(EasingType.CubicOut, tFW);

    QueueRedraw();
  }

  protected override void OnHitFXStopped()
  {
    (_outlineScale, _outlineAlpha) = (0f, 0f);
    (_fillScale, _fillAlpha, _fillWidth) = (0f, 0f, 0f);
    QueueRedraw();

    // Stop the burst emitter if it is still running.
    if (_burst is not null)
      _burst.Emitting = false;
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Drawing
  // ──────────────────────────────────────────────────────────────────────────

  public override void _Draw()
  {
    DrawFilledDiamond();
    DrawOutlineDiamond();
  }

  private void DrawFilledDiamond()
  {
    if (_fillScale <= 0f || _fillAlpha <= 0f) return;

    float thickness = Mathf.Lerp(0f, 0.5f * _fillScale, _fillWidth);
    DrawHollowDiamond(_fillScale, thickness, _fillAlpha);
  }

  private void DrawOutlineDiamond()
  {
    if (_outlineScale <= 0f || _outlineAlpha <= 0f || OutlineThickness <= 0f) return;

    DrawHollowDiamond(_outlineScale, OutlineThickness, _outlineAlpha);
  }

  /// <summary>
  /// Draws a hollow diamond (rhombus) as 4 non-overlapping, perfectly mitered
  /// trapezoids so there is no alpha overlap at the corners.
  /// </summary>
  private void DrawHollowDiamond(float scale, float thickness, float alpha)
  {
    float ho = 0.5f * scale;
    float hi = Mathf.Max(0f, ho - thickness);

    ReadOnlySpan<Vector2> outer =
    [
        new( 0f, -ho),
            new( ho,  0f),
            new( 0f,  ho),
            new(-ho,  0f),
        ];

    ReadOnlySpan<Vector2> inner =
    [
        new( 0f, -hi),
            new( hi,  0f),
            new( 0f,  hi),
            new(-hi,  0f),
        ];

    Color c = HitColor with { A = HitColor.A * Opacity * alpha };

    Color[] quad = [c, c, c, c];

    DrawPolygon([outer[0], outer[1], inner[1], inner[0]], quad);
    DrawPolygon([outer[1], outer[2], inner[2], inner[1]], quad);
    DrawPolygon([outer[2], outer[3], inner[3], inner[2]], quad);
    DrawPolygon([outer[3], outer[0], inner[0], inner[3]], quad);
  }
}