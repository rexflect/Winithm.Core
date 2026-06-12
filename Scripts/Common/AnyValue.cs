using System;
using System.Globalization;

namespace Winithm.Core.Common;

/// <summary>
/// Supported value types, aligned with GLSL data types.
/// </summary>
public enum AnyValueType
{
  Float, Vec2, Vec3, Vec4, Bool, String, Inherited
}

/// <summary>
/// Universal value for float, vector (up to 4D), string, or inherited placeholder.
/// Formats: "1|2|3" (Vec3), "1" (Float), "-" (Inherited), "string".
/// </summary>
public struct AnyValue : IComparable, IComparable<AnyValue>, IEquatable<AnyValue>
{
  public float X;
  public float Y;
  public float Z;
  public float W;
  public string StringValue;
  public AnyValueType Type;

  public AnyValue(float x)
  {
    X = x; Y = 0; Z = 0; W = 0;
    StringValue = null;
    Type = AnyValueType.Float;
  }

  public AnyValue(float x, float y)
  {
    X = x; Y = y; Z = 0; W = 0;
    StringValue = null;
    Type = AnyValueType.Vec2;
  }

  public AnyValue(float x, float y, float z)
  {
    X = x; Y = y; Z = z; W = 0;
    StringValue = null;
    Type = AnyValueType.Vec3;
  }

  public AnyValue(float x, float y, float z, float w)
  {
    X = x; Y = y; Z = z; W = w;
    StringValue = null;
    Type = AnyValueType.Vec4;
  }


  public AnyValue(string value)
  {
    X = 0; Y = 0; Z = 0; W = 0;
    StringValue = value;
    Type = AnyValueType.String;
  }

  public AnyValue(bool value)
  {
    X = value ? 1 : 0; Y = 0; Z = 0; W = 0;
    StringValue = null;
    Type = AnyValueType.Bool;
  }

  public static AnyValue NaN = new(0);

  /// <summary>Number of float components.</summary>
  public static int ComponentCount(AnyValueType type)
  {
    return type switch
    {
      AnyValueType.Float or AnyValueType.Bool => 1,
      AnyValueType.Vec2 => 2,
      AnyValueType.Vec3 => 3,
      AnyValueType.Vec4 => 4,
      _ => 0
    };
  }

  /// <summary>Parses string to AnyValue. Vector delimiter is '|'.</summary>
  public static AnyValue Parse(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return new("");

    text = text.Trim();

    // Inheritance marker
    if (text is "-") return new() { Type = AnyValueType.Inherited };

    // Pipe-separated vector
    if (text.Contains("|"))
    {
      string[] parts = text.Split('|');
      int count = Math.Min(parts.Length, 4);

      ReadOnlySpan<AnyValueType> vecTypes = [AnyValueType.Float, AnyValueType.Vec2, AnyValueType.Vec3, AnyValueType.Vec4];
      AnyValue result = new() { Type = vecTypes[Math.Min(count - 1, 3)] };

      if (count > 0) float.TryParse(parts[0].Trim(), NumberStyles.Float, ParserUtils.INV, out result.X);
      if (count > 1) float.TryParse(parts[1].Trim(), NumberStyles.Float, ParserUtils.INV, out result.Y);
      if (count > 2) float.TryParse(parts[2].Trim(), NumberStyles.Float, ParserUtils.INV, out result.Z);
      if (count > 3) float.TryParse(parts[3].Trim(), NumberStyles.Float, ParserUtils.INV, out result.W);
      return result;
    }

    // Boolean
    if (ParserUtils.TryParseBool(text, out bool bValue))
    {
      return new(bValue);
    }

    // Plain number (float)
    if (ParserUtils.TryParseFloat(text, out float fValue))
    {
      return new(fValue);
    }

    // String
    return new(text.Trim('\"'));
  }

  /// <summary>Linearly interpolates numeric types. Non-numeric types snap at t >= 1.</summary>
  public static AnyValue Lerp(AnyValue from, AnyValue to, double t)
  {
    if (
      from.Type is AnyValueType.String ||
      to.Type is AnyValueType.String ||
      from.Type is AnyValueType.Inherited ||
      to.Type is AnyValueType.Inherited ||
      from.Type is AnyValueType.Bool ||
      to.Type is AnyValueType.Bool)
    {
      return t >= 1f ? to : from;
    }

    int sizeFrom = ComponentCount(from.Type);
    int sizeTo = ComponentCount(to.Type);
    int maxSize = Math.Max(sizeFrom, sizeTo);

    AnyValue result = new()
    {
      // Determine resulting type based on max component count
      Type = maxSize switch
      {
        1 => AnyValueType.Float,
        2 => AnyValueType.Vec2,
        3 => AnyValueType.Vec3,
        4 => AnyValueType.Vec4,
        _ => AnyValueType.Float
      }
    };

    if (maxSize >= 1) result.X = (float)(from.X + (to.X - from.X) * t);
    if (maxSize >= 2) result.Y = (float)(from.Y + (to.Y - from.Y) * t);
    if (maxSize >= 3) result.Z = (float)(from.Z + (to.Z - from.Z) * t);
    if (maxSize >= 4) result.W = (float)(from.W + (to.W - from.W) * t);

    return result;
  }

