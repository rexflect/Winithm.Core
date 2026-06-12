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
  public event Action<BPMStop> OnStartBeatChanged;
  public event Action<BPMStop> OnInvalidate;
  public event Action<BPMStop> OnUpdated;

  private BeatTime _startBeat = BeatTime.Zero;
  public BeatTime StartBeat { get => _startBeat; set { if (_startBeat == value) return; _startBeat = value; OnStartBeatChanged?.Invoke(this); OnInvalidate?.Invoke(this); } }

  private float _bpm = 120;
  public float BPM { get => _bpm; set { if (_bpm == value) return; _bpm = value; OnInvalidate?.Invoke(this); } }

  private int _timeSignatureNum = 4;
  public int TimeSignatureNum { get => _timeSignatureNum; set { if (_timeSignatureNum == value) return; _timeSignatureNum = value; OnUpdated?.Invoke(this); } }

  private int _timeSignatureDen = 4;
  public int TimeSignatureDen { get => _timeSignatureDen; set { if (_timeSignatureDen == value) return; _timeSignatureDen = value; OnUpdated?.Invoke(this); } }

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
    return new()
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
  public event Action<BaseBPM> OnInvalidate;
  public event Action<BaseBPM> OnUpdated;

  private double _baseOffsetSeconds = 0;
  public double BaseOffsetSeconds { get => _baseOffsetSeconds; set { if (_baseOffsetSeconds == value) return; _baseOffsetSeconds = value; OnInvalidate?.Invoke(this); } }

  private float _initialBPM = 120;
  public float InitialBPM { get => _initialBPM; set { if (_initialBPM == value) return; _initialBPM = value; OnInvalidate?.Invoke(this); } }

  private int _timeSignatureNum = 4;
  public int TimeSignatureNum { get => _timeSignatureNum; set { if (_timeSignatureNum == value) return; _timeSignatureNum = value; OnUpdated?.Invoke(this); } }

  private int _timeSignatureDen = 4;
  public int TimeSignatureDen { get => _timeSignatureDen; set { if (_timeSignatureDen == value) return; _timeSignatureDen = value; OnUpdated?.Invoke(this); } }

  public float BeatsPerSecond => InitialBPM / 60f;

  public static readonly BaseBPM NaN = new()
  {
    BaseOffsetSeconds = 0,
    InitialBPM = 0,
    TimeSignatureNum = 0,
    TimeSignatureDen = 0
  };
}
