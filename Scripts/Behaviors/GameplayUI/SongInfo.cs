using Godot;

namespace Winithm.Core.Behaviors.GameplayUI;

public partial class SongInfo : Control
{
  protected bool isColorDirty = false;
  protected bool isInfoDirty = false;
  protected bool isIconDirty = false;
  protected bool isProgressDirty = false;

  public float SongProgress
  {
    get; set
    { if (field != value) { isProgressDirty = true; field = value; } }
  } = 0f;

  [Export]
  public Color TextColor
  {
    get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = Colors.White;
  [Export]
  public Color BgStripeColor
  {
    get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0.1f, 0.1f, 0.1f);
  [Export]
  public Color BgColor
  {
    get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);
  [Export]
  public Color PadColor
  {
    get; set
    { if (field != value) { isColorDirty = true; field = value; } }
  } = new(0f, 0f, 0f);
  [Export]
  public string SongName
  {
    get; set
    { if (field != value) { isInfoDirty = true; field = value; } }
  } = "Song Name";
  [Export]
  public float BPM
  {
    get; set
    { if (field != value) { isInfoDirty = true; field = value; } }
  } = 120f;
  [Export]
  public Texture2D SongIcon
  {
    get; set
    { if (field != value) { isIconDirty = true; field = value; } }
  } = GD.Load<Texture2D>("res://Winithm.Core/Resources/Textures/song_placeholder_image.png");
  [Export]
  public Vector2 IconCenter
  {
    get; set
    { if (field != value) { isIconDirty = true; field = value; } }
  } = new(0.5f, 0.5f);
  [Export]
  public float IconSize
  {
    get; set
    { if (field != value) { isIconDirty = true; field = value; } }
  } = 1f;

  private TextureRect? _icon;
  private Label? _name;
  private Label? _bpm;
  private ColorRect? _background;
  private AtlasTexture? _atlasTex;
  private ShaderMaterial? _nameMaterial;
  private ColorRect? _progressBgFill;
  private ShaderMaterial? _bgFillMaterial;

  public override void _Ready()
  {
    _icon = GetNodeOrNull<TextureRect>("TextureRect");
    _name = GetNodeOrNull<Label>("Name");
    _bpm = GetNodeOrNull<Label>("BPM");
    _background = GetNodeOrNull<ColorRect>("Background");
    _progressBgFill = GetNodeOrNull<ColorRect>("ProgressBgFill");

    if (_name?.Material is ShaderMaterial material)
      _nameMaterial = material;
    if (_progressBgFill?.Material is ShaderMaterial bgMaterial)
      _bgFillMaterial = bgMaterial;

    UpdateVisual();
  }

  public void UpdateVisual()
  {
    if (isColorDirty) UpdateColor();
    if (isInfoDirty) UpdateInfo();
    if (isIconDirty) UpdateIcon();
    if (isProgressDirty || isColorDirty) UpdateProgress();

    isColorDirty = false;
    isInfoDirty = false;
    isIconDirty = false;
    isProgressDirty = false;
  }

  private void UpdateColor()
  {
    _name?.AddThemeColorOverride("font_color", TextColor);
    _bpm?.AddThemeColorOverride("font_color", TextColor);

    if (_background is ColorRect and { Material: ShaderMaterial material })
    {
      material.SetShaderParameter("stripe_color", BgStripeColor);
      material.SetShaderParameter("bg_color", BgColor);
    }
  }

  private void UpdateProgress()
  {
    _nameMaterial?.SetShaderParameter("progress", SongProgress);
    _nameMaterial?.SetShaderParameter("text_color", TextColor);

    _bgFillMaterial?.SetShaderParameter("progress", SongProgress);
    _bgFillMaterial?.SetShaderParameter("text_color", TextColor);
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
  }

  private void UpdateInfo()
  {
    _name?.Text = SongName;

    string bpmText = $"BPM: {BPM}";
    _bpm?.Text = bpmText;

    if (!IsInstanceValid(_name) || !IsInstanceValid(_bpm))
    {
      GD.PushWarning("[GameplayUI] SongInfo: _name or _bpm is null");
      return;
    }

    // Extract contextual font assets to calculate text dimensions without forcing a UI layout pass
    var nameFont = _name.GetThemeFont("font");
    int nameFontSize = _name.GetThemeFontSize("font_size");
    var nameSize = nameFont.GetStringSize(SongName, fontSize: nameFontSize);

    var bpmFont = _bpm.GetThemeFont("font");
    int bpmFontSize = _bpm.GetThemeFontSize("font_size");
    var bpmSize = bpmFont.GetStringSize(bpmText, fontSize: bpmFontSize);

    float nameStartX = _name.Position.X;
    float bpmStartX = _bpm.Position.X;

    // Determine the maximum bounding width required to properly contain both labels
    float maxTextWidth = Mathf.Max(nameSize.X, bpmSize.X);
    float textStartX = Mathf.Min(nameStartX, bpmStartX);
    float bgWidth = maxTextWidth + 20f; // Incorporates safety margins on both lateral bounds

    _progressBgFill?.Position = _name.Position;
    _progressBgFill?.Size = nameSize;
    _progressBgFill?.SetAnchorsPreset(LayoutPreset.TopLeft, true);

    _nameMaterial?.SetShaderParameter("width", nameSize.X);
    _bgFillMaterial?.SetShaderParameter("width", nameSize.X);

    // Anchor background constraints to the operational song icon dimensions
    if (IsInstanceValid(_icon))
    {
      if (IsInstanceValid(_background))
      {
        _background.Position = new Vector2(textStartX - 10f, _icon.OffsetTop);
        _background.Size = new Vector2(bgWidth, _icon.Size.Y);
        _background.SetAnchorsPreset(LayoutPreset.TopLeft, true);
      }
    }
    else
      GD.PushWarning("[GameplayUI] SongInfo: _icon is null");
  }
}