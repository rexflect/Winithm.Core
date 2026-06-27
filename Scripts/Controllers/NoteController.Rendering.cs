using Godot;
using Winithm.Core.Behaviors;
using Winithm.Core.Common;
using Winithm.Core.Data;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

public partial class NoteController
{
  private static bool IsVerticalSide(NoteSide side)
  {
    return side is NoteSide.Top || side is NoteSide.Bottom;
  }

  private static float ComputeViewportScale(Vector2 playerAreaSize)
  {
    return OSDisplayUtils.GetReferenceResolutionScale(playerAreaSize);
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
    state.WindowVisual.AddNoteVisual(noteVisual, noteData);

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

    var windowVisual = state.WindowVisual;
    var playerAreaSize = windowVisual.PlayerAreaSize;

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

    if (!IsInstanceValid(windowVisual.WindowBody))
    {
      GD.PushWarning("[NoteController] Window body is not exist to compute note width.");
      return;
    }

    // Width depends on whether the note sits on a vertical or horizontal edge
    float noteWidth = IsVerticalSide(side)
      ? windowVisual.WindowBody.Size.X * noteData.Width
      : windowVisual.WindowBody.Size.Y * noteData.Width;

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
      side, windowVisual.WindowBody.Size, lateralPosition, headOffsetPx
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