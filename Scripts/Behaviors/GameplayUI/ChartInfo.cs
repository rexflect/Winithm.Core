using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class ChartInfo : Control
{
  public record struct LastState
  {
    public string DifficultText;
    public Color TextColor, BgStripeColor, BgColor, PadColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public string DifficultText = "Info: 5";
  [Export] public Color BgStripeColor = new(0.1f, 0.1f, 0.1f);
  [Export] public Color BgColor = new(0f, 0f, 0f);
  [Export] public Color PadColor = new(0f, 0f, 0f);
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
      || BgColor != _lastState.BgColor
      || PadColor != _lastState.PadColor;
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

    _pad?.Color = PadColor;

    _lastState.TextColor = TextColor;
    _lastState.BgStripeColor = BgStripeColor;
    _lastState.BgColor = BgColor;
    _lastState.PadColor = PadColor;
  }

  private void UpdateInfo()
  {
    if (!IsInstanceValid(_difficult))
    {
      GD.PushWarning("[GameplayUI] ChartInfo: _difficult is null");
      return;
    }

    _difficult.Text = DifficultText;

    // Resolve active typographic metrics from the theme context to calculate text bounds deterministically
    var font = _difficult.GetThemeFont("font");
    int fontSize = _difficult.GetThemeFontSize("font_size");
    var textSize = font.GetStringSize(DifficultText, fontSize: fontSize);

    float textWidth = textSize.X;
    float textHeight = textSize.Y;

    // Dynamically calculate the true visual starting X position based on Godot's GrowHorizontal configuration
    float rightEdge = _difficult.Position.X + _difficult.Size.X;
    float textStartX = rightEdge - textWidth;

    // Establish dynamic background dimensions incorporating a uniform padding structure
    float bgWidth = textWidth + 20f;
    float bgHeight = textHeight + 20f - PAD_HEIGHT;

    if (IsInstanceValid(_background))
    {
      _background.Size = new Vector2(bgWidth, bgHeight);

      // Snap background position to the calculated true visual layout bounds
      float bgTopEdge = _difficult.Position.Y - 10f;
      _background.Position = new Vector2(textStartX - 10f, bgTopEdge);

      // Synchronize the status pad directly beneath the primary background block
      if (IsInstanceValid(_pad))
      {
        _pad.Position = new Vector2(_background.Position.X, _background.Position.Y + _background.Size.Y);
        _pad.Size = new Vector2(_background.Size.X, PAD_HEIGHT);
      }
    }
    else
      GD.PushWarning("[GameplayUI] ChartInfo: _background is null");

    _lastState.DifficultText = DifficultText;
  }
}
