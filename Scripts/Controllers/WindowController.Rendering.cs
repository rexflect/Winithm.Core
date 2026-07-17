using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
using Winithm.Core.Behaviors.Windows;
using Winithm.Core.Common;
using Winithm.Core.Data;

namespace Winithm.Core.Controllers;

public partial class WindowController
{
  private readonly List<string> _staleIds = [];
  private void CollectStaleWindows()
  {
    _staleIds.Clear();
    foreach (var kvp in _windowStates)
    {
      if (kvp.Value.FrameSessionToken != _frameSessionToken)
      {
        _staleIds.Add(kvp.Key);
      }
    }

    foreach (var id in _staleIds)
    {
      _windowPool?.Release(_windowStates[id].Visual);
      _windowStates.Remove(id);
      _noteController?.UnregisterWindow(id);
    }
  }

  private static int FindRenderCursor(double[] maxEnds, double currentBeat)
  {
    if (maxEnds == null || maxEnds.Length == 0) return 0;
    int left = 0, right = maxEnds.Length - 1;
    int best = maxEnds.Length;

    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (maxEnds[mid] >= currentBeat)
      {
        best = mid;
        right = mid - 1;
      }
      else
      {
        left = mid + 1;
      }
    }
    return best;
  }

  private void AnimateMissFocusGrayscale(
    WindowBase windowVisual, WindowData windowData, double currentBeat
)
  {
    if (_audioController?.CurrentTime is null)
    {
      windowVisual.MissFocusGrayscale = 0f;
      GD.PushWarning(
          "[WindowController] Cannot animate focusable overlay since _audioController.CurrentTime is not initialized."
      );
      return;
    }

    var period = GetMissFocusPeriodAt(windowData.ID, _audioController.CurrentTime);

    if (period is not { } p)
    {
      windowVisual.MissFocusGrayscale = 0f;
      return;
    }

    // Infinite period -> giữ trạng thái đầy đủ
    if (double.IsNaN(p.End))
    {
      windowVisual.MissFocusGrayscale = 1f;
      return;
    }

    double duration = p.End - p.Start;

    if (duration <= 0)
    {
      windowVisual.MissFocusGrayscale = 1f;
      return;
    }

    float progress = Mathf.Clamp((float)((currentBeat - p.Start) / duration), 0f, 1f);

    windowVisual.MissFocusGrayscale = Mathf.Lerp(
        1f,
        0f,
        (float)EasingFunctions.Evaluate(EasingType.CubicOut, progress)
    );
  }

  private static void AnimateUnresponsiveOverlay(
    WindowBase windowVisual, WindowData windowData, double currentBeat
  )
  {
    if (currentBeat < windowData.UnresponsiveStartBeat)
    {
      windowVisual.UnresponsiveOverlayOpacity = 0f;
      windowVisual.IsNotResponding = false;
      if (windowVisual.WindowBody != null) windowVisual.WindowBody.Modulate = Colors.White;
    }
    else if (currentBeat < windowData.UnresponsiveEndBeat)
    {
      windowVisual.IsNotResponding = true;

      double t =
        (currentBeat - windowData.UnresponsiveStartBeat)
        / (windowData.UnresponsiveEndBeat - windowData.UnresponsiveStartBeat);

      float easingVal = (float)EasingFunctions.Evaluate(EasingType.CubicOut, t);
      // Reduce opacity to up to 50% (0.5f)
      float windowModulateVal = Mathf.Lerp(1f, 0.5f, easingVal);
      windowVisual.UnresponsiveOverlayOpacity = 0f; // No separate overlay anymore, just window opacity
      if (windowVisual.WindowBody != null) windowVisual.WindowBody.Modulate = new Color(1f, 1f, 1f, windowModulateVal);
    }
    else
    {
      windowVisual.IsNotResponding = true;
      windowVisual.UnresponsiveOverlayOpacity = 0f; // No separate overlay
      if (windowVisual.WindowBody != null) windowVisual.WindowBody.Modulate = new Color(1f, 1f, 1f, 0.5f);
    }
  }

  /// <summary>
  /// Lifecycle scale for spawn/despawn animations.
  /// Purely beat-driven interpolation using accurate pre-computed animation bounds.
  /// </summary>
  protected static float CalculateLifeCycleScale(WindowData windowData, double currentBeat)
  {
    if (currentBeat < windowData.StartInStartBeat) return 0f;
    if (currentBeat > windowData.EndOutEndBeat) return 0f;

    // Spawn fade-in
    if (currentBeat < windowData.StartInEndBeat)
    {
      double t = (currentBeat - windowData.StartInStartBeat) / (windowData.StartInEndBeat - windowData.StartInStartBeat);
      return (float)EasingFunctions.Evaluate(EasingType.CubicOut, t);
    }

    // Despawn fade-out
    if (currentBeat >= windowData.EndOutStartBeat)
    {
      double t = (currentBeat - windowData.EndOutStartBeat) / (windowData.EndOutEndBeat - windowData.EndOutStartBeat);
      return (float)(1f - EasingFunctions.Evaluate(EasingType.CubicIn, t));
    }

    return 1f;
  }

  protected static float EvaluateProperty(
    WindowData windowData,
    StoryboardProperty propType,
    double currentBeat,
    float defaultValue
  )
  {
    if (windowData.StoryboardEvents is null || !windowData.StoryboardEvents.TryGetValue(propType, out _)) return defaultValue;

    return windowData.StoryboardEvents.Evaluate(
      propType, currentBeat, new AnyValue(defaultValue)
    ).X;
  }

  /// <summary>
  /// Evaluates storyboard-driven position, scale, title and color/theme,
  /// applies group transform, and writes the final transform/appearance onto the visual.
  /// </summary>
  private void ApplyWindowTransformAndAppearance(
    WindowBase windowVisual,
    WindowData windowData,
    double currentBeat,
    float lifeCycleScale,
    bool force)
  {
    float x = EvaluateProperty(
      windowData, StoryboardProperty.X, currentBeat, windowData.InitX
    );
    float y = EvaluateProperty(
      windowData, StoryboardProperty.Y, currentBeat, windowData.InitY
    );
    float scaleX = EvaluateProperty(
      windowData, StoryboardProperty.ScaleX, currentBeat, windowData.InitScaleX
    );
    float scaleY = EvaluateProperty(
      windowData, StoryboardProperty.ScaleY, currentBeat, windowData.InitScaleY
    );

    if (windowData.StoryboardEvents is not null
      && windowData.StoryboardEvents.TryGetValue(StoryboardProperty.Title, out var titleEvents)
      && titleEvents?.Count > 0
    )
    {
      var titleVal = windowData.StoryboardEvents.Evaluate(
        StoryboardProperty.Title, currentBeat, new(windowData.Title)
      );
      if (titleVal.Type is AnyValueType.String) windowVisual.Title = titleVal.StringValue ?? string.Empty;
    }

    float animScale = Mathf.Lerp(0.95f, 1.0f, lifeCycleScale);

    var finalPos = new Vector2(x, y);
    var finalScale = new Vector2(scaleX, scaleY) * animScale;

    if (_groupController is not null && !string.IsNullOrEmpty(windowData.GroupID))
    {
      var gNode = force ?
        _groupController.ForceGetGroupNode(windowData.GroupID, currentBeat)
        : _groupController.GetGroupNode(windowData.GroupID, currentBeat);

      if (IsInstanceValid(gNode))
      {
        var gTrans = gNode.GlobalTransform;
        finalPos = gTrans * finalPos;

        finalScale.X *= gNode.GlobalScale.X;
        finalScale.Y *= gNode.GlobalScale.Y;
      }
    }

    var viewScale = new Vector2(
      PlayerAreaSize.X / Constants.Visual.DESIGN_RESOLUTION.X,
      PlayerAreaSize.Y / Constants.Visual.DESIGN_RESOLUTION.Y
    );

    windowVisual.Position = finalPos * viewScale.Abs();
    windowVisual.RotationDegrees = 0f;
    windowVisual.WindowSize = finalScale * viewScale.Abs();

    var finalWindowColor = windowVisual.WindowColor;
    float finalNoteA = windowVisual.NoteOpacity;

    if (_themeController is not null
        && !string.IsNullOrEmpty(windowData.ThemeChannelID)
        && (_themeController.HasThemeChannel(windowData.ThemeChannelID) ?? false)
    )
    {
      var themeColor = _themeController?.GetThemeColor(windowData.ThemeChannelID, currentBeat);
      if (themeColor.HasValue)
      {
        finalWindowColor = themeColor.Value.WindowColor;
        finalNoteA = themeColor.Value.NoteA;
      }
    }
    else
    {
      float r = EvaluateProperty(
        windowData, StoryboardProperty.ColorR, currentBeat, windowData.InitR
      );
      float g = EvaluateProperty(
        windowData, StoryboardProperty.ColorG, currentBeat, windowData.InitG
      );
      float b = EvaluateProperty(
        windowData, StoryboardProperty.ColorB, currentBeat, windowData.InitB
      );
      float a = EvaluateProperty(
        windowData, StoryboardProperty.ColorA, currentBeat, windowData.InitA
      );
      float noteA = EvaluateProperty(
        windowData, StoryboardProperty.NoteA, currentBeat, windowData.InitNoteA
      );

      finalWindowColor = new Color(r, g, b, a);
      finalNoteA = noteA;
    }

    windowVisual.WindowColor = finalWindowColor;
    windowVisual.NoteOpacity = finalNoteA;
    windowVisual.Modulate = new Color(1, 1, 1, lifeCycleScale);

    windowVisual.ScreenSize = ScreenSize;
    windowVisual.PlayerAreaSize = PlayerAreaSize;
  }
}