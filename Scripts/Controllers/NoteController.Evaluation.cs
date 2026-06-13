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
    if (isBackward)
    {
      state.AutoFireSessionToken++;
      state.EvalCursors[side] = Math.Min(state.EvalCursors[side], state.RenderCursors[side]);
      return;
    }

    double dragWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Bad];
    double missWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Miss];
    int evalCursor = state.EvalCursors[side];
    var noteList = state.WindowData.Notes[side];

    while (evalCursor < noteList.Count)
    {
      NoteData note = noteList[evalCursor];

      bool isAutoHittable = (Autoplay && !note.IsMutedGhost) || note.IsLoudGhost;

      if (isAutoHittable)
      {
        // Skip if already fired in this session
        if (note.AutoFiredSessionToken == state.AutoFireSessionToken) { evalCursor++; continue; }
      }
      else
      {
        // Player evaluation uses traditional state
        if (note.IsEvaluated) { evalCursor++; continue; }
        if (note.IsHoldActive) { evalCursor++; continue; }
      }

      if (note.StartBeat.AbsoluteValue > currentBeat) break;

      double elapsedMs = _metronome.ToDeltaMilliSeconds(
        note.StartBeat.AbsoluteValue, currentBeat
      );

      if (isAutoHittable && elapsedMs >= 0f)
      {
        note.AutoFiredSessionToken = state.AutoFireSessionToken;

        if (note.Type is NoteType.Hold)
        {
          note.IsHoldActive = true;
          state.ActiveHolds.Add(note);
        }
        OnAutoHit?.Invoke(windowId, note);
        evalCursor++;
        continue;
      }

      // Muted ghost: skip without evaluation
      if (note.IsMutedGhost && elapsedMs >= 0f)
      {
        evalCursor++;
        continue;
      }

      // Drag notes: notify when inside judgement zone
      if (note.Type is NoteType.Drag && note.IsHittable
          && elapsedMs >= 0 && elapsedMs <= dragWindowMs)
      {
        OnDragReady?.Invoke(windowId, note, elapsedMs);
      }

      // Miss: exceeded timing window
      if (!Autoplay && elapsedMs > missWindowMs)
      {
        if (note.IsHittable) OnNoteMiss?.Invoke(windowId, note);
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
