using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Winithm.Core.Data;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

public enum ComponentType
{
  Combo,
  Score,
  Info,
  Difficulty
}

/// <summary>
/// Manages active component data and tracks property changes.
/// </summary>
public class ComponentManager : IObjectManager<ComponentType, ComponentData>
{
  public event Action<ComponentManager> OnUpdated;

  private readonly Dictionary<ComponentType, ComponentData> _componentDictionary = [];

  public int Count => _componentDictionary.Count;

  public IEnumerator<KeyValuePair<ComponentType, ComponentData>> GetEnumerator() => _componentDictionary.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => _componentDictionary.GetEnumerator();

  public ComponentData this[ComponentType type] => _componentDictionary.TryGetValue(type, out var c) ? c : null;
  public ComponentData this[int index] => Enumerable.ElementAtOrDefault(_componentDictionary.Values, index);

  public ICollection<ComponentType> Keys => _componentDictionary.Keys;
  public ICollection<ComponentData> Values => _componentDictionary.Values;

  public bool ContainsKey(ComponentType key) => _componentDictionary.ContainsKey(key);
  public bool TryGetValue(ComponentType type, out ComponentData data) => _componentDictionary.TryGetValue(type, out data);

  public ComponentManager()
  {
    BeginUpdate();
    SetComponent(ComponentType.Info, new());
    SetComponent(ComponentType.Difficulty, new());
    SetComponent(ComponentType.Combo, new());
    SetComponent(ComponentType.Score, new());
    EndUpdate();
  }

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

  private void SubscribeChangeEvent(ComponentData componentData)
  {
    componentData.OnUpdated -= HandleUpdated;
    componentData.OnUpdated += HandleUpdated;
  }

  private void UnsubscribeChangeEvent(ComponentData componentData)
  {
    componentData.OnUpdated -= HandleUpdated;
  }

  private void HandleUpdated(ComponentData componentData) => NotifyChanged();

  public void SetComponent(ComponentType type, ComponentData compData)
  {
    if (_componentDictionary.TryGetValue(type, out var comp))
    {
      UnsubscribeChangeEvent(comp);
    }

    _componentDictionary[type] = compData;
    SubscribeChangeEvent(compData);

    NotifyChanged();
  }

  public ComponentData GetComponent(ComponentType type) => _componentDictionary[type];
}