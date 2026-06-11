using System;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// HUD component data with optional storyboard animations.
/// </summary>
public class ComponentData : IStoryboardable<StoryboardProperty>
{
  public event Action<ComponentData> OnUpdated;

  private float _initX = 0f;
  public float InitX { get => _initX; set { if (_initX == value) return; _initX = value; OnUpdated?.Invoke(this); } }
  private float _initY = 0f;
  public float InitY { get => _initY; set { if (_initY == value) return; _initY = value; OnUpdated?.Invoke(this); } }
  private float _initRotate = 1f;
  public float InitRotate { get => _initRotate; set { if (_initRotate == value) return; _initRotate = value; OnUpdated?.Invoke(this); } }
  private float _initScale = 1f;
  public float InitScale { get => _initScale; set { if (_initScale == value) return; _initScale = value; OnUpdated?.Invoke(this); } }
  private float _initAlpha = 1f;
  public float InitAlpha { get => _initAlpha; set { if (_initAlpha == value) return; _initAlpha = value; OnUpdated?.Invoke(this); } }
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