  public readonly Godot.Color ToGodotColor(float alpha = 1f)
  {
    if (Type is AnyValueType.Vec4) return new(X, Y, Z, W);
    return new(X, Y, Z, alpha);
  }

  public readonly Godot.Vector2 ToGodotVector2() => new(X, Y);
  public readonly Godot.Vector3 ToGodotVector3() => new(X, Y, Z);

  public override readonly string ToString()
  {
    return Type switch
    {
      AnyValueType.Inherited => "-",
      AnyValueType.String when StringValue is not null && StringValue.Contains(' ') => $"\"{StringValue}\"",
      AnyValueType.String => StringValue ?? "",
      AnyValueType.Bool => ParserUtils.FormatIntBool(X),
      AnyValueType.Float => ParserUtils.FormatFloat(X),
      AnyValueType.Vec2 => $"{ParserUtils.FormatFloat(X)}|{ParserUtils.FormatFloat(Y)}",
      AnyValueType.Vec3 => $"{ParserUtils.FormatFloat(X)}|{ParserUtils.FormatFloat(Y)}|{ParserUtils.FormatFloat(Z)}",
      AnyValueType.Vec4 => $"{ParserUtils.FormatFloat(X)}|{ParserUtils.FormatFloat(Y)}|{ParserUtils.FormatFloat(Z)}|{ParserUtils.FormatFloat(W)}",
      _ => ""
    };
  }

  // ==========================================
  // IComparable / IEquatable
  // ==========================================

  /// <summary>
  /// Compares by squared magnitude for Vec types, by X for scalar types.
  /// String and Inherited are not orderable — throws InvalidOperationException.
  /// </summary>
  public readonly int CompareTo(AnyValue other)
  {
    AssertNumeric(this);
    AssertNumeric(other);
    return SquaredMagnitude(this).CompareTo(SquaredMagnitude(other));
  }

  public readonly int CompareTo(object obj)
  {
    if (obj is null) return 1;
    if (obj is AnyValue other) return CompareTo(other);
    throw new ArgumentException("Object must be of type AnyValue.", nameof(obj));
  }

  public readonly bool Equals(AnyValue other)
  {
    if (Type != other.Type) return false;

    return Type switch
    {
      AnyValueType.String => StringValue == other.StringValue,
      AnyValueType.Inherited => true,
      _ => X == other.X && Y == other.Y && Z == other.Z && W == other.W
    };
  }

  public override readonly bool Equals(object obj)
  {
    if (obj is AnyValue other) return Equals(other);
    return false;
  }

  public override readonly int GetHashCode()
  {
    switch (Type)
    {
      case AnyValueType.String:
        return (StringValue ?? "").GetHashCode();
      case AnyValueType.Inherited:
        return Type.GetHashCode();
      default:
        unchecked
        {
          int hash = Type.GetHashCode();
          hash = (hash * 397) ^ X.GetHashCode();
          hash = (hash * 397) ^ Y.GetHashCode();
          hash = (hash * 397) ^ Z.GetHashCode();
          hash = (hash * 397) ^ W.GetHashCode();
          return hash;
        }
    }
  }

  // ==========================================
  // Comparison Operators
  // ==========================================

  public static bool operator ==(AnyValue a, AnyValue b) => a.Equals(b);
  public static bool operator !=(AnyValue a, AnyValue b) => !a.Equals(b);
  public static bool operator <(AnyValue a, AnyValue b) => a.CompareTo(b) < 0;
  public static bool operator >(AnyValue a, AnyValue b) => a.CompareTo(b) > 0;
  public static bool operator <=(AnyValue a, AnyValue b) => a.CompareTo(b) <= 0;
  public static bool operator >=(AnyValue a, AnyValue b) => a.CompareTo(b) >= 0;

  // ==========================================
  // Arithmetic Operators
  // ==========================================

  /// <summary>Component-wise addition. Result type = widest of the two operands.</summary>
  public static AnyValue operator +(AnyValue a, AnyValue b)
  {
    AssertNumeric(a); AssertNumeric(b);
    AnyValue r = MakeResultShell(a, b);
    int n = ComponentCount(r.Type);
    if (n >= 1) r.X = a.X + b.X;
    if (n >= 2) r.Y = a.Y + b.Y;
    if (n >= 3) r.Z = a.Z + b.Z;
    if (n >= 4) r.W = a.W + b.W;
    return r;
  }

