using System;
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
    var uid = UniqueIDGenerator.Generate(CurrentIDSeed);
    CurrentIDSeed++;
    return uid;
  }

  /// <summary>
  /// Synchronizes the next ID seed with an existing ID to prevent collisions.
  /// </summary>
  public void SyncMaxIDSeed(string ID)
  {
    if (string.IsNullOrEmpty(ID) || ID.Length != 6) return;

    long seed = UniqueIDGenerator.Decode(ID);
    if (seed <= 0) return;

    CurrentIDSeed = Math.Max(CurrentIDSeed, seed);
  }

  // ==========================================
  // Factory Methods
  // ==========================================

  public BPMStop CreateBPMStop(BeatTime startBeat, float bpm, int signatureNum, int signatureDen) => new()
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
}
