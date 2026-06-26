using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Scroll speed segment for a window.
/// </summary>
public class SpeedStepData : IStoryboardable<StoryboardProperty>, IDeepCloneableUID<SpeedStepData>
{
  public event Action<SpeedStepData, double>? OnStartBeatChanged;
  public event Action<SpeedStepData>? OnUpdated;

  public string ID = "";

  public BeatTime StartBeat
  {
    get;
    set
    {
      if (field == value) return;
      double prevStartBeat = field.AbsoluteValue;
      field = value;
      OnStartBeatChanged?.Invoke(this, prevStartBeat);
    }
  } = BeatTime.NaN;

  public float Multiplier { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public SpeedStepData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public SpeedStepData DeepCloner(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new SpeedStepData();

    // Detach bubbling from the default StoryboardEvents created by constructor
    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;

    cloned.ID = objectFactory.GenerateUID();
    cloned.StartBeat = StartBeat + (offset ?? BeatTime.Zero);
    cloned.Multiplier = Multiplier;
    cloned.StoryboardEvents = StoryboardEvents?.DeepCloner(objectFactory, offset) ?? new StoryboardManager<StoryboardProperty>();

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}
