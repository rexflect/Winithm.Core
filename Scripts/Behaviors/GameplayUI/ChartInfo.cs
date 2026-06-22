using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class ChartInfo : Control
{
  public record struct LastState
  {
    public string DifficultText;
    public Color TextColor, BgStripeColor, BgColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public string DifficultText = "Info: 5";
  [Export] public Color BgStripeColor = new(0.1f, 0.1f, 0.1f);
  [Export] public Color BgColor = new(0f, 0f, 0f);
  [Export] public Color TextColor = Colors.White;

  public readonly float PAD_HEIGHT = 7.5f;

  private LastState _lastState = new();

  private Label? _difficult;
  private ColorRect? _background;
  private ColorRect? _pad;

  public override void _Ready()
  {
    _difficult = GetNodeOrNull<Label>("Difficult");
    _background = GetNodeOrNull<ColorRect>("Background");
    _pad = GetNodeOrNull<ColorRect>("Pad");

    UpdateVisual();
  }

  public void UpdateVisual()
  {
    bool isColorDirty =
      TextColor != _lastState.TextColor
      || BgStripeColor != _lastState.BgStripeColor
      || BgColor != _lastState.BgColor;
    bool isInfoDirty = DifficultText != _lastState.DifficultText;

    if (isColorDirty) UpdateColor();
    if (isInfoDirty) UpdateInfo();
  }

  private void UpdateColor()
  {

    _difficult?.AddThemeColorOverride("font_color", TextColor);
    
    if (_background is ColorRect and { Material: ShaderMaterial material })
    {
      material.SetShaderParameter("stripe_color", BgStripeColor);
      material.SetShaderParameter("bg_color", BgColor);
    }

    _pad?.Color = TextColor;

    _lastState.TextColor = TextColor;
    _lastState.BgStripeColor = BgStripeColor;
    _lastState.BgColor = BgColor;
  }

  private void UpdateInfo()
  {

    _difficult?.Text = DifficultText;

    if (IsInstanceValid(_difficult))
    {
      _difficult.ResetSize();

      // Calculate exact text dimensions
      float textWidth = _difficult.Size.X;
      float textHeight = _difficult.Size.Y;

      // Set background size with 10px padding on all sides
      float bgWidth = textWidth + 20f;
      float bgHeight = textHeight + 20f - PAD_HEIGHT;
      _background?.Size = new Vector2(bgWidth, bgHeight);

      // Align background to the right edge of the label
      float bgTopEdge = _difficult.Position.Y - 10f;

      _background?.Position = new Vector2(_difficult.Position.X - 10f, bgTopEdge);

      // Position pad directly below the background
      if (IsInstanceValid(_background))
      {
        _pad?.Position = new Vector2(_background.Position.X, _background.Position.Y + _background.Size.Y);
        _pad?.Size = new Vector2(_background.Size.X, PAD_HEIGHT);
      }
      else
        GD.PushWarning("[GameplayUI] ChartInfo: _background is null");
    }
    else
      GD.PushWarning("[GameplayUI] ChartInfo: _difficult is null");


    _lastState.DifficultText = DifficultText;
  }
}
