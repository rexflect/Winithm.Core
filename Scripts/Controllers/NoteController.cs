using Godot;
using System;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
using Winithm.Core.Behaviors.Windows;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

/// <summary>
/// Manages note spawning, rendering, and lifecycle for all windows.
/// Single instance with shared object pool.
/// </summary>
public partial class NoteController : Node
{
  private WindowManager? _windowManager;

  public event Action<string, NoteData>? OnActiveHoldTick;
  public event Action<string, NoteData>? OnActiveHoldEnded;
  public event Action<string, NoteData, double>? OnDragReady;
  public event Action<string, NoteData>? OnNoteMiss;
  public event Action<string, NoteData>? OnAutoHit;

  [Export] public float PlayerNoteSize = 1f;
  [Export] public float PlayerNoteSpeed = 1f;
  [Export] public bool NoteHighlightSimulation = false;

  public bool Autoplay { get; private set; } = false;

  public static readonly float NOTE_SPEED_PIXELS_PER_SEC = 72f;

  private static readonly Color NOTE_COLOR_DEFAULT = new(1f, 1f, 1f, 1f);
  private static readonly Color NOTE_COLOR_EVALUATED = new(0.5f, 0.5f, 0.5f, 0.5f);

  /// <summary>Off-screen margin multiplier relative to note head height.</summary>
  private const float OFF_SCREEN_MARGIN_FACTOR = 3f;

  private PackedScene? _noteScene;
  private Metronome? _metronome;
  private NodePool<Note>? _notePool;
  private double _lastBeat = double.MinValue;

  public Dictionary<string, WindowNoteState> WindowStates { get; private set; } = [];

  public class WindowNoteState
  {
    public required WindowData WindowData;
    public required WindowBase WindowVisual;

    public Dictionary<NoteData, Note> NoteVisualMap = [];
    public Dictionary<NoteSide, int> RenderCursors = [];
    public Dictionary<NoteSide, int> EvalCursors = [];

    public HashSet<NoteData> ActiveHolds = [];
    public List<NoteData> PendingHoldRemovals = [];
    public List<NoteData> PendingVisualRemovals = [];

    public ulong AutoFireSessionToken = 1;
    public ulong FrameSessionToken = 1;
    public ulong ConsumeSessionToken = 1;
    public double LastBeat = double.MinValue;
  }

  // =============================================
  // Initialization
  // =============================================

  public void Initialize(Metronome metronome, WindowManager windowManager, bool autoplay = false)
  {
    _lastBeat = double.MinValue;
    foreach (var windowId in WindowStates.Keys)
      UnregisterWindow(windowId);

    Autoplay = autoplay;
    _metronome = metronome;
    _windowManager = windowManager;
    _noteScene = GD.Load<PackedScene>("res://Winithm.Core/Resources/Sprites/Note.tscn");
    _notePool = new NodePool<Note>(this, _noteScene);
  }

  public void SetNoteHighlightSimulation(bool active) => NoteHighlightSimulation = active;

  // =============================================
  // Window Registration
  // =============================================

  public void RegisterWindow(string windowId, WindowData windowData, WindowBase windowVisual)
  {
    if (WindowStates.ContainsKey(windowId)) return;

    var state = new WindowNoteState() { WindowData = windowData, WindowVisual = windowVisual };

    foreach (var side in Enum.GetValues<NoteSide>())
    {
      state.RenderCursors[side] = 0;
      state.EvalCursors[side] = 0;
    }

    WindowStates[windowId] = state;
  }

  public void UnregisterWindow(string windowId)
  {
    if (!WindowStates.TryGetValue(windowId, out var state))
    {
      GD.PushWarning($"[NoteController] Window {windowId} not found in window states to unregister.");
      return;
    }

    foreach (var noteVisual in state.NoteVisualMap.Values)
      ReturnToPool(noteVisual);

    WindowStates.Remove(windowId);
  }

  public override void _ExitTree()
  {
    base._ExitTree();

    foreach (var state in WindowStates.Values)
    {
      foreach (var noteVisual in state.NoteVisualMap.Values)
      {
        if (IsInstanceValid(noteVisual)) noteVisual.QueueFree();
      }
      state.NoteVisualMap.Clear();
    }

    _notePool?.Dispose();
  }

  // =============================================
  // Per-Frame Update
  // =============================================

  public void Update(double currentBeat)
  {
    if (currentBeat == _lastBeat) return;
    ForceUpdate(currentBeat, false);
  }

  public void ForceUpdate(double currentBeat, bool _force = true)
  {
    foreach (var entry in WindowStates)
      ProcessWindow(entry.Key, entry.Value, currentBeat, _force);

    _lastBeat = currentBeat;
  }

