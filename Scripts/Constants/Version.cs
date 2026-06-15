namespace Winithm.Core.Constants;

public static class Version
{
  public record struct BuildVersion
  {
    public int Major, Minor;
    public int? Revision;
  }

  public static readonly BuildVersion CLIENT_VERSION = new() { Major = 0, Minor = 1, Revision = 0 };
  public static readonly BuildVersion EDITOR_VERSION = new() { Major = 0, Minor = 1, Revision = 0 };

  public static readonly BuildVersion SONG_CHART_FORMAT_VERSION = new() { Major = 1, Minor = 3 };
  public static readonly BuildVersion SONG_META_FORMAT_VERSION = new() { Major = 1, Minor = 0 };
}