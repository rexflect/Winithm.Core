using Godot;
using Winithm.Core.Behaviors.GameplayUI;
using Winithm.Core.Managers;
using Winithm.Core.Data;
using Winithm.Core.Common;
using Winithm.Core.Logic;

namespace Winithm.Core.Controllers;

[Tool]
public partial class ComponentController : Control
{
  private Metronome _metronome;
  private ComponentManager _componentManager;
  private SongMetaData _songMetaData;
  private ChartMetadata _chartMetaData;

  private struct LastState
  {
    public Vector2 ScreenSize;
    public Color TextColor, TextOutLineColor;
  }

  [Export] public Vector2 ScreenSize = Constants.Visual.DESIGN_RESOLUTION;
  [Export] public float SongProgressPercent = 0f;
  [Export] public Color TextColor = Colors.White;
  [Export] public Color TextOutLineColor = Colors.Black;
  [Export] public Color CompBackgroundColor = Colors.Gray;

  private LastState _lastState;

  private Control _songInfoTransform;
  private Control _songInfoSubTransform;
  private SongInfo _songInfo;
  private Control _chartInfoTransform;
  private Control _chartInfoSubTransform;
  private ChartInfo _chartInfo;
  private Control _playerComboTransform;
  private Control _playerComboSubTransform;
  private PlayerCombo _playerCombo;
  private Control _playerScoreTransform;
  private Control _playerScoreSubTransform;
  private PlayerScore _playerScore;

  private double _lastUpdateBeat;

  public void Initialize(
    ComponentManager manager, Metronome metronome, SongMetaData songMeta, ChartMetadata chartMeta
  )
  {
    _componentManager = manager;
    _metronome = metronome;
    _songMetaData = songMeta;
    _chartMetaData = chartMeta;

    _songInfoTransform = GetNodeOrNull<Control>("SongInfoTransform");
    _songInfoSubTransform = _songInfoTransform?.GetNodeOrNull<Control>("SubTransform");
    _songInfo = _songInfoSubTransform?.GetNodeOrNull<SongInfo>("SongInfo");

    _chartInfoTransform = GetNodeOrNull<Control>("ChartInfoTransform");
    _chartInfoSubTransform = _chartInfoTransform?.GetNodeOrNull<Control>("SubTransform");
    _chartInfo = _chartInfoSubTransform?.GetNodeOrNull<ChartInfo>("ChartInfo");

    _playerComboTransform = GetNodeOrNull<Control>("PlayerComboTransform");
    _playerComboSubTransform = _playerComboTransform?.GetNodeOrNull<Control>("SubTransform");
    _playerCombo = _playerComboSubTransform?.GetNodeOrNull<PlayerCombo>("PlayerCombo");

    _playerScoreTransform = GetNodeOrNull<Control>("PlayerScoreTransform");
    _playerScoreSubTransform = _playerScoreTransform?.GetNodeOrNull<Control>("SubTransform");
    _playerScore = _playerScoreSubTransform?.GetNodeOrNull<PlayerScore>("PlayerScore");

    UpdateLayout();
  }

  public void Update(double currentBeat)
  {
    if (currentBeat == _lastUpdateBeat) return;

    ForceUpdate(currentBeat, false);
  }

  public void ForceUpdate(double currentBeat, bool _force = true)
  {
    bool isLayoutDirty = _lastState.ScreenSize != ScreenSize;
    bool isColorDirty =
      _lastState.TextColor != TextColor
      || _lastState.TextOutLineColor != TextOutLineColor;

    if (isLayoutDirty) UpdateLayout();
    if (isColorDirty) UpdateColor();

    if (_songInfo is not null && _songMetaData is not null && _metronome is not null)
    {
      _songInfo.SongName = _songMetaData.Name;
      _songInfo.BPM = _metronome.GetBPMAtBeat(currentBeat);
      _songInfo.SongIcon = _songMetaData.Illustration.IllustrationTexture;
      _songInfo.IconCenter = _songMetaData.Illustration.IconCenter;
      _songInfo.IconSize = _songMetaData.Illustration.IconSize;
      _songInfo.UpdateVisual();
    }

    if (_chartInfo is not null && _chartMetaData is not null)
    {
      _chartInfo.DifficultText = $"{_chartMetaData.ChartName} {_chartMetaData.Level}";
      _chartInfo.UpdateVisual();
    }

    UpdateComponentStoryboard(
      ComponentType.Info,
      _songInfoTransform, _songInfoSubTransform,
      _songInfo, currentBeat
    );
    UpdateComponentStoryboard(
      ComponentType.Difficulty,
      _chartInfoTransform, _chartInfoSubTransform,
      _chartInfo, currentBeat
    );
    UpdateComponentStoryboard(
      ComponentType.Combo,
      _playerComboTransform, _playerComboSubTransform,
      _playerCombo, currentBeat
    );
    UpdateComponentStoryboard(
      ComponentType.Score,
      _playerScoreTransform, _playerScoreSubTransform,
      _playerScore, currentBeat
    );

    _lastUpdateBeat = currentBeat;
  }