  private void ProcessWindow(
    string windowId,
    WindowNoteState state,
    double currentBeat,
    bool force
  )
  {
    if (_metronome is null || _notePool is null)
    {
      GD.PushWarning("[NoteController] Metronome or NotePool is not initialized.");
      return;
    }

    if (currentBeat == state.LastBeat && !force) return;

    bool isBackward = currentBeat < state.LastBeat;

    if (isBackward)
      state.ConsumeSessionToken++;

    ProcessActiveHoldNotes(windowId, state, currentBeat);

    var playerAreaSize = state.WindowVisual.PlayerAreaSize;
    var windowSize = state.WindowVisual.WindowSize;

    double beatsPerSecond = _metronome.GetBPSAtBeat(currentBeat);
    float pixelsPerBeat =
      NOTE_SPEED_PIXELS_PER_SEC * PlayerNoteSpeed / (float)(
        beatsPerSecond > 0f ? beatsPerSecond : 2f
      );

    float noteHeadHeight = PlayerNoteSize * Mathf.Min(
      playerAreaSize.X,
      playerAreaSize.Y
    ) * Note.NOTE_HEAD_HEIGHT_RATIO;

    float offScreenMarginPx = noteHeadHeight * OFF_SCREEN_MARGIN_FACTOR;

    float viewportScale = ComputeViewportScale(playerAreaSize);

    foreach (var sideEntry in state.WindowData.Notes)
    {
      var side = sideEntry.Key;
      var noteList = sideEntry.Value;

      float viewportLengthPx = IsVerticalSide(side) ? windowSize.Y * viewportScale : windowSize.X * viewportScale;

      int renderCursor = state.RenderCursors[side];

      // Move cursor backwards if currentBeat rewound
      renderCursor = SyncCursorBackward(
        state, side, renderCursor, currentBeat, pixelsPerBeat, viewportScale, offScreenMarginPx
      );

      // Advance render cursor for notes that are far behind viewport
      renderCursor = SyncCursorForward(
        state, noteList, renderCursor, currentBeat, pixelsPerBeat, viewportScale, offScreenMarginPx
      );

      state.RenderCursors[side] = renderCursor;

      EvaluateNoteLifecycle(windowId, state, side, currentBeat, isBackward);

      // Render visible notes
      for (int i = renderCursor; i < noteList.Count; i++)
      {
        var noteData = noteList[i];

        double noteStartBeat = noteData.StartBeat.AbsoluteValue;
        double noteEndBeat = noteStartBeat + noteData.Length;

        // Skip consumed notes
        if (noteData.ConsumedSessionToken == state.ConsumeSessionToken) continue;

        // Hold notes should disappear immediately when playback reaches their tail
        if (noteData.Type is NoteType.Hold && currentBeat >= noteEndBeat) continue;

        float headOffsetPx = state.WindowData.SpeedSteps.GetVisualOffset(
          currentBeat, noteStartBeat
        ) * pixelsPerBeat * viewportScale;

        // Notes beyond viewport: all subsequent are even further (sorted by StartBeat)
        if (headOffsetPx > viewportLengthPx + offScreenMarginPx) break;

        float tailOffsetPx = (noteData.Length == 0 || noteData.Type is not NoteType.Hold)
          ? headOffsetPx
          : state.WindowData.SpeedSteps.GetVisualOffset(currentBeat, noteEndBeat) * pixelsPerBeat * viewportScale;

        if (!state.NoteVisualMap.TryGetValue(noteData, out var noteVisual))
          noteVisual = SpawnNote(state, noteData);

        noteVisual?.Modulate = noteData.IsEvaluated ? NOTE_COLOR_EVALUATED : NOTE_COLOR_DEFAULT;

        PositionNoteVisual(
          side, noteData, noteVisual, headOffsetPx, tailOffsetPx, state
        );

        noteData.LastSeenFrameSessionToken = state.FrameSessionToken;
      }
    }

    // Return off-screen visuals to pool
    CollectStaleNoteVisuals(state);

    state.FrameSessionToken++;
    state.LastBeat = currentBeat;
  }

  // =============================================
  // Cursor Synchronization
  // =============================================

  private static int SyncCursorBackward(
    WindowNoteState state,
    NoteSide side,
    int cursor,
    double currentBeat,
    float pixelsPerBeat,
    float viewportScale,
    float offScreenMarginPx
  )
  {
    state.WindowData.Notes.MaxEndBeats.TryGetValue(side, out double[]? maxEndBeats);
    if (maxEndBeats is null)
    {
      GD.PushWarning($"[NoteController] MaxEndBeats is not exist for side {side}.");
      return cursor;
    }

    if (cursor <= 0)
      return 0;


    int lo = 0, hi = cursor - 1, result = cursor;
    while (lo <= hi)
    {
      int mid = (lo + hi) / 2;
      float distancePx = state.WindowData.SpeedSteps.GetVisualOffset(
        currentBeat, maxEndBeats[mid]
      ) * pixelsPerBeat * viewportScale;

      if (distancePx >= -offScreenMarginPx)
      {
        result = mid;
        hi = mid - 1;
      }
      else
      {
        lo = mid + 1;
      }
    }

    return result;
  }

  private static int SyncCursorForward(
    WindowNoteState state,
    List<NoteData> noteList,
    int cursor,
    double currentBeat,
    float pixelsPerBeat,
    float viewportScale,
    float offScreenMarginPx
  )
  {
    while (cursor < noteList.Count)
    {
      var noteData = noteList[cursor];

      double noteEndBeat = noteData.Type is NoteType.Hold
        ? noteData.StartBeat.AbsoluteValue + noteData.Length
        : noteData.StartBeat.AbsoluteValue;

      float distancePx =
        state.WindowData.SpeedSteps.GetVisualOffset(currentBeat, noteEndBeat) * pixelsPerBeat * viewportScale;

      if (distancePx < -offScreenMarginPx) cursor++;
      else break;
    }

    return cursor;
  }
}