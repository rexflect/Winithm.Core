using Godot;
using System;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

public partial class NoteController
{
  // =============================================
  // Note Lifecycle (Miss + Auto-fire)
  // =============================================

  /// <summary>
  /// Per-frame: auto-hits ghost/autoplay notes, fires OnDragReady for Drag notes
  /// in judgement zone, and fires OnNoteMiss for notes past the timing window.
  /// </summary>
  private void EvaluateNoteLifecycle(
    string windowId,
    WindowNoteState state,
    NoteSide side,
    double currentBeat,
    bool isBackward)
  {
    if (_metronome is null)
    {
      GD.PushWarning("[NoteController] Metronome or NotePool is not initialized.");
      return;
    }

    if (isBackward)
    {
      state.AutoFireSessionToken++;
      state.EvalCursors[side] = Math.Min(state.EvalCursors[side], state.RenderCursors[side]);
      return;
    }

    double dragWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Good];
    double hoverWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Good];
    double missWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Miss];
    int evalCursor = state.EvalCursors[side];
    var noteList = state.WindowData.Notes[side];

    while (evalCursor < noteList?.Count)
    {
      var noteData = noteList[evalCursor];

      // Note is in the future
      if (noteData.StartBeat.AbsoluteValue > currentBeat) break;

      // Skip indicator notes
      if (noteData.Type is NoteType.Indicator) { evalCursor++; continue; }

      // Unbounded note is skipped
      if (!noteData.IsLifecycleBounded) { evalCursor++; continue; }

      // Determine if note should be auto-hit
      bool isAutoHittable = (Autoplay && !noteData.IsMutedGhost) || noteData.IsLoudGhost;

      if (isAutoHittable)
      {
        // Skip if already fired in this session
        if (noteData.AutoFiredSessionToken == state.AutoFireSessionToken) { evalCursor++; continue; }
      }
      else
      {
        // Player evaluation uses traditional state
        if (noteData.IsEvaluated) { evalCursor++; continue; }

        // If hold note is not evaluated, add to active holds
        if (noteData.IsHoldActive)
        {
          state.ActiveHolds.Add(noteData);

          evalCursor++; continue;
        }
      }


      double elapsedMs = _metronome.ToDeltaMilliSeconds(
        noteData.StartBeat.AbsoluteValue, currentBeat
      );

      if (isAutoHittable && elapsedMs >= 0f)
      {
        noteData.AutoFiredSessionToken = state.AutoFireSessionToken;

        if (noteData.Type is NoteType.Hold)
        {
          noteData.IsHoldActive = true;
          state.ActiveHolds.Add(noteData);
        }
        OnAutoHit?.Invoke(windowId, noteData);
        evalCursor++;
        continue;
      }

      // Muted ghost: skip without evaluation
      if (noteData.IsMutedGhost && elapsedMs >= 0f)
      {
        evalCursor++;
        continue;
      }

      // Drag notes: notify when inside judgement zone
      if (!state.WindowData.UnFocus
          && noteData.Type is NoteType.Drag
          && noteData.IsHittable
          && elapsedMs >= 0
          && elapsedMs <= dragWindowMs
      )
      {
        OnDragReady?.Invoke(windowId, noteData, elapsedMs);
      }

      // Hover notes: notify when inside judgement zone
      if (!state.WindowData.UnFocus
          && noteData.Type is NoteType.Hover
          && noteData.IsHittable
          && elapsedMs >= 0
          && elapsedMs <= hoverWindowMs
      )
      {
        OnHoverReady?.Invoke(windowId, noteData, elapsedMs);
      }

      // Miss: exceeded timing window
      if (!Autoplay && elapsedMs > missWindowMs)
      {
        if (noteData.IsHittable) OnNoteMiss?.Invoke(windowId, noteData);
        evalCursor++;
      }
      else
      {
        break; // Within timing window, waiting for player input
      }
    }

    state.EvalCursors[side] = evalCursor;
  }

  private void ProcessActiveHoldNotes(string windowId, WindowNoteState state, double currentBeat)
  {
    state.PendingHoldRemovals.Clear();

    foreach (var holdNote in state.ActiveHolds)
    {
      double holdStartBeat = holdNote.StartBeat.AbsoluteValue;
      double holdEndBeat = holdStartBeat + holdNote.Length;

      // Defensive reset: playback rewound before hold start
      if (currentBeat < holdStartBeat && (double.IsNaN(holdNote.HoldStartResult.OffsetMs) || Autoplay))
      {
        holdNote.IsHoldActive = false;
        state.PendingHoldRemovals.Add(holdNote);
        continue;
      }

      // Hold tail reached: finalize scoring
      if (currentBeat >= holdEndBeat)
      {
        if (holdNote.IsHittable && !Autoplay && !holdNote.IsEvaluated)
        {
          OnActiveHoldEnded?.Invoke(windowId, holdNote);
        }

        holdNote.IsHoldActive = false;
        state.PendingHoldRemovals.Add(holdNote);
        continue;
      }

      // Already judged: stop tracking
      if (holdNote.IsEvaluated)
      {
        holdNote.IsHoldActive = false;
        state.PendingHoldRemovals.Add(holdNote);
        continue;
      }

      // Hold still active: emit sustain tick
      OnActiveHoldTick?.Invoke(windowId, holdNote);
    }

    foreach (var holdNote in state.PendingHoldRemovals)
    {
      state.ActiveHolds.Remove(holdNote);
    }
  }
}