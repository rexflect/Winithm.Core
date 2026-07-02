using Godot;
using Winithm.Core.Behaviors;
using Winithm.Core.Common;
using Winithm.Core.Data;

namespace Winithm.Core.ResourcePacks.Default.VFX;

public partial class HitFXDefault : HitFX
{
  public const float Duration = 0.5f;
  public const float Opacity = 0.35f;
  public const float FXWidth = 0.75f;
  public const float OutlineThickness = 0.05f;

  protected Color HitColor { get; private set; } = Colors.White;

  private float _outlineScale;
  private float _outlineAlpha;
  private float _fillScale;
  private float _fillAlpha;
  private float _fillWidth;

  private CpuParticles2D? _burst;
  private MeshInstance2D? _fillMesh;
  private MeshInstance2D? _outlineMesh;

  // Cache last built params to avoid rebuilding identical mesh
  private float _lastFillScale = -1f, _lastFillAlpha = -1f, _lastFillWidth = -1f;
  private float _lastOutlineScale = -1f, _lastOutlineAlpha = -1f;
  private Color _lastHitColor;

  public override void _Ready()
  {
    _burst = GetNodeOrNull<CpuParticles2D>("Burst");
  }

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
    Modulate = HitColor;

    (_outlineScale, _outlineAlpha) = (0f, 1f);
    (_fillScale, _fillAlpha, _fillWidth) = (0f, 1f, 1f);



    _burst?.Restart();
  }

  protected override void OnHitFXProcess(double delta)
  {
    float tOS = Mathf.Clamp(Elapsed / 0.25f, 0f, 1f);
    _outlineScale = (float)EasingFunctions.Evaluate(EasingType.CubicOut, tOS);

    float tOA = Mathf.Clamp((Elapsed - 0.25f) / 0.10f, 0f, 1f);
    _outlineAlpha = 1f - 0.75f * (float)EasingFunctions.Evaluate(EasingType.CubicIn, tOA);

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



    _burst?.Emitting = false;
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Fast Draw API (replaces expensive per-frame ArrayMesh building)
  // ──────────────────────────────────────────────────────────────────────────

  private static readonly Vector2[] _quadVerts = new Vector2[4];
  private static readonly Color[] _quadColors = new Color[4];

  public override void _Draw()
  {
    if (_fillScale > 0f && _fillAlpha > 0f)
    {
      float thickness = Mathf.Lerp(0f, 0.5f * _fillScale, _fillWidth);
      DrawHollowDiamond(_fillScale, thickness, _fillAlpha);
    }

    if (_outlineScale > 0f && _outlineAlpha > 0f && OutlineThickness > 0f)
    {
      DrawHollowDiamond(_outlineScale, OutlineThickness, _outlineAlpha);
    }
  }

  private void DrawHollowDiamond(float scale, float thickness, float alpha)
  {
    float ho = 0.5f * scale;
    float hi = Mathf.Max(0f, ho - thickness);

    var c = HitColor with { A = HitColor.A * Opacity * alpha };
    _quadColors[0] = c; _quadColors[1] = c; _quadColors[2] = c; _quadColors[3] = c;

    var outer = new Vector2[] { new(0f, -ho), new(ho, 0f), new(0f, ho), new(-ho, 0f) };
    var inner = new Vector2[] { new(0f, -hi), new(hi, 0f), new(0f, hi), new(-hi, 0f) };

    for (int i = 0; i < 4; i++)
    {
      int next = (i + 1) % 4;
      _quadVerts[0] = outer[i];
      _quadVerts[1] = outer[next];
      _quadVerts[2] = inner[next];
      _quadVerts[3] = inner[i];
      DrawPolygon(_quadVerts, _quadColors);
    }
  }
}