using Godot;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Behaviors;

public partial class WindowVS : Control, IPoolable
{
  // --- Dirty tracking ---
  private struct WindowState
  {
    public Vector2 Pivot, ScreenSize, PlayerAreaSize, WindowSize;
    public Color TitleBarColor, TitleTextColor, WindowColor;
    public string Title;
    public bool Borderless, IsNotRespondingTitle;
    public float UnFocusOverlayOpacity, UnresponsiveOverlayOpacity, NoteOpacity;
  }

  private WindowState _lastState;

  // --- Exported properties ---
  [Export] public Vector2 Pivot { get; set; } = new(0.5f, 0.5f);
  [Export] public Color TitleBarColor { get; set; } = Colors.Coral;
  [Export] public Color TitleTextColor { get; set; } = Colors.Black;
  [Export] public Vector2 ScreenSize { get; set; } = new(1280, 720);
  [Export] public Vector2 PlayerAreaSize { get; set; } = new(1280, 720);

  [Export] public string Title { get; set; } = "Winithm";
  [Export] public Vector2 WindowSize { get; set; } = new(300, 500);
  [Export] public Color WindowColor { get; set; } = new(0.1f, 0.1f, 0.1f, 0.85f);
  [Export] public float NoteOpacity { get; set; } = 1f;
  [Export] public bool Borderless { get; set; }
  [Export] public bool UnFocus { get; set; }

  // --- Runtime state injected by WindowManager each frame ---
  public float UnFocusOverlayOpacity { get; set; }
  public float UnresponsiveOverlayOpacity { get; set; }
  public bool IsNotRespondingTitle { get; set; }

  // --- Child references ---
  public Control TitleBar { get; private set; }
  public Control WindowBody { get; private set; }
  public Control WindowFrame { get; private set; }

  // --- Runtime layers (Z-ordered inside WindowBody) ---
  // NoteLayer → UnfocusOverlay → FocusNoteLayer → UnresponsiveOverlay → HitFXLayer
  public Control NoteLayer { get; private set; }
  public Control UnfocusOverlay { get; private set; }
  public Control FocusNoteLayer { get; private set; }
  public Control UnresponsiveOverlay { get; private set; }

  // --- Resources ---
  private static readonly Texture2D _iconTex = GD.Load<Texture2D>("res://icon.png");
  private static readonly Texture2D _closeTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/close.svg");
  private static readonly Texture2D _maxTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/maximize.svg");
  private static readonly Texture2D _minTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/minimize.svg");

  private static readonly FontFile _fontFile = GD.Load<FontFile>("res://Winithm.Core/Resources/Fonts/Quicksand.ttf");

  public static readonly float TitleBarHeightRatio = 0.0375f;
  public static readonly Color UnfocusOverlayTint = new(0.25f, 0.25f, 0.25f, 0.5f);
  public static readonly Color UnresponsiveOverlayTint = new(1f, 1f, 1f, 0.75f);
  public static readonly Color UnresponsiveWindowModulate = new(1f, 1f, 1f, 0.75f);
  internal float TitleBarHeight { get; private set; }

  public override void _Ready()
  {
    TitleBar = GetNode<Control>("TitleBar");
    WindowBody = GetNode<Control>("WindowBody");
    WindowFrame = GetNode<Control>("Frame");

    NoteLayer = GetNode<Control>("WindowBody/NoteLayer");
    UnfocusOverlay = GetNode<Control>("WindowBody/UnfocusOverlay");
    FocusNoteLayer = GetNode<Control>("WindowBody/FocusNoteLayer");
    UnresponsiveOverlay = GetNode<Control>("WindowBody/UnresponsiveOverlay");

    TitleBar.Draw += OnTitleBarDraw;
    WindowBody.Draw += OnWindowBodyDraw;
    UnfocusOverlay.Draw += OnUnfocusOverlayDraw;
    UnresponsiveOverlay.Draw += OnUnresponsiveOverlayDraw;

    UpdateVisual();
  }

  public void OnSpawn() { }
  public void OnDespawn() { }

