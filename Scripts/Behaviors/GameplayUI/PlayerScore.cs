using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class PlayerScore : Control
{
  public struct LastState
  {
    public Color TextColor, TextOutLineColor, CompBackgroundColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public Color TextColor = Colors.White;
  [Export] public Color TextOutLineColor = Colors.Black;
  [Export] public Color CompBackgroundColor = new(0.25f, 0.25f, 0.25f);

  private LastState _lastState = new();

  private HBoxContainer _scoreContainer;
  private Label _accuracyLabel;
  private ColorRect _background;
  private ColorRect _padLeft;
  private ColorRect _padRight;

  public override void _Ready()
  {
    _scoreContainer = GetNodeOrNull<HBoxContainer>("Score");
    _accuracyLabel = GetNodeOrNull<Label>("Accuracy");
    _background = GetNodeOrNull<ColorRect>("Background");
    _padLeft = GetNodeOrNull<ColorRect>("PadLeft");
    _padRight = GetNodeOrNull<ColorRect>("PadRight");
    
    UpdateVisual();
  }

  public void UpdateVisual()
  {
    bool isColorDirty =
      TextColor != _lastState.TextColor
      || TextOutLineColor != _lastState.TextOutLineColor
      || CompBackgroundColor != _lastState.CompBackgroundColor;

    if (isColorDirty) UpdateColor();
  }

  private void UpdateColor()
  {
    if (_accuracyLabel is not null)
    {
      _accuracyLabel.AddThemeColorOverride("font_color", TextColor);
      _accuracyLabel.AddThemeColorOverride("font_outline_color", TextOutLineColor);
    }

    if (_scoreContainer is not null)
    {
      foreach (Node child in _scoreContainer.GetChildren())
      {
        if (child is DigitRoller roller)
        {
          roller.UpdateColor(TextColor, TextOutLineColor);
        }
      }
    }

    if (_background is { Material: ShaderMaterial mat })
    {
      mat.SetShaderParameter("bg_color", CompBackgroundColor);
      mat.SetShaderParameter("stripe_color", new Color(0f, 0f, 0f, 0f));
    }

    if (_padLeft is not null && _padRight is not null)
    {
      _padLeft.Color = TextColor;
      _padRight.Color = TextColor;
    }

    _lastState.TextColor = TextColor;
    _lastState.TextOutLineColor = TextOutLineColor;
    _lastState.CompBackgroundColor = CompBackgroundColor;
  }

  public void SetAccuracy(float accuracy)
  {
    if (_accuracyLabel is not null)
      _accuracyLabel.Text = $"{accuracy * 100:F2}%";
  }

  public void SetScore(int score, bool instant)
  {
    ApplyScoreToRollers(score, instant);
  }

  private void ApplyScoreToRollers(int score, bool instant)
  {
    if (_scoreContainer is null) return;
    string scoreStr = score.ToString("D7");
    int i = 0;
    foreach (Node child in _scoreContainer.GetChildren())
    {
      if (child is DigitRoller roller && i < 7)
      {
        int digit = scoreStr[i] - '0';
        roller.SetDigit(digit, instant);
        i++;
      }
    }
  }
}