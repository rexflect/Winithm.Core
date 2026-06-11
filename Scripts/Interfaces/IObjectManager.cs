using System.Collections.Generic;

namespace Winithm.Core.Interfaces;

public interface IObjectManager<TKey, TValue>
  : IEnumerable<KeyValuePair<TKey, TValue>>
{
  int Count { get; }

  TValue this[TKey key] { get; }
  TValue this[int index] { get; }

  ICollection<TKey> Keys { get; }
  ICollection<TValue> Values { get; }


  bool ContainsKey(TKey key);
  bool TryGetValue(TKey key, out TValue value);

  void BeginUpdate();
  void EndUpdate(bool success = true);
}

public interface IObjectManager<TValue> : IEnumerable<TValue>
{
  int Count { get; }

  TValue this[int index] { get; }
  ICollection<TValue> Values { get; }

  bool TryGetValue(string id, out TValue value);

  void BeginUpdate();
  void EndUpdate(bool success = true);
}