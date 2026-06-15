using Winithm.Core.Managers;

namespace Winithm.Core.Interfaces;

/// <summary>
/// Defines an object that supports dynamic runtime modifications via Storyboards.
/// <typeparam name="TProp">The property type (e.g. StoryboardProperty or string)</typeparam>
/// </summary>
public interface IStoryboardable<TProp>
  where TProp : notnull
{
  /// <summary>
  /// The mapped event sequences per property. Null by default to save memory.
  /// Lists must be sorted by StartBeat.
  /// </summary>
  StoryboardManager<TProp> StoryboardEvents { get; set; }
}

