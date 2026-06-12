using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// Tracks the index of the last active event to speed up progressive timeline iterations.
/// </summary>
public class Cursor
{
  internal int LastIndex;
  public Cursor() { LastIndex = 0; }
  public void Reset() { LastIndex = 0; }
}

/// <summary>
/// Manages timeline events and interpolates values.
/// </summary>
public class StoryboardManager<TProp>
  : IDeepCloneable<StoryboardManager<TProp>>, IObjectManager<TProp, List<EventData>>
{
  public event Action<StoryboardManager<TProp>> OnUpdated;

  private readonly Dictionary<TProp, List<EventData>> _eventCollection = [];

  public int Count => _eventCollection.Count;

  public IEnumerator<KeyValuePair<TProp, List<EventData>>> GetEnumerator() => _eventCollection.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public List<EventData> this[TProp prop] => _eventCollection.TryGetValue(prop, out var list) ? list : null;
  public List<EventData> this[int index] => _eventCollection.Values.ElementAtOrDefault(index);

  public ICollection<TProp> Keys => _eventCollection.Keys;
  public ICollection<List<EventData>> Values => _eventCollection.Values;

  public bool TryGetValue(TProp prop, out List<EventData> data) => _eventCollection.TryGetValue(prop, out data);
  public bool ContainsKey(TProp prop) => _eventCollection.ContainsKey(prop);


  private Dictionary<TProp, Cursor> _propertyCursors = [];

  private int _updateLockCount = 0;

  /// <summary>
  /// Suspends notifications to allow batch edits.
  /// </summary>
  public void BeginUpdate() => _updateLockCount++;

  /// <summary>
  /// Resumes notifications and triggers OnUpdated if edits were made.
  /// </summary>
  public void EndUpdate(bool success = true)
  {
    if (_updateLockCount > 0) _updateLockCount--;
    if (_updateLockCount == 0 && success) OnUpdated?.Invoke(this);
  }

  private void NotifyChanged()
  {
    if (_updateLockCount == 0) OnUpdated?.Invoke(this);
  }

  public StoryboardManager<TProp> DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    var newStoryboard = new StoryboardManager<TProp>();

    newStoryboard.BeginUpdate();

    foreach (var events in _eventCollection)
    {
      foreach (var evt in events.Value)
        newStoryboard.AddEvent(events.Key, evt.DeepClone(objectFactory, offset));
    }

    newStoryboard.EndUpdate();

    return newStoryboard;
  }

  private readonly Dictionary<EventData, TProp> _eventKeyMap = new Dictionary<EventData, TProp>();
  private void SubscribeChangeEvent(TProp prop, EventData evt)
  {
    evt.OnStartBeatChanged -= HandleStartBeatChanged;
    evt.OnStartBeatChanged += HandleStartBeatChanged;
    evt.OnUpdated -= HandleUpdated;
    evt.OnUpdated += HandleUpdated;
    _eventKeyMap[evt] = prop;
  }

  private void UnSubscribeChangeEvent(EventData evt)
  {
    evt.OnStartBeatChanged -= HandleStartBeatChanged;
    evt.OnUpdated -= HandleUpdated;
    _eventKeyMap.Remove(evt);
  }

  private void OnEventStartBeatChanged(TProp prop, EventData evt)
  {
    if (!_eventCollection.TryGetValue(prop, out var list)) return;
    if (!list.Contains(evt)) return;

    list.Remove(evt);
    int index = FindAddIndex(list, evt);
    list.Insert(index, evt);

    _propertyCursors[prop].Reset();
    NotifyChanged();
  }

  private void HandleStartBeatChanged(EventData evt)
  {
    if (_eventKeyMap.TryGetValue(evt, out var key)) OnEventStartBeatChanged(key, evt);
  }

  private void HandleUpdated(EventData evt)
  {
    if (_eventKeyMap.ContainsKey(evt)) NotifyChanged();
  }

  public int AddEvent(TProp prop, EventData evt)
  {
    if (!_eventCollection.TryGetValue(prop, out var list))
    {
      list = new List<EventData>();
      _eventCollection[prop] = list;
      _propertyCursors[prop] = new Cursor();
    }

    int index = FindAddIndex(list, evt);
    list.Insert(index, evt);
    _propertyCursors[prop].Reset();

    SubscribeChangeEvent(prop, evt);
    NotifyChanged();

    return index;
  }

  public int[] AddEvents(TProp prop, IEnumerable<EventData> evts)
  {
    if (!evts.Any()) return [];

    BeginUpdate();

    int[] indices = new int[evts.Count()];
    for (int i = 0; i < evts.Count(); i++)
      indices[i] = AddEvent(prop, evts.ElementAt(i));

    EndUpdate();

    return indices;
  }

  public bool RemoveEvent(TProp prop, EventData evt)
  {
    if (!_eventCollection.TryGetValue(prop, out var list)) return false;
    if (!list.Remove(evt)) return false;

    UnSubscribeChangeEvent(evt);

    if (list.Count == 0)
    {
      _eventCollection.Remove(prop);
      _propertyCursors.Remove(prop);
    }
    else _propertyCursors[prop].Reset();

    NotifyChanged();
    return true;
  }

  public int RemoveEvents(TProp prop, IEnumerable<EventData> evts)
  {
    if (!evts.Any()) return 0;

    BeginUpdate();

    int success = evts.Count(evt => RemoveEvent(prop, evt));

    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveEvent(EventData evt)
  {
    if (_eventKeyMap.TryGetValue(evt, out var prop))
    {
      return RemoveEvent(prop, evt);
    }
    return false;
  }

  public int RemoveEvents(IEnumerable<EventData> evts)
  {
    if (!evts.Any()) return 0;

    BeginUpdate();

    int success = evts.Count(RemoveEvent);

    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveEvent(TProp prop, string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    if (!_eventCollection.TryGetValue(prop, out var list)) return false;

    var toRemove = list.FindAll(x => x.ID == id);
    if (toRemove.Count == 0) return false;

    foreach (var evt in toRemove) UnSubscribeChangeEvent(evt);
    list.RemoveAll(x => x.ID == id);

    if (list.Count == 0)
    {
      _eventCollection.Remove(prop);
      _propertyCursors.Remove(prop);
    }
    else _propertyCursors[prop].Reset();

    NotifyChanged();
    return true;
  }

  public int RemoveEvents(TProp prop, IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();

    int success = ids.Count(id => RemoveEvent(prop, id));

    EndUpdate(success > 0);

    return success;
  }

  public bool RemoveEvent(string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    BeginUpdate();

    bool anySuccess = false;
    foreach (var key in _eventCollection.Keys.ToList())
    {
      if (RemoveEvent(key, id)) anySuccess = true;
    }

    EndUpdate(anySuccess);

    return anySuccess;
  }

  public int RemoveEvents(IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();

    int success = ids.Count(RemoveEvent);

    EndUpdate(success > 0);

    return success;
  }

  public EventData GetEvent(TProp prop, string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    if (!_eventCollection.TryGetValue(prop, out var evts)) return null;

    var result = evts.FirstOrDefault(e => e.ID == id);

    if (result == default) return null;
    return result;
  }

  public IReadOnlyList<EventData> GetEvents(TProp prop, IEnumerable<string> ids)
  {
    if (!ids.Any()) return [];

    var result = new List<EventData>();
    if (_eventCollection.TryGetValue(prop, out var list))
    {
      var idSet = new HashSet<string>(ids);
      result.AddRange(list.Where(e => idSet.Contains(e.ID)));
    }
    return result;
  }

  public EventData GetEvent(string id, out TProp prop)
  {
    prop = default;

    if (string.IsNullOrEmpty(id)) return null;

    foreach (var pair in _eventCollection)
    {
      var result = pair.Value.FirstOrDefault(e => e.ID == id);
      if (result != default)
      {
        prop = pair.Key;
        return result;
      }
    }

    return null;
  }

  public IReadOnlyDictionary<TProp, List<EventData>> GetEvents(IEnumerable<string> ids)
  {
    var result = new Dictionary<TProp, List<EventData>>();
    var idSet = new HashSet<string>(ids);

    if (idSet.Count == 0) return result;

    foreach (var pair in _eventCollection)
    {
      var found = pair.Value.Where(e => idSet.Contains(e.ID)).ToList();
      if (found.Count > 0) result[pair.Key] = found;
    }
    return result;
  }

  public IReadOnlyList<EventData> GetPropEvents(TProp prop)
  {
    if (_eventCollection.TryGetValue(prop, out var events)) return events;
    return [];
  }

  public IReadOnlyDictionary<TProp, List<EventData>> GetAllEvents() => _eventCollection;

  /// <summary>Finds insertion index to maintain sorted stability.</summary>
  public int FindAddIndex(List<EventData> list, EventData evt)
  {
    if (list.Count == 0) return 0;
    int left = 0, right = list.Count - 1;
    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (list[mid].StartBeat <= evt.StartBeat) left = mid + 1;
      else right = mid - 1;
    }
    return left;
  }

  public AnyValue Evaluate(TProp prop, double currentBeat, AnyValue defaultValue)
  {
    if (!_eventCollection.TryGetValue(prop, out var events) || events.Count == 0)
      return defaultValue;

    int idx = AdvanceCursor(prop, currentBeat);
    return EvaluateRecursive(events, idx, currentBeat, defaultValue);
  }

  private static AnyValue EvaluateRecursive(List<EventData> events, int idx, double currentBeat, AnyValue defaultValue)
  {
    if (idx < 0) return defaultValue;

    var evt = events[idx];
    var resolvedFrom = evt.From.Type == AnyValueType.Inherited
      ? EvaluateRecursive(events, idx - 1, currentBeat, defaultValue)
      : evt.From;

    return Interpolate(evt, currentBeat, resolvedFrom);
  }

  private static AnyValue Interpolate(EventData evt, double currentBeat, AnyValue resolvedFrom)
  {
    double startBeat = evt.StartBeat.AbsoluteValue;
    double endBeat = startBeat + evt.Length;

    if (currentBeat >= endBeat) return evt.To;

    double length = evt.Length;
    double t = length > 0.0 ? (currentBeat - startBeat) / length : 1.0;

    t = evt.Easing == EasingType.Bezier
      ? EasingFunctions.EvaluateBezier(evt.EasingBezier, t)
      : EasingFunctions.Evaluate(evt.Easing, t);

    return AnyValue.Lerp(resolvedFrom, evt.To, t);
  }

  private int AdvanceCursor(TProp prop, double currentBeat)
  {
    var events = _eventCollection[prop];
    var cursor = _propertyCursors[prop];

    int n = events.Count;
    int last = Math.Min(cursor.LastIndex, n - 1);

    // --- Scrubbing detection via neighbor window ---
    double lowerBound = last > 0
      ? events[last - 1].StartBeat.AbsoluteValue
      : double.NegativeInfinity;

    double upperBound = last < n - 1
      ? events[last + 1].StartBeat.AbsoluteValue
      : double.PositiveInfinity;

    if (currentBeat < lowerBound || currentBeat > upperBound)
    {
      // Playhead is outside the expected neighborhood → scrubbing / seek detected.
      int idx = FindLastStarted(prop, currentBeat);
      cursor.LastIndex = Math.Max(0, idx);
      return idx;
    }

    // --- Normal forward walk ---
    while (last + 1 < n && events[last + 1].StartBeat.AbsoluteValue <= currentBeat)
      last++;

    cursor.LastIndex = last;

    // Return -1 when currentBeat is still before the very first event
    return events[last].StartBeat.AbsoluteValue <= currentBeat ? last : -1;
  }

  private int FindLastStarted(TProp prop, double currentBeat)
  {
    var events = _eventCollection[prop];
    int left = 0, right = events.Count - 1, best = -1;

    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (events[mid].StartBeat.AbsoluteValue <= currentBeat)
      {
        best = mid;
        left = mid + 1;
      }
      else right = mid - 1;
    }
    return best;
  }
}