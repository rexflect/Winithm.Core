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
  public float VERSION = 1f;

  public event Action<SongMetaData> OnMetronomeUpdated;
  public event Action<SongMetaData> OnUpdated;

  public string _id = "prototype.test";
  public string ID { get => _id; set { if (_id == value) return; _id = value; OnUpdated?.Invoke(this); } }

  private string _name = "Unnamed";
  public string Name { get => _name; set { if (_name == value) return; _name = value; OnUpdated?.Invoke(this); } }

  private string _nameAlt = "";
  public string NameAlt { get => _nameAlt; set { if (_nameAlt == value) return; _nameAlt = value; OnUpdated?.Invoke(this); } }

  private string _artist = "Noname";
  public string Artist { get => _artist; set { if (_artist == value) return; _artist = value; OnUpdated?.Invoke(this); } }

  private string _artistAlt = "";
  public string ArtistAlt { get => _artistAlt; set { if (_artistAlt == value) return; _artistAlt = value; OnUpdated?.Invoke(this); } }

  private string _tags = "Genreless";
  public string Tags { get => _tags; set { if (_tags == value) return; _tags = value; OnUpdated?.Invoke(this); } }

  public AudioResource Audio { get; } = new();
  public IllustrationResource Illustration { get; } = new();
  public List<ChartReference> Charts { get; } = [];

  public SongMetaData()
  {
    Audio.OnMetronomeUpdated += (a) => OnMetronomeUpdated?.Invoke(this);
    Audio.OnUpdated += (a) => OnUpdated?.Invoke(this);
    Illustration.OnUpdated += (i) => OnUpdated?.Invoke(this);
  }

  public void CopyFrom(SongMetaData other)
  {
    if (other == null) return;

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
    foreach (ChartReference chart in other.Charts)
    {
      Charts.Add(new()
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
  public event Action<AudioResource> OnMetronomeUpdated;
  public event Action<AudioResource> OnUpdated;

  public string SongPath = "song.mp3";
  public AudioStream SongStream;

  private double _previewStart = 0;
  public double PreviewStart { get => _previewStart; set { if (_previewStart == value) return; _previewStart = value; OnUpdated?.Invoke(this); } }
  private double _previewEnd = 15;
  public double PreviewEnd { get => _previewEnd; set { if (_previewEnd == value) return; _previewEnd = value; OnUpdated?.Invoke(this); } }

  public Metronome Metronome = new();

  public AudioResource()
  {
    Metronome.OnUpdated += (m) => OnMetronomeUpdated?.Invoke(this);
  }
}

public class IllustrationResource
{
  public event Action<IllustrationResource> OnUpdated;

  public string IllustrationPath = "illustration.png";
  public Texture2D IllustrationTexture;

  private string _illustrator = "Noname";
  public string Illustrator { get => _illustrator; set { if (_illustrator == value) return; _illustrator = value; OnUpdated?.Invoke(this); } }
  private Vector2 _iconCenter = new(0.5f, 0.5f);
  public Vector2 IconCenter { get => _iconCenter; set { if (_iconCenter == value) return; _iconCenter = value; OnUpdated?.Invoke(this); } }
  private float _iconSize = 1f;
  public float IconSize { get => _iconSize; set { if (_iconSize == value) return; _iconSize = value; OnUpdated?.Invoke(this); } }
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

