using Godot;
using System;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Behaviors;

public partial class Note : Control, IPoolable
{
  // --- Dirty tracking ---
  // Stores the last state to avoid redundant visual updates (dirty tracking)
  private record struct NoteState
  {
    public Vector2 PlayerAreaSize;
    public float Width, BodyHeight, NoteSize;
  }

  private NoteState _lastState;

  // --- Child references (assigned in _Ready) ---
  // References to scene nodes assigned during initialization
  private Control? _headContainer;
  private NinePatchRect? _headBase;
  private TextureRect? _headOverlay;
  private Control? _bodyContainer;
  private NinePatchRect? _bodyBase;

  // --- Properties set by NoteManager ---
  // Configurable properties typically managed by NoteManager
  [Export] public Vector2 PlayerAreaSize { get; set; } = new(1280, 720);
  [Export] public float Width { get; set; } = 300f;
  [Export] public NoteType Type { get; set; } = NoteType.Tap;
  [Export] public float NoteSize { get; set; } = 1f;
  [Export] public float BodyHeight { get; set; } = 0f;
  public ResourcePack? ResourcePack { get; set; } = null;

  public static readonly float NOTE_HEAD_HEIGHT_RATIO = 0.0175f;
  public static readonly float NOTE_HEAD_OVERLAY_RATIO_SIZE = 1.2f;
  public static readonly float BODY_TO_HEAD_WIDTH_OFFSET = 0.015f;

  public static readonly float BASE_HIGHTLIGHTING_SIZE = 10f;

  // Initialize node references and perform initial visual update
  public override void _Ready()
  {
    _headContainer = GetNodeOrNull<Control>("Head");
    _headBase = GetNodeOrNull<NinePatchRect>("Head/Base");
    _headOverlay = GetNodeOrNull<TextureRect>("Head/Overlay");

    _bodyContainer = GetNodeOrNull<Control>("Body");
    _bodyBase = GetNodeOrNull<NinePatchRect>("Body/Base");

    UpdateVisual();
  }

  // Logic to execute when the note is retrieved from the object pool
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
    bool isDirty = Type != type
                  || !ReferenceEquals(ResourcePack?.TEX, resourcePack.TEX);
    if (!isDirty) return;

    Type = type;
    ResourcePack = resourcePack;

    _bodyContainer?.Visible = Type is NoteType.Hold;

    _headBase?.PatchMarginLeft = resourcePack.Config.NinePatchHeadMarginH;
    _headBase?.PatchMarginRight = resourcePack.Config.NinePatchHeadMarginH;
    _headBase?.PatchMarginTop = 0;
    _headBase?.PatchMarginBottom = 0;

    _bodyBase?.PatchMarginLeft = resourcePack.Config.NinePatchBodyMarginH;
    _bodyBase?.PatchMarginRight = resourcePack.Config.NinePatchBodyMarginH;
    _bodyBase?.PatchMarginTop = resourcePack.Config.NinePatchBodyMarginV;
    _bodyBase?.PatchMarginBottom = resourcePack.Config.NinePatchBodyMarginV;

    NoteType headType = Type is NoteType.Hold ? NoteType.Tap : Type;

    _headBase?.Texture = GetTextureSafe(headType, NotePart.Base);
    _headOverlay?.Texture = GetTextureSafe(headType, NotePart.Overlay);

    if (Type is NoteType.Hold)
    {
      _bodyBase?.Texture = GetTextureSafe(NoteType.Hold, NotePart.Base);
    }

