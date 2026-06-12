using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// Manages GroupData configurations and tracks data changes.
/// </summary>
public class GroupManager : IObjectManager<GroupData>
{
  public event Action<GroupManager> OnUpdated;

  private readonly Dictionary<string, GroupData> _groupCollection = [];
  public int Count => _groupCollection.Count;

  public IEnumerator<GroupData> GetEnumerator() => _groupCollection.Values.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public GroupData this[string id] => _groupCollection.TryGetValue(id, out var g) ? g : null;
  public GroupData this[int index] => _groupCollection.Values.ElementAtOrDefault(index);

  public ICollection<string> Keys => _groupCollection.Keys;
  public ICollection<GroupData> Values => _groupCollection.Values;

  public bool TryGetValue(string id, out GroupData data) => _groupCollection.TryGetValue(id, out data);

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

  private void SubscribeChangeEvent(GroupData groupData)
  {
    groupData.OnUpdated -= HandleUpdated;
    groupData.OnUpdated += HandleUpdated;
  }

  private void UnsubscribeChangeEvent(GroupData groupData)
  {
    groupData.OnUpdated -= HandleUpdated;
  }

  private void HandleUpdated(GroupData groupData) => NotifyChanged();

  public void AddGroup(GroupData groupData)
  {
    if (string.IsNullOrEmpty(groupData.ID)) return;

    if (_groupCollection.TryGetValue(groupData.ID, out var existing))
      UnsubscribeChangeEvent(existing);

    _groupCollection[groupData.ID] = groupData;
    SubscribeChangeEvent(groupData);
    NotifyChanged();
  }

  public void AddGroups(IEnumerable<GroupData> groups)
  {
    if (!groups.Any()) return;

    BeginUpdate();
    foreach (var group in groups) AddGroup(group);
    EndUpdate();
  }

  public bool RemoveGroup(string id)
  {
    if (string.IsNullOrEmpty(id)) return false;

    if (!_groupCollection.TryGetValue(id, out var groupData)) return false;

    UnsubscribeChangeEvent(groupData);
    _groupCollection.Remove(id);
    NotifyChanged();

    return true;
  }

  public int RemoveGroups(IEnumerable<string> ids)
  {
    if (!ids.Any()) return 0;

    BeginUpdate();
    int success = ids.Count(RemoveGroup);
    EndUpdate(success > 0);

    return success;
  }

  public GroupData GetGroup(string id)
  {
    if (string.IsNullOrEmpty(id)) return null;

    if (_groupCollection.TryGetValue(id, out var groupData)) return groupData;
    return null;
  }

  public IReadOnlyList<GroupData> GetGroups(IEnumerable<string> ids)
  {
    if (!ids.Any()) return [];

    var result = new List<GroupData>();
    foreach (var id in ids)
    {
      var group = GetGroup(id);
      if (group is not null) result.Add(group);
    }
    return result;
  }

  public bool ContainsGroup(string id) => _groupCollection.ContainsKey(id);

  public IReadOnlyDictionary<string, GroupData> GetAllGroups() => _groupCollection;
}

