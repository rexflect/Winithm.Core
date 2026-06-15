using Godot;
using Winithm.Core.Data;

namespace Winithm.Core.Common;

/// <summary>
/// Facade for level I/O operations.
/// Handles directory traversal and file loading using Godot's virtual filesystem (res://, user://).
/// </summary>
public static class WinithmIO
{
  public static readonly string CHART_METADATA_FILE = "metadata.wnm";
  public static readonly string CHART_DATA_FILE = ".wnc";

  /// <summary>Loads metadata (.wnm) and a specific chart (.wnc) from a level folder.</summary>
  public static ChartData LoadLevel(string levelDir, string songID, string chartID)
  {
    string metaDataFilePath = levelDir.PathJoin(songID).PathJoin(CHART_METADATA_FILE);

    if (!FileAccess.FileExists(metaDataFilePath))
    {
      GD.PushError($"[WinithmIO] Metadata file missing: {metaDataFilePath}");
      return new ChartData();
    }

    // Load shared chart metadata
    var songMetaData = WNMParser.Parse(metaDataFilePath);

    string chartFilePath = levelDir.PathJoin(songID).PathJoin(chartID + CHART_DATA_FILE);

    if (!FileAccess.FileExists(chartFilePath))
    {
      GD.PushError($"[WinithmIO] Chart data file missing: {chartFilePath}");
      return new ChartData();
    }

    // Load chart data
    var data = new ChartData() { SongMetaData = songMetaData };
    WNCParser.Parse(chartFilePath, data);
    data.Windows.ComputeAllAnimations();
    return data;
  }

  public static ChartData LoadLevel(string levelDir, SongMetaData songMetaData, string chartID)
  {
    string chartFilePath = levelDir.PathJoin(songMetaData.ID).PathJoin(chartID + CHART_DATA_FILE);

    if (!FileAccess.FileExists(chartFilePath))
    {
      GD.PushError($"[WinithmIO] Chart data file missing: {chartFilePath}");
      return new ChartData();
    }

    var data = new ChartData() { SongMetaData = songMetaData };
    WNCParser.Parse(chartFilePath, data);
    data.Windows.ComputeAllAnimations();
    return data;
  }

  /// <summary>Saves metadata and chart files to disk, ensuring directory existence.</summary>
  public static void SaveLevel(string levelsDir, ChartData data)
  {
    string songDir = levelsDir.PathJoin(data.SongMetaData.ID);

    if (!DirAccess.DirExistsAbsolute(songDir))
    {
      DirAccess.MakeDirRecursiveAbsolute(songDir);
    }

    WNMGenerator.Generate(
        songDir.PathJoin(CHART_METADATA_FILE),
        data.SongMetaData
    );

    WNCGenerator.Generate(
        songDir.PathJoin(data.ChartMetadata.ChartID + CHART_DATA_FILE),
        data
    );
  }
}
