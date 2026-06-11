using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Hierarchical transform node for grouping windows or other groups.
/// </summary>
public class GroupData : IStoryboardable<StoryboardProperty>, IDeepCloneable<GroupData>
{
  public event Action<GroupData> OnUpdated;

  public string ID;
  private string _name;
  public string Name { get => _name; set { if (_name == value) return; _name = value; OnUpdated?.Invoke(this); } }

  private string _parentGroupID;
  public string ParentGroupID { get => _parentGroupID; set { if (_parentGroupID == value) return; _parentGroupID = value; OnUpdated?.Invoke(this); } }

  private float _initX = 0f;
  public float InitX { get => _initX; set { if (_initX == value) return; _initX = value; OnUpdated?.Invoke(this); } }

  private float _initY = 0f;
  public float InitY { get => _initY; set { if (_initY == value) return; _initY = value; OnUpdated?.Invoke(this); } }

  private float _initScaleX = 1f;
  public float InitScaleX { get => _initScaleX; set { if (_initScaleX == value) return; _initScaleX = value; OnUpdated?.Invoke(this); } }

  private float _initScaleY = 1f;
  public float InitScaleY { get => _initScaleY; set { if (_initScaleY == value) return; _initScaleY = value; OnUpdated?.Invoke(this); } }

  private float _initRotation = 0f;
  public float InitRotation { get => _initRotation; set { if (_initRotation == value) return; _initRotation = value; OnUpdated?.Invoke(this); } }

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public GroupData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public GroupData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new GroupData();

    // Detach bubbling from the default StoryboardEvents created by constructor
    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;

    cloned.ID = objectFactory.GenerateUID();
    cloned.Name = Name;
    cloned.ParentGroupID = ParentGroupID;
    cloned.InitX = InitX;
    cloned.InitY = InitY;
    cloned.InitScaleX = InitScaleX;
    cloned.InitScaleY = InitScaleY;
    cloned.InitRotation = InitRotation;
    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset);

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}