using Godot;
using System;
using System.Collections.Generic;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Root metadata structure for a song.
/// </summary>
public class SongMetaData
{
  public Constants.Version.BuildVersion VERSION = Constants.Version.SONG_META_FORMAT_VERSION;

  public event Action<SongMetaData>? OnMetronomeUpdated;
  public event Action<SongMetaData>? OnUpdated;

  public string ID { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "prototype.test";

  public string Name { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Unnamed";

  public string NameAlt { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "";

  public string Artist { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Noname";

  public string ArtistAlt { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "";

  public string Tags { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Genreless";

  public AudioResource Audio { get; } = new();
  public IllustrationResource Illustration { get; } = new();
  public List<ChartReference> Charts { get; } = [];

  public SongMetaData()
  {
    Audio.OnMetronomeUpdated += (a) => OnMetronomeUpdated?.Invoke(this);
    Audio.OnUpdated += (a) => OnUpdated?.Invoke(this);
    Illustration.OnUpdated += (i) => OnUpdated?.Invoke(this);
  }

  public void CopyFrom(SongMetaData? other)
  {
    if (other is null) return;

    ID = other.ID;
    Name = other.Name;
    NameAlt = other.NameAlt;
    Artist = other.Artist;
    ArtistAlt = other.ArtistAlt;
    Tags = other.Tags;

    Audio.SongPath = other.Audio.SongPath;
    Audio.PreviewStart = other.Audio.PreviewStart;
    Audio.PreviewEnd = other.Audio.PreviewEnd;
    Audio.Metronome = other.Audio.Metronome;

    Illustration.Illustrator = other.Illustration.Illustrator;
    Illustration.IllustrationPath = other.Illustration.IllustrationPath;
    Illustration.IconCenter = other.Illustration.IconCenter;
    Illustration.IconSize = other.Illustration.IconSize;

    Charts.Clear();
    foreach (var chart in other.Charts)
    {
      Charts.Add(new ChartReference()
      {
        ID = chart.ID,
        Index = chart.Index,
        Name = chart.Name,
        Charter = chart.Charter,
        Level = chart.Level,
        Constant = chart.Constant
      });
    }

    OnUpdated?.Invoke(this);
    OnMetronomeUpdated?.Invoke(this);
  }
}

public class AudioResource
{
  public event Action<AudioResource>? OnMetronomeUpdated;
  public event Action<AudioResource>? OnUpdated;

  public string SongPath = "song.mp3";
  public AudioStream? SongStream;

  public double PreviewStart { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;
  public double PreviewEnd { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 15;

  public Metronome Metronome = new();

  public AudioResource()
  {
    Metronome.OnUpdated += (m) => OnMetronomeUpdated?.Invoke(this);
  }
}

public class IllustrationResource
{
  public event Action<IllustrationResource>? OnUpdated;

  public string IllustrationPath = "illustration.png";
  public Texture2D IllustrationTexture = GD.Load<Texture2D>("res://Winithm.Core/Resources/Textures/song_placeholder_image.png");

  public string Illustrator { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = "Noname";
  public Vector2 IconCenter { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = new(0.5f, 0.5f);
  public float IconSize { get => field; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
}

public class ChartReference
{
  public string ID = "test";
  public int Index = 0;
  public string Name = "Unamed";
  public string Charter = "Noname";
  public string Level = "1";
  public float Constant = 1f;
}
