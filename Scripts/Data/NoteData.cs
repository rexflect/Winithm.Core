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
  public event Action<NoteData, double>? OnStartBeatChanged;
  public event Action<NoteData>? OnInvalidate;
  public event Action<NoteData>? OnUpdated;

  public string ID = "";

  public NoteType Type { get; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = NoteType.Tap;

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

  public double Length { get; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = 0;

  public float X { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;

  public float Width { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1;

  public int FakeType { get; set { if (field == value) return; field = value; OnInvalidate?.Invoke(this); } } = 0;

  public ResourcePack? ResourcePack { get; set { if (Nullable.Equals(field, value)) return; field = value; OnUpdated?.Invoke(this); } }

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
    return new NoteData()
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

  public static NoteSide ParseNoteSide(string name)
  {
    return name.ToLowerInvariant() switch
    {
      "bottom" => NoteSide.Bottom,
      "top" => NoteSide.Top,
      "left" => NoteSide.Left,
      "right" => NoteSide.Right,
      _ => NoteSide.Bottom
    };
  }
}
