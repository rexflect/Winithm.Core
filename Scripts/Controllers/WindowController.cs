using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors.Windows;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

[Tool]
public partial class WindowController : Node
{
  protected Metronome? _metronome;
  protected GroupController? _groupController;
  protected ThemeChannelController? _themeController;
  protected NoteController? _noteController;
  protected WindowManager? _windowManager;

  private Control? _objectsLayer;
  private PackedScene _windowScene = GD.Load<PackedScene>("res://Winithm.Core/Resources/Sprites/Windows/WindowWD.tscn");
  [Export] public Vector2 ScreenSize = new(1280, 720);
  [Export] public Vector2 PlayerAreaSize = new(1280, 720);

  [Export] public Color TitleBarColor = Colors.Coral;
  [Export] public Color TitleTextColor = Colors.Black;

  [Export] public float FocusablePulseFrequency = 5f;

  private class WindowState
  {
    public required WindowBase Visual;
    public required WindowData Data;
    public ulong FrameSessionToken = 0;
  }

  private readonly Dictionary<string, WindowState> _windowStates = [];

  private double _lastUpdateBeat = -1f;
  private int _renderCursor = 0;
  private ulong _frameSessionToken = 1;

  private NodePool<WindowBase>? _windowPool;

  public void Initialize(
    Control objectsLayer,
    WindowManager windowManager,
    Metronome metronome,
    GroupController groupController,
    ThemeChannelController themeController,
    NoteController noteController
  )
  {
    _windowPool = new NodePool<WindowBase>(this, _windowScene);

    _windowStates.Clear();
    _renderCursor = 0;
    _frameSessionToken = 0;
    _lastUpdateBeat = -1f;

    _objectsLayer = objectsLayer;

    _metronome = metronome;
    _groupController = groupController;
    _themeController = themeController;
    _noteController = noteController;
    _windowManager = windowManager;
  }

  public void Update(double currentBeat)
  {
    if (_lastUpdateBeat == currentBeat) return;

    ForceUpdate(currentBeat, false);

    _lastUpdateBeat = currentBeat;
  }

  public void ForceUpdate(double currentBeat, bool _force = true)
  {
    if (_metronome is null
      || _windowManager is null
      || _windowPool is null
      )
    {
      GD.PushError("[WindowController] Not initialized");
      return;
    }

    bool isBackward = currentBeat < _lastUpdateBeat;
    var maxEnds = _windowManager.MaxEndBeats;
    int windowCount = _windowManager.Count;

    _frameSessionToken++;

    if (isBackward)
    {
      _renderCursor = FindRenderCursor(maxEnds, currentBeat);
    }
    else
    {
      while (_renderCursor < windowCount && maxEnds[_renderCursor] < currentBeat)
      {
        _renderCursor++;
      }
    }

    for (int i = _renderCursor; i < windowCount; i++)
    {
      var windowData = _windowManager[i];

      if (windowData is null) continue;

      if (windowData.StartBeat.AbsoluteValue > currentBeat) break;

      float lifeCycleScale = CalculateLifeCycleScale(windowData, currentBeat);
      bool shouldBeActive = lifeCycleScale > 0.001f;

      bool isActive = _windowStates.TryGetValue(windowData.ID, out var state);
      if (!shouldBeActive)
      {
        continue;
      }


      WindowBase windowVisual;
      if (!isActive)
      {
        windowVisual = _windowPool.Get();
        windowVisual.Name = string.IsNullOrEmpty(windowData.ID) ? "Window" : windowData.ID;
        windowVisual.Pivot = new Vector2(windowData.AnchorX, windowData.AnchorY);
        windowVisual.Title = windowData.Title;
        windowVisual.Borderless = windowData.Borderless;
        windowVisual.TitleBarColor = TitleBarColor;
        windowVisual.TitleTextColor = TitleTextColor;

        state = new WindowState() { Visual = windowVisual, Data = windowData };

        _windowStates[windowData.ID] = state;
        _noteController?.RegisterWindow(windowData.ID, windowData, windowVisual);

        if (windowVisual.GetParent() != _objectsLayer)
          windowVisual.Reparent(_objectsLayer);

        windowVisual.ZIndex = LayerUtils.ComposeLayerIndex(windowData.Layer, windowData.SubLayer);
      }
      else
      {
        windowVisual = state?.Visual ?? _windowScene.Instantiate<WindowBase>();
      }

      state?.FrameSessionToken = _frameSessionToken;

      ApplyWindowTransformAndAppearance(windowVisual, windowData, currentBeat, lifeCycleScale, _force);

      if (windowData.UnFocus)
      {
        bool isFocusableNow = IsFocusableAt(windowData.ID, currentBeat);
        if (isFocusableNow)
          AnimateFocusableOverlay(windowVisual, currentBeat);
        else
        {
          windowVisual.UnFocusOverlayOpacity = WindowBase.UnfocusOverlayTint.A;
          windowVisual.UnFocus = true;
        }
      }
      else
      {
        windowVisual.UnFocusOverlayOpacity = 0f;
        windowVisual.UnFocus = false;
      }

      if (windowData.Unresponsive)
        AnimateUnresponsiveOverlay(windowVisual, windowData, currentBeat);
      else
      {
        windowVisual.UnresponsiveOverlayOpacity = 0f;
        windowVisual.IsNotRespondingTitle = false;
      }

      windowVisual.UpdateVisual();
    }

    CollectStaleWindows();
  }

  /// <summary>
  /// Called at runtime when a window enters the UnResponsive state.
  /// Computes overlay animation timestamps and extends the window's lifetime.
  /// </summary>
  public void SetUnresponsive(string windowId)
  {
    if (!_windowStates.TryGetValue(windowId, out var state)) return;
    var windowData = state.Data;
    if (windowData.Unresponsive) return;

    windowData.Unresponsive = true;

    if (_metronome is not null)
    {
      windowData.ComputeAnimationWhenUnresponsive(_metronome);
    }
    else
      GD.PushError("[WindowController] _metronome is not initialized to compute window animation");
  }

  public void AddStartFocusable(string windowId, double currentBeat)
  {
    if (!_windowStates.TryGetValue(windowId, out var state)) return;
    var windowData = state.Data;

    windowData.UnFocus = true;
    windowData.FocusablePeriods.Add((currentBeat, double.NaN));
  }

  public void AddEndFocusable(string windowId, double currentBeat)
  {
    if (!_windowStates.TryGetValue(windowId, out var state)) return;
    var windowData = state.Data;
    if (!windowData.UnFocus) return;

    var periods = windowData.FocusablePeriods;

    // Active period (End == NaN) is always the last one,
    // since we always close before opening a new period.
    int last = periods.Count - 1;
    if (last >= 0 && double.IsNaN(periods[last].End))
    {
      periods[last] = (periods[last].Start, currentBeat);
      windowData.UnFocus = false;
    }
  }
}