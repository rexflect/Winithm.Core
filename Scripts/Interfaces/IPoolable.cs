namespace Winithm.Core.Interfaces;

/// <summary>
/// Interface for objects that can be pooled.
/// Provides lifecycle callbacks when an object is taken from or returned to a pool.
/// </summary>
public interface IPoolable
{
  /// <summary>
  /// Called when the object is instantiated or taken from the pool.
  /// Initialize necessary fields here.
  /// </summary>
  void OnSpawn();

  /// <summary>
  /// Called when the object is returned to the pool.
  /// Reset your object's state cleanly and securely here.
  /// </summary>
  void OnDespawn();
}
