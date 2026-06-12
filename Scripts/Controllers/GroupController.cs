using Godot;
using System.Collections.Generic;
using Winithm.Core.Data;
using Winithm.Core.Common;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

[Tool]
public partial class GroupController : Node
{
  private GroupManager _groupManager;
  private readonly Dictionary<string, Node2D> _groupNodes = [];
  private readonly Dictionary<string, double> _lastUpdateBeat = [];

  public void Initialize(GroupManager manager)
  {
    _groupManager = manager ?? new();

    foreach (var node in _groupNodes.Values)
    {
      if (IsInstanceValid(node)) node.QueueFree();
    }
    _groupNodes.Clear();
    _lastUpdateBeat.Clear();

    foreach (var g in _groupManager)
    {
      _lastUpdateBeat[g.ID] = -1f;

      var node = new Node2D
      {
        Name = string.IsNullOrEmpty(g.ID) ? "Group" : g.ID,
        Position = new(g.InitX, g.InitY),
        Scale = new(g.InitScaleX, g.InitScaleY),
        RotationDegrees = g.InitRotation
      };

      _groupNodes[g.ID] = node;
    }

    foreach (var g in _groupManager)
    {
      var node = _groupNodes[g.ID];
      if (
        !string.IsNullOrEmpty(g.ParentGroupID) && _groupNodes.TryGetValue(g.ParentGroupID, out var parentNode)
      )
      {
        parentNode.AddChild(node);
      }
      else
      {
        AddChild(node);
      }
    }
  }

  public Node2D GetGroupNode(string id, double currentBeat)
  {
    if (Mathf.Abs((float)(_lastUpdateBeat[id] - currentBeat)) <= 0.0001f)
      return _groupNodes[id];

    return ForceGetGroupNode(id, currentBeat, false);
  }

  public Node2D ForceGetGroupNode(string id, double currentBeat, bool _force = true)
  {
    if (string.IsNullOrEmpty(id) || !_groupManager.ContainsGroup(id))
      return null;

    var g = _groupManager.GetGroup(id);
    if (!string.IsNullOrEmpty(g.ParentGroupID))
    {
      if (_force) ForceGetGroupNode(g.ParentGroupID, currentBeat);
      else GetGroupNode(g.ParentGroupID, currentBeat);
    }

    var node = _groupNodes[id];

    float x = EvaluateProperty(g, StoryboardProperty.X, currentBeat, g.InitX);
    float y = EvaluateProperty(g, StoryboardProperty.Y, currentBeat, g.InitY);
    float scale = EvaluateProperty(g, StoryboardProperty.Scale, currentBeat, 1f);
    float scaleX = EvaluateProperty(g, StoryboardProperty.ScaleX, currentBeat, g.InitScaleX);
    float scaleY = EvaluateProperty(g, StoryboardProperty.ScaleY, currentBeat, g.InitScaleY);
    float rotation = EvaluateProperty(g, StoryboardProperty.Rotation, currentBeat, g.InitRotation);

    node.Position = new(x, y);
    node.Scale = new(scale * scaleX, scale * scaleY);
    node.RotationDegrees = rotation;

    _lastUpdateBeat[id] = currentBeat;

    return node;
  }

  private float EvaluateProperty(GroupData g, StoryboardProperty prop, double beat, float defaultValue)
  {
    if (g.StoryboardEvents is null || !g.StoryboardEvents.TryGetValue(prop, out _))
      return defaultValue;

    return g.StoryboardEvents.Evaluate(prop, beat, new(defaultValue)).X;
  }
}

