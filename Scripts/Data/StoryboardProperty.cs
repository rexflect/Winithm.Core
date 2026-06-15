namespace Winithm.Core.Data;

/// <summary>
/// Properties that can be animated via Storyboard events.
/// </summary>
public enum StoryboardProperty
{
  Custom = 0,

  // Transform
  X,
  Y,
  Scale,
  ScaleX,
  ScaleY,
  Rotation,
  ColorR,
  ColorG,
  ColorB,
  ColorA,
  Alpha,
  NoteA,
  Speed,
  Title
}

public static class StoryboardPropertyExtension
{
  public static StoryboardProperty ParseEventProperty(string prop)
  {
    return prop switch
    {
      "Move_X" => StoryboardProperty.X,
      "Move_Y" => StoryboardProperty.Y,
      "Scale" => StoryboardProperty.Scale,
      "Scale_X" => StoryboardProperty.ScaleX,
      "Scale_Y" => StoryboardProperty.ScaleY,
      "Rotation" => StoryboardProperty.Rotation,
      "Color_R" => StoryboardProperty.ColorR,
      "Color_G" => StoryboardProperty.ColorG,
      "Color_B" => StoryboardProperty.ColorB,
      "Color_A" => StoryboardProperty.ColorA,
      "Alpha" => StoryboardProperty.Alpha,
      "Note_A" => StoryboardProperty.NoteA,
      "Title" => StoryboardProperty.Title,
      "Speed" => StoryboardProperty.Speed,
      _ => StoryboardProperty.Custom,
    };

  }

  public static string FormatEventProperty(StoryboardProperty type, string customProperty)
  {
    if (type is StoryboardProperty.Custom) return customProperty;
    return type switch
    {
      StoryboardProperty.X => "Move_X",
      StoryboardProperty.Y => "Move_Y",
      StoryboardProperty.Scale => "Scale",
      StoryboardProperty.ScaleX => "Scale_X",
      StoryboardProperty.ScaleY => "Scale_Y",
      StoryboardProperty.Rotation => "Rotation",
      StoryboardProperty.ColorR => "Color_R",
      StoryboardProperty.ColorG => "Color_G",
      StoryboardProperty.ColorB => "Color_B",
      StoryboardProperty.ColorA => "Color_A",
      StoryboardProperty.Alpha => "Alpha",
      StoryboardProperty.NoteA => "Note_A",
      StoryboardProperty.Title => "Title",
      StoryboardProperty.Speed => "Speed",
      _ => customProperty,
    };

  }
}
