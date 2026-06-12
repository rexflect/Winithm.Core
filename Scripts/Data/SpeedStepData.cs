using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Scroll speed segment for a window.
/// </summary>
public class SpeedStepData : IStoryboardable<StoryboardProperty>, IDeepCloneable<SpeedStepData>
{
  public event Action<SpeedStepData> OnStartBeatChanged;
  public event Action<SpeedStepData> OnUpdated;

  public string ID;

  private BeatTime _startBeat = BeatTime.NaN;
  public BeatTime StartBeat { get => _startBeat; set { if (_startBeat == value) return; _startBeat = value; OnStartBeatChanged?.Invoke(this); } }

  private float _multiplier = 1f;
  public float Multiplier { get => _multiplier; set { if (_multiplier == value) return; _multiplier = value; OnUpdated?.Invoke(this); } }

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public SpeedStepData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public SpeedStepData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new SpeedStepData();

    // Detach bubbling from the default StoryboardEvents created by constructor
    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;

    cloned.ID = objectFactory.GenerateUID();
    cloned.StartBeat = StartBeat + (offset ?? BeatTime.Zero);
    cloned.Multiplier = Multiplier;
    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset);

    // Re-wire bubbling to the cloned StoryboardEvents
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  // Named delegate for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
}

