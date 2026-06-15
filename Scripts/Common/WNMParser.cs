using Godot;
using System;
using System.Collections.Generic;
using Winithm.Core.Data;

namespace Winithm.Core.Common;

/// <summary>
/// Parser for .wnm (Metadata) files.
/// Reads [FORMAT], [METADATA], [RESOURCES], and [CHARTS] sections.
/// </summary>
public static class WNMParser
{
  /// <summary>Parse a .wnm metadata file.</summary>
  public static SongMetaData Parse(string filePath)
  {
    // Use Godot-safe path extraction to preserve res:// prefix (System.IO.Path breaks it on Windows)
    int lastSlash = filePath.LastIndexOf('/');
    var songFolder = lastSlash >= 0 ? filePath[..lastSlash] : "";

    var data = new SongMetaData();
    using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

    if (file is null)
    {
      System.Diagnostics.Trace.TraceError($"[WNMParser] Failed to open file: {filePath}");
      return data;
    }

    string currentSection = "";
    string currentResource = "";
    ChartReference? currentChart = null;

    data.Audio.Metronome.BeginUpdate();

    try
    {
      string line;
      while (!file.EofReached())
      {
        line = file.GetLine().TrimEnd();
        if (string.IsNullOrWhiteSpace(line)) continue;

        if (line.StartsWith("[") && line.EndsWith("]"))
        {
          currentSection = line.Substring(1, line.Length - 2);
          continue;
        }

        switch (currentSection)
        {
          case "FORMAT":
            ParseFormatLine(line, data);
            break;
          case "METADATA":
            ParseMetadataLine(line, data);
            break;
          case "RESOURCES":
            ParseResourceLine(line, data, ref currentResource, songFolder);
            break;
          case "CHARTS":
            currentChart = ParseChartReferenceLine(line, data.Charts, currentChart);
            break;
        }
      }
    }
    finally
    {
      file.Close();
    }

    data.Audio.Metronome.EndUpdate();
    return data;
  }

  private static void ParseFormatLine(string line, SongMetaData meta)
  {
    string trimmed = line.TrimStart();
    if (ParserUtils.TryParseProperty(trimmed, "Version:", out string version))
      meta.VERSION = ParserUtils.TryParseFloat(version, out float v)
        ? v : WNMGenerator.METADATA_FORMAT_VERSION;
  }

  private static void ParseMetadataLine(string line, SongMetaData meta)
  {
    string trimmed = line.TrimStart();
    if (ParserUtils.TryParseProperty(trimmed, "ID:", out string id)) meta.ID = id;
    if (ParserUtils.TryParseProperty(trimmed, "Name:", out string name)) meta.Name = name;
    if (ParserUtils.TryParseProperty(trimmed, "Name Alt:", out string nameAlt)) meta.NameAlt = nameAlt;
    if (ParserUtils.TryParseProperty(trimmed, "Artist:", out string artist)) meta.Artist = artist;
    if (ParserUtils.TryParseProperty(trimmed, "Artist Alt:", out string artistAlt)) meta.ArtistAlt = artistAlt;
    if (ParserUtils.TryParseProperty(trimmed, "Tags:", out string tags)) meta.Tags = tags;
  }

  private static void ParseResourceLine(
    string line, SongMetaData meta, ref string currentResource, string songFolder
  )
  {
    string trimmed = line.TrimStart();

    if (trimmed.StartsWith("* "))
    {
      currentResource = trimmed[2..].Trim();
      return;
    }

    if (trimmed.StartsWith("+ ") && currentResource == "Song")
    {
      var parts = trimmed[2..].Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries
      );

      var bPMStop = new BPMStop();

      if (parts.Length >= 1)
        bPMStop.StartBeat = BeatTime.TryParse(parts[0], out var sb) ? sb : BeatTime.Zero;
      if (parts.Length >= 2)
        bPMStop.BPM = ParserUtils.TryParseFloat(parts[1], out float b) ? b : 120f;
      if (parts.Length >= 3)
        bPMStop.TimeSignatureNum = int.TryParse(parts[2], out int tsn) ? tsn : 4;
      if (parts.Length >= 4)
        bPMStop.TimeSignatureDen = int.TryParse(parts[3], out int tsd) ? tsd : 4;

      meta.Audio.Metronome.AddBPMStop(bPMStop);
      return;
    }

