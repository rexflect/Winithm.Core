using System;

namespace Winithm.Core.Data;

/// <summary>
/// Merged metadata from chart and song definition files.
/// </summary>
public class ChartMetadata
{
  public float VERSION = 1.3f;

  public event Action<ChartMetadata> OnUpdated;
  private int _index = 0;
  public int Index { get => _index; set { if (_index == value) return; _index = value; OnUpdated?.Invoke(this); } }

  private string _chartId = "test";
  public string ChartID { get => _chartId; set { if (_chartId == value) return; _chartId = value; OnUpdated?.Invoke(this); } }

  private string _chartName = "Unamed";
  public string ChartName { get => _chartName; set { if (_chartName == value) return; _chartName = value; OnUpdated?.Invoke(this); } }

  private string _charter = "Noname";
  public string Charter { get => _charter; set { if (_charter == value) return; _charter = value; OnUpdated?.Invoke(this); } }

  private string _level = "1";
  public string Level { get => _level; set { if (_level == value) return; _level = value; OnUpdated?.Invoke(this); } }

  private float _constant = 1f;
  public float Constant { get => _constant; set { if (_constant == value) return; _constant = value; OnUpdated?.Invoke(this); } }
}
