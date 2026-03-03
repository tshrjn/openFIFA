using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Director mode states for the broadcast camera system.
    /// </summary>
    public enum DirectorState
    {
        Live = 0,
        Replay = 1,
        Celebration = 2,
        Tactical = 3
    }

    /// <summary>
    /// Game event types that can trigger camera changes.
    /// </summary>
    public enum BroadcastGameEvent
    {
        None = 0,
        Goal = 1,
        NearMiss = 2,
        Foul = 3,
        Save = 4,
        Corner = 5,
        Kickoff = 6
    }

    /// <summary>
    /// Pitch zones for automatic camera angle selection.
    /// Based on ball position relative to the 50m x 30m pitch.
    /// </summary>
    public enum PitchZone
    {
        /// <summary>Central area of the pitch (|x| less than 30% of half-length).</summary>
        Midfield = 0,

        /// <summary>Attacking third approaching TeamA's goal (x less than -30% half-length).</summary>
        AttackingThirdA = 1,

        /// <summary>Attacking third approaching TeamB's goal (x greater than 30% half-length).</summary>
        AttackingThirdB = 2,

        /// <summary>Penalty area zone near TeamA's goal.</summary>
        PenaltyAreaA = 3,

        /// <summary>Penalty area zone near TeamB's goal.</summary>
        PenaltyAreaB = 4
    }

    /// <summary>
    /// Tracks game intensity/momentum over time for modulating cut frequency.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class MomentumTracker
    {
        private readonly DirectorConfig _config;
        private float _currentMomentum;

        /// <summary>Current momentum value (0 to 1, clamped).</summary>
        public float CurrentMomentum => _currentMomentum;

        /// <summary>Whether momentum is above the threshold for increased cut frequency.</summary>
        public bool IsHighIntensity => _currentMomentum >= _config.MomentumThreshold;

        public MomentumTracker(DirectorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _currentMomentum = 0f;
        }

        /// <summary>
        /// Update momentum decay over time.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            _currentMomentum -= _config.MomentumDecayRate * deltaTime;
            if (_currentMomentum < 0f) _currentMomentum = 0f;
        }

        /// <summary>
        /// Apply a momentum boost from a game event.
        /// </summary>
        public void ApplyEvent(BroadcastGameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case BroadcastGameEvent.Goal:
                    _currentMomentum += _config.GoalMomentumBoost;
                    break;
                case BroadcastGameEvent.NearMiss:
                    _currentMomentum += _config.NearMissMomentumBoost;
                    break;
                case BroadcastGameEvent.Foul:
                    _currentMomentum += _config.FoulMomentumBoost;
                    break;
                case BroadcastGameEvent.Save:
                    _currentMomentum += _config.NearMissMomentumBoost * 0.8f;
                    break;
                case BroadcastGameEvent.Corner:
                    _currentMomentum += _config.FoulMomentumBoost * 0.5f;
                    break;
                default:
                    break;
            }

            if (_currentMomentum > 1f) _currentMomentum = 1f;
        }

        /// <summary>
        /// Reset momentum to zero.
        /// </summary>
        public void Reset()
        {
            _currentMomentum = 0f;
        }

        /// <summary>
        /// Get the current cut interval in seconds based on momentum.
        /// Higher momentum leads to shorter intervals (more frequent cuts).
        /// </summary>
        public float GetCurrentCutInterval()
        {
            // Convert frequency (cuts/min) to interval (seconds/cut)
            float baseInterval = 60f / _config.BaseCutFrequency;
            float maxInterval = 60f / _config.MaxCutFrequency;

            // Lerp between base and max based on momentum
            float t = _currentMomentum;
            return baseInterval + (maxInterval - baseInterval) * t;
        }
    }

    /// <summary>
    /// Selects the best camera angle based on ball position on the pitch.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class AngleSelector
    {
        private readonly float _pitchHalfLength;
        private readonly float _pitchHalfWidth;
        private readonly float _goalAreaDepth;

        public AngleSelector(float pitchLength = 50f, float pitchWidth = 30f, float goalAreaDepth = 4f)
        {
            _pitchHalfLength = pitchLength / 2f;
            _pitchHalfWidth = pitchWidth / 2f;
            _goalAreaDepth = goalAreaDepth;
        }

        /// <summary>
        /// Determine the pitch zone based on ball world position (x = along length, z = along width).
        /// </summary>
        public PitchZone GetPitchZone(float ballX, float ballZ)
        {
            float absX = Math.Abs(ballX);

            // Penalty area: within goal area depth of either end
            if (absX >= _pitchHalfLength - _goalAreaDepth)
            {
                return ballX < 0 ? PitchZone.PenaltyAreaA : PitchZone.PenaltyAreaB;
            }

            // Attacking third: beyond 30% of the half-length from center
            float attackingThirdThreshold = _pitchHalfLength * 0.6f;
            if (absX >= attackingThirdThreshold)
            {
                return ballX < 0 ? PitchZone.AttackingThirdA : PitchZone.AttackingThirdB;
            }

            return PitchZone.Midfield;
        }

        /// <summary>
        /// Select the best camera angle for the given pitch zone.
        /// </summary>
        public CameraAngle SelectAngle(PitchZone zone)
        {
            switch (zone)
            {
                case PitchZone.Midfield:
                    return CameraAngle.Wide;

                case PitchZone.AttackingThirdA:
                case PitchZone.AttackingThirdB:
                    return CameraAngle.Medium;

                case PitchZone.PenaltyAreaA:
                case PitchZone.PenaltyAreaB:
                    return CameraAngle.Close;

                default:
                    return CameraAngle.Wide;
            }
        }

        /// <summary>
        /// Select the best camera angle based on raw ball position.
        /// </summary>
        public CameraAngle SelectAngleForPosition(float ballX, float ballZ)
        {
            var zone = GetPitchZone(ballX, ballZ);
            return SelectAngle(zone);
        }
    }

    /// <summary>
    /// Replay angle definition for multi-angle replay sequencing.
    /// </summary>
    public struct ReplayAngleStep
    {
        /// <summary>The camera angle for this step.</summary>
        public CameraAngle Angle;

        /// <summary>Playback speed multiplier for this step.</summary>
        public float PlaybackSpeed;

        /// <summary>Duration of this step in real-time seconds.</summary>
        public float Duration;

        public ReplayAngleStep(CameraAngle angle, float playbackSpeed, float duration)
        {
            Angle = angle;
            PlaybackSpeed = playbackSpeed;
            Duration = duration;
        }
    }

    /// <summary>
    /// Orchestrates multi-angle replay sequences.
    /// Transitions through replay angles at configurable speeds,
    /// then returns to live.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ReplaySequencer
    {
        private readonly ReplayCameraConfig _config;
        private readonly ReplayAngleStep[] _steps;
        private int _currentStepIndex;
        private float _stepElapsed;
        private bool _isActive;

        /// <summary>Whether the replay sequence is currently active.</summary>
        public bool IsActive => _isActive;

        /// <summary>Current step index in the replay sequence.</summary>
        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>Number of steps in the replay sequence.</summary>
        public int StepCount => _steps.Length;

        /// <summary>Elapsed time in the current step.</summary>
        public float StepElapsed => _stepElapsed;

        /// <summary>Current camera angle for the active step, or Wide if inactive.</summary>
        public CameraAngle CurrentAngle => _isActive && _currentStepIndex < _steps.Length
            ? _steps[_currentStepIndex].Angle
            : CameraAngle.Wide;

        /// <summary>Current playback speed for the active step, or 1.0 if inactive.</summary>
        public float CurrentPlaybackSpeed => _isActive && _currentStepIndex < _steps.Length
            ? _steps[_currentStepIndex].PlaybackSpeed
            : 1f;

        /// <summary>Fired when the replay sequence completes.</summary>
        public event Action OnReplayComplete;

        /// <summary>Fired when stepping to the next angle. Arg: new step index.</summary>
        public event Action<int> OnStepChanged;

        public ReplaySequencer(ReplayCameraConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Build default multi-angle sequence
            float stepDuration = config.ReplayDuration / config.MultiAngleCount;
            _steps = new ReplayAngleStep[config.MultiAngleCount];

            if (config.MultiAngleCount >= 3)
            {
                _steps[0] = new ReplayAngleStep(CameraAngle.Close, config.SlowMoSpeedFirst, stepDuration);
                _steps[1] = new ReplayAngleStep(CameraAngle.BehindGoal, config.SlowMoSpeedSecond, stepDuration);
                _steps[2] = new ReplayAngleStep(CameraAngle.Wide, config.NormalSpeed, stepDuration);

                // Fill remaining steps if more than 3
                for (int i = 3; i < config.MultiAngleCount; i++)
                {
                    _steps[i] = new ReplayAngleStep(CameraAngle.Medium, config.SlowMoSpeedSecond, stepDuration);
                }
            }
            else if (config.MultiAngleCount == 2)
            {
                _steps[0] = new ReplayAngleStep(CameraAngle.Close, config.SlowMoSpeedFirst, stepDuration);
                _steps[1] = new ReplayAngleStep(CameraAngle.Wide, config.SlowMoSpeedSecond, stepDuration);
            }
            else
            {
                _steps[0] = new ReplayAngleStep(CameraAngle.Close, config.SlowMoSpeedFirst, config.ReplayDuration);
            }

            _currentStepIndex = 0;
            _stepElapsed = 0f;
            _isActive = false;
        }

        /// <summary>
        /// Start the replay sequence.
        /// </summary>
        public void Start()
        {
            _isActive = true;
            _currentStepIndex = 0;
            _stepElapsed = 0f;
        }

        /// <summary>
        /// Update the replay sequencer.
        /// </summary>
        /// <param name="deltaTime">Unscaled delta time.</param>
        /// <returns>True if the sequence is still active.</returns>
        public bool Update(float deltaTime)
        {
            if (!_isActive) return false;

            _stepElapsed += deltaTime;

            if (_currentStepIndex < _steps.Length && _stepElapsed >= _steps[_currentStepIndex].Duration)
            {
                _stepElapsed -= _steps[_currentStepIndex].Duration;
                _currentStepIndex++;
                if (_currentStepIndex < _steps.Length)
                {
                    OnStepChanged?.Invoke(_currentStepIndex);
                }
            }

            if (_currentStepIndex >= _steps.Length)
            {
                Stop();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stop the replay sequence immediately.
        /// </summary>
        public void Stop()
        {
            _isActive = false;
            _currentStepIndex = 0;
            _stepElapsed = 0f;
            OnReplayComplete?.Invoke();
        }
    }

    /// <summary>
    /// Auto-cut logic that decides when to trigger a camera cut based on
    /// elapsed time, ball position changes, and game events.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class AutoCutLogic
    {
        private readonly AutoCutConfig _config;
        private readonly MomentumTracker _momentumTracker;
        private float _timeSinceLastCut;
        private CameraAngle _currentAngle;

        /// <summary>Time elapsed since the last camera cut.</summary>
        public float TimeSinceLastCut => _timeSinceLastCut;

        /// <summary>Current camera angle.</summary>
        public CameraAngle CurrentAngle => _currentAngle;

        /// <summary>Fired when a cut should occur. Arg: the new CameraAngle.</summary>
        public event Action<CameraAngle> OnCutTriggered;

        public AutoCutLogic(AutoCutConfig config, MomentumTracker momentumTracker)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _momentumTracker = momentumTracker ?? throw new ArgumentNullException(nameof(momentumTracker));
            _timeSinceLastCut = 0f;
            _currentAngle = CameraAngle.Wide;
        }

        /// <summary>
        /// Update the auto-cut timer and check if a cut should occur.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <param name="suggestedAngle">Angle suggested by the AngleSelector based on ball position.</param>
        /// <returns>True if a cut was triggered.</returns>
        public bool Update(float deltaTime, CameraAngle suggestedAngle)
        {
            _timeSinceLastCut += deltaTime;

            // Force cut if max duration exceeded
            if (_timeSinceLastCut >= _config.MaxCutDuration)
            {
                return TriggerCut(suggestedAngle);
            }

            // Only consider a cut if minimum duration has passed
            if (_timeSinceLastCut < _config.MinCutDuration)
                return false;

            // Cut if angle should change (e.g. ball moved to different zone)
            if (suggestedAngle != _currentAngle)
            {
                float cutInterval = _momentumTracker.GetCurrentCutInterval();
                if (_timeSinceLastCut >= cutInterval)
                {
                    return TriggerCut(suggestedAngle);
                }
            }

            return false;
        }

        /// <summary>
        /// Force an immediate event-triggered cut.
        /// Respects min cut duration unless this is an event-triggered cut and those are enabled.
        /// </summary>
        public bool TriggerEventCut(CameraAngle targetAngle)
        {
            if (!_config.EventTriggeredCutsEnabled) return false;

            // Event cuts override minimum duration with a lower floor (1 second)
            if (_timeSinceLastCut < 1f) return false;

            return TriggerCut(targetAngle);
        }

        private bool TriggerCut(CameraAngle newAngle)
        {
            _currentAngle = newAngle;
            _timeSinceLastCut = 0f;
            OnCutTriggered?.Invoke(newAngle);
            return true;
        }

        /// <summary>
        /// Reset the auto-cut state.
        /// </summary>
        public void Reset()
        {
            _timeSinceLastCut = 0f;
            _currentAngle = CameraAngle.Wide;
        }
    }

    /// <summary>
    /// Master broadcast director logic — state machine controlling Live, Replay,
    /// Celebration, and Tactical camera modes. Coordinates auto-cuts, angle selection,
    /// replay sequencing, and momentum tracking.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class BroadcastDirectorLogic
    {
        private readonly DirectorConfig _directorConfig;
        private readonly AutoCutConfig _autoCutConfig;
        private readonly ReplayCameraConfig _replayConfig;
        private readonly MomentumTracker _momentumTracker;
        private readonly AngleSelector _angleSelector;
        private readonly AutoCutLogic _autoCutLogic;
        private readonly ReplaySequencer _replaySequencer;

        private DirectorState _state;
        private CameraAngle _activeAngle;
        private float _celebrationElapsed;
        private float _celebrationDuration;
        private float _tacticalElapsed;
        private float _tacticalDuration;

        /// <summary>Current director state.</summary>
        public DirectorState State => _state;

        /// <summary>Currently active camera angle.</summary>
        public CameraAngle ActiveAngle => _activeAngle;

        /// <summary>Momentum tracker for game intensity.</summary>
        public MomentumTracker Momentum => _momentumTracker;

        /// <summary>Auto-cut logic for timing camera cuts.</summary>
        public AutoCutLogic AutoCut => _autoCutLogic;

        /// <summary>Angle selector for zone-based camera selection.</summary>
        public AngleSelector Angles => _angleSelector;

        /// <summary>Replay sequencer for multi-angle replays.</summary>
        public ReplaySequencer Replays => _replaySequencer;

        /// <summary>Fired on director state change. Args: (oldState, newState).</summary>
        public event Action<DirectorState, DirectorState> OnStateChanged;

        /// <summary>Fired when the active camera angle changes. Arg: new angle.</summary>
        public event Action<CameraAngle> OnAngleChanged;

        public BroadcastDirectorLogic(
            DirectorConfig directorConfig = null,
            AutoCutConfig autoCutConfig = null,
            ReplayCameraConfig replayConfig = null,
            float pitchLength = 50f,
            float pitchWidth = 30f,
            float goalAreaDepth = 4f)
        {
            _directorConfig = directorConfig ?? new DirectorConfig();
            _autoCutConfig = autoCutConfig ?? new AutoCutConfig();
            _replayConfig = replayConfig ?? new ReplayCameraConfig();

            _momentumTracker = new MomentumTracker(_directorConfig);
            _angleSelector = new AngleSelector(pitchLength, pitchWidth, goalAreaDepth);
            _autoCutLogic = new AutoCutLogic(_autoCutConfig, _momentumTracker);
            _replaySequencer = new ReplaySequencer(_replayConfig);

            _state = DirectorState.Live;
            _activeAngle = CameraAngle.Wide;
            _celebrationDuration = 3f;
            _tacticalDuration = 5f;

            // Wire up internal events
            _autoCutLogic.OnCutTriggered += HandleCutTriggered;
            _replaySequencer.OnReplayComplete += HandleReplayComplete;
        }

        /// <summary>
        /// Main update tick. Call each frame with delta time and ball position.
        /// </summary>
        /// <param name="deltaTime">Unscaled delta time.</param>
        /// <param name="ballX">Ball world-space X (along pitch length).</param>
        /// <param name="ballZ">Ball world-space Z (along pitch width).</param>
        public void Update(float deltaTime, float ballX, float ballZ)
        {
            _momentumTracker.Update(deltaTime);

            switch (_state)
            {
                case DirectorState.Live:
                    UpdateLive(deltaTime, ballX, ballZ);
                    break;

                case DirectorState.Replay:
                    UpdateReplay(deltaTime);
                    break;

                case DirectorState.Celebration:
                    UpdateCelebration(deltaTime);
                    break;

                case DirectorState.Tactical:
                    UpdateTactical(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Notify the director of a game event.
        /// </summary>
        public void NotifyEvent(BroadcastGameEvent gameEvent)
        {
            _momentumTracker.ApplyEvent(gameEvent);

            switch (gameEvent)
            {
                case BroadcastGameEvent.Goal:
                    TransitionToState(DirectorState.Celebration);
                    SetActiveAngle(CameraAngle.Celebration);
                    _celebrationElapsed = 0f;
                    break;

                case BroadcastGameEvent.NearMiss:
                    if (_state == DirectorState.Live)
                    {
                        // Check if we should show a replay of the near miss
                        _autoCutLogic.TriggerEventCut(CameraAngle.Close);
                    }
                    break;

                case BroadcastGameEvent.Foul:
                    if (_state == DirectorState.Live)
                    {
                        _autoCutLogic.TriggerEventCut(CameraAngle.Close);
                    }
                    break;

                case BroadcastGameEvent.Kickoff:
                    if (_state != DirectorState.Live)
                    {
                        TransitionToState(DirectorState.Live);
                    }
                    SetActiveAngle(CameraAngle.Wide);
                    _autoCutLogic.Reset();
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Start a replay sequence.
        /// </summary>
        public void StartReplay()
        {
            if (_state == DirectorState.Replay) return;

            TransitionToState(DirectorState.Replay);
            _replaySequencer.Start();
            SetActiveAngle(_replaySequencer.CurrentAngle);
        }

        /// <summary>
        /// Start tactical view for a specified duration.
        /// </summary>
        /// <param name="duration">Duration of tactical view in seconds.</param>
        public void StartTacticalView(float duration = 5f)
        {
            _tacticalDuration = duration;
            _tacticalElapsed = 0f;
            TransitionToState(DirectorState.Tactical);
            SetActiveAngle(CameraAngle.Tactical);
        }

        /// <summary>
        /// Force return to live camera mode.
        /// </summary>
        public void ReturnToLive()
        {
            if (_state == DirectorState.Replay)
            {
                _replaySequencer.Stop();
            }

            TransitionToState(DirectorState.Live);
            SetActiveAngle(CameraAngle.Wide);
            _autoCutLogic.Reset();
        }

        /// <summary>
        /// Set the celebration duration.
        /// </summary>
        public void SetCelebrationDuration(float duration)
        {
            _celebrationDuration = duration;
        }

        private void UpdateLive(float deltaTime, float ballX, float ballZ)
        {
            var suggestedAngle = _angleSelector.SelectAngleForPosition(ballX, ballZ);
            _autoCutLogic.Update(deltaTime, suggestedAngle);
        }

        private void UpdateReplay(float deltaTime)
        {
            bool stillActive = _replaySequencer.Update(deltaTime);
            if (stillActive)
            {
                SetActiveAngle(_replaySequencer.CurrentAngle);
            }
            // If not active, HandleReplayComplete will be called via event
        }

        private void UpdateCelebration(float deltaTime)
        {
            _celebrationElapsed += deltaTime;
            if (_celebrationElapsed >= _celebrationDuration)
            {
                // After celebration, start replay of the goal
                StartReplay();
            }
        }

        private void UpdateTactical(float deltaTime)
        {
            _tacticalElapsed += deltaTime;
            if (_tacticalElapsed >= _tacticalDuration)
            {
                TransitionToState(DirectorState.Live);
                SetActiveAngle(CameraAngle.Wide);
                _autoCutLogic.Reset();
            }
        }

        private void HandleCutTriggered(CameraAngle newAngle)
        {
            SetActiveAngle(newAngle);
        }

        private void HandleReplayComplete()
        {
            TransitionToState(DirectorState.Live);
            SetActiveAngle(CameraAngle.Wide);
            _autoCutLogic.Reset();
        }

        private void TransitionToState(DirectorState newState)
        {
            if (_state == newState) return;

            var oldState = _state;
            _state = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void SetActiveAngle(CameraAngle angle)
        {
            if (_activeAngle == angle) return;

            _activeAngle = angle;
            OnAngleChanged?.Invoke(angle);
        }
    }
}
