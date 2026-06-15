using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Animation state or transition for a property.
/// </summary>
public class EventData : IDeepCloneable<EventData>
{
  public event Action<EventData>? OnStartBeatChanged;
  public event Action<EventData>? OnUpdated;

  public string ID = "";

  public BeatTime StartBeat { get; set { if (field == value) return; field = value; OnStartBeatChanged?.Invoke(this); } } = BeatTime.NaN;

  public double Length { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;

  public AnyValue From { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = new(0f);

  public AnyValue To { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = new(0f);

  public EasingType Easing { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = EasingType.Linear;

  public AnyValue EasingBezier { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = new(0f, 0f, 1f, 1f);

  public EventData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    return new EventData()
    {
      ID = objectFactory.GenerateUID(),
      StartBeat = StartBeat + (offset ?? BeatTime.Zero),
      Length = Length,
      From = From,
      To = To,
      Easing = Easing,
      EasingBezier = EasingBezier
    };
  }
}
