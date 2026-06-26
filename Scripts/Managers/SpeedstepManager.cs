using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// Holds the baked prefix sum distances for a single time frame.
/// </summary>
public class FrameCache
{
  internal double CachedBeat = double.NaN;
  internal float[] PrefixDistance = [];
}

/// <summary>
/// Manages scroll speed segments and visual lane distances.
/// </summary>
public class SpeedStepManager :
  IDeepCloneableUID<SpeedStepManager>, IObjectManager<SpeedStepData>
{
  public event Action<SpeedStepManager>? OnUpdated;

  private readonly List<SpeedStepData> _speedStepCollection = [];
  private FrameCache _frameCache = new();

  public int Count => _speedStepCollection.Count;

  public IEnumerator<SpeedStepData> GetEnumerator() => _speedStepCollection.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public SpeedStepData? this[int index] => _speedStepCollection.ElementAtOrDefault(index);

  public ICollection<SpeedStepData> Values => _speedStepCollection;
  public bool TryGetValue(string id, out SpeedStepData? value)
  {
    value = GetSpeedStep(id);
    return value is not null;
  }

  private int _updateLockCount = 0;

  /// <summary>
  /// Suspends notifications to allow batch edits.
  /// </summary>
  public void BeginUpdate() => _updateLockCount++;

  /// <summary>
  /// Resumes notifications and clears frame cache if edits were made.
  /// </summary>
  public void EndUpdate(bool success = true)
  {
    if (_updateLockCount > 0) _updateLockCount--;
    if (_updateLockCount == 0 && success)
    {
      if (_frameCache is not null) _frameCache.CachedBeat = double.NaN;
      OnUpdated?.Invoke(this);
    }
  }

  private void NotifyChanged()
  {
    if (_updateLockCount == 0)
    {
      if (_frameCache is not null) _frameCache.CachedBeat = double.NaN;
      OnUpdated?.Invoke(this);
    }
  }

  public SpeedStepManager DeepCloner(ObjectFactory objectFactory, BeatTime? offset)
  {
    var newSpeedStep = new SpeedStepManager();

    newSpeedStep.BeginUpdate();

    foreach (var speedStep in _speedStepCollection)
      newSpeedStep.AddSpeedStep(speedStep.DeepCloner(objectFactory, offset));

    newSpeedStep.EndUpdate();

    return newSpeedStep;
  }

  private void SubscribeChangeSpeedSteps(SpeedStepData speedStep)
  {
    speedStep.OnStartBeatChanged -= HandleStartBeatChanged;
    speedStep.OnStartBeatChanged += HandleStartBeatChanged;

    speedStep.OnUpdated -= HandleUpdated;
    speedStep.OnUpdated += HandleUpdated;
  }

  private void UnSubscribeChangeSpeedSteps(SpeedStepData speedStep)
  {
    speedStep.OnStartBeatChanged -= HandleStartBeatChanged;
    speedStep.OnUpdated -= HandleUpdated;
  }

  private void HandleStartBeatChanged(SpeedStepData speedStep, double prevStartBeat)
  {
    RemoveSpeedStep(speedStep);
    AddSpeedStep(speedStep);
  }

  private void HandleUpdated(SpeedStepData speedStep)
  {
    NotifyChanged();
  }

  public int AddSpeedStep(SpeedStepData speedStep)
  {
    int index = FindAddIndex(speedStep);
    _speedStepCollection.Insert(index, speedStep);
    SubscribeChangeSpeedSteps(speedStep);

    NotifyChanged();
    return index;
  }

  public int[] AddSpeedSteps(IEnumerable<SpeedStepData> speedSteps)
  {
    if (!speedSteps.Any()) return [];

    BeginUpdate();

    var indices = new int[speedSteps.Count()];
    for (int i = 0; i < speedSteps.Count(); i++)
      indices[i] = AddSpeedStep(speedSteps.ElementAt(i));

    EndUpdate();

    return indices;
  }

  public bool RemoveSpeedStep(SpeedStepData speedStep)
  {
    if (!_speedStepCollection.Remove(speedStep)) return false;
    UnSubscribeChangeSpeedSteps(speedStep);

    NotifyChanged();
    return true;
  }

  public int RemoveSpeedSteps(IEnumerable<SpeedStepData> speedSteps)
  {
    if (!speedSteps.Any()) return 0;

    BeginUpdate();

    int removedCount = 0;
    foreach (var speedStep in speedSteps)
      if (RemoveSpeedStep(speedStep)) removedCount++;

    EndUpdate();

    return removedCount;
  }

  public bool RemoveSpeedStep(string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    var toRemove = _speedStepCollection.FindAll(st => st.ID == id);
    if (toRemove.Count == 0) return false;

    foreach (var st in toRemove) UnSubscribeChangeSpeedSteps(st);
    _speedStepCollection.RemoveAll(x => x.ID == id);

    NotifyChanged();
    return true;
  }

  public int RemoveSpeedSteps(IEnumerable<string> ids)
  {
    if (ids.Count() == 0) return 0;

    BeginUpdate();

    int removedCount = 0;
    foreach (var id in ids)
      if (RemoveSpeedStep(id)) removedCount++;

    EndUpdate();

    return removedCount;
  }


  public SpeedStepData? GetSpeedStep(string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    var speedStep = _speedStepCollection.FirstOrDefault(st => st.ID == id);

    if (speedStep is null) return null;
    return speedStep;
  }

  public IReadOnlyList<SpeedStepData> GetSpeedSteps(IEnumerable<string> ids)
  {
    if (!ids.Any()) return [];

    var result = new List<SpeedStepData>();
    foreach (var id in ids)
    {
      var speedStep = GetSpeedStep(id);
      if (speedStep is not null) result.Add(speedStep);
    }

    return result;
  }

  public SpeedStepData? GetFirst()
  {
    if (_speedStepCollection.Count == 0) return null;
    return _speedStepCollection[0];
  }

  public SpeedStepData? GetLast()
  {
    if (_speedStepCollection.Count == 0) return null;
    return _speedStepCollection[_speedStepCollection.Count - 1];
  }

  /// <summary>
  /// Rebuilds the distance cache for the current beat.
  /// </summary>
  public void BakeFrameCache(double currentBeat)
  {
    if (_frameCache is null || _speedStepCollection.Count == 0) return;

    // Rebuild only when the beat has actually changed.
    if (_frameCache.CachedBeat == currentBeat) return;

    _frameCache.CachedBeat = currentBeat;

    int n = _speedStepCollection.Count;
    if (_frameCache.PrefixDistance.Length < n)
      _frameCache.PrefixDistance = new float[n];

    // Origin is at steps[0].Start
    _frameCache.PrefixDistance[0] = 0f;

    for (int i = 1; i < n; i++)
    {
      double segStart = _speedStepCollection[i - 1].StartBeat.AbsoluteValue;
      double segEnd = _speedStepCollection[i].StartBeat.AbsoluteValue;
      double segLen = segEnd - segStart;

      float speed = EvaluateSpeed(_speedStepCollection[i], currentBeat);
      _frameCache.PrefixDistance[i] = _frameCache.PrefixDistance[i - 1] + speed * (float)segLen;
    }
  }

  /// <summary>
  /// Returns the visual offset of targetBeat relative to currentBeat.
  /// </summary>
  public float GetVisualOffset(double currentBeat, double targetBeat, bool forceRebuild = false)
  {
    if (Math.Abs(currentBeat - targetBeat) < 0.0001f) return 0f;

    if (_frameCache is null) _frameCache = new FrameCache();
    if (_frameCache.CachedBeat != currentBeat && !forceRebuild) BakeFrameCache(currentBeat);

    double laneStart = _speedStepCollection.Count > 0 ? _speedStepCollection[0].StartBeat.AbsoluteValue : 0.0;

    // Clamp both ends to lane start — no visual meaning before spawn.
    double clampedCurrent = Math.Max(currentBeat, laneStart);
    double clampedTarget = Math.Max(targetBeat, laneStart);
    if (Math.Abs(clampedCurrent - clampedTarget) < 0.0001f) return 0f;

    float distCurrent = DistanceFromOrigin(currentBeat, clampedCurrent);
    float distTarget = DistanceFromOrigin(currentBeat, clampedTarget);

    return distTarget - distCurrent;
  }

  /// <summary>
  /// Calculates current scroll speed multiplier at a specific beat.
  /// </summary>
  public float GetSpeedAt(double currentBeat, double beat)
  {
    if (_speedStepCollection is null || _speedStepCollection.Count == 0) return 1f;

    int n = _speedStepCollection.Count;
    double lastStart = _speedStepCollection[n - 1].StartBeat.AbsoluteValue;
    if (beat >= lastStart)
    {
      return EvaluateSpeed(_speedStepCollection[n - 1], currentBeat);
    }

    int idx = FindStepIndex(beat);
    return EvaluateSpeed(_speedStepCollection[idx + 1], currentBeat);
  }

  private float DistanceFromOrigin(double currentBeat, double beat)
  {
    if (_speedStepCollection.Count == 0) return 0f;

    double laneStart = _speedStepCollection[0].StartBeat.AbsoluteValue;
    if (beat <= laneStart) return 0f;

    int n = _speedStepCollection.Count;

    // Beat is past the last step — extend with last step's speed.
    double lastStart = _speedStepCollection[n - 1].StartBeat.AbsoluteValue;
    if (beat >= lastStart)
    {
      double tail = beat - lastStart;
      float speed = EvaluateSpeed(_speedStepCollection[n - 1], currentBeat);
      return _frameCache.PrefixDistance[n - 1] + speed * (float)tail;
    }

    int idx = FindStepIndex(beat);

    double segStart = _speedStepCollection[idx].StartBeat.AbsoluteValue;
    double tail2 = beat - segStart;
    float speed2 = EvaluateSpeed(_speedStepCollection[idx + 1], currentBeat);
    return _frameCache.PrefixDistance[idx] + speed2 * (float)tail2;
  }

  private static float EvaluateSpeed(SpeedStepData step, double currentBeat)
  {
    return step.StoryboardEvents.Evaluate(StoryboardProperty.Speed, currentBeat, new AnyValue(step.Multiplier)).X;
  }

  public int FindAddIndex(SpeedStepData speedStep)
  {
    if (_speedStepCollection.Count == 0) return 0;

    int left = 0, right = _speedStepCollection.Count - 1;
    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (_speedStepCollection[mid].StartBeat <= speedStep.StartBeat) left = mid + 1;
      else right = mid - 1;
    }
    return left;
  }

  private int FindStepIndex(double targetBeat)
  {
    if (_speedStepCollection.Count == 0) return -1;
    if (targetBeat < _speedStepCollection[0].StartBeat.AbsoluteValue) return 0;

    int lo = 0, hi = _speedStepCollection.Count - 1;
    while (lo < hi)
    {
      int mid = (lo + hi + 1) / 2;
      if (_speedStepCollection[mid].StartBeat.AbsoluteValue <= targetBeat) lo = mid;
      else hi = mid - 1;
    }
    return lo;
  }
}
