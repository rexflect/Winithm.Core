using Godot;
using System.Collections.Generic;
using Winithm.Core.Behaviors;
using Winithm.Core.Data;

namespace Winithm.Core.Controllers;

public partial class NoteController
{
  public struct NoteGlobalTransformInfo
  {
    public Vector2 Position;
    public float Rotation;
    public float NoteWidth;
    public Vector2 PlayerAreaSize;
  }

  /// <summary>
  /// Returns a read-only dictionary of registered window states.
  /// </summary>
  public IReadOnlyDictionary<string, WindowNoteState> GetRegisteredWindowStates() => WindowStates;

  public bool TryGetNoteGlobalTransformInfo(string windowId, NoteData note, out NoteGlobalTransformInfo? info)
  {
    info = null;

    if (!WindowStates.TryGetValue(windowId, out var state))
    {
      GD.PushWarning($"[NoteController] Window {windowId} not found or window is not registered or is destroyed.");
      return false;
    }

    if (state.NoteVisualMap.TryGetValue(note, out var noteVisual) && IsInstanceValid(noteVisual))
    {
      float headHeight = noteVisual.NoteSize
        * Mathf.Min(noteVisual.PlayerAreaSize.X, noteVisual.PlayerAreaSize.Y)
        * Note.NOTE_HEAD_HEIGHT_RATIO;

      var visualTransform = noteVisual.GetGlobalTransform();
      var globalCenter = visualTransform * new Vector2(0, -headHeight * 0.5f);

      info = new NoteGlobalTransformInfo()
      {
        Position = globalCenter,
        Rotation = visualTransform.Rotation,
        NoteWidth = noteVisual.Width,
        PlayerAreaSize = noteVisual.PlayerAreaSize,
      };

      return true;
    }

    if (!state.WindowData.Notes.TryGetNoteSide(note, out var noteSide))
    {
      GD.PushWarning($"[NoteController] Note {note.ID} not found in window {windowId}.");
      return false;
    }

    var playerAreaSize = state.WindowVisual.PlayerAreaSize;
    var windowSize = state.WindowVisual.WindowSize;
    float viewportScale = ComputeViewportScale(playerAreaSize);
    var scaledWindowSize = windowSize * viewportScale;

    float noteWidth = IsVerticalSide(noteSide)
      ? scaledWindowSize.X * note.Width
      : scaledWindowSize.Y * note.Width;

    float lateralPosition = note.X * (1f - note.Width) + note.Width / 2f;
    float headOffsetPx = 0f;

    var (localPosition, rotationDegrees) = ComputeNoteLocalPositionAndRotation(
      noteSide, scaledWindowSize, lateralPosition, headOffsetPx
    );

    float fallbackHeadHeight = PlayerNoteSize
      * Mathf.Min(playerAreaSize.X, playerAreaSize.Y)
      * Note.NOTE_HEAD_HEIGHT_RATIO;

    var noteTransform = new Transform2D(Mathf.DegToRad(rotationDegrees), localPosition);

    var parentLayer = GetNoteParentLayer(state, note);
    if (!IsInstanceValid(parentLayer))
    {
      GD.PushWarning($"[NoteController] Parent layer for note {note.ID} not found in window {windowId}.");
      return false;
    }

    var parentTransform = parentLayer.GetGlobalTransform();
    var globalPos = parentTransform * noteTransform * new Vector2(0, -fallbackHeadHeight * 0.5f);

    info = new NoteGlobalTransformInfo()
    {
      Position = globalPos,
      Rotation = parentTransform.Rotation + Mathf.DegToRad(rotationDegrees),
      NoteWidth = noteWidth,
      PlayerAreaSize = playerAreaSize,
    };

    return true;
  }

  public int GetTotalComboPassedInActivingWindows(double currentBeat)
  {
    int total = 0;
    foreach (var state in WindowStates.Values)
    {
      var comboBeats = state.WindowData.Notes.ComboEventBeats;
      var comboPrefix = state.WindowData.Notes.ComboPrefixSum;

      if (comboBeats is null || comboBeats.Length == 0) continue;

      int left = 0, right = comboBeats.Length - 1;
      int best = -1;

      while (left <= right)
      {
        int mid = left + (right - left) / 2;
        if (comboBeats[mid] <= currentBeat)
        {
          best = mid;
          left = mid + 1;
        }
        else
        {
          right = mid - 1;
        }
      }

      if (best >= 0)
      {
        total += comboPrefix[best];
      }
    }
    return total;
  }
}
