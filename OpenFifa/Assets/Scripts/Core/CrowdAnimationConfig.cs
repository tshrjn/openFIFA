using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Animation states for crowd members. Each section transitions
    /// between these states in response to game events.
    /// </summary>
    public enum CrowdAnimState
    {
        Seated = 0,
        Idle = 1,
        Cheering = 2,
        Booing = 3,
        Standing = 4,
        WaveLeft = 5,
        WaveRight = 6,
        Celebrating = 7,
        Dejected = 8
    }

    /// <summary>
    /// Team affiliation for a crowd section. Determines how sections
    /// react to events from each team.
    /// </summary>
    public enum SectionAffiliation
    {
        Home = 0,
        Away = 1,
        Neutral = 2
    }

    /// <summary>
    /// Pure C# configuration for crowd animation parameters.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class CrowdAnimationConfig
    {
        /// <summary>Total number of crowd sections around the stadium.</summary>
        public int SectionCount { get; }

        // --- State Duration Ranges (seconds) ---

        /// <summary>Minimum cheering duration in seconds.</summary>
        public float CheeringDurationMin { get; }

        /// <summary>Maximum cheering duration in seconds.</summary>
        public float CheeringDurationMax { get; }

        /// <summary>Minimum celebrating duration in seconds.</summary>
        public float CelebratingDurationMin { get; }

        /// <summary>Maximum celebrating duration in seconds.</summary>
        public float CelebratingDurationMax { get; }

        /// <summary>Booing duration in seconds.</summary>
        public float BooingDuration { get; }

        /// <summary>Standing duration in seconds.</summary>
        public float StandingDuration { get; }

        /// <summary>Dejected duration in seconds.</summary>
        public float DejectedDuration { get; }

        /// <summary>Duration per section for wave animations in seconds.</summary>
        public float WaveDurationPerSection { get; }

        // --- Transition ---

        /// <summary>Blend time when transitioning between animation states (seconds).</summary>
        public float TransitionBlendTime { get; }

        // --- Section Reaction Delay ---

        /// <summary>
        /// Delay in seconds per unit distance for sections further from the action.
        /// Closer sections react first; farther sections are delayed by
        /// (distance * ReactionDelayPerUnit).
        /// </summary>
        public float ReactionDelayPerUnit { get; }

        // --- Intensity ---

        /// <summary>Minimum intensity for crowd animation amplitude (0-1).</summary>
        public float MinIntensity { get; }

        /// <summary>Maximum intensity for crowd animation amplitude (0-1).</summary>
        public float MaxIntensity { get; }

        // --- Mexican Wave ---

        /// <summary>Delay between successive sections during a Mexican wave (seconds).</summary>
        public float MexicanWaveDelay { get; }

        /// <summary>Whether wave moves clockwise (true) or counter-clockwise (false).</summary>
        public bool MexicanWaveClockwise { get; }

        /// <summary>
        /// Minimum section participation ratio for Mexican wave (0-1).
        /// e.g., 0.75 means at least 75% of sections participate.
        /// </summary>
        public float MexicanWaveMinParticipation { get; }

        /// <summary>Goal lead required to trigger a Mexican wave.</summary>
        public int MexicanWaveGoalLeadThreshold { get; }

        /// <summary>Duration each section stays in the wave-up position (seconds).</summary>
        public float MexicanWaveUpDuration { get; }

        public CrowdAnimationConfig(
            int sectionCount = 8,
            float cheeringDurationMin = 3f,
            float cheeringDurationMax = 5f,
            float celebratingDurationMin = 8f,
            float celebratingDurationMax = 10f,
            float booingDuration = 4f,
            float standingDuration = 3f,
            float dejectedDuration = 5f,
            float waveDurationPerSection = 2f,
            float transitionBlendTime = 0.3f,
            float reactionDelayPerUnit = 0.02f,
            float minIntensity = 0.2f,
            float maxIntensity = 1f,
            float mexicanWaveDelay = 0.5f,
            bool mexicanWaveClockwise = true,
            float mexicanWaveMinParticipation = 0.75f,
            int mexicanWaveGoalLeadThreshold = 2,
            float mexicanWaveUpDuration = 0.8f)
        {
            if (sectionCount <= 0)
                throw new ArgumentException("SectionCount must be positive.", nameof(sectionCount));
            if (cheeringDurationMin < 0f || cheeringDurationMax < cheeringDurationMin)
                throw new ArgumentException("Invalid cheering duration range.", nameof(cheeringDurationMin));
            if (celebratingDurationMin < 0f || celebratingDurationMax < celebratingDurationMin)
                throw new ArgumentException("Invalid celebrating duration range.", nameof(celebratingDurationMin));
            if (minIntensity < 0f || maxIntensity > 1f || minIntensity > maxIntensity)
                throw new ArgumentException("Invalid intensity range.", nameof(minIntensity));
            if (mexicanWaveMinParticipation < 0f || mexicanWaveMinParticipation > 1f)
                throw new ArgumentException("MexicanWaveMinParticipation must be between 0 and 1.", nameof(mexicanWaveMinParticipation));

            SectionCount = sectionCount;
            CheeringDurationMin = cheeringDurationMin;
            CheeringDurationMax = cheeringDurationMax;
            CelebratingDurationMin = celebratingDurationMin;
            CelebratingDurationMax = celebratingDurationMax;
            BooingDuration = booingDuration;
            StandingDuration = standingDuration;
            DejectedDuration = dejectedDuration;
            WaveDurationPerSection = waveDurationPerSection;
            TransitionBlendTime = transitionBlendTime;
            ReactionDelayPerUnit = reactionDelayPerUnit;
            MinIntensity = minIntensity;
            MaxIntensity = maxIntensity;
            MexicanWaveDelay = mexicanWaveDelay;
            MexicanWaveClockwise = mexicanWaveClockwise;
            MexicanWaveMinParticipation = mexicanWaveMinParticipation;
            MexicanWaveGoalLeadThreshold = mexicanWaveGoalLeadThreshold;
            MexicanWaveUpDuration = mexicanWaveUpDuration;
        }

        /// <summary>
        /// Get the state duration for a given state. For states with a range,
        /// returns the midpoint. Use GetStateDurationRange for min/max.
        /// </summary>
        public float GetStateDuration(CrowdAnimState state)
        {
            switch (state)
            {
                case CrowdAnimState.Cheering:
                    return (CheeringDurationMin + CheeringDurationMax) / 2f;
                case CrowdAnimState.Celebrating:
                    return (CelebratingDurationMin + CelebratingDurationMax) / 2f;
                case CrowdAnimState.Booing:
                    return BooingDuration;
                case CrowdAnimState.Standing:
                    return StandingDuration;
                case CrowdAnimState.Dejected:
                    return DejectedDuration;
                case CrowdAnimState.WaveLeft:
                case CrowdAnimState.WaveRight:
                    return WaveDurationPerSection;
                case CrowdAnimState.Seated:
                case CrowdAnimState.Idle:
                default:
                    return 0f; // No timeout for rest states
            }
        }

        /// <summary>
        /// Get the min/max duration range for a given state.
        /// </summary>
        public void GetStateDurationRange(CrowdAnimState state, out float min, out float max)
        {
            switch (state)
            {
                case CrowdAnimState.Cheering:
                    min = CheeringDurationMin;
                    max = CheeringDurationMax;
                    break;
                case CrowdAnimState.Celebrating:
                    min = CelebratingDurationMin;
                    max = CelebratingDurationMax;
                    break;
                default:
                    float d = GetStateDuration(state);
                    min = d;
                    max = d;
                    break;
            }
        }

        /// <summary>
        /// Minimum number of sections that participate in a Mexican wave.
        /// </summary>
        public int MexicanWaveMinSections =>
            Math.Max(1, (int)(SectionCount * MexicanWaveMinParticipation));

        /// <summary>
        /// Total time for a full Mexican wave to propagate across all sections.
        /// </summary>
        public float MexicanWaveTotalDuration =>
            SectionCount * MexicanWaveDelay + MexicanWaveUpDuration;
    }

    /// <summary>
    /// Tracks per-section animation state, team affiliation, and timing.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class CrowdSectionBehavior
    {
        /// <summary>Section index (0-based).</summary>
        public int SectionIndex { get; }

        /// <summary>Which team this section supports.</summary>
        public SectionAffiliation Affiliation { get; }

        /// <summary>Current animation state.</summary>
        public CrowdAnimState CurrentState { get; private set; }

        /// <summary>Previous animation state (before current transition).</summary>
        public CrowdAnimState PreviousState { get; private set; }

        /// <summary>Time elapsed in the current state (seconds).</summary>
        public float StateElapsed { get; private set; }

        /// <summary>Duration the current state should last before reverting. 0 = indefinite.</summary>
        public float StateDuration { get; private set; }

        /// <summary>Current animation intensity (0-1).</summary>
        public float Intensity { get; private set; }

        /// <summary>Pending reaction delay before entering the queued state.</summary>
        public float PendingDelay { get; private set; }

        /// <summary>The state queued to enter after PendingDelay expires.</summary>
        public CrowdAnimState QueuedState { get; private set; }

        /// <summary>Duration for the queued state.</summary>
        public float QueuedStateDuration { get; private set; }

        /// <summary>Intensity for the queued state.</summary>
        public float QueuedIntensity { get; private set; }

        /// <summary>Whether this section has a queued state pending.</summary>
        public bool HasQueuedState { get; private set; }

        /// <summary>Blend progress for transitioning between states (0-1).</summary>
        public float BlendProgress { get; private set; }

        public CrowdSectionBehavior(int sectionIndex, SectionAffiliation affiliation)
        {
            if (sectionIndex < 0)
                throw new ArgumentException("SectionIndex must be non-negative.", nameof(sectionIndex));

            SectionIndex = sectionIndex;
            Affiliation = affiliation;
            CurrentState = CrowdAnimState.Idle;
            PreviousState = CrowdAnimState.Idle;
            Intensity = 0.3f;
            BlendProgress = 1f;
        }

        /// <summary>
        /// Immediately transition to a new state with the given duration and intensity.
        /// </summary>
        public void SetState(CrowdAnimState newState, float duration, float intensity)
        {
            intensity = Math.Max(0f, Math.Min(1f, intensity));
            PreviousState = CurrentState;
            CurrentState = newState;
            StateElapsed = 0f;
            StateDuration = duration;
            Intensity = intensity;
            BlendProgress = 0f;
            HasQueuedState = false;
        }

        /// <summary>
        /// Queue a state to enter after a delay (used for reaction delay / wave propagation).
        /// </summary>
        public void QueueState(CrowdAnimState state, float delay, float duration, float intensity)
        {
            QueuedState = state;
            PendingDelay = delay;
            QueuedStateDuration = duration;
            QueuedIntensity = Math.Max(0f, Math.Min(1f, intensity));
            HasQueuedState = true;
        }

        /// <summary>
        /// Advance the section by deltaTime. Handles queued states, timeouts, and blend progress.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        /// <param name="blendTime">Blend time for transitions (seconds).</param>
        public void Update(float deltaTime, float blendTime)
        {
            // Process pending queued state
            if (HasQueuedState)
            {
                PendingDelay -= deltaTime;
                if (PendingDelay <= 0f)
                {
                    SetState(QueuedState, QueuedStateDuration, QueuedIntensity);
                    HasQueuedState = false;
                }
            }

            // Advance state elapsed
            StateElapsed += deltaTime;

            // Advance blend progress
            if (BlendProgress < 1f)
            {
                if (blendTime > 0f)
                    BlendProgress = Math.Min(1f, BlendProgress + deltaTime / blendTime);
                else
                    BlendProgress = 1f;
            }

            // Check for state timeout (revert to Idle)
            if (StateDuration > 0f && StateElapsed >= StateDuration)
            {
                // Active states revert to Idle when their duration expires
                if (CurrentState != CrowdAnimState.Idle && CurrentState != CrowdAnimState.Seated)
                {
                    SetState(CrowdAnimState.Idle, 0f, 0.3f);
                }
            }
        }
    }
}
