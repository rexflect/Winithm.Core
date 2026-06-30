using Godot;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Behaviors;

public partial class NoteFloat : Control, IPoolable
{
  // --- Dirty tracking ---
  private record struct NoteFloatState
  {
    public Vector2 WindowSize;
    public float Width, X;
    public NoteSide Side;
    public float Progress;
  }

  private NoteFloatState _lastState;

  // --- Child references ---
  private CanvasGroup? _canvasGroup;
  private NinePatchRect? _indicator;
  private Control? _bodyGroup;
  private NinePatchRect? _base;
  private TextureRect? _overlay;

  // --- Properties set by NoteController ---
  [Export] public Vector2 WindowSize { get; set; } = new(1280, 720);
  [Export] public NoteType Type { get; set; } = NoteType.Hover;
  [Export] public NoteSide Side { get; set; } = NoteSide.Bottom;
  [Export] public float X { get; set; } = 0.5f;
  [Export] public float Width { get; set; } = 300f;
  [Export] public float Progress { get; set; } = 0f;
  
  public ResourcePack? ResourcePack { get; set; } = null;

  public static readonly float INDICATOR_FULL_OPACITY = 0.5f;
  public static readonly float BODY_OPACITY = 0.5f;
  public static readonly float PERSPECTIVE_DEPTH_K = 5f;
  public static readonly float SLIDE_STRENGTH = 0.08f;

  public float FloatOverlayRatio { get; set; } = 0.5f;

  public override void _Ready()
  {
    _canvasGroup = GetNodeOrNull<CanvasGroup>("CanvasGroup");
    _indicator = GetNodeOrNull<NinePatchRect>("Indicator");
    _bodyGroup = GetNodeOrNull<Control>("CanvasGroup/BodyGroup");
    _base = GetNodeOrNull<NinePatchRect>("CanvasGroup/BodyGroup/Base");
    _overlay = GetNodeOrNull<TextureRect>("CanvasGroup/BodyGroup/Overlay");

    UpdateVisual();
  }

  public void OnSpawn() { }
  public void OnDespawn() { }

  private Texture2D? GetTextureSafe(NoteType type, NotePart part)
  {
    if (ResourcePack?.TEX.TryGetValue(type, out var parts) is true
        && parts.TryGetValue(part, out var tex))
    {
      return tex;
    }
    return null;
  }

  public void SetNoteType(NoteType type, ResourcePack resourcePack)
  {
    if (type is not NoteType.Hover
      && type is not NoteType.Focus
      && type is not NoteType.Close
    )
    {
      GD.PushWarning($"[NoteFloat] Invalid note type for float notes: {type}");
      return;
    }

    bool isDirty = Type != type || !ReferenceEquals(ResourcePack?.TEX, resourcePack.TEX);
    if (!isDirty) return;

    Type = type;
    ResourcePack = resourcePack;

    FloatOverlayRatio = resourcePack.Config.FloatOverlayRatio;

    int margin = resourcePack.Config.NinePatchFloatMargin;

    if (_base is not null)
    {
      _base.PatchMarginLeft = margin;
      _base.PatchMarginRight = margin;
      _base.PatchMarginTop = margin;
      _base.PatchMarginBottom = margin;
      _base.Texture = GetTextureSafe(Type, NotePart.Base);
    }

    if (_indicator is not null)
    {
      _indicator.PatchMarginLeft = margin;
      _indicator.PatchMarginRight = margin;
      _indicator.PatchMarginTop = margin;
      _indicator.PatchMarginBottom = margin;
      _indicator.Texture = GetTextureSafe(NoteType.Indicator, NotePart.Base);
    }

    if (_overlay is not null)
    {
      _overlay.Texture = GetTextureSafe(Type, NotePart.Overlay);
    }

    _lastState = default;
    UpdateVisual();
  }

