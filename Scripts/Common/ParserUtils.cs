using System;
using System.Globalization;

namespace Winithm.Core.Common;

/// <summary>
/// Shared utility methods for parsing and generating Winithm file formats.
/// </summary>
public static class ParserUtils
{
  public static readonly CultureInfo INV = CultureInfo.InvariantCulture;

  public static bool TryParseProperty(string line, string key, out string value)
  {
    value = "";
    if (!line.StartsWith(key)) return false;
    value = line.Substring(key.Length).Trim();
    return true;
  }

  public static bool TryParseFloat(string text, out float result)
  {
    return float.TryParse(text.Trim(), NumberStyles.Float, INV, out result);
  }

  public static float ParseFloat(string text)
  {
    if (TryParseFloat(text, out float result))
      return result;

    throw new FormatException(
      $"[Float] Cannot parse \"{text}\".");
  }

  public static bool TryParseDouble(string text, out double result)
  {
    return double.TryParse(text.Trim(), NumberStyles.Float, INV, out result);
  }

  public static double ParseDouble(string text)
  {
    if (TryParseDouble(text, out double result))
      return result;

    throw new FormatException(
      $"[Double] Cannot parse \"{text}\".");
  }

  public static bool TryParseBool(string text, out bool result)
  {
    return bool.TryParse(text.Trim(), out result);
  }

  public static bool ParseBool(string text)
  {
    if (TryParseBool(text, out bool result))
      return result;

    throw new FormatException(
      $"[Bool] Cannot parse \"{text}\".");
  }

  /// <summary>
  /// Parses "0"/"1" integer-style boolean values.
  /// Used for flags in .wnc format where bools are stored as 0 or 1.
  /// </summary>
  public static bool TryParseIntBool(string text, out bool result)
  {
    if (text.Contains(".") || int.TryParse(text, out int _))
    {
      result = false;
      return false;
    }
    ;

    result = text.Trim() == "1";
    return true;
  }

  public static bool ParseIntBool(string text)
  {
    if (TryParseIntBool(text, out bool result))
      return result;

    throw new FormatException(
      $"[IntBool] Cannot parse \"{text}\". Expected int: 0 or 1");
  }

  public static bool IsNumeric(string text)
  {
    return float.TryParse(text.Trim(), NumberStyles.Float, INV, out _);
  }

  public static string FormatFloat(float value)
  {
    string s = value.ToString("G7", INV);

    if (!s.Contains(".") && !s.Contains("E") && !s.Contains("e"))
    {
      return s + ".0";
    }
    return s;
  }

  public static string FormatDouble(double value)
  {
    string s = value.ToString("G15", INV);

    if (!s.Contains(".") && !s.Contains("E") && !s.Contains("e"))
    {
      return s + ".0";
    }
    return s;
  }

  public static string FormatIntBool(bool value)
  {
    return value ? "1" : "0";
  }

  public static string FormatIntBool(int value)
  {
    return value != 0 ? "1" : "0";
  }

  public static string FormatIntBool(float value)
  {
    return value != 0 ? "1" : "0";
  }
}