  public override void _Process(double delta)
  {
    bool overlayDirty =
      UnFocusOverlayOpacity != _lastState.UnFocusOverlayOpacity ||
      UnresponsiveOverlayOpacity != _lastState.UnresponsiveOverlayOpacity;

    if (overlayDirty)
    {
      UnfocusOverlay?.QueueRedraw();
      UnresponsiveOverlay?.QueueRedraw();
      TitleBar?.QueueRedraw();

      _lastState.UnFocusOverlayOpacity = UnFocusOverlayOpacity;
      _lastState.UnresponsiveOverlayOpacity = UnresponsiveOverlayOpacity;
    }
  }

  /// <summary>
  /// Recalculates layout of TitleBar, WindowBody, and Frame.
  /// Call after changing any exported property.
  /// </summary>
  public void UpdateVisual()
  {
    if (TitleBar is null || WindowBody is null) return;

    bool layoutDirty =
      Pivot != _lastState.Pivot ||
      ScreenSize != _lastState.ScreenSize ||
      PlayerAreaSize != _lastState.PlayerAreaSize ||
      WindowSize != _lastState.WindowSize ||
      Borderless != _lastState.Borderless;

    bool titleBarDirty = layoutDirty ||
      TitleBarColor != _lastState.TitleBarColor ||
      TitleTextColor != _lastState.TitleTextColor ||
      Title != _lastState.Title ||
      IsNotRespondingTitle != _lastState.IsNotRespondingTitle;

    bool bodyDirty = layoutDirty ||
      WindowColor != _lastState.WindowColor ||
      NoteOpacity != _lastState.NoteOpacity;

    if (!layoutDirty && !titleBarDirty && !bodyDirty) return;

    if (layoutDirty)
    {
      float viewScale = Mathf.Abs(Mathf.Min(
        PlayerAreaSize.X / Constants.Visual.DESIGN_RESOLUTION.X,
        PlayerAreaSize.Y / Constants.Visual.DESIGN_RESOLUTION.Y
      ));

      Vector2 scaledSize = WindowSize * viewScale;
      TitleBarHeight = Mathf.Min(ScreenSize.X, ScreenSize.Y) * TitleBarHeightRatio;

      float totalHeight = scaledSize.Y + (!Borderless ? TitleBarHeight : 0f);
      Vector2 bodyOffset = new(
        -scaledSize.X * Pivot.X,
        -totalHeight * Pivot.Y + (!Borderless ? TitleBarHeight : 0f)
      );

      WindowBody.Size = scaledSize;
      WindowBody.Position = bodyOffset;

      TitleBar.Visible = !Borderless;
      TitleBar.Size = new Vector2(scaledSize.X, TitleBarHeight);
      TitleBar.Position = bodyOffset - new Vector2(0f, TitleBarHeight);

      if (WindowFrame is not null)
      {
        WindowFrame.Visible = !Borderless;
        WindowFrame.Size = new Vector2(scaledSize.X, scaledSize.Y + TitleBarHeight);
        WindowFrame.Position = TitleBar.Position;
        WindowFrame.QueueRedraw();
      }

      _lastState.Pivot = Pivot;
      _lastState.ScreenSize = ScreenSize;
      _lastState.PlayerAreaSize = PlayerAreaSize;
      _lastState.WindowSize = WindowSize;
      _lastState.Borderless = Borderless;
    }

    if (titleBarDirty)
    {
      TitleBar.QueueRedraw();

      _lastState.TitleBarColor = TitleBarColor;
      _lastState.TitleTextColor = TitleTextColor;
      _lastState.Title = Title;
      _lastState.IsNotRespondingTitle = IsNotRespondingTitle;
    }

    if (bodyDirty)
    {
      WindowBody.QueueRedraw();

      // Apply NoteOpacity to layers containing notes
      Color noteModulate = new(1f, 1f, 1f, NoteOpacity);
      if (NoteLayer is not null) NoteLayer.Modulate = noteModulate;
      if (FocusNoteLayer is not null) FocusNoteLayer.Modulate = noteModulate;

      _lastState.WindowColor = WindowColor;
      _lastState.NoteOpacity = NoteOpacity;
    }
  }

  // --- Draw callbacks ---

