using System.Collections.Generic;
using Winithm.Core.Data;

namespace Winithm.Core.Constants;

public static class HitResult
{
  public static readonly Dictionary<HitResultType, double> TimmingWindowMs = new()
    {
      { HitResultType.Perfect, 55 },
      { HitResultType.Good, 115 },
      { HitResultType.Bad, 165 },
      // Miss is not a timing window, but a flag for notes that were not hit.
      { HitResultType.Miss, 165 },
    };

  public static readonly Dictionary<HitResultType, float> ResultWeight = new()
    {
      { HitResultType.Perfect, 1f },
      { HitResultType.Good, 0.65f },
      { HitResultType.Bad, 0.1f },
      { HitResultType.Miss, 0f },
    };

  public static readonly Dictionary<HitResultType, string> HitResultNames = new()
    {
      { HitResultType.Perfect, "Sharp" },
      { HitResultType.Good, "Clear" },
      { HitResultType.Bad, "Blur" },
      { HitResultType.Miss, "Lost" },
    };
}
