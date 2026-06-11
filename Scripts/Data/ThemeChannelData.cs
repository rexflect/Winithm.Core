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
  public event Action<ThemeChannelData> OnUpdated;

  public string ID;
  private string _name;
  public string Name { get => _name; set { if (_name == value) return; _name = value; OnUpdated?.Invoke(this); } }

  private float _initR = 0f;
  public float InitR { get => _initR; set { if (_initR == value) return; _initR = value; OnUpdated?.Invoke(this); } }

  private float _initG = 0f;
  public float InitG { get => _initG; set { if (_initG == value) return; _initG = value; OnUpdated?.Invoke(this); } }

  private float _initB = 0f;
  public float InitB { get => _initB; set { if (_initB == value) return; _initB = value; OnUpdated?.Invoke(this); } }

  private float _initA = 1f;
  public float InitA { get => _initA; set { if (_initA == value) return; _initA = value; OnUpdated?.Invoke(this); } }

  private float _initNoteA = 1f;
  public float InitNoteA { get => _initNoteA; set { if (_initNoteA == value) return; _initNoteA = value; OnUpdated?.Invoke(this); } }

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
    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset);

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}