  private void OnTitleBarDraw()
  {
    if (Borderless) return;

    float w = TitleBar.Size.X;
    float h = TitleBar.Size.Y;

    TitleBar.DrawRect(new Rect2(Vector2.Zero, TitleBar.Size), TitleBarColor);

    float margin = h * 0.2f;
    float btnSize = h * 0.6f;
    float spacing = h * 1.25f;
    float iconSize = h * 0.7f;

    float iconWidth = margin + iconSize + margin;
    float oneBtn = margin + btnSize + margin;
    float twoBtns = margin + btnSize * 2f + spacing + margin;
    float threeBtns = margin + btnSize * 3f + spacing * 2f + margin;

    bool showIcon = w >= iconWidth;
    bool showClose = w >= iconWidth + oneBtn;
    bool showMax = w >= iconWidth + twoBtns;
    bool showMin = w >= iconWidth + threeBtns;

    int fontSize = (int)(h * 0.55f);
    bool fontReady = _fontFile is not null;

    string titleText =
      IsNotRespondingTitle ? (Title ?? "") + " (Not Responding)" : (Title ?? "");
    string displayTitle = "";
    if (showClose && fontReady && titleText.Length > 0)
    {
      float avail = w - iconWidth - threeBtns - 10f;

      if (_fontFile.GetStringSize(titleText, fontSize: fontSize).X <= avail)
      {
        displayTitle = titleText;
      }
      else
      {
        for (int i = titleText.Length - 1; i >= 1; i--)
        {
          string candidate = titleText[..i] + "...";
          if (_fontFile.GetStringSize(candidate, fontSize: fontSize).X <= avail)
          {
            displayTitle = candidate;
            break;
          }
        }
      }
    }

    float currentX = margin;
    if (showIcon && _iconTex is not null)
    {
      TitleBar.DrawTextureRect(
        _iconTex,
        new Rect2(margin, (h - iconSize) / 2f, iconSize, iconSize),
        false
      );
      currentX += iconSize + margin;
    }

    if (!string.IsNullOrEmpty(displayTitle))
    {
      float ascent = _fontFile.GetAscent(fontSize);
      Vector2 textPos = new(
        currentX,
        h / 2f + ascent / 2f - 2f * (h / 27f)
      );
      TitleBar.DrawString(_fontFile, textPos, displayTitle, modulate: TitleTextColor, fontSize: fontSize);
    }

    float btnX = w - margin - btnSize;
    float btnY = (h - btnSize) / 2f;

    if (showClose && _closeTex is not null)
    {
      TitleBar.DrawTextureRect(_closeTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
      btnX -= btnSize + spacing;
    }
    if (showMax && _maxTex is not null)
    {
      TitleBar.DrawTextureRect(_maxTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
      btnX -= btnSize + spacing;
    }
    if (showMin && _minTex is not null)
    {
      TitleBar.DrawTextureRect(_minTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
    }

    if (UnresponsiveOverlayOpacity > 0f)
    {
      TitleBar.DrawRect(new Rect2(Vector2.Zero, TitleBar.Size), new Color(1f, 1f, 1f, UnresponsiveOverlayOpacity));
    }
  }

  private void OnWindowBodyDraw()
  {
    Color bgColor = WindowColor with
    {
      A = WindowColor.A * Mathf.Lerp(1f, 0.9f, UnFocusOverlayOpacity)
    };

    // Background only — notes and overlays live in Z-ordered layers above
    WindowBody.DrawRect(new Rect2(Vector2.Zero, WindowBody.Size), bgColor);
  }

  private void OnUnfocusOverlayDraw()
  {
    Color unfocusColor = UnfocusOverlayTint with
    {
      A = UnFocusOverlayOpacity
    };

    UnfocusOverlay.DrawRect(
      new Rect2(Vector2.Zero, UnfocusOverlay.Size),
      unfocusColor
    );
  }

  private void OnUnresponsiveOverlayDraw()
  {
    Color unresponsiveColor = UnresponsiveOverlayTint with
    {
      A = UnresponsiveOverlayOpacity
    };

    UnresponsiveOverlay.DrawRect(
      new Rect2(Vector2.Zero, UnresponsiveOverlay.Size),
      unresponsiveColor
    );
  }
}
