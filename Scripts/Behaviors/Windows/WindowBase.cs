using Godot;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Behaviors.Windows;

/// <summary>
/// Abstract base for all OS-style window visuals (Windows, macOS, Linux, Native).
/// Owns layout geometry, shared exported properties, overlay dirty-tracking,
/// and the node-wiring lifecycle. Subclasses implement the draw hooks to
/// produce their platform-specific chrome.
/// </summary>
public abstract partial class WindowBase : Control, IPoolable
{
  // ---------------------------------------------------------------------------
  // Dirty tracking
  // ---------------------------------------------------------------------------

  protected bool isUnresponsiveDirty = false;
  protected bool isMissFocusDirty = false;
  protected bool isLayoutDirty = false;
  protected bool isTitleBarDirty = false;
  protected bool isBodyDirty = false;


  // ---------------------------------------------------------------------------
  // Exported properties — shared by every style
  // ---------------------------------------------------------------------------

  [Export] public Vector2 Pivot { get; set
    { if (field != value) { isLayoutDirty = true; field = value; } }
  } = new(0.5f, 0.5f);
  [Export] public Vector2 ScreenSize { get; set
    { if (!value.IsEqualApprox(field)) { isLayoutDirty = true; field = value; } }
  } = new(1280, 720);
  [Export] public Vector2 PlayerAreaSize { get; set
    { if (!value.IsEqualApprox(field)) { isLayoutDirty = true; field = value; } }
  } = new(1280, 720);
  [Export] public Color TitleBarColor { get; set
    { if (field != value) { isTitleBarDirty = true; field = value; } }
  } = Colors.DarkSlateGray;
  [Export] public string Title { get; set
    { if (field != value) { isTitleBarDirty = true; field = value; } }
  } = "Winithm";
  [Export] public Vector2 WindowSize { get; set
    { if (!value.IsEqualApprox(field)) { isLayoutDirty = true; field = value; } }
  } = new(300, 500);
  [Export] public Color WindowColor { get; set
    { if (field != value) { isBodyDirty = true; field = value; } }
  } = new(0.1f, 0.1f, 0.1f, 0.85f);
  [Export] public float NoteOpacity { get; set
    { if (field != value) { isBodyDirty = true; field = value; } }
  } = 1f;
  [Export] public bool Borderless { get; set
    { if (field != value) { isLayoutDirty = true; field = value; } }
  } = false;

  // ---------------------------------------------------------------------------
  // Runtime state injected by WindowManager each frame
  // ---------------------------------------------------------------------------

  public float MissFocusGrayscale { get; set
    { if (field != value) { isMissFocusDirty = true; field = value; } }
  }
  public float UnresponsiveOverlayOpacity { get; set
    { if (field != value) { isUnresponsiveDirty = true; field = value; } }
  }
  public bool IsNotResponding { get; set
    { if (field != value) { isTitleBarDirty = true; field = value; } }
  }

  // ---------------------------------------------------------------------------
  // Child node references — resolved in _Ready, written by subclass via Init
  // ---------------------------------------------------------------------------

  public Control? TitleBar { get; private set; }
  public Control? WindowBody { get; private set; }
  public Control? WindowFrame { get; private set; }

  // NoteLayer → FloatNoteLayer -> UnresponsiveOverlay → HitFXLayer
  public Control? NoteLayer { get; private set; }
  public Control? FloatNoteLayer { get; private set; }
  public Control? UnresponsiveOverlay { get; private set; }

  // ---------------------------------------------------------------------------
  // Miss-focus grayscale — scoped to WindowBody's subtree only
  // ---------------------------------------------------------------------------
  private static readonly Shader _grayscaleShader =
    GD.Load<Shader>("res://Winithm.Core/Resources/Shaders/WindowGrayscale.gdshader");

  private ShaderMaterial? _grayscaleMaterial;

  // ---------------------------------------------------------------------------
  // Shared constants
  // ---------------------------------------------------------------------------

  public static readonly Color UnresponsiveOverlayTint = new(1f, 1f, 1f);

  protected float TitleBarHeight { get; set; }
  protected Color TitleTextColor { get; set; } = Colors.White;

  // ---------------------------------------------------------------------------
  // Godot lifecycle
  // ---------------------------------------------------------------------------

  public override void _Ready()
  {
    OnReady();
    UpdateVisual();
  }

  /// <summary>
  /// Optional hook called at the end of <see cref="_Ready"/> before
  /// <see cref="UpdateVisual"/>. Subclasses can load their own resources here.
  /// </summary>
  public virtual void OnReady()
  {
    TitleBar = GetNodeOrNull<Control>("TitleBar");
    WindowBody = GetNodeOrNull<Control>("WindowBody");
    WindowFrame = GetNodeOrNull<Control>("Frame");

    NoteLayer = GetNodeOrNull<Control>("WindowBody/NoteLayer");
    FloatNoteLayer = GetNodeOrNull<Control>("WindowBody/FloatNoteLayer");
    UnresponsiveOverlay = GetNodeOrNull<Control>("WindowBody/UnresponsiveOverlay");

    // Godot does not auto-cascade CanvasItem materials to children.
    // We force children to use WindowBody's material so they are grayed out together.
    _grayscaleMaterial = new ShaderMaterial { Shader = _grayscaleShader };
    WindowBody?.Material = _grayscaleMaterial;

    Draw += OnWindowLayoutUpdate;
    TitleBar?.Draw += OnTitleBarDraw;
    WindowBody?.Draw += OnWindowBodyDraw;
    UnresponsiveOverlay?.Draw += OnUnresponsiveOverlayDraw;
    WindowFrame?.Draw += OnWindowFrameDraw;
  }

