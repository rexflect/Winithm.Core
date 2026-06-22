using Godot;
using System;
using System.Collections.Generic;
using Winithm.Core.Behaviors.Windows;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Managers;
using Winithm.Native;

namespace Winithm.Core.Controllers;

public enum WindowMode
{
  InGame,
  Editor,
  Native
}

[Tool]
public partial class WindowController : Node
{
  protected Metronome? _metronome;
  protected GroupController? _groupController;
  protected ThemeChannelController? _themeController;
  protected NoteController? _noteController;
  protected WindowManager? _windowManager;
  private Control? _objectsLayer;

  private PackedScene _windowScene = GD.Load<PackedScene>("res://Winithm.Core/Resources/Sprites/Window.tscn");

  /// <summary>Holds the dynamically loaded WM-specific script.</summary>
  private Script? _windowScript;

  [Export] public Vector2 ScreenSize = new(1280, 720);
  [Export] public Vector2 PlayerAreaSize = new(1280, 720);
  [Export] public Color TitleBarColor = Colors.DarkSlateGray;
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

  public void SetWindowMode(WindowMode mode)
  {
    switch (mode)
    {
      case WindowMode.InGame:
        // Detect/load script before constructing the pool — createFunc needs it.
        var wm = WindowManagerDetector.Detect();
        _windowScript = wm switch
        {
          WindowManagerType.Windows10 => GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowWD10.cs"),
          WindowManagerType.Windows11 => GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowWD11.cs"),
          WindowManagerType.MacOS => GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowMC.cs"),
          WindowManagerType.X11 or WindowManagerType.Wayland => GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowLN.cs"),
          _ => GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowWD10.cs")
        };
        break;
      case WindowMode.Editor:
        _windowScript = GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowED.cs");
        break;
      case WindowMode.Native:
        _windowScript = GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowNT.cs");
        break;
      default:
        _windowScript = GD.Load<Script>("res://Winithm.Core/Scripts/Behaviors/Windows/WindowWD10.cs");
        break;
    }

    _windowPool = new NodePool<WindowBase>(this, _windowScene, createFunc: CreatePooledWindow);
  }

  private WindowBase CreatePooledWindow()
  {
    var rawInstance = _windowScene.Instantiate<Control>();

    if (_windowScript is not null)
    {
      ulong id = rawInstance.GetInstanceId();
      rawInstance.SetScript(_windowScript);

      rawInstance = InstanceFromId(id) as Control;
    }

    if (rawInstance is not WindowBase windowVisual)
    {
      GD.PushError("[WindowController] Window.tscn root did not become a WindowBase after SetScript.");
      rawInstance?.QueueFree();
      throw new InvalidOperationException("Failed to create pooled WindowBase instance.");
    }

    AddChild(windowVisual);

    return windowVisual;
  }

  public void Update(double currentBeat)
  {
    if (_lastUpdateBeat == currentBeat) return;

    ForceUpdate(currentBeat, false);

    _lastUpdateBeat = currentBeat;
  }

  public void ForceUpdate(double currentBeat, bool _force = true)
  {
    if (_metronome is null || _windowManager is null)
    {
      GD.PushError("[WindowController] Not initialized");
      return;
    }

    if (_windowPool is null)
    {
      GD.PushError("[WindowController] Window mode not set");
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
      if (!shouldBeActive) continue;

      WindowBase windowVisual;
      if (!isActive)
      {
        windowVisual = _windowPool.Get();

        // Recycled pool nodes may carry a stale script; CreatePooledWindow
        // already attaches the right one for new nodes, so this is a guard.
        if (_windowScript is not null)
        {
          var currentScript = windowVisual.GetScript().As<Script>();

          if (currentScript != _windowScript)
          {
            windowVisual.DetachScriptEvents();

            ulong id = windowVisual.GetInstanceId();
            windowVisual.SetScript(_windowScript);

            if (InstanceFromId(id) is WindowBase newWrapper)
              windowVisual = newWrapper;


            windowVisual.ResetDirtyState();
            windowVisual.OnReady();
            windowVisual.UpdateVisual();
          }
        }

        windowVisual.Name = string.IsNullOrEmpty(windowData.ID) ? "Window" : windowData.ID;
        windowVisual.Pivot = new Vector2(windowData.AnchorX, windowData.AnchorY);
        windowVisual.Title = windowData.Title;
        windowVisual.Borderless = windowData.Borderless;
        windowVisual.TitleBarColor = TitleBarColor;

        state = new WindowState() { Visual = windowVisual, Data = windowData };

        _windowStates[windowData.ID] = state;
        _noteController?.RegisterWindow(windowData.ID, windowData, windowVisual);

        if (windowVisual.GetParent() != _objectsLayer)
          windowVisual.Reparent(_objectsLayer);

        windowVisual.ZIndex = LayerUtils.ComposeLayerIndex(windowData.Layer, windowData.SubLayer);
      }
      else
      {
        // Should be unreachable (TryGetValue true implies state non-null),
        // but fall back via CreatePooledWindow() to avoid a cast-fail.
        windowVisual = state?.Visual ?? CreatePooledWindow();
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
      windowData.ComputeAnimationWhenUnresponsive(_metronome);
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