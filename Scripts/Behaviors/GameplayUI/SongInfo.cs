using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class SongInfo : Control
{
  public record struct LastState
  {
    public Color TextColor, TextOutLineColor, CompBackgroundColor;
    public string SongName;
    public float BPM, IconSize;
    public Vector2 IconCenter;
    public Texture2D SongIcon;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public Color TextColor = Colors.White;
  [Export] public Color TextOutLineColor = Colors.Black;
  [Export] public Color CompBackgroundColor = new(0.25f, 0.25f, 0.25f);
  [Export] public string SongName = "Song Name";
  [Export] public float BPM = 120f;
  [Export]
  public Texture2D SongIcon =
    GD.Load<Texture2D>("res://Winithm.Core/Resources/Textures/song_placeholder_image.png");
  [Export] public Vector2 IconCenter = new(0.5f, 0.5f);
  [Export] public float IconSize = 1f;

  private LastState _lastState = new();

  private TextureRect? _icon;
  private Label? _name;
  private Label? _bpm;
  private ColorRect? _background;
  private AtlasTexture? _atlasTex;

  public override void _Ready()
  {
    _icon = GetNodeOrNull<TextureRect>("TextureRect");
    _name = GetNodeOrNull<Label>("Name");
    _bpm = GetNodeOrNull<Label>("BPM");
    _background = GetNodeOrNull<ColorRect>("Background");

    UpdateVisual();
  }

  public void UpdateVisual()
  {
    bool isColorDirty =
      TextColor != _lastState.TextColor
      || TextOutLineColor != _lastState.TextOutLineColor
      || CompBackgroundColor != _lastState.CompBackgroundColor;

    bool isInfoDirty = SongName != _lastState.SongName || BPM != _lastState.BPM;
    bool isIconDirty = SongIcon != _lastState.SongIcon ||
                       IconCenter != _lastState.IconCenter ||
                       IconSize != _lastState.IconSize;

    if (isColorDirty) UpdateColor();
    if (isInfoDirty) UpdateInfo();
    if (isIconDirty) UpdateIcon();
  }

  private void UpdateColor()
  {

    _name?.AddThemeColorOverride("font_color", TextColor);
    _name?.AddThemeColorOverride("font_outline_color", TextOutLineColor);


    _bpm?.AddThemeColorOverride("font_color", TextColor);
    _bpm?.AddThemeColorOverride("font_outline_color", TextOutLineColor);

    if (_background is { Material: ShaderMaterial mat })
    {
      mat.SetShaderParameter("bg_color", CompBackgroundColor);
      mat.SetShaderParameter("stripe_color", new Color(0f, 0f, 0f, 0f)); // Transparent
    }

    _lastState.TextColor = TextColor;
    _lastState.TextOutLineColor = TextOutLineColor;
    _lastState.CompBackgroundColor = CompBackgroundColor;
  }

  private void UpdateIcon()
  {
    _atlasTex = new AtlasTexture
    {
      Atlas = SongIcon
    };

    var texSize = SongIcon.GetSize();
    float minDim = Mathf.Min(texSize.X, texSize.Y);
    float zoom = Mathf.Max(0.01f, IconSize);
    float cropSize = minDim / zoom;

    var centerPx = new Vector2(texSize.X * IconCenter.X, texSize.Y * IconCenter.Y);
    var topLeft = centerPx - new Vector2(cropSize / 2f, cropSize / 2f);

    _atlasTex.Region = new Rect2(topLeft, new Vector2(cropSize, cropSize));
    _icon?.Texture = _atlasTex;


    _lastState.SongIcon = SongIcon;
    _lastState.IconCenter = IconCenter;
    _lastState.IconSize = IconSize;
  }

  private void UpdateInfo()
  {
    _name?.Text = SongName;
    _bpm?.Text = $"BPM: {BPM}";

    if (IsInstanceValid(_name) && IsInstanceValid(_bpm))
    {
      _name.ResetSize();
      _bpm.ResetSize();

      float nameWidth = _name.Size.X;
      float bpmWidth = _bpm.Size.X;
      float maxTextWidth = Mathf.Max(nameWidth, bpmWidth);

      float textStartX = Mathf.Min(_name.OffsetLeft, _bpm.OffsetLeft);
      float bgWidth = maxTextWidth + 20f; // 5px padding on left and right

      // Height matches song icon
      if (IsInstanceValid(_icon))
      {
        _background?.Position = new Vector2(textStartX - 10f, _icon.OffsetTop);
        _background?.Size = new Vector2(bgWidth, _icon.Size.Y);
      } 
      else
        GD.PushWarning("[GameplayUI] SongInfo: _icon is null");
    } else
      GD.PushWarning("[GameplayUI] SongInfo: _name or _bpm is null");

    _lastState.SongName = SongName;
    _lastState.BPM = BPM;
  }
}