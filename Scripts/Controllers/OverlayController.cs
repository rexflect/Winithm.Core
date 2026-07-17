using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors.Gameplay;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

[Tool]
public partial class OverlayController : Node
{
  private AudioController? _audioController;
  private OverlayManager? _overlayManager;
  private Control? _outterShaderLayer;
  private Control? _innerShaderLayer;

  private NodePool<OverlayBase>? _overlayPool;
  private readonly Dictionary<string, Shader> _shaderCache = [];
  private readonly Dictionary<string, List<string>> _shaderUniformCache = [];

  private class OverlayState
  {
    public required OverlayBase Visual;
    public required OverlayData Data;
    public ulong FrameSessionToken = 0;
  }

  private readonly Dictionary<string, OverlayState> _overlayStates = [];
  private double _lastUpdateBeat = -1f;
  private int _renderCursor = 0;
  private ulong _frameSessionToken = 1;

  public void Initialize(
      Control outterShaderLayer,
      Control innerShaderLayer,
      OverlayManager overlayManager,
      AudioController audioController
  )
  {
    foreach (var state in _overlayStates.Values)
      _overlayPool?.Release(state.Visual);
    _overlayStates.Clear();

    _renderCursor = 0;
    _frameSessionToken = 0;
    _lastUpdateBeat = -1f;

    _outterShaderLayer = outterShaderLayer;
    _innerShaderLayer = innerShaderLayer;
    _overlayManager = overlayManager;
    _audioController = audioController;

    _overlayPool = new NodePool<OverlayBase>(this, null, CreatePooledOverlay);

    PreloadShaders();
  }

  private void PreloadShaders()
  {
    _shaderCache.Clear();
    if (_overlayManager is null) return;

    var shaderDir = "res://Winithm.Core/Resources/Shaders/Gameplay/";

    foreach (var overlay in _overlayManager.GetAllOverlays())
    {
      if (string.IsNullOrEmpty(overlay.ShaderFile)) continue;

      if (!_shaderCache.ContainsKey(overlay.ShaderFile))
      {
        string path = shaderDir + overlay.ShaderFile;
        if (!path.EndsWith(".gdshader")) path += ".gdshader";

        if (ResourceLoader.Exists(path))
        {
          var shader = GD.Load<Shader>(path);
          _shaderCache[overlay.ShaderFile] = shader;
          _shaderUniformCache[overlay.ShaderFile] = ShaderUtils.GetUserUniformNames(shader.Code);
        }
        else
        {
          GD.PushError($"[OverlayController] Shader file not found: {path}");
        }
      }
    }
  }

  private OverlayBase CreatePooledOverlay()
  {
    var overlay = new OverlayBase();
    AddChild(overlay);
    return overlay;
  }

  public void Update(double currentBeat)
  {
    if (_lastUpdateBeat == currentBeat) return;
    ForceUpdate(currentBeat);
    _lastUpdateBeat = currentBeat;
  }

