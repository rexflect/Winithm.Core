using Godot;
using System;
using System.Text;
using Winithm.Core.Data;

namespace Winithm.Core.Common;

/// <summary>
/// Generator for .wnm (Metadata) files.
/// Writes ChartData back into the Winithm .wnm plaintext format.
/// </summary>
public static class WNMGenerator
{
  public static readonly float METADATA_FORMAT_VERSION = 1f;

  public static void Generate(string filePath, SongMetaData data)
  {
    var sb = new StringBuilder();

    // [FORMAT]
    sb.AppendLine("[FORMAT]");
    sb.AppendLine("Type: Metadata");
    sb.AppendLine($"Version: {data.VERSION}");
    sb.AppendLine();

    // [METADATA]
    sb.AppendLine("[METADATA]");
    sb.AppendLine($"ID: {data.ID}");
    sb.AppendLine($"Name: {data.Name}");
    sb.AppendLine($"Name Alt: {data.NameAlt}");
    sb.AppendLine($"Artist: {data.Artist}");
    sb.AppendLine($"Artist Alt: {data.ArtistAlt}");
    sb.AppendLine($"Tags: {data.Tags}");
    sb.AppendLine();

    // [RESOURCES]
    sb.AppendLine("[RESOURCES]");
    sb.AppendLine("* Song");
    sb.AppendLine($"  Path: {data.Audio.SongPath}");
    sb.AppendLine($"  Preview Range: {ParserUtils.FormatDouble(data.Audio.PreviewStart)} {ParserUtils.FormatDouble(data.Audio.PreviewEnd)}");
    sb.AppendLine($"  Base BPM: {ParserUtils.FormatFloat((float)data.Audio.Metronome.BaseBPM.BaseOffsetSeconds)} {ParserUtils.FormatFloat(data.Audio.Metronome.BaseBPM.InitialBPM)} {data.Audio.Metronome.BaseBPM.TimeSignatureNum} {data.Audio.Metronome.BaseBPM.TimeSignatureDen}");
    sb.AppendLine("  BPM List:");
    foreach (var bpm in data.Audio.Metronome)
      sb.AppendLine($"  + {bpm.StartBeat} {ParserUtils.FormatFloat(bpm.BPM)} {bpm.TimeSignatureNum} {bpm.TimeSignatureDen}");

    sb.AppendLine("* Illustration");
    sb.AppendLine($"  Illustrator: {data.Illustration.Illustrator}");
    sb.AppendLine($"  Path: {data.Illustration.IllustrationPath}");
    sb.AppendLine($"  Icon Center: {ParserUtils.FormatFloat(data.Illustration.IconCenter.X)} {ParserUtils.FormatFloat(data.Illustration.IconCenter.Y)}");
    sb.AppendLine($"  Icon Size: {ParserUtils.FormatFloat(data.Illustration.IconSize)}");
    sb.AppendLine();

    // [CHARTS]
    sb.AppendLine("[CHARTS]");
    foreach (var chart in data.Charts)
    {
      sb.AppendLine($"+ ID: {chart.ID}");
      sb.AppendLine($"  Index: {chart.Index}");
      sb.AppendLine($"  Name: {chart.Name}");
      sb.AppendLine($"  Charter: {chart.Charter}");
      sb.AppendLine($"  Level: {chart.Level}");
      sb.AppendLine($"  Constant: {ParserUtils.FormatFloat(chart.Constant)}");
    }

    using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
    if (file != null)
      file.StoreString(sb.ToString());
    else
      GD.PushError($"Failed to open file for writing at: {filePath}");
  }
}
