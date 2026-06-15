using System;
namespace Winithm.Core.Data;

/// <summary>
/// Merged metadata from chart and song definition files.
/// </summary>
public class ChartMetadata
{
  public Constants.Version.BuildVersion VERSION = Constants.Version.SONG_CHART_FORMAT_VERSION;

  public event Action<ChartMetadata>? OnUpdated;

  public int Index { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;

  public string ChartID { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "test";

  public string ChartName { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Unamed";

  public string Charter { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Noname";

  public string Level { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "1";

  public float Constant { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
}