  public void SetCombo(int combo, bool instant = false) => _playerCombo?.SetCombo(combo, instant);
  public void SetStatus(ScoreEngine.CompletionStatus status) => _playerCombo?.SetStatus(status);
  public void DrainPauseBar() => _playerCombo?.DrainPauseBar();
  public void FillPauseBar() => _playerCombo?.FillPauseBar();

  public void SetScore(int score, bool instant = false) => _playerScore?.SetScore(score, instant);
  public void SetAccuracy(float accuracy) => _playerScore?.SetAccuracy(accuracy);

  private void UpdateComponentStoryboard(
    ComponentType compType,
    Control transformControl,
    Control subTransformControl,
    Control targetControl,
    double currentBeat
  )
  {
    if (transformControl is null || targetControl is null || _componentManager is null) return;

    var targetCompData = _componentManager[compType];
    if (targetCompData is null) return;

    float x = targetCompData.StoryboardEvents.Evaluate(
      StoryboardProperty.X, currentBeat, new(targetCompData.InitX)
    ).X;
    float y = targetCompData.StoryboardEvents.Evaluate(
      StoryboardProperty.Y, currentBeat, new(targetCompData.InitY)
    ).X;
    float r = targetCompData.StoryboardEvents.Evaluate(
      StoryboardProperty.Rotation, currentBeat, new(targetCompData.InitRotate)
    ).X;
    float s = targetCompData.StoryboardEvents.Evaluate(
      StoryboardProperty.Scale, currentBeat, new(targetCompData.InitScale)
    ).X;
    float a = targetCompData.StoryboardEvents.Evaluate(
      StoryboardProperty.Alpha, currentBeat, new(targetCompData.InitAlpha)
    ).X;

    float viewScale = Mathf.Abs(Mathf.Min(
      ScreenSize.X / Constants.Visual.DESIGN_RESOLUTION.X,
      ScreenSize.Y / Constants.Visual.DESIGN_RESOLUTION.Y
    ));

    transformControl.Position = new(x * viewScale, y * viewScale);
    subTransformControl.Scale = new(s, s);
    subTransformControl.RotationDegrees = r;
    transformControl.Modulate = new(1f, 1f, 1f, a);
  }

  private void UpdateLayout()
  {
    float viewScale = Mathf.Abs(Mathf.Min(
      ScreenSize.X / Constants.Visual.DESIGN_RESOLUTION.X,
      ScreenSize.Y / Constants.Visual.DESIGN_RESOLUTION.Y
    ));

    // Use Scale on each component — this uniformly scales all
    // internal positions, sizes, fonts, and clip areas without
    // fighting Godot's layout system.
    var scale = new Vector2(viewScale, viewScale);

    if (_songInfo is not null) _songInfo.Scale = scale;
    if (_chartInfo is not null) _chartInfo.Scale = scale;
    if (_playerCombo is not null) _playerCombo.Scale = scale;
    if (_playerScore is not null) _playerScore.Scale = scale;

    _lastState.ScreenSize = ScreenSize;
  }

  private void UpdateColor()
  {
    if (_songInfo is not null)
    {
      _songInfo.TextColor = TextColor;
      _songInfo.TextOutLineColor = TextOutLineColor;
      _songInfo.CompBackgroundColor = CompBackgroundColor;
      _songInfo.UpdateVisual();
    }
    if (_chartInfo is not null)
    {
      _chartInfo.TextColor = TextColor;
      _chartInfo.TextOutLineColor = TextOutLineColor;
      _chartInfo.CompBackgroundColor = CompBackgroundColor;
      _chartInfo.UpdateVisual();
    }
    if (_playerCombo is not null)
    {
      _playerCombo.TextColor = TextColor;
      _playerCombo.TextOutLineColor = TextOutLineColor;
      _playerCombo.CompBackgroundColor = CompBackgroundColor;
      _playerCombo.UpdateVisual();
    }
    if (_playerScore is not null)
    {
      _playerScore.TextColor = TextColor;
      _playerScore.TextOutLineColor = TextOutLineColor;
      _playerScore.CompBackgroundColor = CompBackgroundColor;
      _playerScore.UpdateVisual();
    }

    _lastState.TextColor = TextColor;
    _lastState.TextOutLineColor = TextOutLineColor;
  }
}