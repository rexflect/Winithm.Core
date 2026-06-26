using Godot;
using System;
using Winithm.Core.Managers;

namespace Winithm.Core.Controllers;

/// <summary>
/// Master clock for chart and audio synchronization.
/// Tick() must be called once per frame by the owner.
/// </summary>
public partial class AudioController : Node
{
  public Metronome? Metronome { get; private set; } = null;

  private readonly AudioStreamPlayer _player = new();

  // Chart time in seconds. Can be negative during pre-roll.
  private double _clock;

  private double _minClock;
  private double _maxClock;

  // Positive: audio leads chart. Negative: chart leads audio.
  private double _audioOffset;

  private bool _isPlaying;
  private bool _streamStarted;

  // ── Public state ────────────────────────────────────────────────────────────

  public bool IsPlaying => _isPlaying;

  public double CurrentTime => _clock - _minClock;
  public double CurrentTimeMs => CurrentTime * 1000d;
  public double? CurrentBeat => Metronome?.ToBeat(_clock);

  public double Length => _player.Stream is { } s ? (double)s.GetLength() : 0d;

  /// <summary>
  /// Total chart-time duration of the level (pre-roll + stream tail included).
  /// Only valid after a stream has been assigned (returns 0 otherwise).
  /// </summary>
  public double LevelLength
  {
    get
    {
      double streamLength = Length;
      if (streamLength <= 0d) return 0d;
      // _minClock is the pre-roll floor (≤ 0); _maxClock is the chart time
      // at which the audio stream ends. Their difference is the full span.
      double min = -Math.Max(0d, _audioOffset);
      double max = streamLength - _audioOffset;
      return max - min;
    }
  }

  // ── Initialisation ──────────────────────────────────────────────────────────

  public void Initialize(Metronome metronome)
  {
    Metronome = metronome;
    AddChild(_player);
  }

  // ── Clock update ────────────────────────────────────────────────────────────

  /// <summary>
  /// Advances the master clock one frame.
  ///
  /// • When the stream is running: clock is derived from the DSP position
  ///   (drift-free). Monotonic fallback to delta when DSP hasn't advanced.
  /// • When in pre-roll (clock &lt; -offset, offset &gt; 0) or waiting for the
  ///   audio entry point (offset &lt; 0): clock advances by delta until the
  ///   stream entry condition is met.
  /// </summary>
  public void Tick(double delta)
  {
    if (!_isPlaying) return;

    if (_streamStarted && _player.Playing)
    {
      // DSP-corrected stream position — accounts for output latency so the
      // clock matches what the listener actually hears right now.
      double dspPosition = _player.GetPlaybackPosition()
        + AudioServer.GetTimeSinceLastMix()
        - AudioServer.GetOutputLatency();

      // Convert stream position back to chart time.
      double dspClock = dspPosition - _audioOffset;

      // Godot's mix-chunk scheduler can hold or slightly reverse the reported
      // position between frames. Enforce monotonic advancement.
      if (dspClock > _clock)
        _clock = dspClock;
      else
        _clock += delta;
    }
    else
    {
      _clock += delta;

      // stream entry condition: clock + offset >= 0  →  stream position >= 0
      if (!_streamStarted && _clock + _audioOffset >= 0d)
        StartStream();
    }

    ClampClock();
  }

  // ── Playback control ────────────────────────────────────────────────────────

  /// <summary>
  /// Resumes playback from the current clock position.
  /// When starting from zero with a positive offset, seeds the clock into
  /// pre-roll (-offset) so the audio stream always begins at position 0.
  /// </summary>
  public void Resume()
  {
    if (_isPlaying) return;
    _isPlaying = true;
    _streamStarted = false; // defensive: ensure Tick() doesn't enter DSP branch prematurely

    // Starting fresh (clock == 0) with audio-leads offset: the stream entry
    // point is in the future. Seed into pre-roll so Tick() advances toward it
    // and StartStream() fires with streamPosition == 0.
    if (_clock == 0d && _audioOffset > 0d)
      _clock = -_audioOffset;

    // Already past the stream entry point → start immediately.
    // Still in pre-roll → Tick() will call StartStream() when ready.
    if (_clock + _audioOffset >= 0d)
      StartStream();
  }

