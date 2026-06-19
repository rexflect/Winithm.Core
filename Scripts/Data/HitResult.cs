namespace Winithm.Core.Data;

public enum HitResultType
{
  Perfect,
  Good,
  Bad,
  Miss
}

/// <summary>
/// Detailed result of a hit interaction.
/// </summary>
public struct HitResult
{
  /// <summary>Weight: 1.0 (Perfect), 0.65 (Good), 0.1 (Bad), 0.0 (Miss).</summary>
  public float Weight;
  /// <summary>Timing offset in milliseconds (negative = early, positive = late).</summary>
  public double OffsetMs;
  /// <summary>The type of hit result.</summary>
  public HitResultType Type;

  /// <summary>The note that was evaluated.</summary>
  public NoteData Note;

  public readonly bool IsHit => Weight > 0f;

  public static HitResult None(NoteData note) =>
    new()
    {
      Weight = Constants.HitResult.ResultWeight[HitResultType.Miss],
      OffsetMs = double.NaN,
      Note = note,
      Type = HitResultType.Miss
    };

  public static HitResult AutoHit(NoteData note) =>
    new()
    {
      Weight = Constants.HitResult.ResultWeight[HitResultType.Perfect],
      OffsetMs = 0,
      Note = note,
      Type = HitResultType.Perfect
    };

  public static HitResult Miss(NoteData note) =>
    new()
    {
      Weight = Constants.HitResult.ResultWeight[HitResultType.Miss],
      OffsetMs = float.MaxValue,
      Note = note,
      Type = HitResultType.Miss
    };

  public static HitResult FromOffset(NoteData note, double offsetMs)
  {
    double absMs = System.Math.Abs(offsetMs);
    float weight;
    HitResultType type;

    if (absMs <= Constants.HitResult.TimmingWindowMs[HitResultType.Perfect])
    {
      type = HitResultType.Perfect;
      weight = Constants.HitResult.ResultWeight[HitResultType.Perfect];
    }
    else if (absMs <= Constants.HitResult.TimmingWindowMs[HitResultType.Good])
    {
      type = HitResultType.Good;
      weight = Constants.HitResult.ResultWeight[HitResultType.Good];
    }
    else if (absMs <= Constants.HitResult.TimmingWindowMs[HitResultType.Bad])
    {
      type = HitResultType.Bad;
      weight = Constants.HitResult.ResultWeight[HitResultType.Bad];
    }
    else
    {
      type = HitResultType.Miss;
      weight = Constants.HitResult.ResultWeight[HitResultType.Miss];
    }

    if (note.Type is NoteType.Hold) weight *= 2f;

    return new HitResult() { Weight = weight, OffsetMs = offsetMs, Note = note, Type = type };
  }

  /// <summary>Binary result for evaluation.</summary>
  public static HitResult FromBinary(NoteData note, double offsetMs)
  {
    double absMs = System.Math.Abs(offsetMs);
    float weight =
      absMs <= Constants.HitResult.TimmingWindowMs[HitResultType.Good] ?
      Constants.HitResult.ResultWeight[HitResultType.Perfect]
      : Constants.HitResult.ResultWeight[HitResultType.Miss];
      
    HitResultType type =
      absMs <= Constants.HitResult.TimmingWindowMs[HitResultType.Good] ?
      HitResultType.Perfect
      : HitResultType.Miss;

    return new HitResult() { Weight = weight, OffsetMs = offsetMs, Note = note, Type = type };
  }
}
