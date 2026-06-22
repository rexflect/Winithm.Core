using Godot;
using Winithm.Core.Logic;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class PlayerCombo : Control
{
  public record struct LastState
  {
    public Color TextColor, BgStripeColor, BgColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public Color TextColor = Colors.White;
  [Export] public Color BgStripeColor = new(0.1f, 0.1f, 0.1f);
  [Export] public Color BgColor = new(0f, 0f, 0f);

  private LastState _lastState = new();

  private Label? _comboLabel;
  private Label? _statusLabel;
  private Control? _pauseControl;
  private ColorRect? _progressRect;
  private ColorRect? _background;

  public enum PauseAnimState { Idle, Draining, Filling }
  private PauseAnimState _pauseState = PauseAnimState.Idle;
  private float _pauseTimer = 0f;
  private const float PAUSE_DURATION = 0.5f;

  private float _comboColorTimer = 0f;
  private const float COMBO_COLOR_DURATION = 0.15f;
  private int _currentComboValue = -1;

  public override void _Ready()
  {
    _comboLabel = GetNodeOrNull<Label>("Combo");
    _statusLabel = GetNodeOrNull<Label>("Status");
    _pauseControl = GetNodeOrNull<Control>("Pause");
    _progressRect = _pauseControl?.GetNodeOrNull<ColorRect>("Progess");
    _background = GetNodeOrNull<ColorRect>("Background");

    UpdateVisual();
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;

    if (_comboColorTimer > 0f)
    {
      _comboColorTimer -= dt;
      if (_comboColorTimer < 0f) _comboColorTimer = 0f;

      float tm = 1f - (_comboColorTimer / COMBO_COLOR_DURATION);

      var inverted = new Color(1f - TextColor.R, 1f - TextColor.G, 1f - TextColor.B, TextColor.A);

      _progressRect?.Color = inverted.Lerp(TextColor, tm);
    }
    else
    {
      _progressRect?.Color = TextColor;
    }

    if (_pauseState == PauseAnimState.Draining)
    {
      _pauseTimer += dt;
      if (_pauseTimer >= PAUSE_DURATION)
      {
        _pauseTimer = PAUSE_DURATION;
        _pauseState = PauseAnimState.Idle;
      }
    }
    else if (_pauseState == PauseAnimState.Filling)
    {
      _pauseTimer -= dt;
      if (_pauseTimer <= 0f)
      {
        _pauseTimer = 0f;
        _pauseState = PauseAnimState.Idle;
      }
    }


    float t = _pauseTimer / PAUSE_DURATION;
    if (IsInstanceValid(_pauseControl))
    {
      float yOffset = _pauseControl.Size.Y * t;
      _progressRect?.OffsetTop = yOffset;
      _progressRect?.OffsetBottom = yOffset;
    } else
      GD.PushWarning("[GameplayUI] PlayerCombo: _pauseControl is null");
  }

  public void UpdateVisual()
  {
    bool isColorDirty =
      TextColor != _lastState.TextColor
      || BgStripeColor != _lastState.BgStripeColor
      || BgColor != _lastState.BgColor;

    if (isColorDirty) UpdateColor();
  }

  private void UpdateColor()
  {
    _comboLabel?.AddThemeColorOverride("font_color", TextColor);
    _statusLabel?.AddThemeColorOverride("font_color", TextColor);

    if (_background is ColorRect and { Material: ShaderMaterial material })
    {
      material.SetShaderParameter("stripe_color", BgStripeColor);
      material.SetShaderParameter("bg_color", BgColor);
    }

    _progressRect?.Color = TextColor;

    _lastState.TextColor = TextColor;
    _lastState.BgStripeColor = BgStripeColor;
    _lastState.BgColor = BgColor;
  }

  public void SetCombo(int combo, bool instant)
  {
    _comboLabel?.Text = $"x{combo}";
    if (combo == _currentComboValue) return;
    _currentComboValue = combo;

    if (instant) return;

    _comboColorTimer = COMBO_COLOR_DURATION;
  }

  public void SetStatus(ScoreEngine.CompletionStatus status)
  {
    _statusLabel?.Text = status switch
    {
      ScoreEngine.CompletionStatus.AT => "AUTOPLAY!",
      ScoreEngine.CompletionStatus.AP => "ALL PERFECT!",
      ScoreEngine.CompletionStatus.FC => "FULL COMBO!",
      ScoreEngine.CompletionStatus.CL => "COMPLETED!",
      ScoreEngine.CompletionStatus.FL => "FAILED!",
      _ => _statusLabel.Text
    };
  }

  public void DrainPauseBar() => _pauseState = PauseAnimState.Draining;

  public void FillPauseBar() => _pauseState = PauseAnimState.Filling;
}