  /// <summary>Pause playback and stop the audio stream.</summary>
  public void Pause()
  {
    if (!_isPlaying) return;
    _isPlaying = false;
    _streamStarted = false;
    _player.Stop();
  }

  /// <summary>Stops playback and resets the clock to the beginning.</summary>
  public void Stop()
  {
    _isPlaying = false;
    _streamStarted = false;
    _player.Stop();
    _clock = _audioOffset > 0d ? -_audioOffset : 0d;
    ClampClock();
  }

  /// <summary>Stops the current playback and starts it again from the beginning.</summary>
  public void Restart()
  {
    Stop();
    Resume();
  }

  // ── Seeking ─────────────────────────────────────────────────────────────────

  /// <summary>
  /// Seeks to an absolute chart-time position and restarts the stream if playing.
  /// The caller is responsible for passing a value in the valid chart range;
  /// this method clamps it to be safe.
  /// </summary>
  public void SeekSeconds(double? seconds)
  {
    if (seconds is null) return;
    
    _clock = seconds.Value;
    ClampClock();
    RestartStream();
  }

  public void SeekMilliseconds(double ms) => SeekSeconds(ms / 1000d);
  public void SeekBeat(double beat) => SeekSeconds(Metronome?.ToSeconds(beat));

  // ── Clock nudge (rewind animation while paused) ─────────────────────────────

  /// <summary>
  /// Shifts the clock by deltaSecs without resuming playback.
  /// Clamps the result to the valid chart range.
  /// </summary>
  public void AdjustTime(double deltaSecs)
  {
    _clock += deltaSecs;
    ClampClock();
  }

  // ── Audio offset ────────────────────────────────────────────────────────────

  public void SetAudioOffsetSeconds(double offset) => _audioOffset = offset;
  public void SetAudioOffsetMs(double ms) => _audioOffset = ms / 1000d;
  public double GetAudioOffsetSeconds() => _audioOffset;
  public double GetAudioOffsetMs() => _audioOffset * 1000d;

  // ── Stream access ───────────────────────────────────────────────────────────

  public AudioStream? GetStream() => _player.Stream;
  public void SetStream(AudioStream? stream) => _player.Stream = stream;

  // ── Private helpers ─────────────────────────────────────────────────────────

  /// <summary>
  /// Clamps _clock to the valid playable range.
  /// <para>
  /// _minClock: pre-roll floor — negative when offset > 0, zero otherwise.<br/>
  /// _maxClock: chart time at which the audio stream ends
  ///            = streamLength - _audioOffset.<br/>
  ///   • offset &gt; 0 (audio leads):  stream ends before chart end → smaller value<br/>
  ///   • offset &lt; 0 (chart leads):  stream ends after chart content → larger value
  /// </para>
  /// </summary>
  private void ClampClock()
  {
    _minClock = -Math.Max(0d, _audioOffset); // pre-roll floor (≤ 0)
    _clock = Math.Max(_clock, _minClock);

    double streamLength = Length;
    if (streamLength <= 0d) return;

    // FIX: was Math.Max(streamLength, streamLength - _audioOffset), which
    // incorrectly clamped the level 1 second short when _audioOffset > 0.
    _maxClock = streamLength - _audioOffset;
    _clock = Math.Min(_clock, _maxClock);
  }

  /// <summary>
  /// Starts the audio stream at the position matching the current clock.
  /// streamPosition = clock + offset, which equals 0 when the stream first
  /// becomes eligible (clock = -offset), and increases from there.
  /// Guards against out-of-range values in case of floating-point slippage.
  /// </summary>
  private void StartStream()
  {
    double streamPosition = double.Clamp(_clock + _audioOffset, 0d, Length);
    _player.Play((float)streamPosition);
    _streamStarted = true;
  }

  private void RestartStream()
  {
    _streamStarted = false;
    _player.Stop();

    // Seek does not imply resume; only restart the stream if already playing
    // and the clock is past the stream entry point.
    if (_isPlaying && _clock + _audioOffset >= 0d)
      StartStream();
  }
}