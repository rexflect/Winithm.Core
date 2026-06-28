using System.Collections.Generic;
using Winithm.Core.Data;

namespace Winithm.Core.Constants;

public static class Scoring
{
  public enum Grade
  {
    AP,
    FC,
    S,
    A,
    B,
    C,
    D,
    F
  }

  public static readonly Dictionary<Grade, int> GradeMinimumScore = new()
    {
      { Grade.AP, 1_000_000 },
      { Grade.FC, 1_000_000 },
      { Grade.S, 950_000 },
      { Grade.A, 875_000 },
      { Grade.B, 800_000 },
      { Grade.C, 700_000 },
      { Grade.D, 500_000 },
      { Grade.F, 0 }
    };

  public static Grade GetGrade(int score)
  {
    if (score >= GradeMinimumScore[Grade.AP]) return Grade.AP;
    if (score >= GradeMinimumScore[Grade.FC]) return Grade.FC;
    if (score >= GradeMinimumScore[Grade.S]) return Grade.S;
    if (score >= GradeMinimumScore[Grade.A]) return Grade.A;
    if (score >= GradeMinimumScore[Grade.B]) return Grade.B;
    if (score >= GradeMinimumScore[Grade.C]) return Grade.C;
    if (score >= 500_000) return Grade.D;
    return Grade.F;
  }

  public static readonly Dictionary<Grade, string> GradeNames = new()
    {
      { Grade.AP, "FX" },
      { Grade.FC, "X" },
      { Grade.S, "S" },
      { Grade.A, "A" },
      { Grade.B, "B" },
      { Grade.C, "C" },
      { Grade.D, "D" },
      { Grade.F, "F" }
    };
}