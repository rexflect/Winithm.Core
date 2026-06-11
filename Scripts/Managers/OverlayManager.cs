using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// Manages WindowData collections and monitors nested sub-manager changes.
/// </summary>
public class OverlayManager : IObjectManager<OverlayData>
{
  public event Action<OverlayManager> OnUpdated;


  /// <summary>
  /// Collection of overlays sorted by StartBeat.
  /// </summary>
  private readonly List<OverlayData> _overlayCollection = [];
  public int Count => _overlayCollection.Count;

  public IEnumerator<OverlayData> GetEnumerator() => _overlayCollection.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


  public OverlayData this[int index] => _overlayCollection.ElementAtOrDefault(index);

  public ICollection<OverlayData> Values => _overlayCollection;
  public bool TryGetValue(string id, out OverlayData value)
  {
    value = GetOverlay(id);
    return value != null;
  }

  /// <summary>
  /// Prefix-max of EndBeatEndOut over overlayss for binary search.
  /// </summary>
  public double[] MaxEndBeats { get; private set; } = [];

  private int _updateLockCount = 0;
  private bool _needsRecompute = false;

  /// <summary>
  /// Suspends notifications to allow batch edits.
  /// </summary>
  public void BeginUpdate() => _updateLockCount++;

  /// <summary>
  /// Resumes notifications and runs Compute if edits were made.
  /// </summary>
  public void EndUpdate(bool success = true)
  {
    if (_updateLockCount > 0) _updateLockCount--;
    if (_updateLockCount == 0 && success)
    {
      CommitRecompute();
      OnUpdated?.Invoke(this);
    }
  }

  private void NotifyChanged()
  {
    if (_updateLockCount == 0)
    {
      CommitRecompute();
      OnUpdated?.Invoke(this);
    }
  }

  private void RequestRecompute() => _needsRecompute = true;

  private void CommitRecompute()
  {
    if (_needsRecompute)
    {
      Compute();
      _needsRecompute = false;
    }
  }

  /// <summary>
  /// Rebuilds MaxEndBeats and PrefixCombo based on the current WindowCollection.
  /// </summary>
  public void Compute()
  {
    MaxEndBeats = new double[_overlayCollection.Count];

    double runningMax = double.MinValue;

    for (int i = 0; i < _overlayCollection.Count; i++)
    {
      var overlay = _overlayCollection[i];

      runningMax = Math.Max(runningMax, overlay.EndBeat.AbsoluteValue);
      MaxEndBeats[i] = runningMax;
    }
  }

  // ==========================================
  // Event Subscription
  // ==========================================

  private void SubscribeChangeEvent(OverlayData overlayData)
  {
    overlayData.OnUpdated -= HandleUpdated;
    overlayData.OnUpdated += HandleUpdated;

    overlayData.OnLifeCycleChanged -= HandleLifeCycleChanged;
    overlayData.OnLifeCycleChanged += HandleLifeCycleChanged;
  }

  private void UnsubscribeChangeEvent(OverlayData overlayData)
  {
    overlayData.OnUpdated -= HandleUpdated;
    overlayData.OnLifeCycleChanged -= HandleLifeCycleChanged;
  }

  private void HandleUpdated(OverlayData overlayData) => NotifyChanged();
  private void HandleLifeCycleChanged(OverlayData overlayData)
  {
    RequestRecompute();
    NotifyChanged();
  }

  /// <summary>
  /// Adds a window to the collection and maintains sort order.
  /// </summary>
  public void AddOverlay(OverlayData overlay)
  {
    var idx = FindAddIndex(_overlayCollection, overlay);
    _overlayCollection.Insert(idx, overlay);
    SubscribeChangeEvent(overlay);

    RequestRecompute();
    NotifyChanged();
  }

  public void AddOverlays(IEnumerable<OverlayData> overlays)
  {
    if (!overlays.Any()) return;

    BeginUpdate();
    foreach (var overlay in overlays) AddOverlay(overlay);
    EndUpdate();
  }

  /// <summary>
  /// Removes a window by its unique identifier.
  /// </summary>
  public bool RemoveOverlay(string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    var overlay = _overlayCollection.FirstOrDefault(o => o.ID == id);
    if (overlay == default) return false;

    UnsubscribeChangeEvent(overlay);
    _overlayCollection.Remove(overlay);

    RequestRecompute();
    NotifyChanged();

    return true;
  }

  public int RemoveOverlays(IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();
    int success = ids.Count(RemoveOverlay);
    EndUpdate(success > 0);

    return success;
  }

  // ==========================================
  // Fetch Methods
  // ==========================================

  public OverlayData GetOverlay(string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    var result = _overlayCollection.FirstOrDefault(o => o.ID == id);

    if (result == default) return null;

    return result;
  }

  public IReadOnlyList<OverlayData> GetOverlays(IEnumerable<string> ids)
  {
    var result = new List<OverlayData>();
    foreach (var id in ids)
    {
      var overlay = GetOverlay(id);
      if (overlay != null) result.Add(overlay);
    }
    return result;
  }

  public IReadOnlyList<OverlayData> GetAllOverlays() => _overlayCollection;

  /// <summary>
  /// Returns all windows sorted by layer for correct render order.
  /// </summary>
  public IReadOnlyList<OverlayData> GetOverlayByLayer()
  {
    var overlays = new List<OverlayData>(_overlayCollection);
    overlays.Sort((a, b) => a.Layer.CompareTo(b.Layer));
    return overlays;
  }

  /// <summary>
  /// Finds the insertion index for an overlay to keep the list sorted by StartBeat.
  /// </summary>
  public int FindAddIndex(List<OverlayData> list, OverlayData target)
  {
    if (list.Count == 0) return 0;

    int left = 0, right = list.Count - 1;
    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (list[mid].StartBeat <= target.StartBeat) left = mid + 1;
      else right = mid - 1;
    }
    return left;
  }
}

