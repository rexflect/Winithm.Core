using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class PlayerScore : Control
{
  protected bool isColorDirty = false;
  protected int lastScore = -1;

  [Export] public Color TextColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = Colors.White;
  [Export] public Color BgStripeColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0.1f, 0.1f, 0.1f);
  [Export] public Color BgColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);
  [Export] public Color PadColor { get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);

  private HBoxContainer? _scoreContainer;
  private Label? _accuracyLabel;
  private ColorRect? _background;
  private ColorRect? _padLeft;
  private ColorRect? _padRight;

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
    if (isColorDirty) UpdateColor();
  }

  private void UpdateColor()
  {
    _accuracyLabel?.AddThemeColorOverride("font_color", TextColor);

    foreach (Node child in _scoreContainer?.GetChildren() ?? [])
      if (child is DigitRoller roller)
        roller.UpdateColor(TextColor);

    if (_background is ColorRect and { Material: ShaderMaterial material })
    {
      material.SetShaderParameter("stripe_color", BgStripeColor);
      material.SetShaderParameter("bg_color", BgColor);
    }

    _padLeft?.Color = PadColor;
    _padRight?.Color = PadColor;

    isColorDirty = false;
  }

  public void SetAccuracy(float accuracy)
  {
    _accuracyLabel?.Text = $"{accuracy * 100:F2}%";
  }

  public void SetScore(int score, bool instant)
  {
    ApplyScoreToRollers(score, instant);
  }

  private void ApplyScoreToRollers(int score, bool instant)
  {
    if (score == lastScore) return;

    if (!IsInstanceValid(_scoreContainer))
    {
      GD.PushWarning("[GameplayUI] PlayerScore: _scoreContainer is null");
      return;
    }

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

    lastScore = score;
  }
}