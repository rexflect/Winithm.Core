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


  private Dictionary<TProp, Cursor> _propertyCursors = new();

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

  public static StoryboardProperty ParseEventProperty(string prop)
  {
    switch (prop)
    {
      case "Move_X": return StoryboardProperty.X;
      case "Move_Y": return StoryboardProperty.Y;
      case "Scale": return StoryboardProperty.Scale;
      case "Scale_X": return StoryboardProperty.ScaleX;
      case "Scale_Y": return StoryboardProperty.ScaleY;
      case "Rotation": return StoryboardProperty.Rotation;
      case "Color_R": return StoryboardProperty.ColorR;
      case "Color_G": return StoryboardProperty.ColorG;
      case "Color_B": return StoryboardProperty.ColorB;
      case "Color_A": return StoryboardProperty.ColorA;
      case "Alpha": return StoryboardProperty.Alpha;
      case "Note_A": return StoryboardProperty.NoteA;
      case "Title": return StoryboardProperty.Title;
      case "Speed": return StoryboardProperty.Speed;
      default: return StoryboardProperty.Custom;
    }
  }

  public static string FormatEventProperty(StoryboardProperty type, string customProperty)
  {
    if (type == StoryboardProperty.Custom) return customProperty;
    switch (type)
    {
      case StoryboardProperty.X: return "Move_X";
      case StoryboardProperty.Y: return "Move_Y";
      case StoryboardProperty.Scale: return "Scale";
      case StoryboardProperty.ScaleX: return "Scale_X";
      case StoryboardProperty.ScaleY: return "Scale_Y";
      case StoryboardProperty.Rotation: return "Rotation";
      case StoryboardProperty.ColorR: return "Color_R";
      case StoryboardProperty.ColorG: return "Color_G";
      case StoryboardProperty.ColorB: return "Color_B";
      case StoryboardProperty.ColorA: return "Color_A";
      case StoryboardProperty.Alpha: return "Alpha";
      case StoryboardProperty.NoteA: return "Note_A";
      case StoryboardProperty.Title: return "Title";
      case StoryboardProperty.Speed: return "Speed";
      default: return "";
    }
  }

  public static EventData ParseEventLine(string trimmed, out StoryboardProperty type, out string rawPropertyName)
  {
    var evt = new EventData();
    type = StoryboardProperty.Custom;
    rawPropertyName = "";

    var parts = new List<string>();
    bool inQuotes = false;
    int tokenStart = 2;

    for (int i = 2; i < trimmed.Length; i++)
    {
      char c = trimmed[i];
      if (c == '\"') inQuotes = !inQuotes;
      else if (c == ' ' && !inQuotes)
      {
        if (i > tokenStart) parts.Add(trimmed.Substring(tokenStart, i - tokenStart).Trim('\"'));
        tokenStart = i + 1;
      }
    }
    if (tokenStart < trimmed.Length)
    {
      string finalPart = trimmed.Substring(tokenStart).Trim();
      if (finalPart.Length > 0) parts.Add(finalPart.Trim('\"'));
    }

    if (parts.Count >= 1) evt.ID = parts[0];
    if (parts.Count >= 2)
    {
      rawPropertyName = parts[1];
      type = ParseEventProperty(rawPropertyName);
    }
    if (parts.Count >= 3) evt.StartBeat = BeatTime.Parse(parts[2]);
    if (parts.Count >= 4) evt.Length = ParserUtils.TryParseDouble(parts[3], out double length) ? length : 0;
    if (parts.Count >= 5) evt.From = AnyValue.Parse(parts[4]);
    if (parts.Count >= 6) evt.To = AnyValue.Parse(parts[5]);
    if (parts.Count >= 7)
    {
      if (parts[6].Contains("|"))
      {
        evt.Easing = EasingType.Bezier;
        evt.EasingBezier = AnyValue.Parse(parts[6]);
      }
      else
      {
        evt.Easing = EasingFunctions.ParseEasing(parts[6]);
      }
    }
    return evt;
  }

  public static string GenerateEventLine(EventData evt, StoryboardProperty type, string customProperty = null, int indent = 2)
  {
    string easingStr = evt.Easing == EasingType.Bezier ? evt.EasingBezier.ToString() : evt.Easing.ToString();
    string propStr = FormatEventProperty(type, customProperty);
    string result = $"/ {evt.ID} {propStr} {evt.StartBeat} {evt.Length} {evt.From} {evt.To} {easingStr}";
    return result.PadLeft(indent);
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
    if (!ids.Any()) return Array.Empty<EventData>();

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

  public void SortAllEvents()
  {
    if (_eventCollection.Count == 0) return;
    foreach (TProp prop in _eventCollection.Keys.ToList()) SortPropEvents(prop);
    NotifyChanged();
  }

  public void SortPropEvents(TProp key)
  {
    if (!_eventCollection.TryGetValue(key, out var events) || events.Count <= 1) return;

    events.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
    _propertyCursors[key].Reset();
    NotifyChanged();
  }

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

  public AnyValue Evaluate(TProp prop, double currentBeat, AnyValue defaultValue, bool isScrubbing = false)
  {
    if (!_eventCollection.TryGetValue(prop, out var events) || events.Count == 0)
      return defaultValue;

    int idx = AdvanceCursor(prop, currentBeat, isScrubbing);
    return EvaluateRecursive(events, idx, currentBeat, defaultValue);
  }

  private AnyValue EvaluateRecursive(List<EventData> events, int idx, double currentBeat, AnyValue defaultValue)
  {
    if (idx < 0) return defaultValue;

    var evt = events[idx];
    var resolvedFrom = evt.From.Type == AnyValueType.Inherited
      ? EvaluateRecursive(events, idx - 1, currentBeat, defaultValue)
      : evt.From;

    return Interpolate(evt, currentBeat, resolvedFrom);
  }

  private AnyValue Interpolate(EventData evt, double currentBeat, AnyValue resolvedFrom)
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

  private int AdvanceCursor(TProp prop, double currentBeat, bool isScrubbing)
  {
    var events = _eventCollection[prop];
    var cursor = _propertyCursors[prop];

    int n = events.Count;
    int last = cursor.LastIndex;
    if (last >= n) last = n - 1;

    if (isScrubbing)
    {
      int idx = FindLastStarted(prop, currentBeat);
      cursor.LastIndex = Math.Max(0, idx);
      return idx;
    }

    // If we've moved backward in time before our current cursor event starts
    if (events[last].StartBeat.AbsoluteValue > currentBeat)
    {
      // Skip binary search if before the very first element
      if (last == 0) return -1;

      int idx = FindLastStarted(prop, currentBeat);
      cursor.LastIndex = Math.Max(0, idx);
      return idx;
    }

    // Fast forward timeline continuously
    while (last + 1 < n && events[last + 1].StartBeat.AbsoluteValue <= currentBeat)
    {
      last++;
    }

    cursor.LastIndex = last;
    return last;
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