  public void ForceUpdate(double currentBeat)
  {
    if (_audioController?.Metronome is null || _overlayManager is null || _overlayPool is null)
    {
      GD.PushWarning("[OverlayController] Metronome, _overlayManager and _overlayPool is not initialized.");
      return;
    }

    bool isBackward = currentBeat < _lastUpdateBeat;
    var maxEnds = _overlayManager.MaxEndBeats;
    int overlayCount = _overlayManager.Count;

    _frameSessionToken++;

    if (isBackward)
      _renderCursor = FindRenderCursor(maxEnds, currentBeat);
    else
    {
      while (_renderCursor < overlayCount && maxEnds[_renderCursor] < currentBeat)
        _renderCursor++;
    }

    for (int i = _renderCursor; i < overlayCount; i++)
    {
      var overlayData = _overlayManager[i];
      if (overlayData is null) continue;
      if (overlayData.StartBeat.AbsoluteValue > currentBeat) break; // Reached future overlays

      bool shouldBeActive = currentBeat >= overlayData.StartBeat.AbsoluteValue && currentBeat <= overlayData.EndBeat.AbsoluteValue;
      bool isActive = _overlayStates.TryGetValue(overlayData.ID, out var state);

      if (!shouldBeActive) continue;

      OverlayBase overlayVisual;
      if (!isActive)
      {
        overlayVisual = _overlayPool.Get();
        overlayVisual.Name = string.IsNullOrEmpty(overlayData.ID) ? "Overlay" : overlayData.ID;
        overlayVisual.ResetDirtyState();

        if (overlayData.AffectsUI)
        {
          if (overlayVisual.GetParent() != _outterShaderLayer)
            overlayVisual.Reparent(_outterShaderLayer);
        }
        else
        {
          if (overlayVisual.GetParent() != _innerShaderLayer)
            overlayVisual.Reparent(_innerShaderLayer);
        }

        overlayVisual.ZIndex = LayerUtils.ComposeLayerIndex(overlayData.Layer, overlayData.SubLayer);

        state = new OverlayState() { Visual = overlayVisual, Data = overlayData };
        _overlayStates[overlayData.ID] = state;
      }
      else
      {
        overlayVisual = state!.Visual;
      }

      state.FrameSessionToken = _frameSessionToken;

      ApplyOverlayVisuals(overlayVisual, overlayData, currentBeat);
    }

    CollectStaleOverlays();
  }

  private void ApplyOverlayVisuals(OverlayBase visual, OverlayData data, double currentBeat)
  {
    if (_shaderCache.TryGetValue(data.ShaderFile, out var shader))
      visual.UpdateShader(shader);
    else
    {
      GD.PushWarning($"[OverlayController] Shader file not found: {data.ShaderFile}");
      return;
    }

    if (!_shaderUniformCache.TryGetValue(data.ShaderFile, out var uniformNames))
    {
      GD.PushWarning($"[OverlayController] Shader uniform cache not found for: {data.ShaderFile}");
      return;
    }

    for (int i = 0; i < uniformNames.Count; i++)
    {
      string uName = uniformNames[i];

      AnyValue defVal = data.InitParams.TryGetValue(i.ToString(), out var initVal) ? initVal : new AnyValue(0f);

      if (data.StoryboardEvents.TryGetValue(uName, out var events) && events != null && events.Count > 0)
      {
        defVal = data.StoryboardEvents.Evaluate(uName, currentBeat, defVal);
      }

      visual.SetParameter(new StringName(uName), GetVariant(defVal));
    }
  }

  private static Variant GetVariant(AnyValue val)
  {
    return val.Type switch
    {
      AnyValueType.Float => Variant.From(val.X),
      AnyValueType.Vec2 => Variant.From(new Vector2(val.X, val.Y)),
      AnyValueType.Vec3 => Variant.From(new Vector3(val.X, val.Y, val.Z)),
      AnyValueType.Vec4 => Variant.From(new Color(val.X, val.Y, val.Z, val.W)),
      AnyValueType.Bool => Variant.From(val.X > 0.5f),
      AnyValueType.String => Variant.From(val.StringValue ?? string.Empty),
      _ => default
    };
  }

  private readonly List<string> _staleIds = [];
  private void CollectStaleOverlays()
  {
    _staleIds.Clear();
    foreach (var kvp in _overlayStates)
    {
      if (kvp.Value.FrameSessionToken != _frameSessionToken)
      {
        _staleIds.Add(kvp.Key);
      }
    }

    foreach (var id in _staleIds)
    {
      _overlayPool?.Release(_overlayStates[id].Visual);
      _overlayStates.Remove(id);
    }
  }

  private static int FindRenderCursor(double[] maxEnds, double currentBeat)
  {
    if (maxEnds == null || maxEnds.Length == 0) return 0;
    int left = 0, right = maxEnds.Length - 1;
    int best = maxEnds.Length;

    while (left <= right)
    {
      int mid = left + (right - left) / 2;
      if (maxEnds[mid] >= currentBeat)
      {
        best = mid;
        right = mid - 1;
      }
      else
      {
        left = mid + 1;
      }
    }
    return best;
  }
}
