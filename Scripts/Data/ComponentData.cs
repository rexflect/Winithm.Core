using System;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// HUD component data with optional storyboard animations.
/// </summary>
public class ComponentData : IStoryboardable<StoryboardProperty>
{
  public event Action<ComponentData>? OnUpdated;

  public float InitX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;
  public float InitY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;
  public float InitRotate { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
  public float InitScale { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
  public float InitAlpha { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();

  public ComponentData()
  {
    StoryboardEvents.OnUpdated += (sb) => OnUpdated?.Invoke(this);
  }

  public static ComponentType ParseComponentType(string name)
  {
    return name.ToLowerInvariant() switch
    {
      "combo" => ComponentType.Combo,
      "score" => ComponentType.Score,
      "info" => ComponentType.Info,
      "difficulty" => ComponentType.Difficulty,
      _ => ComponentType.Info
    };
  }
}
