using System;
using System.Collections.Generic;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Managers;

/// <summary>
/// A generic object pool implementation, built for Godot C#.
/// Avoids costly garbage collection spikes by reusing instantiated objects.
/// </summary>
public class ObjectPool<T> : IDisposable where T : class
{
  private readonly Stack<T> _stack;
  private readonly Func<T> _createFunc;
  private readonly Action<T> _actionOnGet;
  private readonly Action<T> _actionOnRelease;
  private readonly Action<T> _actionOnDestroy;
  private readonly int _maxSize;
  private readonly bool _collectionCheck;

  /// <summary>The total number of objects managed by the pool.</summary>
  public int CountAll { get; private set; }

  /// <summary>The number of objects that have been retrieved from the pool and not yet returned.</summary>
  public int CountActive => CountAll - CountInactive;

  /// <summary>The number of objects currently available in the pool.</summary>
  public int CountInactive => _stack.Count;

  /// <summary>
  /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
  /// </summary>
  /// <param name="createFunc">Used to create a new instance when the pool is empty.</param>
  /// <param name="actionOnGet">Called when an instance is taken from the pool.</param>
  /// <param name="actionOnRelease">Called when an instance is returned to the pool.</param>
  /// <param name="actionOnDestroy">Called when a pooled element is destroyed instead of returning to the pool (because the pool is full).</param>
  /// <param name="collectionCheck">Collection checks are performed when an instance is returned back to the pool. If true, an exception is thrown if the instance is already in the pool. Collection checks slow down returns significantly, so they should only be used in Development/Debug mode.</param>
  /// <param name="defaultCapacity">The default pool capacity. Determines how many objects the underlying stack initially prepares memory for.</param>
  /// <param name="maxSize">The maximum pool size. When the pool reaches the max size then any further instances returned to the pool will be ignored and can be garbage collected. This can be used to prevent the pool from growing to a huge size.</param>
  public ObjectPool(
      Func<T> createFunc,
      Action<T> actionOnGet = null,
      Action<T> actionOnRelease = null,
      Action<T> actionOnDestroy = null,
      bool collectionCheck = true,
      int defaultCapacity = 10,
      int maxSize = 10000)
  {
    ArgumentNullException.ThrowIfNull(createFunc);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSize);

    _stack = new(defaultCapacity);
    _createFunc = createFunc;
    _actionOnGet = actionOnGet;
    _actionOnRelease = actionOnRelease;
    _actionOnDestroy = actionOnDestroy;
    _maxSize = maxSize;
    _collectionCheck = collectionCheck;
  }

  /// <summary>
  /// Gets an instance from the pool. If the pool is empty then a new instance will be created.
  /// </summary>
  public T Get()
  {
    T element;
    if (_stack.Count == 0)
    {
      element = _createFunc();
      CountAll++;
    }
    else
    {
      element = _stack.Pop();
    }

    _actionOnGet?.Invoke(element);

    if (element is IPoolable poolable)
      poolable.OnSpawn();

    return element;
  }

  /// <summary>
  /// Gets a PooledObject which implements IDisposable so you can use it in a <c>using</c> block.
  /// </summary>
  public PooledObject<T> Get(out T v) => new(v = Get(), this);

  /// <summary>
  /// Returns the instance back to the pool.
  /// </summary>
  public void Release(T element)
  {
    if (element is null)
      throw new ArgumentNullException(nameof(element), "Cannot release a null element.");

    if (_collectionCheck && _stack.Count > 0 && _stack.Contains(element))
      throw new InvalidOperationException("Trying to release an object that has already been released to the pool.");

    if (element is IPoolable poolable)
      poolable.OnDespawn();

    _actionOnRelease?.Invoke(element);

    if (CountInactive < _maxSize)
    {
      _stack.Push(element);
    }
    else
    {
      _actionOnDestroy?.Invoke(element);
      CountAll--;
    }
  }

  /// <summary>
  /// Clears the pool and calls actionOnDestroy on all inactive items.
  /// </summary>
  public void Clear()
  {
    if (_actionOnDestroy != null)
    {
      foreach (var item in _stack)
        _actionOnDestroy(item);
    }
    _stack.Clear();
    CountAll = 0;
  }

  public void Dispose() => Clear();
}

/// <summary>
/// A disposable wrapper struct that automatically releases an object back to its pool when disposed.
/// Useful for <c>using</c> blocks.
/// </summary>
public readonly struct PooledObject<T> : IDisposable where T : class
{
  private readonly T _mToReturn;
  private readonly ObjectPool<T> _mPool;

  internal PooledObject(T value, ObjectPool<T> pool)
  {
    _mToReturn = value;
    _mPool = pool;
  }

  void IDisposable.Dispose() => _mPool.Release(_mToReturn);
}