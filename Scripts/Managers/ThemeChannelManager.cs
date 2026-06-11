using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers
{
  /// <summary>
  /// Manages ThemeChannelData configurations and monitors data changes.
  /// </summary>
  public class ThemeChannelManager : IObjectManager<ThemeChannelData>
  {
    public event Action<ThemeChannelManager> OnUpdated;

    private readonly Dictionary<string, ThemeChannelData> _themeChannelCollection = [];

    public int Count => _themeChannelCollection.Count;
    
    public IEnumerator<ThemeChannelData> GetEnumerator() => _themeChannelCollection.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ThemeChannelData this[string id] => _themeChannelCollection.TryGetValue(id, out var tc) ? tc : null;
    public ThemeChannelData this[int index] => _themeChannelCollection.Values.ElementAtOrDefault(index);
    
    public ICollection<string> Keys => _themeChannelCollection.Keys;
    public ICollection<ThemeChannelData> Values => _themeChannelCollection.Values;

    public bool ContainsKey(string id) => _themeChannelCollection.ContainsKey(id);
    public bool TryGetValue(string id, out ThemeChannelData data) => _themeChannelCollection.TryGetValue(id, out data);

    private int _updateLockCount = 0;

    public void BeginUpdate() => _updateLockCount++;

    public void EndUpdate(bool success = true)
    {
      if (_updateLockCount > 0) _updateLockCount--;
      if (_updateLockCount == 0 && success) OnUpdated?.Invoke(this);
    }

    private void NotifyChanged()
    {
      if (_updateLockCount == 0) OnUpdated?.Invoke(this);
    }

    private void SubscribeChangeEvent(ThemeChannelData themeChannelData)
    {
      themeChannelData.OnUpdated -= HandleUpdated;
      themeChannelData.OnUpdated += HandleUpdated;
    }

    private void UnsubscribeChangeEvent(ThemeChannelData themeChannelData)
    {
      themeChannelData.OnUpdated -= HandleUpdated;
    }

    private void HandleUpdated(ThemeChannelData themeChannelData) => NotifyChanged();

    public void AddThemeChannel(ThemeChannelData themeChannelData)
    {
      if (string.IsNullOrEmpty(themeChannelData.ID))
        throw new ArgumentException("ThemeChannelData ID cannot be null or empty.");

      if (_themeChannelCollection.TryGetValue(themeChannelData.ID, out var existing))
        UnsubscribeChangeEvent(existing);

      _themeChannelCollection[themeChannelData.ID] = themeChannelData;
      SubscribeChangeEvent(themeChannelData);
      NotifyChanged();
    }

    public void AddThemeChannels(IEnumerable<ThemeChannelData> themeChannels)
    {
      if (!themeChannels.Any()) return;

      BeginUpdate();
      foreach (var channel in themeChannels) AddThemeChannel(channel);
      EndUpdate();
    }

    public bool RemoveThemeChannel(string id)
    {
      if (!_themeChannelCollection.TryGetValue(id, out var channelData)) return false;

      UnsubscribeChangeEvent(channelData);
      _themeChannelCollection.Remove(id);
      NotifyChanged();

      return true;
    }

    public int RemoveThemeChannels(IEnumerable<string> ids)
    {
      if (!ids.Any()) return 0;

      BeginUpdate();
      int success = ids.Count(RemoveThemeChannel);
      EndUpdate(success > 0);

      return success;
    }

    public ThemeChannelData GetThemeChannel(string id)
    {
      if (string.IsNullOrEmpty(id)) return null;

      if (_themeChannelCollection.TryGetValue(id, out var channelData)) return channelData;
      return null;
    }

    public IReadOnlyList<ThemeChannelData> GetThemeChannels(IEnumerable<string> ids)
    {
      var result = new List<ThemeChannelData>();
      foreach (var id in ids)
      {
        if (_themeChannelCollection.TryGetValue(id, out var channel)) result.Add(channel);
      }
      return result;
    }

    public bool ContainsThemeChannel(string id) => _themeChannelCollection.ContainsKey(id);

    public IReadOnlyDictionary<string, ThemeChannelData> GetAllThemeChannels() => _themeChannelCollection;
  }
}
