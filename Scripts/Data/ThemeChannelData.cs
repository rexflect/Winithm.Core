using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Shared color palette and opacity channel for themes.
/// </summary>
public class ThemeChannelData : IStoryboardable<StoryboardProperty>, IDeepCloneable<ThemeChannelData>
{
  public event Action<ThemeChannelData>? OnUpdated;

  public string ID = "";
  public string Name { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  public float InitR { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public float InitG { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public float InitB { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;

  public float InitA { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public float InitNoteA { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public ThemeChannelData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public ThemeChannelData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new ThemeChannelData();

    // Detach bubbling from the default StoryboardEvents created by constructor
    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;

    cloned.ID = objectFactory.GenerateUID();
    cloned.Name = Name;
    cloned.InitR = InitR;
    cloned.InitG = InitG;
    cloned.InitB = InitB;
    cloned.InitA = InitA;
    cloned.InitNoteA = InitNoteA;
    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset) ?? new StoryboardManager<StoryboardProperty>();

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}