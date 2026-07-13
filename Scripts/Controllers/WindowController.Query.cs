using Godot;
using System.Collections.Generic;

namespace Winithm.Core.Controllers;

public partial class WindowController
{
  /// <summary>
  /// Checks if the provided global position is currently hovering over a specific window.
  /// </summary>
  public bool IsMouseOverWindowId(string windowId, Vector2 mousePos)
  {
    if (!_windowStates.TryGetValue(windowId, out var state)) return false;

    var visual = state.Visual;
    return visual.WindowBody is not null && visual.WindowBody.GetGlobalRect().HasPoint(mousePos);
  }

  /// <summary>
  /// Populates the provided collection with all window IDs that contain the given global position.
  /// </summary>
  public static void GetWindowIdsAtMousePosition(Vector2 mousePos, HashSet<string> results)
  {
    results.Clear();

    foreach (var entry in _windowStates)
      if (IsMouseOverWindowId(entry.Key, mousePos))
        results.Add(entry.Key);
  }

  public int GetTotalComboPassedInDestroyedWindows(double currentBeat)
  {
    if (_windowManager is null || _windowManager.Count == 0) return 0;

    var maxEnds = _windowManager.MaxEndBeats;
    int cursor = FindRenderCursor(maxEnds, currentBeat);

    if (cursor <= 0) return 0;
    return _windowManager.PrefixCombo[cursor - 1];
  }

  /// <summary>
  /// Binary search O(log n) for the last period where Start <= beat,
  /// then checks containment. Periods are sorted by Start (appended chronologically).
  /// Stateless — safe for scrubbing in any direction.
  /// </summary>
  public bool IsFocusableAt(string windowId, double currentBeat)
  {
    if (!_windowStates.TryGetValue(windowId, out var state)) return false;
    var periods = state.Data.FocusablePeriods;

    int count = periods.Count;
    if (count == 0) return false;

    // Binary search: find largest index where Start <= currentBeat
    int lo = 0, hi = count - 1, candidate = -1;
    while (lo <= hi)
    {
      int mid = lo + ((hi - lo) >> 1);
      if (periods[mid].Start <= currentBeat)
      {
        candidate = mid;
        lo = mid + 1;
      }
      else
      {
        hi = mid - 1;
      }
    }

    if (candidate < 0) return false;

    double end = periods[candidate].End;
    return double.IsNaN(end) || currentBeat <= end;
  }

  /// <summary>Returns the IDs of all currently active (rendered) windows.</summary>
  public IEnumerable<string> GetActiveWindowIds() => _windowStates.Keys;
}