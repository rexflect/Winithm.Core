using Godot;
using Winithm.Core.Common;

namespace Winithm.Core.Behaviors.Windows;

/// <summary>
/// Windows 10-accurate window chrome: flat title bar with icon, left-aligned title,
/// and Close / Maximize / Minimize caption buttons on the right.
/// </summary>
public partial class WindowED : WindowBase
{
  // Resources — loaded once per class, not per instance
  private static readonly Texture2D _iconTex = GD.Load<Texture2D>("res://icon.svg");
  private static readonly FontFile _fontFile = GD.Load<FontFile>("res://Winithm.Core/Resources/Fonts/SegoeUI.ttf");

  protected override void OnWindowLayoutUpdate()
  {
    float scale = OSDisplayUtils.GetReferenceResolutionScale(ScreenSize);

    // Win10 title bar is 28 logical px tall at 100% scale.
    TitleBarHeight = 28f * scale;

    float totalHeight = WindowSize.Y + (!Borderless ? TitleBarHeight : 0f);
    var bodyOffset = new Vector2(
      -WindowSize.X * Pivot.X,
      -totalHeight * Pivot.Y + (!Borderless ? TitleBarHeight : 0f)
    );

    WindowBody?.Size = WindowSize;
    WindowBody?.Position = bodyOffset;

    TitleBar?.Visible = !Borderless;
    TitleBar?.Size = new Vector2(WindowSize.X, TitleBarHeight);
    TitleBar?.Position = bodyOffset - new Vector2(0f, TitleBarHeight);

    WindowFrame?.Visible = !Borderless;
    WindowFrame?.Size = new Vector2(WindowSize.X, WindowSize.Y + TitleBarHeight);
    WindowFrame?.Position = TitleBar?.Position ?? WindowFrame.Position;
    WindowFrame?.QueueRedraw();

    LastState.Pivot = Pivot;
    LastState.ScreenSize = ScreenSize;
    LastState.PlayerAreaSize = PlayerAreaSize;
    LastState.WindowSize = WindowSize;
    LastState.Borderless = Borderless;
  }

  protected override void OnWindowFrameDraw()
  {
    if (Borderless) return;

    // 1 px border using the active accent color, matching Win10's hairline chrome border.
    WindowFrame?.DrawRect(
        new Rect2(Vector2.Zero, WindowFrame.Size),
        TitleBarColor,
        filled: false,
        width: 1f
    );
  }

