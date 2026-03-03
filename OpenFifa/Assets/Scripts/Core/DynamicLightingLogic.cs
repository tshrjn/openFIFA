using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Match lighting phases that the LightingDirector sequences through.
    /// </summary>
    public enum LightingPhase
    {
        Pregame,
        MatchStart,
        HalfTime,
        SecondHalf,
        PostMatch
    }

    /// <summary>
    /// Snapshot of the current lighting state, produced by the logic layer
    /// for consumption by the MonoBehaviour rendering layer.
    /// </summary>
    public class LightingSnapshot
    {
        public float AmbientR { get; set; }
        public float AmbientG { get; set; }
        public float AmbientB { get; set; }
        public float SunElevation { get; set; }
        public float SunAzimuth { get; set; }
        public float SunIntensity { get; set; }
        public float FloodlightIntensityFactor { get; set; }
        public float FloodlightColorTemperature { get; set; }
        public FloodlightState[] FloodlightStates { get; set; }
        public float ShadowMaxDistance { get; set; }
        public int ShadowCascadeCount { get; set; }
        public TimeOfDay CurrentTimeOfDay { get; set; }
        public LightingPhase CurrentPhase { get; set; }
    }

    /// <summary>
    /// State machine that manages lighting phases throughout a match.
    /// Sequences: Pregame -> MatchStart -> HalfTime -> SecondHalf -> PostMatch.
    /// </summary>
    public class LightingDirector
    {
        private LightingPhase _currentPhase;

        public LightingPhase CurrentPhase => _currentPhase;

        public event Action<LightingPhase, LightingPhase> OnPhaseChanged;

        public LightingDirector()
        {
            _currentPhase = LightingPhase.Pregame;
        }

        /// <summary>
        /// Advance to the next lighting phase. Follows strict ordering.
        /// Returns true if the transition was valid.
        /// </summary>
        public bool AdvancePhase()
        {
            var previousPhase = _currentPhase;

            switch (_currentPhase)
            {
                case LightingPhase.Pregame:
                    _currentPhase = LightingPhase.MatchStart;
                    break;
                case LightingPhase.MatchStart:
                    _currentPhase = LightingPhase.HalfTime;
                    break;
                case LightingPhase.HalfTime:
                    _currentPhase = LightingPhase.SecondHalf;
                    break;
                case LightingPhase.SecondHalf:
                    _currentPhase = LightingPhase.PostMatch;
                    break;
                case LightingPhase.PostMatch:
                    return false; // No further phases
                default:
                    return false;
            }

            OnPhaseChanged?.Invoke(previousPhase, _currentPhase);
            return true;
        }

        /// <summary>
        /// Force the director to a specific phase (for testing or scene loading).
        /// </summary>
        public void SetPhase(LightingPhase phase)
        {
            var previous = _currentPhase;
            _currentPhase = phase;
            if (previous != phase)
            {
                OnPhaseChanged?.Invoke(previous, phase);
            }
        }

        /// <summary>
        /// Returns the recommended floodlight state for the given phase and time of day.
        /// </summary>
        public FloodlightState GetRecommendedFloodlightState(TimeOfDay time)
        {
            bool needsFloodlights = time.IsNightTime || time.IsDusk;

            switch (_currentPhase)
            {
                case LightingPhase.Pregame:
                    return needsFloodlights ? FloodlightState.WarmingUp : FloodlightState.Off;
                case LightingPhase.MatchStart:
                case LightingPhase.SecondHalf:
                    return needsFloodlights ? FloodlightState.FullPower : FloodlightState.Off;
                case LightingPhase.HalfTime:
                    return needsFloodlights ? FloodlightState.Dimmed : FloodlightState.Off;
                case LightingPhase.PostMatch:
                    return needsFloodlights ? FloodlightState.Dimmed : FloodlightState.Off;
                default:
                    return FloodlightState.Off;
            }
        }
    }

    /// <summary>
    /// Simulates time of day advancing, calculates sun position and ambient colors.
    /// </summary>
    public class TimeOfDaySimulator
    {
        private TimeOfDay _currentTime;
        private readonly LightingTransitionConfig _transitionConfig;

        /// <summary>Speed multiplier for time progression (1 = real time, 60 = 1 minute per second).</summary>
        public float TimeScale { get; set; }

        /// <summary>Whether time progression is paused.</summary>
        public bool IsPaused { get; set; }

        public TimeOfDay CurrentTime => _currentTime;

        public event Action<TimeOfDay> OnTimeChanged;

        public TimeOfDaySimulator(TimeOfDay startTime = null, LightingTransitionConfig transitionConfig = null, float timeScale = 0f)
        {
            _currentTime = startTime ?? new TimeOfDay(20f);
            _transitionConfig = transitionConfig ?? new LightingTransitionConfig();
            TimeScale = timeScale;
            IsPaused = false;
        }

        /// <summary>
        /// Update the simulator by deltaTime seconds. Advances the time of day if not paused.
        /// </summary>
        public void Update(float deltaTimeSeconds)
        {
            if (IsPaused || TimeScale <= 0f) return;

            float deltaHours = (deltaTimeSeconds * TimeScale) / 3600f;
            _currentTime = _currentTime.Advance(deltaHours);
            OnTimeChanged?.Invoke(_currentTime);
        }

        /// <summary>
        /// Manually set the current time.
        /// </summary>
        public void SetTime(TimeOfDay time)
        {
            _currentTime = time ?? throw new ArgumentNullException(nameof(time));
            OnTimeChanged?.Invoke(_currentTime);
        }

        /// <summary>
        /// Calculates the current sun elevation angle.
        /// </summary>
        public float GetSunElevation()
        {
            return _transitionConfig.EvaluateSunElevation(
                _currentTime.NormalizedTime,
                _currentTime.SunriseHour / 24f,
                _currentTime.SunsetHour / 24f);
        }

        /// <summary>
        /// Calculates the sun azimuth (Y rotation) based on time of day.
        /// East at sunrise, South at noon, West at sunset.
        /// </summary>
        public float GetSunAzimuth()
        {
            float sunriseNorm = _currentTime.SunriseHour / 24f;
            float sunsetNorm = _currentTime.SunsetHour / 24f;
            float t = _currentTime.NormalizedTime;

            if (t < sunriseNorm || t > sunsetNorm) return _transitionConfig.SunAzimuthOffset;

            float dayProgress = (t - sunriseNorm) / (sunsetNorm - sunriseNorm);
            // Sun moves from east (90) through south (180) to west (270)
            return 90f + dayProgress * 180f;
        }

        /// <summary>
        /// Gets the current ambient color from the transition config.
        /// </summary>
        public void GetAmbientColor(out float r, out float g, out float b)
        {
            _transitionConfig.EvaluateAmbientColor(_currentTime.NormalizedTime, out r, out g, out b);
        }

        /// <summary>
        /// Calculates directional light (sun/moon) intensity based on elevation.
        /// </summary>
        public float GetSunIntensity()
        {
            float elevation = GetSunElevation();
            if (elevation <= 0f) return 0f;

            // Intensity scales with sin of elevation angle
            float radians = elevation * (float)Math.PI / 180f;
            return (float)Math.Sin(radians) * 2f; // Max ~2.0 at zenith
        }
    }

    /// <summary>
    /// Manages the state of individual floodlights, including warm-up sequencing
    /// and failure simulation.
    /// </summary>
    public class FloodlightController
    {
        private readonly int _floodlightCount;
        private readonly FloodlightState[] _states;
        private readonly float[] _warmUpTimers;
        private readonly FloodlightBehavior _behavior;

        public int FloodlightCount => _floodlightCount;

        public event Action<int, FloodlightState> OnFloodlightStateChanged;

        public FloodlightController(int floodlightCount = 4, FloodlightBehavior behavior = null)
        {
            if (floodlightCount <= 0)
                throw new ArgumentException("FloodlightCount must be positive.", nameof(floodlightCount));

            _floodlightCount = floodlightCount;
            _behavior = behavior ?? new FloodlightBehavior();
            _states = new FloodlightState[_floodlightCount];
            _warmUpTimers = new float[_floodlightCount];
        }

        /// <summary>Gets the current state of a specific floodlight.</summary>
        public FloodlightState GetState(int index)
        {
            if (index < 0 || index >= _floodlightCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _states[index];
        }

        /// <summary>Gets all floodlight states as an array copy.</summary>
        public FloodlightState[] GetAllStates()
        {
            var copy = new FloodlightState[_floodlightCount];
            Array.Copy(_states, copy, _floodlightCount);
            return copy;
        }

        /// <summary>
        /// Gets the intensity factor for a specific floodlight (0-1).
        /// Accounts for warm-up progress, dimming, and emergency modes.
        /// </summary>
        public float GetIntensityFactor(int index)
        {
            if (index < 0 || index >= _floodlightCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            switch (_states[index])
            {
                case FloodlightState.Off:
                    return 0f;
                case FloodlightState.WarmingUp:
                    return _behavior.CalculateWarmUpIntensity(_warmUpTimers[index]);
                case FloodlightState.FullPower:
                    return 1f;
                case FloodlightState.Dimmed:
                    return _behavior.DimmedIntensityFactor;
                case FloodlightState.Emergency:
                    return _behavior.EmergencyIntensityFactor;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Gets the color temperature for a specific floodlight.
        /// </summary>
        public float GetColorTemperature(int index)
        {
            if (index < 0 || index >= _floodlightCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            switch (_states[index])
            {
                case FloodlightState.Off:
                    return _behavior.ColorTemperatureWarmUp;
                case FloodlightState.WarmingUp:
                    float progress = _behavior.WarmUpDuration > 0f
                        ? Math.Min(_warmUpTimers[index] / _behavior.WarmUpDuration, 1f)
                        : 1f;
                    return _behavior.InterpolateColorTemperature(progress);
                case FloodlightState.FullPower:
                    return _behavior.ColorTemperatureFull;
                case FloodlightState.Dimmed:
                    return _behavior.ColorTemperatureFull;
                case FloodlightState.Emergency:
                    return _behavior.ColorTemperatureWarmUp; // Warm/red tint
                default:
                    return _behavior.ColorTemperatureFull;
            }
        }

        /// <summary>
        /// Sets all floodlights to the specified target state.
        /// WarmingUp transitions reset the warm-up timer.
        /// </summary>
        public void SetAllState(FloodlightState targetState)
        {
            for (int i = 0; i < _floodlightCount; i++)
            {
                SetState(i, targetState);
            }
        }

        /// <summary>
        /// Sets a single floodlight to the specified state.
        /// </summary>
        public void SetState(int index, FloodlightState targetState)
        {
            if (index < 0 || index >= _floodlightCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var previousState = _states[index];
            _states[index] = targetState;

            if (targetState == FloodlightState.WarmingUp)
            {
                _warmUpTimers[index] = 0f;
            }

            if (previousState != targetState)
            {
                OnFloodlightStateChanged?.Invoke(index, targetState);
            }
        }

        /// <summary>
        /// Simulates a floodlight failure (switches to Emergency).
        /// </summary>
        public void SimulateFailure(int index)
        {
            SetState(index, FloodlightState.Emergency);
        }

        /// <summary>
        /// Update warm-up timers. Call every frame with deltaTime.
        /// Automatically transitions WarmingUp floodlights to FullPower
        /// when warm-up is complete.
        /// </summary>
        public void Update(float deltaTimeSeconds)
        {
            for (int i = 0; i < _floodlightCount; i++)
            {
                if (_states[i] == FloodlightState.WarmingUp)
                {
                    _warmUpTimers[i] += deltaTimeSeconds;

                    if (_warmUpTimers[i] >= _behavior.WarmUpDuration)
                    {
                        _states[i] = FloodlightState.FullPower;
                        OnFloodlightStateChanged?.Invoke(i, FloodlightState.FullPower);
                    }
                }
            }
        }

        /// <summary>Gets the warm-up elapsed time for a specific floodlight.</summary>
        public float GetWarmUpElapsed(int index)
        {
            if (index < 0 || index >= _floodlightCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _warmUpTimers[index];
        }
    }

    /// <summary>
    /// Provides smooth blending between two lighting snapshots.
    /// </summary>
    public class LightingBlender
    {
        private LightingSnapshot _from;
        private LightingSnapshot _to;
        private float _duration;
        private float _elapsed;
        private bool _isBlending;

        public bool IsBlending => _isBlending;

        /// <summary>Current blend progress (0 = from, 1 = to).</summary>
        public float Progress => _duration > 0f ? Math.Min(_elapsed / _duration, 1f) : 1f;

        public event Action OnBlendComplete;

        /// <summary>
        /// Start a blend from one snapshot to another over the given duration.
        /// </summary>
        public void StartBlend(LightingSnapshot from, LightingSnapshot to, float durationSeconds)
        {
            _from = from ?? throw new ArgumentNullException(nameof(from));
            _to = to ?? throw new ArgumentNullException(nameof(to));
            _duration = Math.Max(0f, durationSeconds);
            _elapsed = 0f;
            _isBlending = true;
        }

        /// <summary>
        /// Update the blend timer. Call every frame with deltaTime.
        /// </summary>
        public void Update(float deltaTimeSeconds)
        {
            if (!_isBlending) return;

            _elapsed += deltaTimeSeconds;

            if (_elapsed >= _duration)
            {
                _isBlending = false;
                OnBlendComplete?.Invoke();
            }
        }

        /// <summary>
        /// Gets the interpolated snapshot at the current blend progress.
        /// Returns null if no blend is active and no snapshots have been set.
        /// </summary>
        public LightingSnapshot GetCurrentSnapshot()
        {
            if (_from == null || _to == null) return null;

            float t = SmoothStep(Progress);

            return new LightingSnapshot
            {
                AmbientR = Lerp(_from.AmbientR, _to.AmbientR, t),
                AmbientG = Lerp(_from.AmbientG, _to.AmbientG, t),
                AmbientB = Lerp(_from.AmbientB, _to.AmbientB, t),
                SunElevation = Lerp(_from.SunElevation, _to.SunElevation, t),
                SunAzimuth = Lerp(_from.SunAzimuth, _to.SunAzimuth, t),
                SunIntensity = Lerp(_from.SunIntensity, _to.SunIntensity, t),
                FloodlightIntensityFactor = Lerp(_from.FloodlightIntensityFactor, _to.FloodlightIntensityFactor, t),
                FloodlightColorTemperature = Lerp(_from.FloodlightColorTemperature, _to.FloodlightColorTemperature, t),
                ShadowMaxDistance = Lerp(_from.ShadowMaxDistance, _to.ShadowMaxDistance, t),
                ShadowCascadeCount = t < 0.5f ? _from.ShadowCascadeCount : _to.ShadowCascadeCount,
                CurrentTimeOfDay = t < 0.5f ? _from.CurrentTimeOfDay : _to.CurrentTimeOfDay,
                CurrentPhase = t < 0.5f ? _from.CurrentPhase : _to.CurrentPhase,
                FloodlightStates = t < 0.5f ? _from.FloodlightStates : _to.FloodlightStates,
            };
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Hermite smoothstep for smooth transitions (ease in/out).
        /// </summary>
        private static float SmoothStep(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Manages shadow quality based on time-of-day and a performance budget.
    /// At night, shadow cascades can be reduced for performance.
    /// </summary>
    public class ShadowManager
    {
        private readonly ShadowCascadeConfig _daytimeConfig;
        private readonly ShadowCascadeConfig _nighttimeConfig;

        public ShadowCascadeConfig DaytimeConfig => _daytimeConfig;
        public ShadowCascadeConfig NighttimeConfig => _nighttimeConfig;

        public ShadowManager(ShadowCascadeConfig daytimeConfig = null, ShadowCascadeConfig nighttimeConfig = null)
        {
            _daytimeConfig = daytimeConfig ?? new ShadowCascadeConfig(cascadeCount: 4, maxDistance: 80f);
            _nighttimeConfig = nighttimeConfig ?? new ShadowCascadeConfig(cascadeCount: 2, maxDistance: 60f);
        }

        /// <summary>
        /// Returns the appropriate shadow config based on the time of day.
        /// </summary>
        public ShadowCascadeConfig GetConfigForTime(TimeOfDay time)
        {
            if (time == null) return _daytimeConfig;

            if (time.IsNightTime)
                return _nighttimeConfig;

            if (time.IsDusk || time.IsDawn)
                return _nighttimeConfig; // Reduced quality during transitions

            return _daytimeConfig;
        }

        /// <summary>
        /// Calculates interpolated shadow max distance between day and night configs
        /// based on the sun elevation factor.
        /// </summary>
        public float GetInterpolatedMaxDistance(TimeOfDay time)
        {
            if (time == null) return _daytimeConfig.MaxDistance;

            float sunFactor = time.SunElevationFactor;
            return _nighttimeConfig.MaxDistance + (_daytimeConfig.MaxDistance - _nighttimeConfig.MaxDistance) * sunFactor;
        }
    }
}
