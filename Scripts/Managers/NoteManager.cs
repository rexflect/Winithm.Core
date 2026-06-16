using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

public enum NoteSide
{
  Top,
  Bottom,
  Left,
  Right
}

/// <summary>
/// Manages Note segments, boundaries, and spatial data.
/// </summary>
public class NoteManager :
  IDeepCloneable<NoteManager>, IObjectManager<NoteSide, List<NoteData>>
{
  public event Action<double>? OnNoteAddedAtBeat;
  public event Action<double>? OnNoteRemovedAtBeat;

  public event Action<NoteManager>? OnLifeCycleChanged;
  public event Action<NoteManager>? OnUpdated;

  private WindowData? _windowData;

  private readonly Dictionary<NoteSide, List<NoteData>> _noteCollection = [];

  public int Count => _noteCollection.Count;

  public IEnumerator<KeyValuePair<NoteSide, List<NoteData>>> GetEnumerator() => _noteCollection.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public List<NoteData>? this[NoteSide side] => _noteCollection.TryGetValue(side, out var list) ? list : null;
  public List<NoteData>? this[int index] => _noteCollection.Values.ElementAtOrDefault(index);

  public ICollection<NoteSide> Keys => _noteCollection.Keys;
  public ICollection<List<NoteData>> Values => _noteCollection.Values;

  public bool TryGetValue(NoteSide side, out List<NoteData>? data) => _noteCollection.TryGetValue(side, out data);

  public bool ContainsKey(NoteSide side) => _noteCollection.ContainsKey(side);

  public Dictionary<NoteSide, double[]> MaxEndBeats { get; private set; } = [];
  /// <summary>Sorted beats where combo increments occur (Hold → end beat, others → start beat).</summary> 

  public int TotalNoteCount { get; private set; } = 0;
  public int TotalHittableNoteCount { get; private set; } = 0;
  public double[] ComboEventBeats { get; private set; } = [];
  /// <summary>Prefix-sum of combo values aligned with ComboEventBeats.</summary>
  public int[] ComboPrefixSum { get; private set; } = [];

  public BeatTime ExpectedStartFocusBeat { get; private set; } = BeatTime.Max;
  public BeatTime ExpectedEndCloseBeat { get; private set; } = BeatTime.Max;
  public int TotalComboCount { get; private set; } = 0;

  private int _updateLockCount = 0;
  private bool _needsRecompute = false;

  public NoteManager DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new NoteManager();

    cloned.BeginUpdate();
    foreach (var sideNotes in _noteCollection)
    {
      foreach (var note in sideNotes.Value)
        cloned.AddNote(sideNotes.Key, note.DeepClone(objectFactory, offset));
    }
    cloned.EndUpdate();

    return cloned;
  }

  /// <summary>
  /// Suspends calculations to allow batch edits.
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

  private void RequestRecompute()
  {
    _needsRecompute = true;
  }

  private void CommitRecompute()
  {
    if (_needsRecompute)
    {
      Compute();
      _needsRecompute = false;
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

  public void SetWindowData(WindowData windowData)
  {
    _windowData = windowData;
    RequestRecompute();
    NotifyChanged();
  }

  /// <summary>
  /// Re-evaluates note boundaries, max end beats, and combo prefix-sum.
  /// </summary>
  public void Compute()
  {
    if (_windowData is null)
    {
      ExpectedStartFocusBeat = BeatTime.Max;
      ExpectedEndCloseBeat = BeatTime.Max;
      TotalNoteCount = 0;
      TotalHittableNoteCount = 0;
      TotalComboCount = 0;
      MaxEndBeats.Clear();
      ComboEventBeats = [];
      ComboPrefixSum = [];

      GD.PushWarning("[NoteManager] WindowData is null, cannot compute note boundaries");
      return;
    }

    TotalNoteCount = 0;
    var prevExpectedEndCloseBeat = ExpectedEndCloseBeat;
    var prevWindowEndBeat = _windowData.EndBeat;

    ExpectedStartFocusBeat = _windowData.UnFocus ? _windowData.StartBeat : BeatTime.Min;
    ExpectedEndCloseBeat = _windowData.EndBeat;

    foreach (var notes in _noteCollection.Values)
    {
      TotalNoteCount += notes.Count;

      foreach (var note in notes)
      {
        if (note.Type is NoteType.Focus && note.IsHittable)
        {
          if (note.StartBeat < ExpectedStartFocusBeat)
            ExpectedStartFocusBeat = note.StartBeat;
        }

        if (note.Type is NoteType.Close && note.IsHittable)
        {
          if (note.StartBeat < ExpectedEndCloseBeat)
            ExpectedEndCloseBeat = note.StartBeat;
          break;
        }
      }
    }

    _windowData.EndBeat = ExpectedEndCloseBeat;
    TotalHittableNoteCount = 0;
    TotalComboCount = 0;

    var comboEvents = new List<(double beat, int combo)>();

    foreach (var sideNotes in _noteCollection)
    {
      var list = sideNotes.Value;

      // Use existing array if size matches, otherwise allocate new
      if (!MaxEndBeats.TryGetValue(sideNotes.Key, out var maxEnds) || maxEnds.Length != list.Count)
        maxEnds = new double[list.Count];

      double runningMax = double.MinValue;

      for (int i = 0; i < list.Count; i++)
      {
        var note = list[i];

        var noteEndBeat = note.Type == NoteType.Hold
          ? note.StartBeat.AbsoluteValue + note.Length
          : note.StartBeat.AbsoluteValue;

        if (
          note.StartBeat >= ExpectedStartFocusBeat
          && noteEndBeat <= ExpectedEndCloseBeat.AbsoluteValue
          && note.IsHittable
        )
        {
          TotalHittableNoteCount++;
          note.IsLifecycleBounded = true;

          if (note.Type is NoteType.Hold)
          {
            comboEvents.Add((noteEndBeat, 2));
            TotalComboCount += 2;
          }
          else
          {
            comboEvents.Add((note.StartBeat.AbsoluteValue, 1));
            TotalComboCount++;
          }
        }
        else
        {
          note.IsLifecycleBounded = false;
        }

        runningMax = Math.Max(runningMax, noteEndBeat);
        maxEnds[i] = runningMax;
      }
      MaxEndBeats[sideNotes.Key] = maxEnds;
    }

    comboEvents.Sort((a, b) => a.beat.CompareTo(b.beat));

    // Binary search expects exact array lengths
    if (ComboEventBeats.Length != comboEvents.Count)
      ComboEventBeats = new double[comboEvents.Count];
    if (ComboPrefixSum.Length != comboEvents.Count)
      ComboPrefixSum = new int[comboEvents.Count];

    int runningCombo = 0;
    for (int i = 0; i < comboEvents.Count; i++)
    {
      runningCombo += comboEvents[i].combo;
      ComboEventBeats[i] = comboEvents[i].beat;
      ComboPrefixSum[i] = runningCombo;
    }

    if (prevExpectedEndCloseBeat != ExpectedEndCloseBeat || prevWindowEndBeat != _windowData.EndBeat)
      OnLifeCycleChanged?.Invoke(this);
  }

  // ==========================================
  // Event Management
  // ==========================================

  private readonly Dictionary<NoteData, NoteSide> _noteSideMap = [];

  public bool TryGetNoteSide(NoteData note, out NoteSide side) => _noteSideMap.TryGetValue(note, out side);

  private void SubscribeChangeEvent(NoteSide side, NoteData note)
  {
    note.OnStartBeatChanged -= HandleStartBeatChanged;
    note.OnStartBeatChanged += HandleStartBeatChanged;

    note.OnInvalidate -= HandleInvalidate;
    note.OnInvalidate += HandleInvalidate;

    note.OnUpdated -= HandleUpdated;
    note.OnUpdated += HandleUpdated;

    _noteSideMap[note] = side;
  }

  private void UnsubscribeChangeEvent(NoteData note)
  {
    note.OnStartBeatChanged -= HandleStartBeatChanged;
    note.OnInvalidate -= HandleInvalidate;
    note.OnUpdated -= HandleUpdated;

    _noteSideMap.Remove(note);
  }

  private void HandleStartBeatChanged(NoteData note, double prevStartBeat)
  {
    if (!_noteSideMap.TryGetValue(note, out var side)) return;

    var list = _noteCollection[side];
    list.Remove(note);
    OnNoteRemovedAtBeat?.Invoke(prevStartBeat);

    int index = FindAddIndex(side, note);
    list.Insert(index, note);
    OnNoteAddedAtBeat?.Invoke(note.StartBeat.AbsoluteValue);

    RequestRecompute();
    NotifyChanged();
  }

  private void HandleInvalidate(NoteData note) { RequestRecompute(); NotifyChanged(); }
  private void HandleUpdated(NoteData note) => NotifyChanged();

  // ==========================================
  // Lifecycle Management
  // ==========================================

  public int AddNote(NoteSide side, NoteData note)
  {
    if (!_noteCollection.TryGetValue(side, out var list))
    {
      list = [];
      _noteCollection[side] = list;
    }

    int index = FindAddIndex(side, note);
    list.Insert(index, note);

    SubscribeChangeEvent(side, note);
    OnNoteAddedAtBeat?.Invoke(note.StartBeat.AbsoluteValue);

    RequestRecompute();
    NotifyChanged();

    return index;
  }

  public int[] AddNotes(NoteSide side, IEnumerable<NoteData> notes)
  {
    if (!notes.Any()) return [];

    BeginUpdate();

    int[] indices = new int[notes.Count()];
    for (int i = 0; i < notes.Count(); i++)
      indices[i] = AddNote(side, notes.ElementAt(i));

    EndUpdate();
    return indices;
  }

  public bool RemoveNote(NoteSide side, NoteData note)
  {
    if (!_noteCollection.TryGetValue(side, out var list)) return false;
    if (!list.Remove(note)) return false;

    UnsubscribeChangeEvent(note);
    OnNoteRemovedAtBeat?.Invoke(note.StartBeat.AbsoluteValue);

    if (list.Count == 0) _noteCollection.Remove(side);
    RequestRecompute();
    NotifyChanged();

    return true;
  }

  public int RemoveNotes(NoteSide side, IEnumerable<NoteData> notes)
  {
    if (!notes.Any()) return 0;

    BeginUpdate();
    int success = notes.Count(n => RemoveNote(side, n));
    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveNote(NoteData note)
  {
    if (_noteSideMap.TryGetValue(note, out var side))
      return RemoveNote(side, note);
    return false;
  }

  public int RemoveNotes(IEnumerable<NoteData> notes)
  {
    if (!notes.Any()) return 0;

    BeginUpdate();
    int success = notes.Count(RemoveNote);
    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveNote(NoteSide side, string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    if (!_noteCollection.TryGetValue(side, out var list)) return false;

    var toRemove = list.FindAll(x => x.ID == id);
    if (toRemove.Count == 0) return false;

    foreach (var note in toRemove)
    {
      UnsubscribeChangeEvent(note);
      OnNoteRemovedAtBeat?.Invoke(note.StartBeat.AbsoluteValue);
    }
    list.RemoveAll(x => x.ID == id);

    if (list.Count == 0) _noteCollection.Remove(side);
    RequestRecompute();
    NotifyChanged();

    return true;
  }

  public int RemoveNotes(NoteSide side, IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();
    int success = ids.Count(id => RemoveNote(side, id));
    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveNote(string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    if (_noteCollection.Count == 0) return false;

    BeginUpdate();
    bool anySuccess = false;
    foreach (var side in _noteCollection.Keys.ToList())
    {
      if (RemoveNote(side, id)) anySuccess = true;
    }
    EndUpdate(anySuccess);

    return anySuccess;
  }

  public int RemoveNotes(IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();
    int success = ids.Count(RemoveNote);
    EndUpdate(success > 0);

    return success;
  }

  public NoteData? GetNote(NoteSide side, string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    if (!_noteCollection.TryGetValue(side, out var notes)) return null;

    var result = notes.FirstOrDefault((n) => n.ID == id);

    if (result is null) return null;
    return result;
  }

  public IReadOnlyList<NoteData> GetNotes(NoteSide side, IEnumerable<string> ids)
  {
    if (!ids.Any()) return [];

    var result = new List<NoteData>();
    foreach (var id in ids)
    {
      var note = GetNote(side, id);
      if (note is not null) result.Add(note);
    }
    return result;
  }

  public NoteData? GetNote(string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    foreach (var pair in _noteCollection)
    {
      var note = pair.Value.Find(n => n.ID == id);
      if (note is not null) return note;
    }

    return null;
  }

  public IReadOnlyList<NoteData> GetNotes(IEnumerable<string> ids)
  {
    if (!ids.Any()) return [];

    var result = new List<NoteData>();
    foreach (var id in ids)
    {
      var note = GetNote(id);
      if (note is not null) result.Add(note);
    }
    return result;
  }

  public IReadOnlyList<NoteData> GetSideNotes(NoteSide side)
  {
    if (_noteCollection.TryGetValue(side, out var notes)) return notes;

    _noteCollection[side] = [];
    return _noteCollection[side];
  }

  public IReadOnlyDictionary<NoteSide, List<NoteData>> GetAllNotes() => _noteCollection;

  public int GetComboPassedAtBeat(double currentBeat)
  {
    if (ComboPrefixSum.Length == 0) return 0;

    int left = 0, right = ComboEventBeats.Length - 1;
    int best = -1;

    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (ComboEventBeats[mid] <= currentBeat)
      {
        best = mid;
        left = mid + 1;
      }
      else
      {
        right = mid - 1;
      }
    }

    return best >= 0 ? ComboPrefixSum[best] : 0;
  }

  // ==========================================
  // Operations
  // ==========================================

  public int FindAddIndex(NoteSide side, NoteData target)
  {
    var list = GetSideNotes(side);

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

  public double[] GetMaxEndBeats(NoteSide side)
  {
    if (MaxEndBeats.TryGetValue(side, out var maxEnds)) return maxEnds;
    return [];
  }
}
