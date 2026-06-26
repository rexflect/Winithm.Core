using System;
using System.Collections.Generic;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Main rendering container (lane) data including transform and sub-managers.
/// </summary>
public class WindowData : IStoryboardable<StoryboardProperty>, IDeepCloneableUID<WindowData>
{
  public event Action<WindowData>? OnLifeCycleChanged;
  public event Action<WindowData>? OnUnFocusChanged;
  public event Action<WindowData>? OnUnResponsiveChanged;
  public event Action<WindowData>? OnUpdated;

  public string ID = "";
  public string Name { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;
  public string Title { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;
  public int Layer { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;
  public int SubLayer { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0;
  public float AnchorX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0.5f;
  public float AnchorY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0.5f;
  public string GroupID { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;
  public string ThemeChannelID { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = string.Empty;

  // Window Flags
  public bool Borderless { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = false;
  public bool UnFocus { get; set { if (field == value) return; field = value; OnUnFocusChanged?.Invoke(this); } } = false;

  // Transform init values
  public float InitX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 640f;
  public float InitY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 360f;
  public float InitScaleX { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 300f;
  public float InitScaleY { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 500f;

  // Color init values
  public float InitR { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;
  public float InitG { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;
  public float InitB { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 0f;
  public float InitA { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;
  public float InitNoteA { get; set { if (field == value) return; field = value; OnUpdated?.Invoke(this); } } = 1f;

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();
  public SpeedStepManager SpeedSteps { get; set; } = new();
  public NoteManager Notes { get; set; } = new();

  public BeatTime StartBeat = BeatTime.NaN;
  public BeatTime EndBeat = BeatTime.NaN;

  /// <summary>
  /// Gets or sets whether the window transitions to an unresponsive state.
  /// </summary>
  public bool Unresponsive { get; set { if (field == value) return; field = value; OnUnResponsiveChanged?.Invoke(this); } } = false;

  /// <summary>
  /// List of focusable periods. Each period has a Start and End beat.
  /// A period with End == double.NaN means it is currently active (not yet ended).
  /// </summary>
  public List<(double Start, double End)> FocusablePeriods = [];

  /// <summary>
  /// Pre-computed animation timestamps.
  /// </summary>
  public double StartInStartBeat = double.NaN;
  public double StartInEndBeat = double.NaN;
  public double EndOutStartBeat = double.NaN;
  public double EndOutEndBeat = double.NaN;
  public double UnresponsiveStartBeat = double.NaN;
  public double UnresponsiveEndBeat = double.NaN;

  public WindowData()
  {
    StoryboardEvents.OnUpdated += BubbleStoryboard;
    SpeedSteps.OnUpdated += BubbleSpeedStep;
    Notes.OnLifeCycleChanged += BubbleNoteLifeCycle;
    Notes.OnUpdated += BubbleNote;

    // Note requires reference to WindowData for Focus/Close boundary logic
    Notes.SetWindowData(this);
  }

  /// <summary>
  /// Pre-computes animation-related values.
  /// Call when TimeManager.Instance.IsReady
  /// </summary>
  public void PreComputeAnimation(Metronome metronome)
  {
    double startBeatInSecs = metronome.ToSeconds(StartBeat);
    double endBeatInSecs = metronome.ToSeconds(EndBeat);

    StartInStartBeat = StartBeat.AbsoluteValue;
    StartInEndBeat = metronome.ToBeat(startBeatInSecs + 0.2);

    EndOutStartBeat = EndBeat.AbsoluteValue;
    EndOutEndBeat = metronome.ToBeat(endBeatInSecs + 0.2);
  }

  /// <summary>
  /// Computes animation-related values when the window is unresponsive.
  /// Call when TimeManager.Instance.IsReady
  /// </summary>
  public void ComputeAnimationWhenUnresponsive(Metronome metronome)
  {
    double endBeatInSecs = metronome.ToSeconds(EndBeat);
    double missTimingWindowMs = Constants.HitResult.TimmingWindowMs[HitResultType.Miss];

    double secsAfterCloseNoteMissed = endBeatInSecs + (missTimingWindowMs / 1000.0);

    UnresponsiveStartBeat = EndBeat.AbsoluteValue;
    UnresponsiveEndBeat = metronome.ToBeat(secsAfterCloseNoteMissed + 0.5);
    EndOutStartBeat = metronome.ToBeat(secsAfterCloseNoteMissed + 1);
    EndOutEndBeat = metronome.ToBeat(secsAfterCloseNoteMissed + 1.2);
  }

  public WindowData DeepCloner(ObjectFactory objectFactory, BeatTime? offset)
  {
    var cloned = new WindowData();

    // Unsubscribe default sub-manager bubbling created in constructor
    // before replacing with cloned sub-managers
    cloned.StoryboardEvents.OnUpdated -= cloned.BubbleStoryboard;
    cloned.SpeedSteps.OnUpdated -= cloned.BubbleSpeedStep;
    cloned.Notes.OnLifeCycleChanged -= cloned.BubbleNoteLifeCycle;
    cloned.Notes.OnUpdated -= cloned.BubbleNote;

    cloned.ID = objectFactory.GenerateUID();
    cloned.Name = Name;
    cloned.Title = Title;
    cloned.Layer = Layer;
    cloned.AnchorX = AnchorX;
    cloned.AnchorY = AnchorY;
    cloned.GroupID = GroupID;
    cloned.ThemeChannelID = ThemeChannelID;
    cloned.Borderless = Borderless;
    cloned.UnFocus = UnFocus;
    cloned.InitX = InitX;
    cloned.InitY = InitY;
    cloned.InitScaleX = InitScaleX;
    cloned.InitScaleY = InitScaleY;
    cloned.InitR = InitR;
    cloned.InitG = InitG;
    cloned.InitB = InitB;
    cloned.InitA = InitA;
    cloned.InitNoteA = InitNoteA;
    cloned.StartBeat = StartBeat + (offset ?? BeatTime.Zero);
    cloned.EndBeat = EndBeat + (offset ?? BeatTime.Zero);

    // Clone sub-managers
    cloned.StoryboardEvents = StoryboardEvents?.DeepCloner(objectFactory, offset) ?? new StoryboardManager<StoryboardProperty>();
    cloned.SpeedSteps = SpeedSteps?.DeepCloner(objectFactory, offset) ?? new SpeedStepManager();
    cloned.Notes = Notes?.DeepCloner(objectFactory, offset) ?? new NoteManager();

    // Re-wire sub-manager bubbling to the new clone
    cloned.StoryboardEvents.OnUpdated += cloned.BubbleStoryboard;
    cloned.SpeedSteps.OnUpdated += cloned.BubbleSpeedStep;
    cloned.Notes.OnLifeCycleChanged += cloned.BubbleNoteLifeCycle;
    cloned.Notes.OnUpdated += cloned.BubbleNote;

    // Bind Note's WindowData reference to the new clone
    cloned.Notes.SetWindowData(cloned);

    return cloned;
  }

  // Named delegates for clean subscribe/unsubscribe in DeepClone
  private void BubbleStoryboard(StoryboardManager<StoryboardProperty> sb) => OnUpdated?.Invoke(this);
  private void BubbleSpeedStep(SpeedStepManager sd)
  {
    if (SpeedSteps.Count == 0)
    {
      StartBeat = BeatTime.Zero;
      EndBeat = BeatTime.Zero;

      OnLifeCycleChanged?.Invoke(this);
      return;
    }

    if (
      SpeedSteps.GetFirst()?.StartBeat != StartBeat ||
      SpeedSteps.GetLast()?.StartBeat != EndBeat
    )
    {
      StartBeat = SpeedSteps.GetFirst()?.StartBeat ?? StartBeat;
      EndBeat = SpeedSteps.GetLast()?.StartBeat ?? StartBeat;

      OnLifeCycleChanged?.Invoke(this);
      return;
    }

    OnUpdated?.Invoke(this);
  }
  private void BubbleNote(NoteManager n) => OnUpdated?.Invoke(this);
  private void BubbleNoteLifeCycle(NoteManager n) => OnLifeCycleChanged?.Invoke(this);
}
