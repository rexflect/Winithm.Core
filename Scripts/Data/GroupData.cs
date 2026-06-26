using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Hierarchical transform node for grouping windows or other groups.
/// </summary>
public class GroupData : IStoryboardable<StoryboardProperty>, IDeepCloneableUID<GroupData>
{
  public event Action<GroupData>? OnUpdated;

  public string ID = "";
  public string Name { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  public string ParentGroupID { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  public float InitX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public float InitY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public float InitScaleX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public float InitScaleY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public float InitRotation { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public GroupData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public GroupData DeepCloner(ObjectFactory objectFactory, BeatTime? offset)
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
    cloned.StoryboardEvents = StoryboardEvents?.DeepCloner(objectFactory, offset) ?? new StoryboardManager<StoryboardProperty>();

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}