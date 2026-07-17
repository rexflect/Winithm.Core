using Godot;
using System;

namespace Winithm.Core.Behaviors.Gameplay;

public partial class OverlayBase : ColorRect
{
  private ShaderMaterial? _material;

  public override void _Ready()
  {
    AnchorsPreset = (int)LayoutPreset.FullRect;
    MouseFilter = MouseFilterEnum.Ignore;
    
    _material = new ShaderMaterial();
    Material = _material;
  }

  public override void _Process(double delta)
  {
    Size = GetViewportRect().Size;
  }

  public void UpdateShader(Shader shader)
  {
    if (_material?.Shader != shader)
      _material?.Shader = shader;
  }

  public void SetParameter(StringName name, Variant value)
  {
    _material?.SetShaderParameter(name, value);
  }

  public void ResetDirtyState()
  {
    // Ensure anchors are always full rect when pulled from pool
    AnchorsPreset = (int)LayoutPreset.FullRect;
  }
}
