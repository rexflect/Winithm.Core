using System;
using System.Collections.Generic;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Post-processing shader overlay with dynamic parameters.
/// </summary>
public class OverlayData : IStoryboardable<string>, IDeepCloneable<OverlayData>
{
  public event Action<OverlayData> OnLifeCycleChanged;
  public event Action<OverlayData> OnUpdated;

  public string ID;

  private BeatTime _startBeat;
  public BeatTime StartBeat { get => _startBeat; set { if (_startBeat == value) return; _startBeat = value; OnLifeCycleChanged?.Invoke(this); } }
  private BeatTime _endBeat;
  public BeatTime EndBeat { get => _endBeat; set { if (_endBeat == value) return; _endBeat = value; OnLifeCycleChanged?.Invoke(this); } }

  private string _name;
  public string Name { get => _name; set { if (_name == value) return; _name = value; OnUpdated?.Invoke(this); } }

  private string _shaderFile;
  public string ShaderFile { get => _shaderFile; set { if (_shaderFile == value) return; _shaderFile = value; OnUpdated?.Invoke(this); } }

  private bool _affectsUI = false;
  public bool AffectsUI { get => _affectsUI; set { if (_affectsUI == value) return; _affectsUI = value; OnUpdated?.Invoke(this); } }

  private int _layer = 0;
  public int Layer { get => _layer; set { if (_layer == value) return; _layer = value; OnUpdated?.Invoke(this); } }

  private int _subLayer = 0;
  public int SubLayer { get => _subLayer; set { if (_subLayer == value) return; _subLayer = value; OnUpdated?.Invoke(this); } }

  /// <summary>Shader uniform definitions.</summary>
  public Dictionary<string, ShaderParamDef> ShaderParams { get; } = new();

  public Dictionary<string, AnyValue> InitParams { get; } = new();

  public StoryboardManager<string> StoryboardEvents { get; set; } = new();

  public OverlayData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
  }

  public OverlayData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
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

    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset);
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

