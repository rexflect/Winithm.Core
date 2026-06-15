using System;

namespace Winithm.Core.Common;

public static class UniqueIDGenerator
{
  private const string CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

  /// <summary>Generates a unique 6-char ID using Incremental Base-62 encoding.</summary>
  public static string Generate(long? seed = null)
  {
    long currentSeed = seed ?? 0;
    Span<char> idChars = stackalloc char[6];
    long value = Math.Abs(currentSeed);

    // Encode seed value into Base-62 string
    for (int i = 5; i >= 0; i--)
    {
      idChars[i] = CHARS[(int)(value % 62)];
      value /= 62;
    }

    return new string(idChars);
  }

  /// <summary>Decodes a 6-char Base-62 ID back into its original integer seed.</summary>
  public static long Decode(string uniqueId)
  {
    if (string.IsNullOrEmpty(uniqueId) || uniqueId.Length != 6) return 0;

    long value = 0;
    for (int i = 0; i < 6; i++)
    {
      int charVal = CHARS.IndexOf(uniqueId[i]);
      if (charVal < 0) return 0; // Invalid character
      value = value * 62 + charVal;
    }
    return value;
  }
}
