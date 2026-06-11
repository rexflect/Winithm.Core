using System;
using Godot;

namespace Winithm.Core.Common;

public static class LayerUtils
{
  public static int ComposeLayerIndex(int layer, int subLayer)
  {
    layer = Math.Clamp(layer, -999, 999);
    subLayer = Math.Clamp(subLayer, 0, 999_999);

    int digits = subLayer == 0 ? 0 : (int)Math.Floor(Math.Log10(subLayer) + 1);
    int paddedSubLayer = subLayer * (int)Math.Pow(10, 6 - digits);

    return layer * 1_000_000 + paddedSubLayer;
  }

  public static (int layer, int subLayer) DecomposeLayerIndex(int index)
  {
    return (index / 1_000_000, index % 1_000_000);
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