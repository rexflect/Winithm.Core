using Godot;

namespace Winithm.Core.Behaviors;

public partial class WindowFrame : Control
{
  private Window _parent = null!;

  public override void _Ready()
  {
    _parent = GetParent<Window>();
  }

  public override void _Draw()
  {
    if (_parent.Borderless)
      return;

    var color = _parent.TitleBarColor with
    {
      A = 0.5f
    };

    float lineWidth = Mathf.Max(1f, _parent.TitleBarHeight * 0.025f);

    DrawRect(
        new Rect2(Vector2.Zero, Size),
        color,
        false,
        lineWidth
    );
  }
}