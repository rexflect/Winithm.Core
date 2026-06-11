using Godot;
using System;

namespace Winithm.Core.Managers;

/// <summary>
/// A Godot 4 Object Pool that automatically handles scene instantiation,
/// toggling visibility/processing, and queueing free on destroy.
/// </summary>
public class NodePool<T> : ObjectPool<T> where T : Node
{
  /// <summary>
  /// Instantiates a Godot Node pool.
  /// </summary>
  /// <param name="parent">The parent Node that all pooled objects will be added to upon creation.</param>
  /// <param name="scene">Optional: The PackedScene to instantiate. If null, it will default to new T().</param>
  /// <param name="createFunc">Optional override for creation logic.</param>
  /// <param name="actionOnGet">Optional override for get logic. Defaults to enabling ProcessMode and making the node visible.</param>
  /// <param name="actionOnRelease">Optional override for release logic. Defaults to disabling ProcessMode and making the node invisible.</param>
  /// <param name="actionOnDestroy">Optional override for destroy logic. Defaults to calling QueueFree().</param>
  /// <param name="collectionCheck">Whether to throw exceptions if the same object is returned twice.</param>
  /// <param name="defaultCapacity">Initial stack size.</param>
  /// <param name="maxSize">Max stack size (objects beyond this are queued free).</param>
  public NodePool(
      Node parent,
      PackedScene scene = null,
      Func<T> createFunc = null,
      Action<T> actionOnGet = null,
      Action<T> actionOnRelease = null,
      Action<T> actionOnDestroy = null,
      bool collectionCheck = true,
      int defaultCapacity = 10,
      int maxSize = 10000)
      : base(
          createFunc ?? (() =>
          {
            T instance = scene != null ? scene.Instantiate<T>() : Activator.CreateInstance<T>();
            // Add to the tree immediately so it never needs to be re-parented
            parent.AddChild(instance);
            return instance;
          }),
          actionOnGet ?? (element =>
          {
            // ProcessMode.Inherit re-enables all processing (including physics and input)
            // for the node and its children — more thorough than individual SetProcess calls
            element.ProcessMode = Node.ProcessModeEnum.Inherit;

            if (element is CanvasItem canvasItem)
              canvasItem.Visible = true;
            else if (element is Node3D node3D)
              node3D.Visible = true;
          }),
          actionOnRelease ?? (element =>
          {
            // ProcessMode.Disabled suspends all processing for the node and its children
            element.ProcessMode = Node.ProcessModeEnum.Disabled;

            if (element is CanvasItem canvasItem)
              canvasItem.Visible = false;
            else if (element is Node3D node3D)
              node3D.Visible = false;
          }),
          actionOnDestroy ?? (element =>
          {
            if (GodotObject.IsInstanceValid(element) && !element.IsQueuedForDeletion())
              element.QueueFree();
          }),
          collectionCheck,
          defaultCapacity,
          maxSize)
  {
  }
}