    switch (currentResource)
    {
      case "Song":
        if (ParserUtils.TryParseProperty(trimmed, "Path:", out string songPath))
        {
          meta.Audio.SongPath = songPath;
          var audioStream = GD.Load<AudioStream>(songFolder.PathJoin(songPath));
          AudioStreamUtils.ClampStreamLoop(audioStream);
          meta.Audio.SongStream = audioStream;
        }
        if (ParserUtils.TryParseProperty(trimmed, "Preview Range:", out string previewRange))
        {
          var parts = previewRange.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
          );
          if (parts.Length >= 2)
          {
            meta.Audio.PreviewStart = ParserUtils.TryParseFloat(parts[0], out float start) ? start : 0f;
            meta.Audio.PreviewEnd = ParserUtils.TryParseFloat(parts[1], out float end) ? end : 0f;
          }
        }
        if (ParserUtils.TryParseProperty(trimmed, "Base BPM:", out string bpmBase))
        {
          var parts = bpmBase.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
          );

          var baseBPM = new BaseBPM();
          if (parts.Length >= 1)
            baseBPM.BaseOffsetSeconds = ParserUtils.TryParseFloat(parts[0], out float s) ? s : 0f;
          if (parts.Length >= 2)
            baseBPM.InitialBPM = ParserUtils.TryParseFloat(parts[1], out float b) ? b : 120f;
          if (parts.Length >= 3)
            baseBPM.TimeSignatureNum = int.TryParse(parts[2], out int tsn) ? tsn : 4;
          if (parts.Length >= 4)
            baseBPM.TimeSignatureDen = int.TryParse(parts[3], out int tsd) ? tsd : 4;

          meta.Audio.Metronome.SetBaseBPM(baseBPM);
          return;
        }
        break;
      case "Illustration":
        if (ParserUtils.TryParseProperty(trimmed, "Illustrator:", out string illustrator))
          meta.Illustration.Illustrator = illustrator;
        else if (ParserUtils.TryParseProperty(trimmed, "Path:", out string illPath))
        {
          meta.Illustration.IllustrationPath = illPath;
          meta.Illustration.IllustrationTexture =
            GD.Load<Texture2D>(songFolder.PathJoin(illPath));
        }
        else if (ParserUtils.TryParseProperty(trimmed, "Icon Center:", out string center))
        {
          var parts = center.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
          );
          if (parts.Length >= 2)
          {
            float IconCenterX = ParserUtils.TryParseFloat(parts[0], out float x) ? x : 0.5f;
            float IconCenterY = ParserUtils.TryParseFloat(parts[1], out float y) ? y : 0.5f;

            meta.Illustration.IconCenter = new Vector2(IconCenterX, IconCenterY);
          }
        }
        else if (ParserUtils.TryParseProperty(trimmed, "Icon Size:", out string size))
          meta.Illustration.IconSize = ParserUtils.TryParseFloat(size, out float s) ? s : 1f;
        break;
    }
  }

  private static ChartReference? ParseChartReferenceLine(
      string line, List<ChartReference> charts, ChartReference? current)
  {
    string trimmed = line.TrimStart();

    if (trimmed.StartsWith("+ "))
    {
      current = new ChartReference();
      charts.Add(current);
      if (ParserUtils.TryParseProperty(trimmed[2..], "ID:", out string id))
        current.ID = id;
      return current;
    }

    if (current is null) return current;

    if (ParserUtils.TryParseProperty(trimmed, "Index:", out string index))
    { int.TryParse(index, out int idx); current.Index = idx; }
    else if (ParserUtils.TryParseProperty(trimmed, "Name:", out string name))
      current.Name = name;
    else if (ParserUtils.TryParseProperty(trimmed, "Charter:", out string charter))
      current.Charter = charter;
    else if (ParserUtils.TryParseProperty(trimmed, "Level:", out string level))
      current.Level = level;
    else if (ParserUtils.TryParseProperty(trimmed, "Constant:", out string constant))
      current.Constant = ParserUtils.TryParseFloat(constant, out float c) ? c : 1f;

    return current;
  }
}