    // Force update visual since texture changed
    _lastState = default;
    UpdateVisual();
  }

  public void SetNoteHighlighting(bool active)
  {
    if (ResourcePack is null)
    {
      GD.PushWarning("[Note] ResourcePack is not setted");
      return;
    }

    if (_headBase?.Material is ShaderMaterial shaderMaterial)
    {
      shaderMaterial.SetShaderParameter("is_highlighted", active);
      shaderMaterial.SetShaderParameter(
        "glow_radius", BASE_HIGHTLIGHTING_SIZE * ResourcePack.Value.Config.HighlightSize
      );
    }
  }

  // Recalculates sizes and positions of all components based on current properties
  public void UpdateVisual()
  {
    float minScale = Mathf.Min(PlayerAreaSize.X, PlayerAreaSize.Y);
    float headH = NoteSize * minScale * NOTE_HEAD_HEIGHT_RATIO;

    // 1. Remove minimum width limit to allow shrinking to 0
    float headW = MathF.Max(Width, 0f);

    float headScale = 1f;
    if (_headBase?.Texture is { } headTex && headTex.GetSize().Y > 0)
    {
      headScale = headH / headTex.GetSize().Y;
    }

    bool headDirty =
      PlayerAreaSize != _lastState.PlayerAreaSize ||
      Width != _lastState.Width ||
      NoteSize != _lastState.NoteSize;

    bool bodyDirty =
      PlayerAreaSize != _lastState.PlayerAreaSize ||
      Width != _lastState.Width ||
      BodyHeight != _lastState.BodyHeight;

    if (headDirty)
    {
      // Update head component layout
      _headContainer?.Position = new(-headW / 2f, -headH);

      if (_headBase?.Texture is { } baseTex)
      {
        float targetLogicW = headScale > 0 ? headW / headScale : headW;
        float minSafeW = _headBase.PatchMarginLeft + _headBase.PatchMarginRight;

        // 2. Prevent NinePatch distortion by scaling X-axis if width < margins
        if (targetLogicW < minSafeW && minSafeW > 0)
        {
          _headBase.Size = new Vector2(minSafeW, baseTex.GetSize().Y);
          _headBase.Scale = new Vector2(headScale * (targetLogicW / minSafeW), headScale);
        }
        else
        {
          _headBase.Size = new Vector2(targetLogicW, baseTex.GetSize().Y);
          _headBase.Scale = new Vector2(headScale, headScale);
        }

        _headBase.Position = Vector2.Zero;
      }

      if (_headOverlay?.Texture is { } overlayTex)
      {
        float overlaySize = headH * NOTE_HEAD_OVERLAY_RATIO_SIZE;
        float texW = overlayTex.GetSize().X;
        float texH = overlayTex.GetSize().Y;

        _headOverlay.Scale = new Vector2(texW > 0 ? overlaySize / texW : 0f, texH > 0 ? overlaySize / texH : 0f);
        _headOverlay.Size = new Vector2(texW, texH);
        _headOverlay.Position = new Vector2(headW / 2f - overlaySize / 2f, headH / 2f - overlaySize / 2f);
      }
    }

    if (bodyDirty)
    {
      // Update body component layout (for Hold notes)
      float bodyWidthOffset = minScale * BODY_TO_HEAD_WIDTH_OFFSET;
      float bodyW = MathF.Max(headW - bodyWidthOffset, 0f);

      _bodyContainer?.Position = new Vector2(-bodyW / 2f, -BodyHeight - headH);

      float targetLogicW = headScale > 0 ? bodyW / headScale : bodyW;
      float targetLogicH = headScale > 0 ? BodyHeight / headScale : BodyHeight;
      float minSafeW = _bodyBase?.PatchMarginLeft + _bodyBase?.PatchMarginRight ?? headW;

      // 3. Apply the same squish logic to the body NinePatch
      if (targetLogicW < minSafeW && minSafeW > 0)
      {
        _bodyBase?.Size = new Vector2(minSafeW, targetLogicH);
        _bodyBase?.Scale = new Vector2(headScale * (targetLogicW / minSafeW), headScale);
      }
      else
      {
        _bodyBase?.Size = new Vector2(targetLogicW, targetLogicH);
        _bodyBase?.Scale = new Vector2(headScale, headScale);
      }

      _bodyBase?.Position = Vector2.Zero;

    }

    // Save current state for next dirty check
    _lastState = new NoteState()
    {
      PlayerAreaSize = PlayerAreaSize,
      Width = Width,
      NoteSize = NoteSize,
      BodyHeight = BodyHeight
    };
  }
}