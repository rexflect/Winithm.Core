using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using Winithm.Core.Common;
using Winithm.Core.Data;

namespace Winithm.Core.Managers;

public enum NotePart
{
  Base,
  Overlay
}

public record struct ResourcePackConfig
{
  public bool Particle;
  public float HighlightSize;
  public float HighlightInsensity;
  public int NinePatchHeadMargin;
  public int NinePatchBodyMarginH;
  public int NinePatchBodyMarginV;
  public int NinePatchFloatMargin;
  public float HeadOverlayRatio;
  public float FloatOverlayRatio;
  public float bodyWidthOffset;
  public HitResultType HitFXAutoResult;
  public int HitFXHoldTickMs;
  public bool HitFXAdditiveBlending;
}

public record struct ResourcePack
{
  public Dictionary<NoteType, Dictionary<NotePart, Texture2D>> TEX;
  public Dictionary<NoteType, AudioStream> SFX;
  public Node2D VFX;
  public PackedScene HitFXScene;
  public ResourcePackConfig Config;
}

public partial class ResourcePackManager : Node
{
  public static ResourcePackManager Instance { get; private set; } = default!;

  public static NotePart ParseNotePart(string name)
  {
    return name.ToLowerInvariant() switch
    {
      "base" => NotePart.Base,
      "overlay" => NotePart.Overlay,
      _ => NotePart.Base
    };
  }

  public static HitResultType ParseHitResultType(string name)
  {
    return name.ToLowerInvariant() switch
    {
      "perfect" => HitResultType.Perfect,
      "good" => HitResultType.Good,
      "bad" => HitResultType.Bad,
      "miss" => HitResultType.Miss,
      _ => HitResultType.Perfect
    };
  }

  public static readonly string RESOURCE_PACKS_PATH = "res://Winithm.Core/Resources/ResourcePacks";
  
  private Dictionary<string, ResourcePack>? _resourcePacks;
  private ResourcePack _activeResourcePack;

  public string ActiveResourcePackName
  {
    get;
    set
    {
      if (_resourcePacks is not null && _resourcePacks.TryGetValue(value, out var pack))
      {
        field = value;
        _activeResourcePack = pack;
      }
      else
        GD.PushError($"[NoteResourceManager] Skin pack not found: {value}");
    }
  } = "default";

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _Ready()
  {
    using var resourcePacksDir = DirAccess.Open(RESOURCE_PACKS_PATH);
    if (resourcePacksDir is null)
    {
      GD.PushError($"[ResourcePackManager] Failed to open resource packs directory: {RESOURCE_PACKS_PATH}");
      return;
    }

    _resourcePacks = [with(StringComparer.OrdinalIgnoreCase)];

    foreach (string resourcePackName in resourcePacksDir.GetDirectories())
    {
      string resourcePackPath = RESOURCE_PACKS_PATH + "/" + resourcePackName;
      var resourcePack = new ResourcePack
      {
        TEX = [],
        SFX = [],
        VFX = new(),
        HitFXScene = new(),
        Config = new()
        {
          Particle = false,
          HitFXAutoResult = HitResultType.Perfect,
          HitFXHoldTickMs = 150,
          HitFXAdditiveBlending = true,
        }
      };

      LoadConfig(resourcePackPath.PathJoin("config.ini"), ref resourcePack);
      LoadTexture(resourcePackPath.PathJoin("tex"), ref resourcePack);
      LoadSoundEffect(resourcePackPath.PathJoin("sfx"), ref resourcePack);
      LoadHitFX(resourcePackPath.PathJoin("vfx"), ref resourcePack);

      _resourcePacks[resourcePackName] = resourcePack;
    }

    ActiveResourcePackName = "default";
  }

  private static void LoadConfig(string path, ref ResourcePack resourcePack)
  {
    if (!FileAccess.FileExists(path)) return; // Config is optional

    using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
    if (file is null)
    {
      GD.PushError($"[ResourcePackManager] Failed to open config file: {path}");
      return;
    }

    try
    {
      while (!file.EofReached())
      {
        string line = file.GetLine().Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

        // Faster parsing with IndexOf instead of allocating array from Split
        int delimiterIdx = line.IndexOf('=');
        if (delimiterIdx == -1) continue;

        string key = line[..delimiterIdx].Trim();
        string val = line[(delimiterIdx + 1)..].Trim();

        switch (key)
        {
          case "particle":
            resourcePack.Config.Particle = bool.TryParse(val, out bool ptl) && ptl;
            break;
          case "ninePatchHeadMargin":
            resourcePack.Config.NinePatchHeadMargin = int.TryParse(val, out int h) ? h : 16;
            break;
          case "ninePatchBodyMarginH":
            resourcePack.Config.NinePatchBodyMarginH = int.TryParse(val, out int mh) ? mh : 0;
            break;
          case "ninePatchBodyMarginV":
            resourcePack.Config.NinePatchBodyMarginV = int.TryParse(val, out int mv) ? mv : 0;
            break;
          case "ninePatchFloatMargin":
            resourcePack.Config.NinePatchFloatMargin = int.TryParse(val, out int fm) ? fm : 0;
            break;
          case "headOverlayRatio":
            resourcePack.Config.HeadOverlayRatio = float.TryParse(val, CultureInfo.InvariantCulture, out float hos) ? hos : 1.2f;
            break;
          case "floatOverlayRatio":
            resourcePack.Config.FloatOverlayRatio = float.TryParse(val, CultureInfo.InvariantCulture, out float fos) ? fos : 0.5f;
            break;
          case "bodyWidthOffset":
            resourcePack.Config.bodyWidthOffset = float.TryParse(val, CultureInfo.InvariantCulture, out float bwo) ? bwo : 0.015f;
            break;
          case "highlightSize":
            resourcePack.Config.HighlightSize = float.TryParse(val, CultureInfo.InvariantCulture, out float sz) ? sz : 0.75f;
            break;
          case "highlightIntensity":
            resourcePack.Config.HighlightInsensity = float.TryParse(val, CultureInfo.InvariantCulture, out float ins) ? ins : 1.5f;
            break;
          case "hitfxAutoResult":
            resourcePack.Config.HitFXAutoResult = ParseHitResultType(val);
            break;
          case "hitfxHoldTickMs":
            resourcePack.Config.HitFXHoldTickMs =
              int.TryParse(val, out int tickMs) ? tickMs : 150;
            break;
          case "hitfxAdditiveBlending":
            resourcePack.Config.HitFXAdditiveBlending = bool.TryParse(val, out bool ab) && ab;
            break;
        }
      }
    }
    finally
    {
      file.Close();
    }
  }

