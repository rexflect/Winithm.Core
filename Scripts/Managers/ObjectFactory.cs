using System;
using Godot;
using Winithm.Core.Common;
using Winithm.Core.Data;


namespace Winithm.Core.Managers;

public class ObjectFactory
{
  public long CurrentIDSeed { get; private set; } = 0;

  public readonly BeatTime DEFAULT_WINDOW_LIFECYCLE_DURATION = new(15, 0, 0);

  /// <summary>
  /// Generates a new unique identifier.
  /// </summary>
  public string GenerateUID()
  {
    var uid = Generate(CurrentIDSeed);
    CurrentIDSeed++;
    return uid;
  }

  /// <summary>
  /// Synchronizes the next ID seed with an existing ID to prevent collisions.
  /// </summary>
  public void SyncMaxIDSeed(string ID)
  {
    if (string.IsNullOrEmpty(ID) || ID.Length != 6)
    {
      GD.PushError("[ObjectFactory] Invalid ID format");
      return;
    }

    long seed = Decode(ID);
    if (seed <= 0)
    {
      GD.PushError("[ObjectFactory] Invalid ID seed");
      return;
    }

    CurrentIDSeed = Math.Max(CurrentIDSeed, seed);
  }

  // ==========================================
  // Factory Methods
  // ==========================================

  public static BPMStop CreateBPMStop(BeatTime startBeat, float bpm, int signatureNum, int signatureDen) => new()
  {
    StartBeat = startBeat,
    BPM = bpm,
    TimeSignatureNum = signatureNum,
    TimeSignatureDen = signatureDen
  };

  public OverlayData CreateOverlay() => new() { ID = GenerateUID() };
  public GroupData CreateGroup() => new() { ID = GenerateUID() };
  public ThemeChannelData CreateThemeChannel() => new() { ID = GenerateUID() };


  public WindowData CreateWindow(BeatTime startBeat)
  {
    var current = new WindowData
    {
      ID = GenerateUID()
    };

    current.SpeedSteps.AddSpeedStep(CreateSpeedStep(startBeat, 1.0f));
    current.SpeedSteps.AddSpeedStep(
      CreateSpeedStep(startBeat + DEFAULT_WINDOW_LIFECYCLE_DURATION, 1.0f)
    );

    return current;
  }
  public NoteData CreateNote(
    BeatTime startBeat,
    NoteType type = NoteType.Tap,
    double length = 0,
    float x = 0.5f,
    float width = 0.5f,
    int fakeType = 0
  )
    => new()
    {
      ID = GenerateUID(),
      StartBeat = startBeat,
      Type = type,
      Length = length,
      X = x,
      Width = width,
      FakeType = fakeType
    };

  public SpeedStepData CreateSpeedStep(BeatTime startBeat, float multiplier) =>
    new()
    {
      ID = GenerateUID(),
      StartBeat = startBeat,
      Multiplier = multiplier
    };

  public EventData CreateStoryboardEvent(BeatTime startBeat, AnyValue ToValue) =>
    new()
    {
      ID = GenerateUID(),
      StartBeat = startBeat,
      To = ToValue
    };

  private const string CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

  /// <summary>Generates a unique 6-char ID using Incremental Base-62 encoding.</summary>
  public static string Generate(long? seed = null)
  {
    long currentSeed = seed ?? 0;
    Span<char> idChars = stackalloc char[6];
    long value = Math.Abs(currentSeed);

    // Encode seed value into Base-62 string
    for (int i = 5; i >= 0; i--)
    {
      idChars[i] = CHARS[(int)(value % 62)];
      value /= 62;
    }

    return new string(idChars);
  }

  /// <summary>Decodes a 6-char Base-62 ID back into its original integer seed.</summary>
  public static long Decode(string uniqueId)
  {
    if (string.IsNullOrEmpty(uniqueId) || uniqueId.Length != 6) return 0;

    long value = 0;
    for (int i = 0; i < 6; i++)
    {
      int charVal = CHARS.IndexOf(uniqueId[i]);
      if (charVal < 0) return 0; // Invalid character
      value = value * 62 + charVal;
    }
    return value;
  }
}
