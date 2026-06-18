using Godot;

namespace Winithm.Core.Behaviors.Windows;

public partial class WindowFrame : Control
{
  private WindowBase? _parent;

  public override void _Ready()
  {
    _parent = GetParentOrNull<WindowBase>();
  }

  public override void _Draw()
  {
    if (!IsInstanceValid(_parent))
    {
      GD.PushWarning("[WindowFrame] No Parent");
      return;
    }

    if (_parent.Borderless) return;

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
