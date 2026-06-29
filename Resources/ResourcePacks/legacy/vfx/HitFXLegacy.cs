using System;
using Godot;
using Winithm.Core.Behaviors;
using Winithm.Core.Common;
using Winithm.Core.Data;

namespace Winithm.Core.ResourcePacks.Default.VFX;

public partial class HitFXLegacy : HitFX
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
    _fillMesh = GetNodeOrNull<MeshInstance2D>("FillMesh");
    _outlineMesh = GetNodeOrNull<MeshInstance2D>("OutlineMesh");
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

    // Invalidate cache so meshes rebuild on next process
    _lastFillScale = -1f;
    _lastOutlineScale = -1f;
    _lastHitColor = default;

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

    UpdateMeshes();
  }

  protected override void OnHitFXStopped()
  {
    (_outlineScale, _outlineAlpha) = (0f, 0f);
    (_fillScale, _fillAlpha, _fillWidth) = (0f, 0f, 0f);

    _fillMesh?.Visible = false;
    _outlineMesh?.Visible = false;

    _burst?.Emitting = false;
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Mesh update — only rebuilds when params actually changed
  // ──────────────────────────────────────────────────────────────────────────

  private void UpdateMeshes()
  {
    bool colorChanged = HitColor != _lastHitColor;

    // Fill mesh
    bool fillChanged = colorChanged
      || _fillScale != _lastFillScale
      || _fillAlpha != _lastFillAlpha
      || _fillWidth != _lastFillWidth;

    if (fillChanged && _fillMesh is not null)
    {
      if (_fillScale <= 0f || _fillAlpha <= 0f)
      {
        _fillMesh.Visible = false;
      }
      else
      {
        float thickness = Mathf.Lerp(0f, 0.5f * _fillScale, _fillWidth);
        _fillMesh.Mesh = BuildHollowDiamondMesh(_fillScale, thickness, _fillAlpha);
        _fillMesh.Visible = true;
      }

      _lastFillScale = _fillScale;
      _lastFillAlpha = _fillAlpha;
      _lastFillWidth = _fillWidth;
    }

    // Outline mesh
    bool outlineChanged = colorChanged
      || _outlineScale != _lastOutlineScale
      || _outlineAlpha != _lastOutlineAlpha;

    if (outlineChanged && _outlineMesh is not null)
    {
      if (_outlineScale <= 0f || _outlineAlpha <= 0f || OutlineThickness <= 0f)
      {
        _outlineMesh.Visible = false;
      }
      else
      {
        _outlineMesh.Mesh = BuildHollowDiamondMesh(_outlineScale, OutlineThickness, _outlineAlpha);
        _outlineMesh.Visible = true;
      }

      _lastOutlineScale = _outlineScale;
      _lastOutlineAlpha = _outlineAlpha;
    }

    if (colorChanged) _lastHitColor = HitColor;
  }

  /// <summary>
  /// Builds a hollow diamond as an ArrayMesh with 1 surface (8 triangles = 4 quads).
  /// Vertex layout per quad: outer0, outer1, inner1, outer0, inner1, inner0
  /// </summary>
  private ArrayMesh BuildHollowDiamondMesh(float scale, float thickness, float alpha)
  {
    float ho = 0.5f * scale;
    float hi = Mathf.Max(0f, ho - thickness);

    var c = HitColor with { A = HitColor.A * Opacity * alpha };

    // 4 outer + 4 inner vertices
    var outer = new Vector2[] { new(0f, -ho), new(ho, 0f), new(0f, ho), new(-ho, 0f) };
    var inner = new Vector2[] { new(0f, -hi), new(hi, 0f), new(0f, hi), new(-hi, 0f) };

    // 4 quads × 6 vertices (2 triangles each) = 24 vertices total
    var verts = new Vector3[24];
    var colors = new Color[24];

    for (int q = 0; q < 4; q++)
    {
      int next = (q + 1) % 4;
      int base_ = q * 6;

      // Triangle 1: outer[q], outer[next], inner[next]
      verts[base_ + 0] = new Vector3(outer[q].X, outer[q].Y, 0f);
      verts[base_ + 1] = new Vector3(outer[next].X, outer[next].Y, 0f);
      verts[base_ + 2] = new Vector3(inner[next].X, inner[next].Y, 0f);

      // Triangle 2: outer[q], inner[next], inner[q]
      verts[base_ + 3] = new Vector3(outer[q].X, outer[q].Y, 0f);
      verts[base_ + 4] = new Vector3(inner[next].X, inner[next].Y, 0f);
      verts[base_ + 5] = new Vector3(inner[q].X, inner[q].Y, 0f);

      for (int v = 0; v < 6; v++) colors[base_ + v] = c;
    }

    var arrays = new Godot.Collections.Array();
    arrays.Resize((int)Mesh.ArrayType.Max);
    arrays[(int)Mesh.ArrayType.Vertex] = verts;
    arrays[(int)Mesh.ArrayType.Color] = colors;

    var mesh = new ArrayMesh();
    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    return mesh;
  }
}