  /// <summary>Unsubscribes the Draw handlers wired up in <see cref="OnReady"/>.</summary>
  public void DetachScriptEvents()
  {
    Draw -= OnWindowLayoutUpdate;
    TitleBar?.Draw -= OnTitleBarDraw;
    WindowBody?.Draw -= OnWindowBodyDraw;
    UnresponsiveOverlay?.Draw -= OnUnresponsiveOverlayDraw;
    WindowFrame?.Draw -= OnWindowFrameDraw;
  }

  /// <summary>Resets dirty-tracking — call before re-scripting an existing node.</summary>
  public void ResetDirtyState()
  {
    isMissFocusDirty = true;
    isUnresponsiveDirty = true;
    isLayoutDirty = true;
    isTitleBarDirty = true;
    isBodyDirty = true;
  }
  public virtual Control? AddNoteVisual(Control noteVisual, NoteData noteData)
  {
    var layer = GetNoteParentLayer(noteData);

    var currentParent = noteVisual.GetParent();

    if (!IsInstanceValid(currentParent))
      layer?.AddChild(noteVisual);
    else if (currentParent != layer)
      noteVisual.Reparent(layer, false);

    return noteVisual;
  }

  public virtual Control? GetNoteParentLayer(NoteData noteData)
  {
    return noteData.Type switch
    {
      NoteType.Tap => NoteLayer,
      NoteType.Drag => NoteLayer,
      NoteType.Hold => NoteLayer,
      NoteType.Hover => FloatNoteLayer,
      NoteType.Focus => FloatNoteLayer,
      NoteType.Close => FloatNoteLayer,
      _ => null
    };
  }

  // ---------------------------------------------------------------------------
  // IPoolable
  // ---------------------------------------------------------------------------

  public virtual void OnSpawn() { }
  public virtual void OnDespawn() { }

  // ---------------------------------------------------------------------------
  // Layout engine — shared geometry, style-agnostic
  // ---------------------------------------------------------------------------

  /// <summary>
  /// Recalculates layout of TitleBar, WindowBody, and Frame, then queues
  /// redraws on dirty sub-nodes. Call this whenever any exported property
  /// changes at runtime (WindowManager sets properties then calls this).
  /// </summary>
  public void UpdateVisual()
  {
    if (!isLayoutDirty && !isTitleBarDirty && !isBodyDirty && !isUnresponsiveDirty && !isMissFocusDirty) return;

    if (isLayoutDirty)
    {
      QueueRedraw();
      WindowFrame?.QueueRedraw();
    }

    if (isLayoutDirty || isTitleBarDirty)
    {
      TitleBar?.QueueRedraw();

      TitleTextColor = ColorUtils.IsLight(TitleBarColor) ? Colors.Black : Colors.White;
    }

    if (isLayoutDirty || isBodyDirty)
    {
      WindowBody?.QueueRedraw();

      var noteModulate = new Color(1f, 1f, 1f, NoteOpacity);
      NoteLayer?.Modulate = noteModulate;
      FloatNoteLayer?.Modulate = noteModulate;
    }

    if (isMissFocusDirty) OnMissFocusShaderUpdate();

    if (isUnresponsiveDirty)
    {
      UnresponsiveOverlay?.QueueRedraw();
    }

    isLayoutDirty = false;
    isTitleBarDirty = false;
    isBodyDirty = false;
    isMissFocusDirty = false;
    isUnresponsiveDirty = false;
  }

  // ---------------------------------------------------------------------------
  // Abstract draw hooks — each OS style implements its own chrome
  // ---------------------------------------------------------------------------

  /// <summary>
  /// Called when layout properties change, before visual update.
  /// </summary>
  protected abstract void OnWindowLayoutUpdate();

  /// <summary>
  /// Draw the window frame border.
  /// Called every time <see cref="WindowFrame"/> redraws.
  /// </summary>
  protected abstract void OnWindowFrameDraw();

  /// <summary>
  /// Draw the title bar chrome (background, icon, title text, window buttons).
  /// Called every time <see cref="TitleBar"/> redraws.
  /// </summary>
  protected abstract void OnTitleBarDraw();

  /// <summary>
  /// Draw the window body background.
  /// Called every time <see cref="WindowBody"/> redraws.
  /// </summary>
  protected abstract void OnWindowBodyDraw();

  /// <summary>
  /// Pushes <see cref="MissFocusGrayscale"/> into the shared grayscale shader's
  /// uniform. Style-agnostic — override only if a specific OS style ever needs
  /// different desaturation behavior.
  /// </summary>
  protected virtual void OnMissFocusShaderUpdate()
  {
    _grayscaleMaterial?.SetShaderParameter("grayscale_amount", MissFocusGrayscale);
  }

  /// <summary>
  /// Draw the unresponsive (frozen) overlay.
  /// Called every time <see cref="UnresponsiveOverlay"/> redraws.
  /// </summary>
  protected abstract void OnUnresponsiveOverlayDraw();
}