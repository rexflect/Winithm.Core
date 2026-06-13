using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class ChartInfo : Control
{
  public struct LastState
  {
    public string DifficultText;
    public Color TextColor, TextOutLineColor, CompBackgroundColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public string DifficultText = "Info: 5";
  [Export] public Color TextColor = Colors.White;
  [Export] public Color TextOutLineColor = Colors.Black;
  [Export] public Color CompBackgroundColor = new(0.35f, 0.35f, 0.35f);

  public readonly float PAD_HEIGHT = 7.5f;

  private LastState _lastState = new();

  private Label _difficult;
  private ColorRect _background;
  private ColorRect _pad;

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
      || TextOutLineColor != _lastState.TextOutLineColor
      || CompBackgroundColor != _lastState.CompBackgroundColor;
    bool isInfoDirty = DifficultText != _lastState.DifficultText;

    if (isColorDirty) UpdateColor();
    if (isInfoDirty) UpdateInfo();
  }

  private void UpdateColor()
  {
    if (_difficult is not null)
    {
      _difficult.AddThemeColorOverride("font_color", TextColor);
      _difficult.AddThemeColorOverride("font_outline_color", TextOutLineColor);
    }

    if (_background is { Material: ShaderMaterial mat })
    {
      mat.SetShaderParameter("bg_color", CompBackgroundColor);
      mat.SetShaderParameter("stripe_color", new Color(0f, 0f, 0f, 0f)); // Transparent
    }

    if (_pad is not null)
      _pad.Color = TextColor;

    _lastState.TextColor = TextColor;
    _lastState.TextOutLineColor = TextOutLineColor;
    _lastState.CompBackgroundColor = CompBackgroundColor;
  }

  private void UpdateInfo()
  {
    if (_difficult is not null)
    {
      _difficult.Text = DifficultText;

      if (_background is not null)
      {
        // Calculate exact text dimensions
        float textWidth = _difficult.GetThemeFont("font").GetStringSize(_difficult.Text).X;
        float textHeight = _difficult.Size.Y;

        // Set background size with 10px padding on all sides
        float bgWidth = textWidth + 20f;
        float bgHeight = textHeight + 20f - PAD_HEIGHT;
        _background.Size = new(bgWidth, bgHeight);

        // Align background to the right edge of the label
        float labelRightEdge = _difficult.Position.X + _difficult.Size.X;
        float bgLeftEdge = labelRightEdge - bgWidth;
        float bgTopEdge = _difficult.Position.Y - 10f;

        _background.Position = new(bgLeftEdge, bgTopEdge);

        // Position pad directly below the background
        if (_pad is not null)
        {
          _pad.Position = new(_background.Position.X, _background.Position.Y + _background.Size.Y);
          _pad.Size = new(_background.Size.X, PAD_HEIGHT);
        }
      }
    }

    _lastState.DifficultText = DifficultText;
  }
}
