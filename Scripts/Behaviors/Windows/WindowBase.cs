using Godot;
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
    public Color TitleBarColor, TitleTextColor, WindowColor;
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
  [Export] public Color TitleTextColor { get; set; } = Colors.White;
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

  // NoteLayer → UnfocusOverlay → FocusNoteLayer → UnresponsiveOverlay → HitFXLayer
  public Control? NoteLayer { get; private set; }
  public Control? UnfocusOverlay { get; private set; }
  public Control? FocusNoteLayer { get; private set; }
  public Control? UnresponsiveOverlay { get; private set; }

  // ---------------------------------------------------------------------------
  // Shared constants
  // ---------------------------------------------------------------------------

  public static readonly Color UnfocusOverlayTint = new(0.25f, 0.25f, 0.25f, 0.5f);
  public static readonly Color UnresponsiveOverlayTint = new(1f, 1f, 1f, 0.75f);
  public static readonly Color UnresponsiveWindowModulate = new(1f, 1f, 1f, 0.75f);

  protected float TitleBarHeight { get; set; }

  // ---------------------------------------------------------------------------
  // Godot lifecycle
  // ---------------------------------------------------------------------------

  public override void _Ready()
  {
    TitleBar = GetNodeOrNull<Control>("TitleBar");
    WindowBody = GetNodeOrNull<Control>("WindowBody");
    WindowFrame = GetNodeOrNull<Control>("Frame");

    NoteLayer = GetNodeOrNull<Control>("WindowBody/NoteLayer");
    UnfocusOverlay = GetNodeOrNull<Control>("WindowBody/UnfocusOverlay");
    FocusNoteLayer = GetNodeOrNull<Control>("WindowBody/FocusNoteLayer");
    UnresponsiveOverlay = GetNodeOrNull<Control>("WindowBody/UnresponsiveOverlay");

    Draw += OnWindowLayoutUpdate;
    TitleBar?.Draw += OnTitleBarDraw;
    WindowBody?.Draw += OnWindowBodyDraw;
    UnfocusOverlay?.Draw += OnUnfocusOverlayDraw;
    UnresponsiveOverlay?.Draw += OnUnresponsiveOverlayDraw;
    WindowFrame?.Draw += OnWindowFrameDraw;

    OnReady();
    UpdateVisual();
  }

  /// <summary>
  /// Optional hook called at the end of <see cref="_Ready"/> before
  /// <see cref="UpdateVisual"/>. Subclasses can load their own resources here.
  /// </summary>
  protected virtual void OnReady() { }

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
      TitleTextColor != LastState.TitleTextColor ||
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

      LastState.TitleBarColor = TitleBarColor;
      LastState.TitleTextColor = TitleTextColor;
      LastState.Title = Title;
      LastState.IsNotRespondingTitle = IsNotRespondingTitle;
    }

    if (bodyDirty)
    {
      WindowBody?.QueueRedraw();

      var noteModulate = new Color(1f, 1f, 1f, NoteOpacity);
      if (NoteLayer is not null) NoteLayer.Modulate = noteModulate;
      if (FocusNoteLayer is not null) FocusNoteLayer.Modulate = noteModulate;

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