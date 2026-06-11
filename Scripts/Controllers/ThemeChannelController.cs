using Godot;
using System.Collections.Generic;
using Winithm.Core.Data;
using Winithm.Core.Common;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

[Tool]
public partial class ThemeChannelController : Node
{
  private ThemeChannelManager _themeManager;
  private readonly Dictionary<string, (double LastBeat, Color Color, float NoteAlpha)> _lastStates = [];

  public void Initialize(ThemeChannelManager manager)
  {
    _themeManager = manager ?? new();

    foreach (var tc in _themeManager)
    {
      _lastStates[tc.ID] = (-1f, new(tc.InitR, tc.InitG, tc.InitB, tc.InitA), tc.InitNoteA);
    }
  }

  public bool HasThemeChannel(string id) => _themeManager.ContainsThemeChannel(id);

  public (Color WindowColor, float NoteA)? GetThemeColor(string id, double currentBeat)
  {
    var stateVal = _lastStates[id];
    if (Mathf.Abs((float)(stateVal.LastBeat - currentBeat)) <= 0.0001f)
      return (stateVal.Color, stateVal.NoteAlpha);

    return ForceGetThemeColor(id, currentBeat, false);
  }

  public (Color WindowColor, float NoteA)? ForceGetThemeColor(
    string id, double currentBeat, bool _force = true
  )
  {
    if (string.IsNullOrEmpty(id) || !_themeManager.ContainsThemeChannel(id))
      return null;

    var tc = _themeManager.GetThemeChannel(id);

    float r = EvaluateProperty(tc, StoryboardProperty.ColorR, currentBeat, tc.InitR);
    float g = EvaluateProperty(tc, StoryboardProperty.ColorG, currentBeat, tc.InitG);
    float b = EvaluateProperty(tc, StoryboardProperty.ColorB, currentBeat, tc.InitB);
    float a = EvaluateProperty(tc, StoryboardProperty.ColorA, currentBeat, tc.InitA);
    float noteA = EvaluateProperty(tc, StoryboardProperty.NoteA, currentBeat, tc.InitNoteA);

    Color color = new(r, g, b, a);
    _lastStates[id] = (currentBeat, color, noteA);

    return (color, noteA);
  }

  private float EvaluateProperty(ThemeChannelData tc, StoryboardProperty prop, double beat, float defaultValue)
  {
    if (tc.StoryboardEvents == null || !tc.StoryboardEvents.TryGetValue(prop, out _)) 
      return defaultValue;

    return tc.StoryboardEvents.Evaluate(prop, beat, new AnyValue(defaultValue)).X;
  }
}

