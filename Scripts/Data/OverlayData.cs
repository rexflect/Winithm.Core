using System;
using System.Collections.Generic;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Post-processing shader overlay with dynamic parameters.
/// </summary>
public class OverlayData : IStoryboardable<string>, IDeepCloneableUID<OverlayData>
{
  public event Action<OverlayData>? OnLifeCycleChanged;
  public event Action<OverlayData>? OnUpdated;

  public string ID = "";

  public BeatTime StartBeat { get; set { if (field == value) return; field = value; OnLifeCycleChanged?.Invoke(this); } } = BeatTime.NaN;
  public BeatTime EndBeat { get; set { if (field == value) return; field = value; OnLifeCycleChanged?.Invoke(this); } } = BeatTime.NaN;

  public string Name { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  public string ShaderFile { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  public bool AffectsUI { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = false;

  public int Layer { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;

  public int SubLayer { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;

  /// <summary>Shader uniform definitions.</summary>
  public Dictionary<string, ShaderParamDef> ShaderParams { get; } = new();

  public Dictionary<string, AnyValue> InitParams { get; } = new();

  public StoryboardManager<string> StoryboardEvents { get; set; } = new();

  public OverlayData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public OverlayData DeepCloner(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new OverlayData();

    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;

    cloned.ID = objectFactory.GenerateUID();
    cloned.Name = Name;
    cloned.ShaderFile = ShaderFile;
    cloned.AffectsUI = AffectsUI;
    cloned.Layer = Layer;

    foreach (var pair in ShaderParams)
      cloned.ShaderParams[pair.Key] = pair.Value;

    foreach (var pair in InitParams)
      cloned.InitParams[pair.Key] = pair.Value;

    cloned.StoryboardEvents = StoryboardEvents?.DeepCloner(objectFactory, offset) ?? new StoryboardManager<string>();
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;

    return cloned;
  }

  public void SetInitParam(string key, AnyValue value)
  {
    InitParams[key] = value;
    OnUpdated?.Invoke(this);
  }

  public bool RemoveInitParam(string key)
  {
    if (!InitParams.Remove(key)) return false;
    OnUpdated?.Invoke(this);
    return true;
  }

  public void SetShaderParam(string key, ShaderParamDef value)
  {
    ShaderParams[key] = value;
    OnUpdated?.Invoke(this);
  }

  public bool RemoveShaderParam(string key)
  {
    if (!ShaderParams.Remove(key)) return false;
    OnUpdated?.Invoke(this);
    return true;
  }

  private void BubbleStoryboard(StoryboardManager<string> sb) => OnUpdated?.Invoke(this);
}
