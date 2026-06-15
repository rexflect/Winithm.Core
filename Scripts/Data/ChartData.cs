using System;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Root container aggregating all parsed chart and metadata.
/// </summary>
public class ChartData
{
  public event Action<ChartData>? OnMetaDataUpdated;
  public event Action<ChartData>? OnChartUpdated;

  // Metadata
  public SongMetaData SongMetaData = new();
  public ChartMetadata ChartMetadata = new();

  // Contents
  public OverlayManager Overlays = new();
  public ComponentManager Components = new();
  public ThemeChannelManager ThemeChannels = new();
  public GroupManager Groups = new();
  public WindowManager Windows = new();

  public ObjectFactory ObjectFactory = new();

  public ChartData()
  {
    Windows.SetMetronome(SongMetaData.Audio.Metronome);

    SongMetaData.OnUpdated += (sm) => OnMetaDataUpdated?.Invoke(this);
    ChartMetadata.OnUpdated += (cm) => OnMetaDataUpdated?.Invoke(this);

    SongMetaData.OnMetronomeUpdated += (sm) => OnChartUpdated?.Invoke(this);
    Overlays.OnUpdated += (om) => OnChartUpdated?.Invoke(this);
    Components.OnUpdated += (cmp) => OnChartUpdated?.Invoke(this);
    ThemeChannels.OnUpdated += (tc) => OnChartUpdated?.Invoke(this);
    Groups.OnUpdated += (g) => OnChartUpdated?.Invoke(this);
    Windows.OnUpdated += (wm) => OnChartUpdated?.Invoke(this);
  }
}