  /// <summary>Component-wise subtraction. Result type = widest of the two operands.</summary>
  public static AnyValue operator -(AnyValue a, AnyValue b)
  {
    AssertNumeric(a); AssertNumeric(b);
    AnyValue r = MakeResultShell(a, b);
    int n = ComponentCount(r.Type);
    if (n >= 1) r.X = a.X - b.X;
    if (n >= 2) r.Y = a.Y - b.Y;
    if (n >= 3) r.Z = a.Z - b.Z;
    if (n >= 4) r.W = a.W - b.W;
    return r;
  }

  public static AnyValue operator *(AnyValue a, AnyValue b)
  {
    AssertNumeric(a); AssertNumeric(b);

    if (ComponentCount(a.Type) == 1 && ComponentCount(b.Type) > 1)
    {
      AnyValue r = b;
      int n = ComponentCount(b.Type);
      if (n >= 1) r.X = b.X * a.X;
      if (n >= 2) r.Y = b.Y * a.X;
      if (n >= 3) r.Z = b.Z * a.X;
      if (n >= 4) r.W = b.W * a.X;
      return r;
    }
    if (ComponentCount(b.Type) == 1 && ComponentCount(a.Type) > 1)
    {
      AnyValue r = a;
      int n = ComponentCount(a.Type);
      if (n >= 1) r.X = a.X * b.X;
      if (n >= 2) r.Y = a.Y * b.X;
      if (n >= 3) r.Z = a.Z * b.X;
      if (n >= 4) r.W = a.W * b.X;
      return r;
    }

    AnyValue shell = MakeResultShell(a, b);
    int count = ComponentCount(shell.Type);
    if (count >= 1) shell.X = a.X * b.X;
    if (count >= 2) shell.Y = a.Y * b.Y;
    if (count >= 3) shell.Z = a.Z * b.Z;
    if (count >= 4) shell.W = a.W * b.W;
    return shell;
  }

  public static AnyValue operator /(AnyValue a, AnyValue b)
  {
    AssertNumeric(a); AssertNumeric(b);

    if (ComponentCount(b.Type) == 1)
    {
      if (b.X == 0f) throw new DivideByZeroException("[AnyValue] Cannot divide by zero scalar.");
      AnyValue r = a;
      int count = ComponentCount(a.Type);
      if (count >= 1) r.X = a.X / b.X;
      if (count >= 2) r.Y = a.Y / b.X;
      if (count >= 3) r.Z = a.Z / b.X;
      if (count >= 4) r.W = a.W / b.X;
      return r;
    }

    AnyValue shell = MakeResultShell(a, b);
    int n = ComponentCount(shell.Type);
    if (n >= 1) { if (b.X == 0f) throw new DivideByZeroException("[AnyValue] Division by zero in X component."); shell.X = a.X / b.X; }
    if (n >= 2) { if (b.Y == 0f) throw new DivideByZeroException("[AnyValue] Division by zero in Y component."); shell.Y = a.Y / b.Y; }
    if (n >= 3) { if (b.Z == 0f) throw new DivideByZeroException("[AnyValue] Division by zero in Z component."); shell.Z = a.Z / b.Z; }
    if (n >= 4) { if (b.W == 0f) throw new DivideByZeroException("[AnyValue] Division by zero in W component."); shell.W = a.W / b.W; }
    return shell;
  }

  /// <summary>Negation (unary minus). Flips all numeric components.</summary>
  public static AnyValue operator -(AnyValue a)
  {
    AssertNumeric(a);
    AnyValue r = a;
    r.X = -a.X; r.Y = -a.Y; r.Z = -a.Z; r.W = -a.W;
    return r;
  }

  // ==========================================
  // Private Helpers
  // ==========================================

  private static void AssertNumeric(AnyValue v)
  {
    if (v.Type is AnyValueType.String || v.Type is AnyValueType.Inherited)
      throw new InvalidOperationException(
        $"[AnyValue] Operator not supported for type {v.Type}.");
  }

  /// <summary>
  /// Creates a zeroed result shell whose Type matches the wider of the two operands
  /// (same logic used by Lerp).
  /// </summary>
  private static AnyValue MakeResultShell(AnyValue a, AnyValue b)
  {
    int maxSize = Math.Max(ComponentCount(a.Type), ComponentCount(b.Type));
    return new()
    {
      Type = maxSize switch
      {
        1 => AnyValueType.Float,
        2 => AnyValueType.Vec2,
        3 => AnyValueType.Vec3,
        4 => AnyValueType.Vec4,
        _ => AnyValueType.Float
      }
    };
  }



  /// <summary>
  /// Returns the squared magnitude (dot product with itself) for ordering purposes.
  /// For Bool/Float this is simply X².
  /// </summary>
  private static double SquaredMagnitude(AnyValue v)
  {
    double x = v.X, y = v.Y, z = v.Z, w = v.W;
    return x * x + y * y + z * z + w * w;
  }
}
