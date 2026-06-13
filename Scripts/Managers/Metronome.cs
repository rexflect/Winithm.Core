using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Data;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// Manages BPM changes and converts between time and beats.
/// </summary>
public class Metronome : IObjectManager<BPMStop>
{
  public event Action<Metronome> OnUpdated;

  public BaseBPM BaseBPM { get; private set; } = new()
  {
    BaseOffsetSeconds = 0,
    InitialBPM = 120,
    TimeSignatureNum = 4,
    TimeSignatureDen = 4
  };
  private readonly List<BPMStop> _bPMStops = [];

  public int Count => _bPMStops.Count;
  public IEnumerator<BPMStop> GetEnumerator() => _bPMStops.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public BPMStop this[int index] => _bPMStops.ElementAtOrDefault(index);
  public ICollection<BPMStop> Values => _bPMStops;

  public bool TryGetValue(string id, out BPMStop value)
  {
    value = null;
    return false;
  }

  public float LowestBPM = float.MaxValue;
  public float HightestBPM = float.MinValue;

  private int _updateLockCount = 0;
  private int _minRecalculateIdx = int.MaxValue;

  /// <summary>
  /// Suspends notifications and calculations to allow batch edits.
  /// </summary>
  public void BeginUpdate() => _updateLockCount++;

  /// <summary>
  /// Resumes notifications and recalculates state if edits were made.
  /// </summary>
  public void EndUpdate(bool success = true)
  {
    if (_updateLockCount > 0) _updateLockCount--;
    if (_updateLockCount == 0 && success)
    {
      CommitCalculations();
      OnUpdated?.Invoke(this);
    }
  }

  private void NotifyChanged()
  {
    if (_updateLockCount == 0)
    {
      CommitCalculations();
      OnUpdated?.Invoke(this);
    }
  }

  /// <summary>
  /// Flags the starting index that needs to be recomputed upon the next unlock.
  /// </summary>
  private void RequestRecalculate(int index)
  {
    if (index < _minRecalculateIdx) _minRecalculateIdx = index;
  }

  private void CommitCalculations()
  {
    if (_minRecalculateIdx != int.MaxValue)
    {
      RecalculateFrom(_minRecalculateIdx);
      _minRecalculateIdx = int.MaxValue;
    }
  }

  public Metronome()
  {
    BaseBPM.OnInvalidate += (bb) => { RequestRecalculate(0); NotifyChanged(); };
    BaseBPM.OnUpdated += (bb) => NotifyChanged();
  }

  /// <summary>
  /// Force recalculates all cached expected start times.
  /// </summary>
  public void Compute() => RecalculateFrom(0);

  private void RecalculateFrom(int index)
  {
    if (index < 0) index = 0;

    for (int i = index; i < _bPMStops.Count; i++)
    {
      var curr = _bPMStops[i];

      if (i == 0)
      {
        double beatDiff = curr.StartBeat.AbsoluteValue;
        curr.StartTimeSeconds = BaseBPM.BaseOffsetSeconds + (beatDiff / BaseBPM.BeatsPerSecond);
      }
      else
      {
        var prev = _bPMStops[i - 1];
        double beatDiff = curr.StartBeat.AbsoluteValue - prev.StartBeat.AbsoluteValue;
        curr.StartTimeSeconds = prev.StartTimeSeconds + (beatDiff / prev.BeatsPerSecond);
      }
    }

    RecalculateBPMRange();
  }

  /// <summary>
  /// Recomputes LowestBPM and HightestBPM by scanning BaseBPM and all _bPMStops.
  /// </summary>
  private void RecalculateBPMRange()
  {
    LowestBPM = BaseBPM.InitialBPM;
    HightestBPM = BaseBPM.InitialBPM;

    for (int i = 0; i < _bPMStops.Count; i++)
    {
      float bpm = _bPMStops[i].BPM;
      if (bpm < LowestBPM) LowestBPM = bpm;
      if (bpm > HightestBPM) HightestBPM = bpm;
    }
  }

  private void SubscribeChangeBPMStops(BPMStop bPMStop)
  {
    bPMStop.OnStartBeatChanged -= HandleStartBeatChanged;
    bPMStop.OnStartBeatChanged += HandleStartBeatChanged;

    bPMStop.OnInvalidate -= HandleInvalidate;
    bPMStop.OnInvalidate += HandleInvalidate;

    bPMStop.OnUpdated -= HandleUpdated;
    bPMStop.OnUpdated += HandleUpdated;
  }

  private void UnSubscribeChangeBPMStops(BPMStop bPMStop)
  {
    bPMStop.OnStartBeatChanged -= HandleStartBeatChanged;
    bPMStop.OnInvalidate -= HandleInvalidate;
    bPMStop.OnUpdated -= HandleUpdated;
  }

  public void SetBaseBPM(BaseBPM baseBPM)
  {
    if (BaseBPM.BaseOffsetSeconds == baseBPM.BaseOffsetSeconds &&
        BaseBPM.InitialBPM == baseBPM.InitialBPM &&
        BaseBPM.TimeSignatureNum == baseBPM.TimeSignatureNum &&
        BaseBPM.TimeSignatureDen == baseBPM.TimeSignatureDen) return;

    BaseBPM = baseBPM;
    RequestRecalculate(0);
    NotifyChanged();
  }

  public BaseBPM GetBaseBPM() => BaseBPM;

  public int AddBPMStop(BPMStop bPMStop)
  {
    int idx = FindAddIndex(bPMStop);
    _bPMStops.Insert(idx, bPMStop);
    SubscribeChangeBPMStops(bPMStop);

    RequestRecalculate(idx);
    NotifyChanged();

    return idx;
  }

