using Godot;
using System;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
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
    public required WindowVS WindowVisual;

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

  public void RegisterWindow(string windowId, WindowData windowData, WindowVS windowVisual)
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

  private static bool IsVerticalSide(NoteSide side)
  {
    return side is NoteSide.Top || side is NoteSide.Bottom;
  }

  private static CanvasItem? GetNoteParentLayer(WindowNoteState state, NoteData note)
  {
    return (note.Type is NoteType.Focus)
      ? state.WindowVisual.FocusNoteLayer
      : state.WindowVisual.NoteLayer;
  }

  private static float ComputeViewportScale(Vector2 playerAreaSize)
  {
    return Math.Min(
      playerAreaSize.X / Constants.Visual.DESIGN_RESOLUTION.X,
      playerAreaSize.Y / Constants.Visual.DESIGN_RESOLUTION.Y
    );
  }

  /// <summary>
  /// Computes the local spawn position and rotation (degrees) of a note
  /// based on which side of the window it belongs to.
  /// </summary>
  private static (Vector2 localPosition, float rotationDegrees) ComputeNoteLocalPositionAndRotation(
    NoteSide side,
    Vector2 scaledWindowSize,
    float lateralPosition,
    float headOffsetPx)
  {
    switch (side)
    {
      case NoteSide.Bottom:
        return (new(scaledWindowSize.X * lateralPosition, scaledWindowSize.Y - headOffsetPx), 0f);
      case NoteSide.Top:
        return (new(scaledWindowSize.X * lateralPosition, headOffsetPx), 180f);
      case NoteSide.Right:
        return (new(scaledWindowSize.X - headOffsetPx, scaledWindowSize.Y * lateralPosition), -90f);
      case NoteSide.Left:
        return (new(headOffsetPx, scaledWindowSize.Y * lateralPosition), 90f);
      default:
        return (Vector2.Zero, 0f);
    }
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

  // =============================================
  // Spawn & Pool
  // =============================================

  private Note? SpawnNote(WindowNoteState state, NoteData noteData)
  {
    if (_notePool is null)
    {
      GD.PushWarning("[NoteController] Note pool is not created with Note.tscn.");
      return null;
    }

    var noteVisual = _notePool.Get();
    var parentLayer = GetNoteParentLayer(state, noteData);
    var currentParent = noteVisual.GetParent();

    if (!IsInstanceValid(currentParent))
    {
      parentLayer?.AddChild(noteVisual);
    }
    else if (currentParent != parentLayer)
    {
      noteVisual.Reparent(parentLayer, false);
    }

    parentLayer?.MoveChild(noteVisual, -1);

    state.NoteVisualMap[noteData] = noteVisual;
    return noteVisual;
  }

  /// <summary>Removes a note's visual and returns it to the pool.</summary>
  public void ConsumeNote(string windowId, NoteData note)
  {
    note.IsHoldActive = false;

    if (WindowStates.TryGetValue(windowId, out var state))
    {
      if (state.NoteVisualMap.TryGetValue(note, out var noteVisual))
      {
        note.ConsumedSessionToken = state.ConsumeSessionToken;
        ReturnToPool(noteVisual);
        state.NoteVisualMap.Remove(note);
      }
      state.ActiveHolds.Remove(note);
    }
  }

  private void ReturnToPool(Note noteVisual)
  {
    noteVisual.Visible = false;
    _notePool?.Release(noteVisual);
  }

  private void CollectStaleNoteVisuals(WindowNoteState state)
  {
    state.PendingVisualRemovals.Clear();
    foreach (var note in state.NoteVisualMap.Keys)
    {
      if (note.LastSeenFrameSessionToken != state.FrameSessionToken)
        state.PendingVisualRemovals.Add(note);
    }

    foreach (var note in state.PendingVisualRemovals)
    {
      ReturnToPool(state.NoteVisualMap[note]);
      state.NoteVisualMap.Remove(note);
    }
  }

  // =============================================
  // Note Positioning
  // =============================================

  private void PositionNoteVisual(
    NoteSide side,
    NoteData noteData,
    Note? noteVisual,
    float headOffsetPx,
    float tailOffsetPx,
    WindowNoteState state)
  {
    if (!IsInstanceValid(noteVisual))
    {
      GD.PushWarning("[NoteController] Note visual is not exist to be positioned.");
      return;
    }

    var playerAreaSize = state.WindowVisual.PlayerAreaSize;
    var windowSize = state.WindowVisual.WindowSize;
    float viewportScale = ComputeViewportScale(playerAreaSize);
    var scaledWindowSize = windowSize * viewportScale;

    float headHeight =
      noteVisual.NoteSize * Mathf.Min(
        playerAreaSize.X, playerAreaSize.Y
      ) * Note.NOTE_HEAD_HEIGHT_RATIO;
    float bodyHeight = 0f;

    if (noteData.Type is NoteType.Hold)
    {
      bodyHeight = Mathf.Max(0f, tailOffsetPx - headOffsetPx - headHeight);
      if (headOffsetPx < 0f)
      {
        headOffsetPx = 0f;
        bodyHeight = Mathf.Max(0f, tailOffsetPx - headHeight);
      }
    }

    // Width depends on whether the note sits on a vertical or horizontal edge
    float noteWidth = IsVerticalSide(side)
      ? scaledWindowSize.X * noteData.Width
      : scaledWindowSize.Y * noteData.Width;

    noteVisual.Width = noteWidth;
    noteVisual.NoteSize = PlayerNoteSize;
    noteVisual.PlayerAreaSize = playerAreaSize;
    noteVisual.BodyHeight = bodyHeight;

    var resourcePack = noteData.ResourcePack;
    noteVisual.SetNoteType(noteData.Type, resourcePack);

    // Highlight notes sharing the same start beat (chords)
    ApplyChordHighlight(noteData, noteVisual);

    // Lateral position: Note X is a proportion of the available free space (0 to 1).
    // Left edge = X * (1 - Width). The note is drawn centered at (Left edge + Width/2)
    float lateralPosition = noteData.X * (1f - noteData.Width) + noteData.Width / 2f;

    var (notePosition, noteRotationDegrees) = ComputeNoteLocalPositionAndRotation(
      side, scaledWindowSize, lateralPosition, headOffsetPx
    );
    noteVisual.Position = notePosition;
    noteVisual.RotationDegrees = noteRotationDegrees;

    noteVisual.UpdateVisual();
  }

  private void ApplyChordHighlight(NoteData noteData, Note noteVisual)
  {
    if (!NoteHighlightSimulation)
    {
      noteVisual.SetNoteHighlighting(false);
      return;
    }

    if (_windowManager is null)
    {
      GD.PushWarning("[NoteController] _windowManager is not initilized to highlight chord notes.");
      return;
    }

    double startBeat = noteData.StartBeat.AbsoluteValue;
    if (_windowManager.ChordNoteMap.TryGetValue(startBeat, out var count))
      noteVisual.SetNoteHighlighting(count >= 2);
  }

}