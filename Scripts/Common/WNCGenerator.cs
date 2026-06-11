using Godot;
using System.Linq;
using System.Text;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Common;

/// <summary>
/// Generator for .wnc (Chart) files.
/// Writes ChartData back into the Winithm .wnc plaintext format.
/// </summary>
public static class WNCGenerator
{
  public static readonly float CHART_FORMAT_VERSION = 1.2f;

  public static void Generate(string filePath, ChartData data)
  {
    var sb = new StringBuilder();

    // [FORMAT]
    sb.AppendLine("[FORMAT]");
    sb.AppendLine("Type: Chart");
    sb.AppendLine($"Version: {ParserUtils.FormatFloat(CHART_FORMAT_VERSION)}");
    sb.AppendLine();

    // [METADATA]
    sb.AppendLine("[METADATA]");
    sb.AppendLine($"Index: {data.ChartMetadata.Index}");
    sb.AppendLine($"ID: {data.ChartMetadata.ChartID}");
    sb.AppendLine($"Name: {data.ChartMetadata.ChartName}");
    sb.AppendLine($"Level: {data.ChartMetadata.Level}");
    sb.AppendLine($"Constant: {ParserUtils.FormatFloat(data.ChartMetadata.Constant)}");
    sb.AppendLine();

    // [OVERLAYS]
    if (data.Overlays.Count > 0)
    {
      sb.AppendLine("[OVERLAYS]");
      foreach (var overlay in data.Overlays)
      {
        string initParams = string.Join(" ", overlay.InitParams
            .OrderBy(kv => int.TryParse(kv.Key, out _) ? int.Parse(kv.Key) : int.MaxValue)
            .Select(kv => kv.Value.ToString()));

        if (!string.IsNullOrEmpty(initParams)) initParams = " " + initParams;

        sb.AppendLine($"+ {overlay.ID} {overlay.StartBeat} {overlay.EndBeat}{initParams}");
        sb.AppendLine($" Name: {overlay.Name ?? ""}");
        sb.AppendLine($" Shader: {overlay.ShaderFile}");
        sb.AppendLine($" Affects UI: {ParserUtils.FormatIntBool(overlay.AffectsUI)}");
        sb.AppendLine($" Layer: {overlay.Layer} {overlay.SubLayer}");

        if (overlay.StoryboardEvents != null)
        {
          foreach (var kvp in overlay.StoryboardEvents)
          {
            foreach (var evt in kvp.Value)
            {
              sb.AppendLine(StoryboardManager<string>.GenerateEventLine(evt, StoryboardProperty.Custom, kvp.Key));
            }
          }
        }
      }
      sb.AppendLine();
    }

    // [COMPONENTS]
    if (data.Components.Count > 0)
    {
      sb.AppendLine("[COMPONENTS]");
      foreach (var comp in data.Components)
      {
        sb.AppendLine($"* {comp.Key} {ParserUtils.FormatFloat(comp.Value.InitX)} {ParserUtils.FormatFloat(comp.Value.InitY)} {ParserUtils.FormatFloat(comp.Value.InitRotate)} {ParserUtils.FormatFloat(comp.Value.InitScale)} {ParserUtils.FormatFloat(comp.Value.InitAlpha)}");

        if (comp.Value.StoryboardEvents != null)
        {
          foreach (var kvp in comp.Value.StoryboardEvents)
          {
            foreach (var evt in kvp.Value)
            {
              sb.AppendLine(StoryboardManager<StoryboardProperty>.GenerateEventLine(evt, kvp.Key, ""));
            }
          }
        }
      }
      sb.AppendLine();
    }

    // [THEME_CHANNELS]
    if (data.ThemeChannels.Count > 0)
    {
      sb.AppendLine("[THEME_CHANNELS]");
      foreach (var tc in data.ThemeChannels.Values)
      {
        sb.AppendLine($"+ {tc.ID} {ParserUtils.FormatFloat(tc.InitR)} {ParserUtils.FormatFloat(tc.InitG)} {ParserUtils.FormatFloat(tc.InitB)} {ParserUtils.FormatFloat(tc.InitA)} {ParserUtils.FormatFloat(tc.InitNoteA)}");
        sb.AppendLine($" Name: {tc.Name ?? ""}");

        if (tc.StoryboardEvents != null)
        {
          foreach (var kvp in tc.StoryboardEvents)
          {
            foreach (var evt in kvp.Value)
            {
              sb.AppendLine(StoryboardManager<StoryboardProperty>.GenerateEventLine(evt, kvp.Key, ""));
            }
          }
        }
      }
      sb.AppendLine();
    }

    // [GROUPS]
    if (data.Groups.Count > 0)
    {
      sb.AppendLine("[GROUPS]");
      foreach (var g in data.Groups.Values)
      {
        sb.AppendLine($"+ {g.ID} {ParserUtils.FormatFloat(g.InitX)} {ParserUtils.FormatFloat(g.InitY)} {ParserUtils.FormatFloat(g.InitScaleX)} {ParserUtils.FormatFloat(g.InitScaleY)} {ParserUtils.FormatFloat(g.InitRotation)}");
        sb.AppendLine($" Name: {g.Name ?? ""}");
        sb.AppendLine($" Group: {g.ParentGroupID ?? ""}");

        if (g.StoryboardEvents != null)
        {
          foreach (var kvp in g.StoryboardEvents)
          {
            foreach (var evt in kvp.Value)
            {
              sb.AppendLine(StoryboardManager<StoryboardProperty>.GenerateEventLine(evt, kvp.Key, ""));
            }
          }
        }
      }
      sb.AppendLine();
    }

    // [WINDOWS]
    if (data.Windows.Count > 0)
    {
      sb.AppendLine("[WINDOWS]");
      foreach (var w in data.Windows)
      {
        sb.AppendLine($"+ {w.ID} {ParserUtils.FormatFloat(w.InitX)} {ParserUtils.FormatFloat(w.InitY)} {ParserUtils.FormatFloat(w.InitScaleX)} {ParserUtils.FormatFloat(w.InitScaleY)} {ParserUtils.FormatFloat(w.InitR)} {ParserUtils.FormatFloat(w.InitG)} {ParserUtils.FormatFloat(w.InitB)} {ParserUtils.FormatFloat(w.InitA)} {ParserUtils.FormatFloat(w.InitNoteA)}");
        sb.AppendLine($" Name: {w.Name ?? ""}");
        sb.AppendLine($" Title: {w.Title ?? ""}");
        sb.AppendLine($" Flags: {ParserUtils.FormatIntBool(w.Borderless)} {ParserUtils.FormatIntBool(w.UnFocus)}");
        sb.AppendLine($" Anchor: {ParserUtils.FormatFloat(w.AnchorX)} {ParserUtils.FormatFloat(w.AnchorY)}");
        sb.AppendLine($" Layer: {w.Layer} {w.SubLayer}");
        sb.AppendLine($" Group: {w.GroupID ?? ""}");
        sb.AppendLine($" Theme Channel: {w.ThemeChannelID ?? ""}");

        // Window-level storyboard events
        if (w.StoryboardEvents != null)
        {
          foreach (var kvp in w.StoryboardEvents)
          {
            foreach (var evt in kvp.Value)
            {
              sb.AppendLine(StoryboardManager<StoryboardProperty>.GenerateEventLine(evt, kvp.Key, ""));
            }
          }
        }

        // Notes
        foreach (var nss in w.Notes)
        {
          foreach (var n in nss.Value)
          {
            sb.AppendLine($" # {n.ID} {n.Type} {n.StartBeat} {ParserUtils.FormatDouble(n.Length)} {ParserUtils.FormatFloat(n.X)} {ParserUtils.FormatFloat(n.Width)} {nss.Key} {n.FakeType}");
          }
        }

        // SpeedSteps
        foreach (var ss in w.SpeedSteps)
        {
          sb.AppendLine($" | {ss.ID} {ss.StartBeat} {ParserUtils.FormatFloat(ss.Multiplier)}");

          if (ss.StoryboardEvents != null)
          {
            foreach (var kvp in ss.StoryboardEvents)
            {
              foreach (var evt in kvp.Value)
              {
                sb.AppendLine(StoryboardManager<StoryboardProperty>.GenerateEventLine(evt, kvp.Key, "", 4));
              }
            }
          }
        }
      }
    }

    using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
    if (file != null)
      file.StoreString(sb.ToString());
    else
      GD.PushError($"Failed to open file for writing at: {filePath}");
  }
}