  public int[] AddBPMStops(IEnumerable<BPMStop> bPMStops)
  {
    if (!bPMStops.Any()) return [];

    BeginUpdate();

    int[] indices = new int[bPMStops.Count()];
    for (int i = 0; i < bPMStops.Count(); i++)
      indices[i] = AddBPMStop(bPMStops.ElementAt(i));

    EndUpdate();

    return indices;
  }

  public bool RemoveBPMStop(BPMStop bPMStop)
  {
    int idx = _bPMStops.IndexOf(bPMStop);
    if (idx == -1) return false;

    _bPMStops.RemoveAt(idx);
    UnSubscribeChangeBPMStops(bPMStop);

    RequestRecalculate(idx);
    NotifyChanged();

    return true;
  }

  public int RemoveBPMStops(IEnumerable<BPMStop> bPMStops)
  {
    if (!bPMStops.Any()) return 0;

    BeginUpdate();

    int removedCount = 0;
    for (int i = 0; i < bPMStops.Count(); i++)
      if (RemoveBPMStop(bPMStops.ElementAt(i))) removedCount++;

    EndUpdate(removedCount > 0);

    return removedCount;
  }

  private void HandleStartBeatChanged(BPMStop bPMStop)
  {
    RemoveBPMStop(bPMStop);
    AddBPMStop(bPMStop);
  }

  private void HandleInvalidate(BPMStop bPMStop)
  {
    int idx = _bPMStops.IndexOf(bPMStop);
    if (idx == -1) return;

    RequestRecalculate(idx);
    NotifyChanged();
  }

  private void HandleUpdated(BPMStop bPMStop) => NotifyChanged();

  public double ToSeconds(double beat)
  {
    int idx = FindStopIndex(beat);

    if (idx == -1)
    {
      return BaseBPM.BaseOffsetSeconds + (beat / BaseBPM.BeatsPerSecond);
    }

    var stop = _bPMStops[idx];
    double deltaBeat = beat - stop.StartBeat.AbsoluteValue;
    return stop.StartTimeSeconds + (deltaBeat / stop.BeatsPerSecond);
  }

  public float GetBPMAtBeat(double beat)
  {
    int idx = FindStopIndex(beat);
    return idx == -1 ? BaseBPM.InitialBPM : _bPMStops[idx].BPM;
  }

  public (int num, int den) GetTimeSignatureAtBeat(double beat)
  {
    int idx = FindStopIndex(beat);
    return idx == -1 ? (BaseBPM.TimeSignatureNum, BaseBPM.TimeSignatureDen) : (_bPMStops[idx].TimeSignatureNum, _bPMStops[idx].TimeSignatureDen);
  }

  public double ToBeat(double seconds)
  {
    int idx = FindStopIndexByTime(seconds);

    if (idx == -1)
    {
      double deltaSec = seconds - BaseBPM.BaseOffsetSeconds;
      return deltaSec * BaseBPM.BeatsPerSecond;
    }

    var stop = _bPMStops[idx];
    double deltaSeconds = seconds - stop.StartTimeSeconds;
    return stop.StartBeat.AbsoluteValue + (deltaSeconds * stop.BeatsPerSecond);
  }

  public double ToSeconds(BeatTime beatTime) => ToSeconds(beatTime.AbsoluteValue);
  public double ToMiliSeconds(double beat) => ToSeconds(beat) * 1000.0;
  public double ToMiliSeconds(BeatTime beatTime) => ToMiliSeconds(beatTime.AbsoluteValue);

  public double ToDeltaMilliSeconds(double beat1, double beat2) => (ToSeconds(beat2) - ToSeconds(beat1)) * 1000.0;
  public double ToDeltaMilliSeconds(BeatTime beat1, BeatTime beat2) => ToDeltaMilliSeconds(beat1.AbsoluteValue, beat2.AbsoluteValue);

  public double GetCurrentBPS(double beat)
  {
    int idx = FindStopIndexByTime(beat);
    return idx == -1 ? BaseBPM.BeatsPerSecond : _bPMStops[idx].BeatsPerSecond;
  }

  /// <summary>
  /// Finds the index of the closest BPMStop at or before the given beat. Returns -1 if none.
  /// </summary>
  private int FindStopIndex(double beat)
  {
    if (_bPMStops.Count == 0 || beat < _bPMStops[0].StartBeat.AbsoluteValue) return -1;

    int lo = 0, hi = _bPMStops.Count - 1;
    while (lo < hi)
    {
      int mid = (lo + hi + 1) / 2;
      if (_bPMStops[mid].StartBeat.AbsoluteValue <= beat) lo = mid;
      else hi = mid - 1;
    }
    return lo;
  }

  /// <summary>
  /// Finds the index of the closest BPMStop at or before the given time (in seconds). Returns -1 if none.
  /// </summary>
  private int FindStopIndexByTime(double seconds)
  {
    if (_bPMStops.Count == 0 || seconds < _bPMStops[0].StartTimeSeconds) return -1;

    int lo = 0, hi = _bPMStops.Count - 1;
    while (lo < hi)
    {
      int mid = (lo + hi + 1) / 2;
      if (_bPMStops[mid].StartTimeSeconds <= seconds) lo = mid;
      else hi = mid - 1;
    }
    return lo;
  }

  public int FindAddIndex(BPMStop bPMStop)
  {
    if (_bPMStops.Count == 0) return 0;

    int left = 0, right = _bPMStops.Count - 1;
    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (_bPMStops[mid].StartBeat == bPMStop.StartBeat) return mid;
      if (_bPMStops[mid].StartBeat < bPMStop.StartBeat) left = mid + 1;
      else right = mid - 1;
    }
    return left;
  }
}