  private static Color StringToColor(string str)
  {
    var parts = str.Split('|', StringSplitOptions.RemoveEmptyEntries);

    float r = 0, g = 0, b = 0, a = 1;

    if (parts.Length >= 1) _ = float.TryParse(parts[0], CultureInfo.InvariantCulture, out r);
    if (parts.Length >= 2) _ = float.TryParse(parts[1], CultureInfo.InvariantCulture, out g);
    if (parts.Length >= 3) _ = float.TryParse(parts[2], CultureInfo.InvariantCulture, out b);
    if (parts.Length >= 4) _ = float.TryParse(parts[3], CultureInfo.InvariantCulture, out a);

    return new(r, g, b, a);
  }

  private static void LoadTexture(string path, ref ResourcePack resourcePack)
  {
    using var dir = DirAccess.Open(path);
    if (dir is null)
    {
      GD.PushError($"[ResourcePackManager] Failed to open texture directory: {path}");
      return;
    }

    foreach (string fileName in dir.GetFiles())
    {
      if (fileName.EndsWith(".uid")) continue;

      // Sanitize file names to mitigate Godot 4's export artifact remap behavior
      string cleanFileName = fileName.EndsWith(".import")
        ? fileName.Substring(0, fileName.Length - 7)
        : fileName;

      string filePath = path + "/" + cleanFileName;
      string fileNameWOExt = System.IO.Path.GetFileNameWithoutExtension(cleanFileName);

      // Expected naming convention: [NoteType]_[NotePart] (e.g., Tap_Base)
      int underscoreIdx = fileNameWOExt.IndexOf('_');
      if (underscoreIdx == -1) continue;

      string ntStr = fileNameWOExt[..underscoreIdx];
      string tpStr = fileNameWOExt[(underscoreIdx + 1)..];

      var noteType = NoteData.ParseNoteType(ntStr);
      var texturePart = ParseNotePart(tpStr);

      // Lazily initialize the nested dictionary to guarantee safe lookups
      if (!resourcePack.TEX.TryGetValue(noteType, out var value))
      {
        value = [];
        resourcePack.TEX[noteType] = value;
      }

      value[texturePart] = GD.Load<Texture2D>(filePath);
    }
  }

  private static void LoadSoundEffect(string path, ref ResourcePack resourcePack)
  {
    using var dir = DirAccess.Open(path);
    if (dir is null)
    {
      GD.PushError($"[ResourcePackManager] Failed to open sound effect directory: {path}");
      return;
    }

    foreach (string fileName in dir.GetFiles())
    {
      if (fileName.EndsWith(".uid")) continue;

      // Sanitize file names to mitigate Godot 4's export artifact remap behavior
      string cleanFileName = fileName.EndsWith(".import")
        ? fileName.Substring(0, fileName.Length - 7)
        : fileName;

      string filePath = path + "/" + cleanFileName;
      string fileNameWOExt = System.IO.Path.GetFileNameWithoutExtension(cleanFileName);

      var noteType = NoteData.ParseNoteType(fileNameWOExt);
      var audioStream = GD.Load<AudioStream>(filePath);

      if (audioStream is null) continue;

      AudioStreamUtils.ClampStreamLoop(audioStream);
      resourcePack.SFX[noteType] = audioStream;
    }

    // Fallback assignment: If an explicit Hold SFX is missing, reuse Tap SFX as the default behavior
    if (resourcePack.SFX.ContainsKey(NoteType.Tap))
    {
      if (!resourcePack.SFX.ContainsKey(NoteType.Hold))
        resourcePack.SFX[NoteType.Hold] = resourcePack.SFX[NoteType.Tap];
    }
    else
    {
      GD.PushError($"[ResourcePackManager] Critical fallback missing: Default 'Tap' SFX could not be found in {path}");
    }
  }

  private static void LoadHitFX(string path, ref ResourcePack resourcePack)
  {
    string scenePath = path + "/hitfx.tscn";
    if (ResourceLoader.Exists(scenePath))
    {
      resourcePack.HitFXScene = GD.Load<PackedScene>(scenePath);
    }
  }

  public void SetActiveResourcePack(string resourcePackName)
  {
    ActiveResourcePackName = resourcePackName;
  }

  // Direct memory access without dictionary lookup guarantees O(1) high performance calls
  public ResourcePack GetActiveResourcePack() => _activeResourcePack;

  public IEnumerable<ResourcePack>? GetAllResourcePacks() => _resourcePacks?.Values;
}