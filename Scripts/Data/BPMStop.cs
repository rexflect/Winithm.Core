using System;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Represents a tempo change at a specific beat.
/// </summary>
public class BPMStop : IDeepCloneable<BPMStop>
{
  public event Action<BPMStop>? OnStartBeatChanged;
  public event Action<BPMStop>? OnInvalidate;
  public event Action<BPMStop>? OnUpdated;

  public BeatTime StartBeat { get => field; set { if (field == value) return; field = value; OnStartBeatChanged?.Invoke(this); OnInvalidate?.Invoke(this); } } = BeatTime.Zero;

  public float BPM { get => field; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = 120f;

  public int TimeSignatureNum { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 4;

  public int TimeSignatureDen { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 4;

  public double StartTimeSeconds;

  public float BeatsPerSecond => BPM / 60f;

  public static readonly BPMStop NaN = new()
  {
    StartBeat = BeatTime.NaN,
    BPM = 0,
    TimeSignatureNum = 0,
    TimeSignatureDen = 0,
    StartTimeSeconds = 0
  };
  public static readonly BPMStop Max = new()
  {
    StartBeat = BeatTime.Max,
    BPM = 0,
    TimeSignatureNum = 0,
    TimeSignatureDen = 0,
    StartTimeSeconds = 0
  };

  public BPMStop DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    return new BPMStop()
    {
      StartBeat = StartBeat + (offset ?? BeatTime.Zero),
      BPM = BPM,
      TimeSignatureNum = TimeSignatureNum,
      TimeSignatureDen = TimeSignatureDen,
      StartTimeSeconds = StartTimeSeconds
    };
  }
}

/// <summary>
/// Global timing foundation for the beat grid.
/// </summary>
public class BaseBPM
{
  public event Action<BaseBPM>? OnInvalidate;
  public event Action<BaseBPM>? OnUpdated;

  public double BaseOffsetSeconds { get => field; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = 0;

  public float InitialBPM { get; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = 120f;

  public int TimeSignatureNum { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 4;

  public int TimeSignatureDen { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 4;

  public float BeatsPerSecond => InitialBPM / 60f;

  public static readonly BaseBPM NaN = new()
  {
    BaseOffsetSeconds = 0,
    InitialBPM = 0,
    TimeSignatureNum = 0,
    TimeSignatureDen = 0
  };
}
