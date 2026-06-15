using System;
using System.Diagnostics;

namespace Winithm.Core.Common;

public enum EasingType
{
  Linear,
  SineIn,
  SineOut,
  SineInOut,
  CubicIn,
  CubicOut,
  CubicInOut,
  QuadIn,
  QuadOut,
  QuadInOut,
  ExpoIn,
  ExpoOut,
  ExpoInOut,
  CircIn,
  CircOut,
  CircInOut,
  BackIn,
  BackOut,
  BackInOut,
  ElasticIn,
  ElasticOut,
  ElasticInOut,
  BounceIn,
  BounceOut,
  BounceInOut,
  Bezier
}

public static class EasingFunctions
{
  private const double PI = Math.PI;
  private const double HALF_PI = PI / 2f;

  public static double Evaluate(EasingType type, double t)
  {
    t = Math.Clamp(t, 0f, 1f);

    return type switch
    {
      EasingType.Linear => t,
      EasingType.SineIn => SineIn(t),
      EasingType.SineOut => SineOut(t),
      EasingType.SineInOut => SineInOut(t),
      EasingType.CubicIn => CubicIn(t),
      EasingType.CubicOut => CubicOut(t),
      EasingType.CubicInOut => CubicInOut(t),
      EasingType.QuadIn => QuadIn(t),
      EasingType.QuadOut => QuadOut(t),
      EasingType.QuadInOut => QuadInOut(t),
      EasingType.ExpoIn => ExpoIn(t),
      EasingType.ExpoOut => ExpoOut(t),
      EasingType.ExpoInOut => ExpoInOut(t),
      EasingType.CircIn => CircIn(t),
      EasingType.CircOut => CircOut(t),
      EasingType.CircInOut => CircInOut(t),
      EasingType.BackIn => BackIn(t),
      EasingType.BackOut => BackOut(t),
      EasingType.BackInOut => BackInOut(t),
      EasingType.ElasticIn => ElasticIn(t),
      EasingType.ElasticOut => ElasticOut(t),
      EasingType.ElasticInOut => ElasticInOut(t),
      EasingType.BounceIn => BounceIn(t),
      EasingType.BounceOut => BounceOut(t),
      EasingType.BounceInOut => BounceInOut(t),
      _ => t,
    };
  }

