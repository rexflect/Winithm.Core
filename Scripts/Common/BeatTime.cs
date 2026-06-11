using System;
using System.Numerics;

namespace Winithm.Core.Common;

/// <summary>
/// Deterministic timing format: B:N/D (Base beat + Numerator/Denominator).
/// </summary>
public struct BeatTime(int beat, int numerator, int denominator)
  : IComparable, IComparable<BeatTime>, IEquatable<BeatTime>
{
  public int Beat { get; } = beat;
  public int Numerator { get; } = numerator;
  public int Denominator { get; } = denominator;
  /// <summary>
  /// Pre-computed absolute beat value (double avoids precision loss at large values).
  /// </summary>
  public double AbsoluteValue { get; } = (numerator != 0 && denominator == 0)
    ? beat
    : (denominator != 0 ? beat + (double)numerator / denominator : beat);

  // ==========================================
  // Constants
  // ==========================================

  public static readonly BeatTime Zero = new(0, 0, 0);
  public static readonly BeatTime NaN = new(0, 0, 0);
  public static readonly BeatTime Min = new(int.MinValue, 0, 1);
  public static readonly BeatTime Max = new(int.MaxValue, 1, 1);

  // ==========================================
  // Parsing
  // ==========================================

  /// <summary>
  /// Parses a BeatTime string ("B" or "B:N/D"). Throws FormatException if invalid.
  /// </summary>
  public static BeatTime Parse(string text)
  {
    if (TryParse(text, out var result))
      return result;

    throw new FormatException(
      $"[BeatTime] Cannot parse \"{text}\". Expected format: \"B\" or \"B:N/D\" (e.g. \"1\", \"1:3/4\").");
  }

  /// <summary>
  /// Try parsing a BeatTime string ("B" or "B:N/D"). Returns false if invalid.
  /// </summary>
  public static bool TryParse(string text, out BeatTime result)
  {
    result = Zero;

    if (string.IsNullOrWhiteSpace(text))
      return false;

    text = text.Trim();

    int colonIndex = text.IndexOf(':');

    // Format: "B" (integer only)
    if (colonIndex < 0)
    {
      if (!int.TryParse(text, out int beatOnly))
        return false;

      result = new(beatOnly, 0, 0);
      return true;
    }

    int slashIndex = text.IndexOf('/');

    // Colon present but no slash → malformed
    if (slashIndex < 0 || slashIndex < colonIndex)
      return false;

    if (!int.TryParse(text.AsSpan(0, colonIndex), out int beat))
      return false;

    if (!int.TryParse(text.AsSpan(colonIndex + 1, slashIndex - colonIndex - 1), out int numerator))
      return false;

    if (!int.TryParse(text.AsSpan(slashIndex + 1), out int denominator))
      return false;

    if (denominator < 0)
      return false;

    result = new(beat, numerator, denominator);
    return true;
  }

  // ==========================================
  // Formatting
  // ==========================================

  public override readonly string ToString() => $"{Beat}:{Numerator}/{Denominator}";
  

  // ==========================================
  // Comparison Operators
  // ==========================================

  public static bool operator <(BeatTime a, BeatTime b) => a.CompareTo(b) < 0;
  public static bool operator >(BeatTime a, BeatTime b) => a.CompareTo(b) > 0;
  public static bool operator <=(BeatTime a, BeatTime b) => a.CompareTo(b) <= 0;
  public static bool operator >=(BeatTime a, BeatTime b) => a.CompareTo(b) >= 0;
  public static bool operator ==(BeatTime a, BeatTime b) => a.Equals(b);
  public static bool operator !=(BeatTime a, BeatTime b) => !a.Equals(b);

  // ==========================================
  // Arithmetic Operators
  // ==========================================

  public static BeatTime operator +(BeatTime a, BeatTime b)
  {
    ToReducedFraction(a, out var n1, out var d1);
    ToReducedFraction(b, out var n2, out var d2);
    return NormalizeAndCreate((n1 * d2) + (n2 * d1), d1 * d2);
  }

  public static BeatTime operator -(BeatTime a, BeatTime b)
  {
    ToReducedFraction(a, out var n1, out var d1);
    ToReducedFraction(b, out var n2, out var d2);
    return NormalizeAndCreate((n1 * d2) - (n2 * d1), d1 * d2);
  }

  public static BeatTime operator *(BeatTime a, BeatTime b)
  {
    ToReducedFraction(a, out var n1, out var d1);
    ToReducedFraction(b, out var n2, out var d2);
    return NormalizeAndCreate(n1 * n2, d1 * d2);
  }

  /// <summary>Divides a by b. Throws DivideByZeroException if b is zero.</summary>
  public static BeatTime operator /(BeatTime a, BeatTime b)
  {
    if (b == Zero)
      throw new DivideByZeroException("[BeatTime] Cannot divide by zero (b is BeatTime.Zero).");

    ToReducedFraction(a, out var n1, out var d1);
    ToReducedFraction(b, out var n2, out var d2);
    return NormalizeAndCreate(n1 * d2, d1 * n2);
  }

  // ==========================================
  // IComparable / IEquatable
  // ==========================================

  public readonly int CompareTo(BeatTime other)
  {
    ToReducedFraction(this, out var ln, out var ld);
    ToReducedFraction(other, out var rn, out var rd);
    return (ln * rd).CompareTo(rn * ld);
  }

  public readonly int CompareTo(object obj)
  {
    if (obj == null)
      return 1;

    if (obj is BeatTime other)
      return CompareTo(other);

    throw new ArgumentException("Object must be of type BeatTime.", nameof(obj));
  }

  public readonly bool Equals(BeatTime other) => CompareTo(other) == 0;

  public override bool Equals(object obj)
  {
    if (obj is BeatTime other)
      return Equals(other);

    return false;
  }

  public override readonly int GetHashCode()
  {
    ToReducedFraction(this, out var numerator, out var denominator);
    return unchecked((numerator.GetHashCode() * 397) ^ denominator.GetHashCode());
  }

  // ==========================================
  // Private Helpers
  // ==========================================

  /// <summary>
  /// Converts BeatTime to a fully reduced improper fraction (n/d).
  /// </summary>
  private static void ToReducedFraction(BeatTime bt, out BigInteger numerator, out BigInteger denominator)
  {
    if (bt.Denominator == 0)
    {
      numerator = bt.Beat;
      denominator = BigInteger.One;
      return;
    }

    numerator = (BigInteger)bt.Beat * bt.Denominator + bt.Numerator;
    denominator = bt.Denominator;

    // Denominator is always positive coming out of NormalizeAndCreate,
    // but guard defensively in case of direct constructor usage.
    if (denominator.Sign < 0)
    {
      numerator = -numerator;
      denominator = -denominator;
    }
  }

  /// <summary>
  /// Reduces an improper fraction (n/d) to lowest terms and canonical form.
  /// </summary>
  private static BeatTime NormalizeAndCreate(BigInteger n, BigInteger d)
  {
    if (d.IsZero)
      throw new DivideByZeroException("[BeatTime] Result has a zero denominator.");

    // Ensure denominator is positive.
    if (d.Sign < 0)
    {
      n = -n;
      d = -d;
    }

    if (n.IsZero)
      return Zero;

    // Reduce to lowest terms.
    BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
    n /= gcd;
    d /= gcd;

    // Whole number result.
    if (d.IsOne)
      return new((int)n, 0, 0);

    // Split into beat + positive remainder (floor division).
    BigInteger beat = BigInteger.DivRem(n, d, out BigInteger remainder);
    if (remainder.Sign < 0)
    {
      beat -= BigInteger.One;
      remainder += d;
    }

    return new((int)beat, (int)remainder, (int)d);
  }
}

