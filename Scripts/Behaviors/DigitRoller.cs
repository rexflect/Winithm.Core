using Godot;

namespace Winithm.Core.Behaviors;

/// <summary>
/// Displays a single digit (0–9) with an animated rolling scroll.
/// Children are Label nodes inside a VBoxContainer; the root Control is clipped.
/// </summary>
public partial class DigitRoller : Control
{
  private VBoxContainer? _container;
  private Label? _templateLabel;
  private int _targetDigit;

  private float _currentY;
  private float _targetY;

  // Must match custom_minimum_size.y on the template Label in the scene.
  private const float ItemHeight = 37f;

  // ──────────────────────────────────────────────────────────────────────────
  // Godot lifecycle
  // ──────────────────────────────────────────────────────────────────────────

  public override void _Ready()
  {
    _container = GetNodeOrNull<VBoxContainer>("VBoxContainer");

    if (_container?.GetChildCount() > 0 && _container.GetChild(0) is Label first)
    {
      _templateLabel = (Label)first.Duplicate();

      // Keep only the first label; remove any extras left in the scene.
      for (int i = _container.GetChildCount() - 1; i > 0; i--)
      {
        var child = _container.GetChild(i);
        _container.RemoveChild(child);
        child.QueueFree();
      }
    }

    _currentY = 0f;
    _targetY = 0f;
    _container?.Position = Vector2.Zero;
    SetProcess(IsInstanceValid(_container));
  }

  public override void _Process(double delta)
  {
    _currentY = Mathf.Abs(_currentY - _targetY) > 0.01f
        ? Mathf.Lerp(_currentY, _targetY, 15f * (float)delta)
        : _targetY;

    _container?.Position = new Vector2(_container.Position.X, _currentY);

    // Cull labels that have fully scrolled above the viewport.
    while (_currentY <= -ItemHeight && _container?.GetChildCount() > 1)
    {
      var top = _container.GetChild(0);
      _container.RemoveChild(top);
      top.QueueFree();

      _currentY += ItemHeight;
      _targetY += ItemHeight;
      _container.Position = new Vector2(_container.Position.X, _currentY);
    }
  }

  // ──────────────────────────────────────────────────────────────────────────
  // Public API
  // ──────────────────────────────────────────────────────────────────────────

  public void SetDigit(int digit, bool instant)
  {
    digit = Mathf.Clamp(digit, 0, 9);
    if (_targetDigit == digit) return;
    _targetDigit = digit;


    if (instant)
    {
      for (int i = (_container?.GetChildCount() ?? 0) - 1; i >= 0; i--)
      {
        var child = _container?.GetChild(i);
        _container?.RemoveChild(child);
        child?.QueueFree();
      }

      var label = (Label?)_templateLabel?.Duplicate();
      label?.Text = digit.ToString();
      _container?.AddChild(label);

      _currentY = 0f;
      _targetY = 0f;
      _container?.Position = new Vector2(_container.Position.X, 0f);
    }
    else
    {
      var newLabel = (Label?)_templateLabel?.Duplicate();
      newLabel?.Text = digit.ToString();
      _container?.AddChild(newLabel);
      _targetY -= ItemHeight;
    }

    SetProcess(true);
  }

  public void UpdateColor(Color textColor, Color outlineColor)
  {

    _templateLabel?.AddThemeColorOverride("font_color", textColor);
    _templateLabel?.AddThemeColorOverride("font_outline_color", outlineColor);


    if (!IsInstanceValid(_container))
    {
      GD.PushWarning("[DigitRoller] No Container");
      return;
    }

    for (int i = 0; i < _container.GetChildCount(); i++)
    {
      if (_container.GetChild(i) is Label lbl)
      {
        lbl.AddThemeColorOverride("font_color", textColor);
        lbl.AddThemeColorOverride("font_outline_color", outlineColor);
      }
    }
  }
}