  private static double SineIn(double t) => 1f - Math.Cos(t * HALF_PI);
  private static double SineOut(double t) => Math.Sin(t * HALF_PI);
  private static double SineInOut(double t) => -(Math.Cos(PI * t) - 1f) / 2f;
  private static double CubicIn(double t) => t * t * t;
  private static double CubicOut(double t) { double u = 1f - t; return 1f - u * u * u; }
  private static double CubicInOut(double t) => t < 0.5f ? 4f * t * t * t : 1f - Math.Pow(-2f * t + 2f, 3) / 2f;
  private static double QuadIn(double t) => t * t;
  private static double QuadOut(double t) => t * (2f - t);
  private static double QuadInOut(double t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

  private static double ExpoIn(double t) => t == 0f ? 0f : Math.Pow(2f, 10f * (t - 1f));
  private static double ExpoOut(double t) => t == 1f ? 1f : 1f - Math.Pow(2f, -10f * t);
  private static double ExpoInOut(double t)
  {
    if (t == 0f) return 0f;
    if (t == 1f) return 1f;
    return t < 0.5f
        ? Math.Pow(2f, 20f * t - 10f) / 2f
        : (2f - Math.Pow(2f, -20f * t + 10f)) / 2f;
  }

  private static double CircIn(double t) => 1f - Math.Sqrt(1f - t * t);
  private static double CircOut(double t) { double u = t - 1f; return Math.Sqrt(1f - u * u); }
  private static double CircInOut(double t) => t < 0.5f
          ? (1f - Math.Sqrt(1f - 4f * t * t)) / 2f
          : (Math.Sqrt(1f - Math.Pow(-2f * t + 2f, 2)) + 1f) / 2f;

  private static double BackIn(double t) { const double c1 = 1.70158f; return (c1 + 1f) * t * t * t - c1 * t * t; }
  private static double BackOut(double t) { const double c1 = 1.70158f; double u = t - 1f; return 1f + (c1 + 1f) * u * u * u + c1 * u * u; }
  private static double BackInOut(double t)
  {
    const double c2 = 1.70158f * 1.525f;
    return t < 0.5f
        ? Math.Pow(2f * t, 2) * ((c2 + 1f) * 2f * t - c2) / 2f
        : (Math.Pow(2f * t - 2f, 2) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
  }

  private static double ElasticIn(double t)
  {
    if (t == 0f) return 0f;
    if (t == 1f) return 1f;
    return -Math.Pow(2f, 10f * t - 10f) * Math.Sin((t * 10f - 10.75f) * (2f * PI / 3f));
  }

  private static double ElasticOut(double t)
  {
    if (t == 0f) return 0f;
    if (t == 1f) return 1f;
    return Math.Pow(2f, -10f * t) * Math.Sin((t * 10f - 0.75f) * (2f * PI / 3f)) + 1f;
  }

  private static double ElasticInOut(double t)
  {
    if (t == 0f) return 0f;
    if (t == 1f) return 1f;
    const double c5 = 2f * PI / 4.5f;
    return t < 0.5f
      ? -(Math.Pow(2f, 20f * t - 10f) * Math.Sin((20f * t - 11.125f) * c5)) / 2f
      : Math.Pow(2f, -20f * t + 10f) * Math.Sin((20f * t - 11.125f) * c5) / 2f + 1f;
  }

  private static double BounceIn(double t) => 1f - BounceOut(1f - t);

  private static double BounceOut(double t)
  {
    const double n1 = 7.5625f;
    const double d1 = 2.75f;

    if (t < 1f / d1) return n1 * t * t;
    if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
    if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
    t -= 2.625f / d1;
    return n1 * t * t + 0.984375f;
  }

  private static double BounceInOut(double t)
  {
    return t < 0.5f
      ? (1f - BounceOut(1f - 2f * t)) / 2f
      : (1f + BounceOut(2f * t - 1f)) / 2f;
  }

  public static double EvaluateBezier(AnyValue bezier, double t)
  {
    if (t <= 0f) return 0f;
    if (t >= 1f) return 1f;

    double p1x = Math.Max(0f, Math.Min(1f, bezier.X));
    double p1y = bezier.Y;
    double p2x = Math.Max(0f, Math.Min(1f, bezier.Z));
    double p2y = bezier.W;

    // Binary search to find u parameter where X(u) is close to target t
    double u = t;
    double minU = 0f;
    double maxU = 1f;

    for (int i = 0; i < 12; i++)
    {
      double ou = 1f - u;
      double x = 3f * ou * ou * u * p1x + 3f * ou * u * u * p2x + u * u * u;

      if (Math.Abs(x - t) < 0.0005f) break;

      if (x < t) minU = u;
      else maxU = u;

      u = (minU + maxU) / 2f;
    }

    double finalOu = 1f - u;
    return 3f * finalOu * finalOu * u * p1y + 3f * finalOu * u * u * p2y + u * u * u;
  }

  public static EasingType ParseEasing(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return EasingType.Linear;

    return text.Trim().ToLowerInvariant() switch
    {
      "linear" => EasingType.Linear,
      "sinein" => EasingType.SineIn,
      "sineout" => EasingType.SineOut,
      "sineinout" => EasingType.SineInOut,

      "cubicin" => EasingType.CubicIn,
      "cubicout" => EasingType.CubicOut,
      "cubicinout" => EasingType.CubicInOut,

      "quadin" => EasingType.QuadIn,
      "quadout" => EasingType.QuadOut,
      "quadinout" => EasingType.QuadInOut,

      "expoin" => EasingType.ExpoIn,
      "expoout" => EasingType.ExpoOut,
      "expoinout" => EasingType.ExpoInOut,

      "circin" => EasingType.CircIn,
      "circout" => EasingType.CircOut,
      "circinout" => EasingType.CircInOut,

      "backin" => EasingType.BackIn,
      "backout" => EasingType.BackOut,
      "backinout" => EasingType.BackInOut,

      "elasticin" => EasingType.ElasticIn,
      "elasticout" => EasingType.ElasticOut,
      "elasticinout" => EasingType.ElasticInOut,

      "bouncein" => EasingType.BounceIn,
      "bounceout" => EasingType.BounceOut,
      "bounceinout" => EasingType.BounceInOut,

      // Alias support
      "easein" => EasingType.CubicIn,
      "easeout" => EasingType.CubicOut,
      "easeinout" => EasingType.CubicInOut,

      _ => LogUnknownAndReturnLinear(text),
    };
  }

  private static EasingType LogUnknownAndReturnLinear(string text)
  {
    Trace.TraceWarning($"Unknown easing type: '{text}'");
    return EasingType.Linear;
  }

  public static string EasingTypeToString(EasingType easingType)
  {
    return easingType switch
    {
      EasingType.Linear => "Linear",
      EasingType.SineIn => "SineIn",
      EasingType.SineOut => "SineOut",
      EasingType.SineInOut => "SineInOut",

      EasingType.CubicIn => "CubicIn",
      EasingType.CubicOut => "CubicOut",
      EasingType.CubicInOut => "CubicInOut",

      EasingType.QuadIn => "QuadIn",
      EasingType.QuadOut => "QuadOut",
      EasingType.QuadInOut => "QuadInOut",

      EasingType.ExpoIn => "ExpoIn",
      EasingType.ExpoOut => "ExpoOut",
      EasingType.ExpoInOut => "ExpoInOut",

      EasingType.CircIn => "CircIn",
      EasingType.CircOut => "CircOut",
      EasingType.CircInOut => "CircInOut",

      EasingType.BackIn => "BackIn",
      EasingType.BackOut => "BackOut",
      EasingType.BackInOut => "BackInOut",

      EasingType.ElasticIn => "ElasticIn",
      EasingType.ElasticOut => "ElasticOut",
      EasingType.ElasticInOut => "ElasticInOut",

      EasingType.BounceIn => "BounceIn",
      EasingType.BounceOut => "BounceOut",
      EasingType.BounceInOut => "BounceInOut",
      
      _ => throw new ArgumentOutOfRangeException(nameof(easingType), easingType, null),
    };
  }
}

