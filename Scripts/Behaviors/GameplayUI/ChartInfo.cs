using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class ChartInfo : Control
{
  protected bool isInfoDirty = false;
  protected bool isColorDirty = false;

  [Export] public string DifficultText { get; set
    { if (field != value) { isInfoDirty = true; field = value; } }
  } = "Info: 5";
  [Export] public Color BgStripeColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0.1f, 0.1f, 0.1f);
  [Export] public Color BgColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);
  [Export] public Color PadColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);
  [Export] public Color TextColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = Colors.White;

  public readonly float PAD_HEIGHT = 7.5f;



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

    isColorDirty = false;
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

    isInfoDirty = false;
  }
}
