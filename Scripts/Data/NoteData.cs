using System;
using Winithm.Core.Common;
using Winithm.Core.Managers;
using Winithm.Core.Interfaces;

namespace Winithm.Core.Data;

/// <summary>
/// Basic note data including timing, type, and spatial properties.
/// </summary>
public enum NoteType
{
  Tap,
  Hold,
  Drag,
  Focus,
  Close
}

public class NoteData : IDeepCloneable<NoteData>
{
  public event Action<NoteData, double> OnStartBeatChanged;
  public event Action<NoteData> OnInvalidate;
  public event Action<NoteData> OnUpdated;

  public string ID;

  private NoteType _type = NoteType.Tap;
  public NoteType Type { get => _type; set { if (_type == value) return; _type = value; OnInvalidate?.Invoke(this); } }

  private BeatTime _startBeat = BeatTime.NaN;
  public BeatTime StartBeat
  {
    get => _startBeat;
    set
    {
      if (_startBeat == value) return;
      double prevStartBeat = _startBeat.AbsoluteValue;
      _startBeat = value;
      OnStartBeatChanged?.Invoke(this, prevStartBeat);
    }
  }

  private double _length = 0;
  public double Length { get => _length; set { if (_length == value) return; _length = value; OnInvalidate?.Invoke(this); } }

  private float _x = 0;
  public float X { get => _x; set { if (_x == value) return; _x = value; OnUpdated?.Invoke(this); } }

  private float _width = 1;
  public float Width { get => _width; set { if (_width == value) return; _width = value; OnUpdated?.Invoke(this); } }

  private int _fakeType = 0;
  public int FakeType { get => _fakeType; set { if (_fakeType == value) return; _fakeType = value; OnInvalidate?.Invoke(this); } }

  private ResourcePack? _resourcePack;
  public ResourcePack? ResourcePack { get => _resourcePack; set { if (Nullable.Equals(_resourcePack, value)) return; _resourcePack = value; OnUpdated?.Invoke(this); } }

  public bool IsHittable => FakeType == 0;
  public bool IsMutedGhost => FakeType == 1;
  public bool IsLoudGhost => FakeType == 2;

  /// <summary>If note's lifecycle is bounded by the parent window.</summary>
  public bool IsLifecycleBounded = false;


  /// <summary>Gets or sets whether the note has been evaluated.</summary>
  public bool IsEvaluated = false;


  /// <summary>Gets or sets the session token for auto-fired notes.</summary>
  public ulong AutoFiredSessionToken = 0;

  /// <summary>Gets or sets the session token for the last processed frame.</summary>
  public ulong LastSeenFrameSessionToken = 0;

  /// <summary>Gets or sets the session token for consumed notes.</summary>
  public ulong ConsumedSessionToken = 0;


  /// <summary>Gets or sets whether the hold interaction is active.</summary>
  public bool IsHoldActive = false;

  /// <summary>Gets or sets the timing offset at the start of a hold note.</summary>
  public HitResult HoldStartResult;

  public NoteData()
  {
    HoldStartResult = HitResult.None(this);
  }

  public NoteData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
  {
    return new()
    {
      ID = objectFactory.GenerateUID(),
      Type = Type,
      StartBeat = StartBeat + (offset ?? BeatTime.Zero),
      Length = Length,
      X = X,
      Width = Width,
      FakeType = FakeType,
      ResourcePack = ResourcePack
    };
  }

  public static NoteType ParseNoteType(string name)
  {
    return name.ToLowerInvariant() switch
    {
      "tap" => NoteType.Tap,
      "hold" => NoteType.Hold,
      "drag" => NoteType.Drag,
      "focus" => NoteType.Focus,
      "close" => NoteType.Close,
      _ => NoteType.Tap
    };
  }
}
