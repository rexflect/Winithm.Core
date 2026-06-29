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

  protected record struct WindowBaseState
  {
    public Vector2 Pivot, ScreenSize, PlayerAreaSize, WindowSize;
    public Color TitleBarColor, WindowColor;
    public string Title;
    public bool Borderless, IsNotRespondingTitle;
    public float UnFocusOverlayOpacity, UnresponsiveOverlayOpacity, NoteOpacity;
  }

  protected WindowBaseState LastState;

  // ---------------------------------------------------------------------------
  // Exported properties — shared by every style
  // ---------------------------------------------------------------------------

  [Export] public Vector2 Pivot { get; set; } = new(0.5f, 0.5f);
  [Export] public Color TitleBarColor { get; set; } = Colors.DarkSlateGray;
  [Export] public Vector2 ScreenSize { get; set; } = new(1280, 720);
  [Export] public Vector2 PlayerAreaSize { get; set; } = new(1280, 720);
  [Export] public string Title { get; set; } = "Winithm";
  [Export] public Vector2 WindowSize { get; set; } = new(300, 500);
  [Export] public Color WindowColor { get; set; } = new(0.1f, 0.1f, 0.1f, 0.85f);
  [Export] public float NoteOpacity { get; set; } = 1f;
  [Export] public bool Borderless { get; set; }
  [Export] public bool UnFocus { get; set; }

  // ---------------------------------------------------------------------------
  // Runtime state injected by WindowManager each frame
  // ---------------------------------------------------------------------------

  public float UnFocusOverlayOpacity { get; set; }
  public float UnresponsiveOverlayOpacity { get; set; }
  public bool IsNotRespondingTitle { get; set; }

  // ---------------------------------------------------------------------------
  // Child node references — resolved in _Ready, written by subclass via Init
  // ---------------------------------------------------------------------------

  public Control? TitleBar { get; private set; }
  public Control? WindowBody { get; private set; }
  public Control? WindowFrame { get; private set; }

  // NoteLayer → FloatNoteLayer → UnfocusOverlay → FocusNoteLayer → UnresponsiveOverlay → HitFXLayer
  public Control? NoteLayer { get; private set; }
  public Control? FloatNoteLayer { get; private set; }
  public Control? UnfocusOverlay { get; private set; }
  public Control? FocusNoteLayer { get; private set; }
  public Control? UnresponsiveOverlay { get; private set; }

  // ---------------------------------------------------------------------------
  // Shared constants
  // ---------------------------------------------------------------------------

  public static readonly Color UnfocusOverlayTint = new(0.85f, 0.85f, 0.85f, 0.25f);
  public static readonly Color UnresponsiveOverlayTint = new(1f, 1f, 1f, 0.75f);
  public static readonly Color UnresponsiveWindowModulate = new(1f, 1f, 1f, 0.75f);

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
    UnfocusOverlay = GetNodeOrNull<Control>("WindowBody/UnfocusOverlay");
    FocusNoteLayer = GetNodeOrNull<Control>("WindowBody/FocusNoteLayer");
    UnresponsiveOverlay = GetNodeOrNull<Control>("WindowBody/UnresponsiveOverlay");

    Draw += OnWindowLayoutUpdate;
    TitleBar?.Draw += OnTitleBarDraw;
    WindowBody?.Draw += OnWindowBodyDraw;
    UnfocusOverlay?.Draw += OnUnfocusOverlayDraw;
    UnresponsiveOverlay?.Draw += OnUnresponsiveOverlayDraw;
    WindowFrame?.Draw += OnWindowFrameDraw;
  }

  /// <summary>Unsubscribes the Draw handlers wired up in <see cref="OnReady"/>.</summary>
  public void DetachScriptEvents()
  {
    Draw -= OnWindowLayoutUpdate;
    TitleBar?.Draw -= OnTitleBarDraw;
    WindowBody?.Draw -= OnWindowBodyDraw;
    UnfocusOverlay?.Draw -= OnUnfocusOverlayDraw;
    UnresponsiveOverlay?.Draw -= OnUnresponsiveOverlayDraw;
    WindowFrame?.Draw -= OnWindowFrameDraw;
  }

  /// <summary>Resets dirty-tracking — call before re-scripting an existing node.</summary>
  public void ResetDirtyState()
  {
    LastState = default;
  }

  public override void _Process(double delta)
  {
    bool overlayDirty =
      UnFocusOverlayOpacity != LastState.UnFocusOverlayOpacity ||
      UnresponsiveOverlayOpacity != LastState.UnresponsiveOverlayOpacity;

    if (overlayDirty)
    {
      UnfocusOverlay?.QueueRedraw();
      UnresponsiveOverlay?.QueueRedraw();
      TitleBar?.QueueRedraw();

      LastState.UnFocusOverlayOpacity = UnFocusOverlayOpacity;
      LastState.UnresponsiveOverlayOpacity = UnresponsiveOverlayOpacity;
    }
  }

  public virtual Control? AddNoteVisual(Control noteVisual, NoteData noteData)
  {
    var layer = GetNoteParentLayer(noteData);

    var currentParent = noteVisual.GetParent();

    if (!IsInstanceValid(currentParent))
      layer?.AddChild(noteVisual);
    else if (currentParent != layer)
      noteVisual.Reparent(layer, false);

    layer?.MoveChild(noteVisual, -1);
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
      NoteType.Focus => FocusNoteLayer,
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
    bool layoutDirty =
      Pivot != LastState.Pivot ||
      ScreenSize != LastState.ScreenSize ||
      PlayerAreaSize != LastState.PlayerAreaSize ||
      !WindowSize.IsEqualApprox(LastState.WindowSize) ||
      Borderless != LastState.Borderless;

    bool titleBarDirty = layoutDirty ||
      TitleBarColor != LastState.TitleBarColor ||
      Title != LastState.Title ||
      IsNotRespondingTitle != LastState.IsNotRespondingTitle;

    bool bodyDirty = layoutDirty ||
      WindowColor != LastState.WindowColor ||
      NoteOpacity != LastState.NoteOpacity;

    if (!layoutDirty && !titleBarDirty && !bodyDirty) return;

    if (layoutDirty)
    {
      QueueRedraw();
      WindowFrame?.QueueRedraw();

      LastState.Pivot = Pivot;
      LastState.ScreenSize = ScreenSize;
      LastState.PlayerAreaSize = PlayerAreaSize;
      LastState.WindowSize = WindowSize;
      LastState.Borderless = Borderless;
    }

    if (titleBarDirty)
    {
      TitleBar?.QueueRedraw();

      TitleTextColor = ColorUtils.IsLight(TitleBarColor) ? Colors.Black : Colors.White;

      LastState.TitleBarColor = TitleBarColor;
      LastState.Title = Title;
      LastState.IsNotRespondingTitle = IsNotRespondingTitle;
    }

    if (bodyDirty)
    {
      WindowBody?.QueueRedraw();

      var noteModulate = new Color(1f, 1f, 1f, NoteOpacity);
      NoteLayer?.Modulate = noteModulate;
      FloatNoteLayer?.Modulate = noteModulate;
      FocusNoteLayer?.Modulate = noteModulate;

      LastState.WindowColor = WindowColor;
      LastState.NoteOpacity = NoteOpacity;
    }
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
  /// Draw the unfocus dim overlay.
  /// Called every time <see cref="UnfocusOverlay"/> redraws.
  /// </summary>
  protected abstract void OnUnfocusOverlayDraw();

  /// <summary>
  /// Draw the unresponsive (frozen) overlay.
  /// Called every time <see cref="UnresponsiveOverlay"/> redraws.
  /// </summary>
  protected abstract void OnUnresponsiveOverlayDraw();
}