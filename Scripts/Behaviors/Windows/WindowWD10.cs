using Godot;

namespace Winithm.Core.Behaviors.Windows;

/// <summary>
/// Windows Desktop-style window chrome.
/// Flat title bar with icon (left), title text (center-left),
/// and Close / Maximize / Minimize buttons (right).
/// </summary>
public partial class WindowWD10 : WindowBase
{
  // ---------------------------------------------------------------------------
  // Resources — loaded once per class, not per instance
  // ---------------------------------------------------------------------------

  private static readonly Texture2D _iconTex = GD.Load<Texture2D>("res://icon.svg");
  private static readonly Texture2D _closeTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/close.svg");
  private static readonly Texture2D _maxTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/maximize.svg");
  private static readonly Texture2D _minTex = GD.Load<Texture2D>("res://Winithm.Core/Resources/Icons/Window/minimize.svg");

  private static readonly FontFile _fontFile = GD.Load<FontFile>("res://Winithm.Core/Resources/Fonts/Quicksand.ttf");

  protected override void OnReady()
  {
    base.OnReady();

    WindowFrame?.Draw += OnWindowFrameDraw;
  }

  // ---------------------------------------------------------------------------
  // Draw — window frame border
  // ---------------------------------------------------------------------------

  protected override void OnWindowFrameDraw()
  {
    if (Borderless) return;

    var color = TitleBarColor with { A = 0.5f };

    float lineWidth = Mathf.Max(1f, TitleBarHeight * 0.025f);
    WindowFrame?.DrawRect(
        new Rect2(Vector2.Zero, WindowFrame.Size),
        color,
        false,
        lineWidth
    );
  }

  // ---------------------------------------------------------------------------
  // Draw — title bar
  // ---------------------------------------------------------------------------

  protected override void OnTitleBarDraw()
  {
    if (Borderless) return;
    if (!IsInstanceValid(TitleBar))
    {
      GD.PushWarning("[WindowWD] No TitleBar node");
      return;
    }

    float w = TitleBar.Size.X;
    float h = TitleBar.Size.Y;

    TitleBar.DrawRect(new Rect2(Vector2.Zero, TitleBar.Size), TitleBarColor);

    // --- Metric constants ---
    float margin = h * 0.2f;
    float btnSize = h * 0.6f;
    float spacing = h * 1.25f;
    float iconSize = h * 0.7f;

    float iconWidth = margin + iconSize + margin;
    float oneBtn = margin + btnSize + margin;
    float twoBtns = margin + btnSize * 2f + spacing + margin;
    float threeBtns = margin + btnSize * 3f + spacing * 2f + margin;

    // --- Visibility thresholds (responsive to narrow windows) ---
    bool showIcon = w >= iconWidth;
    bool showClose = w >= iconWidth + oneBtn;
    bool showMax = w >= iconWidth + twoBtns;
    bool showMin = w >= iconWidth + threeBtns;

    int fontSize = (int)(h * 0.55f);
    bool fontReady = IsInstanceValid(_fontFile);

    // --- Title text with "(Not Responding)" suffix ---
    string titleText = IsNotRespondingTitle ? (Title ?? "") + " (Not Responding)" : (Title ?? "");
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

    // --- Left side: icon → title ---
    float currentX = margin;

    if (showIcon && IsInstanceValid(_iconTex))
    {
      TitleBar.DrawTextureRect(
        _iconTex,
        new Rect2(margin, (h - iconSize) / 2f, iconSize, iconSize),
        false
      );
      currentX += iconSize + margin;
    }

    if (!string.IsNullOrEmpty(displayTitle) && IsInstanceValid(_fontFile))
    {
      float ascent = _fontFile.GetAscent(fontSize);
      var textPos = new Vector2(
        currentX,
        h / 2f + ascent / 2f - 2f * (h / 27f)
      );
      TitleBar.DrawString(_fontFile, textPos, displayTitle, modulate: TitleTextColor, fontSize: fontSize);
    }

    // --- Right side: Close → Maximize → Minimize (drawn right-to-left) ---
    float btnX = w - margin - btnSize;
    float btnY = (h - btnSize) / 2f;

    if (showClose && IsInstanceValid(_closeTex))
    {
      TitleBar.DrawTextureRect(_closeTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
      btnX -= btnSize + spacing;
    }
    if (showMax && IsInstanceValid(_maxTex))
    {
      TitleBar.DrawTextureRect(_maxTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
      btnX -= btnSize + spacing;
    }
    if (showMin && IsInstanceValid(_minTex))
    {
      TitleBar.DrawTextureRect(_minTex, new Rect2(btnX, btnY, btnSize, btnSize), false, TitleTextColor);
    }

    // --- Unresponsive white tint on top of title bar ---
    if (UnresponsiveOverlayOpacity > 0f)
    {
      TitleBar.DrawRect(
        new Rect2(Vector2.Zero, TitleBar.Size),
        new Color(1f, 1f, 1f, UnresponsiveOverlayOpacity)
      );
    }
  }

  // ---------------------------------------------------------------------------
  // Draw — window body background
  // ---------------------------------------------------------------------------

  protected override void OnWindowBodyDraw()
  {
    if (!IsInstanceValid(WindowBody)) return;

    Color bgColor = WindowColor with
    {
      A = WindowColor.A * Mathf.Lerp(1f, 0.9f, UnFocusOverlayOpacity)
    };

    WindowBody.DrawRect(new Rect2(Vector2.Zero, WindowBody.Size), bgColor);
  }

  // ---------------------------------------------------------------------------
  // Draw — overlays
  // ---------------------------------------------------------------------------

  protected override void OnUnfocusOverlayDraw()
  {
    if (!IsInstanceValid(UnfocusOverlay)) return;

    Color unfocusColor = UnfocusOverlayTint with { A = UnFocusOverlayOpacity };
    UnfocusOverlay.DrawRect(new Rect2(Vector2.Zero, UnfocusOverlay.Size), unfocusColor);
  }

  protected override void OnUnresponsiveOverlayDraw()
  {
    if (!IsInstanceValid(UnresponsiveOverlay)) return;

    Color unresponsiveColor = UnresponsiveOverlayTint with { A = UnresponsiveOverlayOpacity };
    UnresponsiveOverlay.DrawRect(new Rect2(Vector2.Zero, UnresponsiveOverlay.Size), unresponsiveColor);
  }
}