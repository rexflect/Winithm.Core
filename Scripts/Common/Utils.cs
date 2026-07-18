using System;
using Godot;

namespace Winithm.Core.Common;

public static class LayerUtils
{
  public static int ComposeLayerIndex(int layer, int subLayer)
  {
    layer = Math.Clamp(layer, -3, 3);
    subLayer = Math.Clamp(subLayer, 0, 999);

    int digits = subLayer == 0 ? 0 : (int)Math.Floor(Math.Log10(subLayer) + 1);
    int paddedSubLayer = subLayer * (int)Math.Pow(10, 3 - digits);

    return layer * 1_000 + paddedSubLayer;
  }

  public static (int layer, int subLayer) DecomposeLayerIndex(int index)
  {
    return (index / 1_000, index % 1_000);
  }
}

public static class AudioStreamUtils
{
  public static void ClampStreamLoop(AudioStream stream)
  {
    if (stream is AudioStreamWav sample)
      sample.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
    else if (stream is AudioStreamOggVorbis ogg)
      ogg.Loop = false;
    else if (stream is AudioStreamMP3 mp3)
      mp3.Loop = false;
  }
}

public static class OSDisplayUtils
{
  /// <summary>
  /// Get the DPI scale for a specific screen.
  /// </summary>
  /// <param name="screenIndex">The index of the screen. -1 for the current screen.</param>
  /// <returns>The DPI scale.</returns>
  public static float GetDPIScale(int screenIndex = -1)
  {
    int dpi = DisplayServer.ScreenGetDpi(screenIndex);
    return dpi > 0 ? dpi / 96f : 1.0f;
  }

  public static float GetReferenceResolutionScale(Vector2 size)
  {
    return MathF.Min(
      size.X / Constants.Visual.DESIGN_RESOLUTION.X,
      size.Y / Constants.Visual.DESIGN_RESOLUTION.Y
    );
  }
}

public static class ColorUtils
{
  public static bool IsLight(Color color)
  {
    float r = LinearizeChannel(color.R);
    float g = LinearizeChannel(color.G);
    float b = LinearizeChannel(color.B);

    float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
    return luminance > 0.179f;
  }

  public static Color AdjustBrightness(Color color, float offset)
  {
    // Convert sRGB → HSV, shift V, convert back
    color.ToHsv(out float h, out float s, out float v);
    v = Math.Clamp(v + offset, 0f, 1f);
    return Color.FromHsv(h, s, v, color.A);
  }

  private static float LinearizeChannel(float c)
  {
    return c <= 0.04045f
        ? c / 12.92f
        : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
  }
}