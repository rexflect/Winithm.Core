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
  public event Action<EventData> OnStartBeatChanged;
  public event Action<EventData> OnUpdated;

  public string ID;

  private BeatTime _startBeat = BeatTime.NaN;
  public BeatTime StartBeat { get => _startBeat; set { if (_startBeat != value) { _startBeat = value; OnStartBeatChanged?.Invoke(this); } } }

  private double _length = 0;
  public double Length { get => _length; set { if (_length != value) { _length = value; OnUpdated?.Invoke(this); } } }

  private AnyValue _from = new(0f);
  public AnyValue From { get => _from; set { if (_from != value) { _from = value; OnUpdated?.Invoke(this); } } }

  private AnyValue _to = new(0f);
  public AnyValue To { get => _to; set { if (_to != value) { _to = value; OnUpdated?.Invoke(this); } } }

  private EasingType _easing = EasingType.Linear;
  public EasingType Easing { get => _easing; set { if (_easing != value) { _easing = value; OnUpdated?.Invoke(this); } } }

  private AnyValue _easingBezier = new(0f, 0f, 1f, 1f);
  public AnyValue EasingBezier { get => _easingBezier; set { if (_easingBezier != value) { _easingBezier = value; OnUpdated?.Invoke(this); } } }

  public EventData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    return new()
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