  public void UpdateVisual()
  {
    bool isDirty = WindowSize != _lastState.WindowSize ||
                   Width != _lastState.Width ||
                   X != _lastState.X ||
                   Side != _lastState.Side ||
                   Progress != _lastState.Progress;

    if (!isDirty) return;

    var clampedProgress = Mathf.Clamp(Progress, 0f, 1f);

    // --- Indicator: delayed fade-in for anticipation ---
    if (_indicator is not null)
    {
      _indicator.Size = WindowSize;
      _indicator.Position = Vector2.Zero;

      // Appear after 30% progress, ease-in quadratic
      float indicatorT = Mathf.Clamp((clampedProgress - 0.3f) / 0.7f, 0f, 1f);
      indicatorT *= indicatorT;
      _indicator.Modulate = new Color(1f, 1f, 1f, indicatorT * INDICATOR_FULL_OPACITY);
    }

    // --- Body: perspective projection + depth slide ---
    if (_bodyGroup is not null)
    {
      float lateralPosition = X * (1f - Width) + Width / 2f;
      float depth = Width * 0.5f;

      Vector2 pivot = Side switch
      {
        NoteSide.Bottom => new(WindowSize.X * lateralPosition, WindowSize.Y * (1f - depth)),
        NoteSide.Top    => new(WindowSize.X * lateralPosition, WindowSize.Y * depth),
        NoteSide.Left   => new(WindowSize.X * depth, WindowSize.Y * lateralPosition),
        NoteSide.Right  => new(WindowSize.X * (1f - depth), WindowSize.Y * lateralPosition),
        _ => Vector2.Zero
      };

      // Perspective scale: simulates 1/(distance) projection
      // Near field factor k controls depth intensity (higher = more dramatic)
      float perspectiveScale = clampedProgress / (1f + PERSPECTIVE_DEPTH_K * (1f - clampedProgress));

      // Depth slide: body drifts inward from its edge as it approaches
      float slideT = 1f - Mathf.Pow(clampedProgress, 2f);
      Vector2 edgeDir = Side switch
      {
        NoteSide.Bottom => new(0f, 1f),
        NoteSide.Top    => new(0f, -1f),
        NoteSide.Left   => new(-1f, 0f),
        NoteSide.Right  => new(1f, 0f),
        _ => Vector2.Zero
      };
      float slideDistPx = slideT * Mathf.Min(WindowSize.X, WindowSize.Y) * SLIDE_STRENGTH;

      _bodyGroup.Position = pivot + edgeDir * slideDistPx;
      _bodyGroup.Scale = new Vector2(perspectiveScale, perspectiveScale);

      // Opacity: cubic ease-out so note becomes visible early as a faint ghost
      float bodyAlpha = 1f - Mathf.Pow(1f - clampedProgress, 3f);
      _bodyGroup.Modulate = new Color(1f, 1f, 1f, bodyAlpha * BODY_OPACITY);

      if (_base is not null)
      {
        _base.Size = WindowSize;
        _base.Position = -pivot;
      }

      if (_overlay is not null && _overlay.Texture is not null)
      {
        float minSize = Mathf.Min(WindowSize.X, WindowSize.Y);
        float targetSize = minSize * FloatOverlayRatio;

        var texSize = _overlay.Texture.GetSize();
        if (texSize.X > 0 && texSize.Y > 0)
        {
          _overlay.Scale = new Vector2(targetSize / texSize.X, targetSize / texSize.Y);
          _overlay.Size = texSize;

          var scaledSize = new Vector2(targetSize, targetSize);
          _overlay.Position = -pivot + (WindowSize / 2f) - (scaledSize / 2f);
        }
      }
    }

    _lastState = new NoteFloatState()
    {
      WindowSize = WindowSize,
      Width = Width,
      X = X,
      Side = Side,
      Progress = Progress
    };
  }

  public void SetNoteHighlighting(bool active)
  {
    if (ResourcePack is null)
    {
      GD.PushWarning("[NoteFloat] ResourcePack is not setted");
      return;
    }

    if (_canvasGroup?.Material is ShaderMaterial shaderMaterial)
    {
      shaderMaterial.SetShaderParameter("is_highlighted", active);
      shaderMaterial.SetShaderParameter(
        "glow_radius", Note.BASE_HIGHTLIGHTING_SIZE * ResourcePack.Value.Config.HighlightSize
      );
      shaderMaterial.SetShaderParameter("glow_intensity", ResourcePack.Value.Config.HighlightInsensity);
    }
  }
}