  protected override void OnTitleBarDraw()
  {
    if (Borderless) return;
    if (!IsInstanceValid(TitleBar))
    {
      GD.PushWarning("[WindowWD10] TitleBar node is invalid.");
      return;
    }

    float scale = OSDisplayUtils.GetReferenceResolutionScale(ScreenSize);
    
    float w = TitleBar.Size.X;
    float h = TitleBar.Size.Y;

    TitleBar.DrawRect(new Rect2(Vector2.Zero, TitleBar.Size), TitleBarColor);

    // Win10 caption metrics (at 96 DPI / 100% scale)
    float buttonWidth = 46f * scale;     // each caption button slot is 46 px wide
    float iconSize = 18f * scale;
    float iconLeftMargin = 6f * scale;
    float iconTextGap = 4f * scale;
    float textMarginNoIcon = 6f * scale;
    float glyphSize = 10f * scale;       // glyph bounding box for ×, □, and —

    // Hide elements that would overflow or collide.
    bool showIcon = IsInstanceValid(_iconTex) && w >= (iconLeftMargin + iconSize + iconTextGap);
    bool showClose = w >= buttonWidth;
    bool showMax = w >= (buttonWidth * 2f);
    bool showMin = w >= (buttonWidth * 3f);

    float totalButtonsWidth =
      (showClose ? buttonWidth : 0f) +
      (showMax ? buttonWidth : 0f) +
      (showMin ? buttonWidth : 0f);

    // Segoe UI Regular 9 pt at 96 DPI = 12 physical pixels.
    int fontSize = Mathf.RoundToInt(12f * scale);
    bool fontReady = IsInstanceValid(_fontFile);

    // Title X origin: right of icon (or left margin when no icon).
    float textX = showIcon
      ? iconLeftMargin + iconSize + iconTextGap
      : textMarginNoIcon;

    float textRightPad = 8f * scale;
    float availWidth = w - textX - totalButtonsWidth - textRightPad;

    // Build caption string and truncate with ellipsis if it doesn't fit.
    string titleText = IsNotRespondingTitle ? (Title ?? "") + " (Not Responding)" : (Title ?? "");
    string displayTitle = "";

    if (fontReady && titleText.Length > 0 && availWidth > 0f)
    {
      if (_fontFile.GetStringSize(titleText, fontSize: fontSize).X <= availWidth)
      {
        displayTitle = titleText;
      }
      else
      {
        for (int i = titleText.Length - 1; i >= 1; i--)
        {
          string candidate = titleText[..i] + "…"; // U+2026
          if (_fontFile.GetStringSize(candidate, fontSize: fontSize).X <= availWidth)
          {
            displayTitle = candidate;
            break;
          }
        }
      }
    }

    // App icon — centered vertically, floored to avoid sub-pixel blur.
    if (showIcon)
    {
      float iconY = Mathf.Floor((h - iconSize) / 2f);
      TitleBar.DrawTextureRect(_iconTex, new Rect2(iconLeftMargin, iconY, iconSize, iconSize), tile: false);
    }

    // Title text — DrawString origin is the baseline, so compute it from ascent/descent.
    if (!string.IsNullOrEmpty(displayTitle) && fontReady)
    {
      float ascent = _fontFile.GetAscent(fontSize);
      float descent = _fontFile.GetDescent(fontSize);
      float lineHeight = ascent + descent;
      float baselineY = Mathf.Floor((h - lineHeight) / 2f) + ascent;

      TitleBar.DrawString(
        _fontFile,
        new Vector2(textX, baselineY),
        displayTitle,
        modulate: TitleTextColor,
        fontSize: fontSize
      );
    }

    // Caption buttons — stroke is always 1 px regardless of DPI (matches Win10).
    // Coordinates are floored so lines stay crisp without AA bleed.
    float thickness = 1f;

    // Close: × glyph (two diagonal lines)
    if (showClose)
    {
      float centerX = Mathf.Floor(w - buttonWidth + buttonWidth / 2f);
      float centerY = Mathf.Floor(h / 2f);
      float half = glyphSize / 2f;

      TitleBar.DrawLine(
        new Vector2(centerX - half, centerY - half),
        new Vector2(centerX + half, centerY + half),
        TitleTextColor, thickness
      );
      TitleBar.DrawLine(
        new Vector2(centerX + half, centerY - half),
        new Vector2(centerX - half, centerY + half),
        TitleTextColor, thickness
      );
    }

    // Maximize: □ glyph (hollow rect)
    if (showMax)
    {
      float centerX = Mathf.Floor(w - buttonWidth * 2f + buttonWidth / 2f);
      float centerY = Mathf.Floor(h / 2f);
      float half = glyphSize / 2f;

      TitleBar.DrawRect(
        new Rect2(centerX - half, centerY - half, glyphSize, glyphSize),
        TitleTextColor,
        filled: false,
        width: thickness
      );
    }

    // Minimize: — glyph (+0.5 snaps the horizontal line to pixel center)
    if (showMin)
    {
      float centerX = Mathf.Floor(w - buttonWidth * 3f + buttonWidth / 2f);
      float centerY = Mathf.Floor(h / 2f) + 0.5f;
      float half = glyphSize / 2f;

      TitleBar.DrawLine(
        new Vector2(centerX - half, centerY),
        new Vector2(centerX + half, centerY),
        TitleTextColor, thickness
      );
    }

    // "Not Responding" tint over the title bar (drawn last, on top of everything).
    if (UnresponsiveOverlayOpacity > 0f)
    {
      TitleBar.DrawRect(
        new Rect2(Vector2.Zero, TitleBar.Size),
        new Color(1f, 1f, 1f, UnresponsiveOverlayOpacity)
      );
    }
  }

  protected override void OnWindowBodyDraw()
  {
    if (!IsInstanceValid(WindowBody)) return;

    // Draw body color at full opacity; unfocus dimming is handled by UnfocusOverlay.
    WindowBody.DrawRect(new Rect2(Vector2.Zero, WindowBody.Size), WindowColor);
  }

  protected override void OnUnfocusOverlayDraw()
  {
    if (!IsInstanceValid(UnfocusOverlay)) return;

    // Semi-transparent tint that dims the window when it loses focus.
    var unfocusColor = UnfocusOverlayTint with { A = UnFocusOverlayOpacity };
    UnfocusOverlay.DrawRect(new Rect2(Vector2.Zero, UnfocusOverlay.Size), unfocusColor);
  }

  protected override void OnUnresponsiveOverlayDraw()
  {
    if (!IsInstanceValid(UnresponsiveOverlay)) return;

    // White wash over the body to match Win10's ghost-window appearance.
    var unresponsiveColor = UnresponsiveOverlayTint with { A = UnresponsiveOverlayOpacity };
    UnresponsiveOverlay.DrawRect(new Rect2(Vector2.Zero, UnresponsiveOverlay.Size), unresponsiveColor);
  }
}