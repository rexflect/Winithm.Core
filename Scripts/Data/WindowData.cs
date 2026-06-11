using System;
using System.Collections.Generic;
using Winithm.Core.Common;
using Winithm.Core.Interfaces;
using Winithm.Core.Managers;

namespace Winithm.Core.Data;

/// <summary>
/// Main rendering container (lane) data including transform and sub-managers.
/// </summary>
public class WindowData : IStoryboardable<StoryboardProperty>, IDeepCloneable<WindowData>
{
  public event Action<WindowData> OnLifeCycleChanged;
  public event Action<WindowData> OnUnFocusChanged;
  public event Action<WindowData> OnUnResponsiveChanged;
  public event Action<WindowData> OnUpdated;

  public string ID;
  private string _name;
  public string Name { get => _name; set { if (_name == value) return; _name = value; OnUpdated?.Invoke(this); } }

  private string _title;
  public string Title { get => _title; set { if (_title == value) return; _title = value; OnUpdated?.Invoke(this); } }

  private int _layer = 0;
  public int Layer { get => _layer; set { if (_layer == value) return; _layer = value; OnUpdated?.Invoke(this); } }

  private int _subLayer = 0;
  public int SubLayer { get => _subLayer; set { if (_subLayer == value) return; _subLayer = value; OnUpdated?.Invoke(this); } }

  private float _anchorX = 0.5f;
  public float AnchorX { get => _anchorX; set { if (_anchorX == value) return; _anchorX = value; OnUpdated?.Invoke(this); } }

  private float _anchorY = 0.5f;
  public float AnchorY { get => _anchorY; set { if (_anchorY == value) return; _anchorY = value; OnUpdated?.Invoke(this); } }

  private string _groupID;
  public string GroupID { get => _groupID; set { if (_groupID == value) return; _groupID = value; OnUpdated?.Invoke(this); } }

  private string _themeChannelID;
  public string ThemeChannelID { get => _themeChannelID; set { if (_themeChannelID == value) return; _themeChannelID = value; OnUpdated?.Invoke(this); } }

  // Window Flags
  private bool _borderless = false;
  public bool Borderless { get => _borderless; set { if (_borderless == value) return; _borderless = value; OnUpdated?.Invoke(this); } }

  private bool _unFocus = false;
  public bool UnFocus { get => _unFocus; set { if (_unFocus == value) return; _unFocus = value; OnUnFocusChanged?.Invoke(this); } }

  // Transform init values
  private float _initX = 640f;
  public float InitX { get => _initX; set { if (_initX == value) return; _initX = value; OnUpdated?.Invoke(this); } }

  private float _initY = 360f;
  public float InitY { get => _initY; set { if (_initY == value) return; _initY = value; OnUpdated?.Invoke(this); } }

  private float _initScaleX = 300f;
  public float InitScaleX { get => _initScaleX; set { if (_initScaleX == value) return; _initScaleX = value; OnUpdated?.Invoke(this); } }

  private float _initScaleY = 500f;
  public float InitScaleY { get => _initScaleY; set { if (_initScaleY == value) return; _initScaleY = value; OnUpdated?.Invoke(this); } }

  // Color init values
  private float _initR = 0f;
  public float InitR { get => _initR; set { if (_initR == value) return; _initR = value; OnUpdated?.Invoke(this); } }

  private float _initG = 0f;
  public float InitG { get => _initG; set { if (_initG == value) return; _initG = value; OnUpdated?.Invoke(this); } }

  private float _initB = 0f;
  public float InitB { get => _initB; set { if (_initB == value) return; _initB = value; OnUpdated?.Invoke(this); } }

  private float _initA = 1f;
  public float InitA { get => _initA; set { if (_initA == value) return; _initA = value; OnUpdated?.Invoke(this); } }

  private float _initNoteA = 1f;
  public float InitNoteA { get => _initNoteA; set { if (_initNoteA == value) return; _initNoteA = value; OnUpdated?.Invoke(this); } }

  public StoryboardManager<StoryboardProperty> StoryboardEvents { get; set; } = new();
  public SpeedStepManager SpeedSteps { get; set; } = new();
  public NoteManager Notes { get; set; } = new();

  public BeatTime StartBeat = BeatTime.NaN;
  public BeatTime EndBeat = BeatTime.NaN;

  /// <summary>
  /// Gets or sets whether the window transitions to an unresponsive state.
  /// </summary>
  private bool _unresponsive = false;
  public bool Unresponsive { get => _unresponsive; set { if (_unresponsive == value) return; _unresponsive = value; OnUnResponsiveChanged?.Invoke(this); } }

  /// <summary>
  /// List of focusable periods. Each period has a Start and End beat.
  /// A period with End == double.NaN means it is currently active (not yet ended).
  /// </summary>
  public List<(double Start, double End)> FocusablePeriods = new();

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

  public WindowData DeepClone(ObjectFactory objectFactory, BeatTime? offset)
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
    cloned.StoryboardEvents = StoryboardEvents?.DeepClone(objectFactory, offset);
    cloned.SpeedSteps = SpeedSteps?.DeepClone(objectFactory, offset);
    cloned.Notes = Notes?.DeepClone(objectFactory, offset);

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
      SpeedSteps.GetFirst().StartBeat != StartBeat ||
      SpeedSteps.GetLast().StartBeat != EndBeat
    )
    {
      StartBeat = SpeedSteps.GetFirst().StartBeat;
      EndBeat = SpeedSteps.GetLast().StartBeat;

      OnLifeCycleChanged?.Invoke(this);
      return;
    }

    OnUpdated?.Invoke(this);
  }
  private void BubbleNote(NoteManager n) => OnUpdated?.Invoke(this);
  private void BubbleNoteLifeCycle(NoteManager n) => OnLifeCycleChanged?.Invoke(this);
}
