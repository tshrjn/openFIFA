using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Game event types that trigger crowd reactions.
    /// </summary>
    public enum CrowdGameEvent
    {
        Goal,
        NearMiss,
        Foul,
        Kickoff,
        HalfTime,
        FullTime,
        BigSave,
        MexicanWaveTrigger
    }

    /// <summary>
    /// Master crowd director: coordinates all section behaviors in response to
    /// game events. Pure C# — no Unity dependency, fully testable in EditMode.
    /// Builds on CrowdReactionLogic (US-025) intensity data without modifying it.
    /// </summary>
    public class CrowdDirector
    {
        private readonly CrowdAnimationConfig _config;
        private readonly List<CrowdSectionBehavior> _sections;
        private readonly WaveOrchestrator _waveOrchestrator;
        private readonly ProximityReactor _proximityReactor;
        private readonly Random _rng;

        /// <summary>The animation config in use.</summary>
        public CrowdAnimationConfig Config => _config;

        /// <summary>All crowd section behaviors.</summary>
        public IReadOnlyList<CrowdSectionBehavior> Sections => _sections;

        /// <summary>The wave orchestrator for Mexican waves.</summary>
        public WaveOrchestrator WaveOrchestrator => _waveOrchestrator;

        /// <summary>The proximity reactor for distance-based intensity.</summary>
        public ProximityReactor ProximityReactor => _proximityReactor;

        /// <summary>
        /// Create a CrowdDirector with the given config and section affiliations.
        /// </summary>
        /// <param name="config">Animation configuration.</param>
        /// <param name="affiliations">
        /// Per-section team affiliation. If null, defaults to:
        /// sections 0-2 = Home, 3-5 = Away, 6-7 = Neutral.
        /// </param>
        /// <param name="seed">Random seed for duration variation.</param>
        public CrowdDirector(
            CrowdAnimationConfig config,
            IList<SectionAffiliation> affiliations = null,
            int seed = 42)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = new Random(seed);
            _sections = new List<CrowdSectionBehavior>();

            for (int i = 0; i < config.SectionCount; i++)
            {
                var affiliation = SectionAffiliation.Neutral;
                if (affiliations != null && i < affiliations.Count)
                    affiliation = affiliations[i];
                else if (affiliations == null)
                    affiliation = DefaultAffiliation(i, config.SectionCount);

                _sections.Add(new CrowdSectionBehavior(i, affiliation));
            }

            _waveOrchestrator = new WaveOrchestrator(config, _sections);
            _proximityReactor = new ProximityReactor(config);
        }

        /// <summary>
        /// Handle a game event, updating all sections accordingly.
        /// </summary>
        /// <param name="gameEvent">The type of event.</param>
        /// <param name="scoringTeam">Team involved (for Goal, NearMiss).</param>
        /// <param name="scoreDifference">Current score difference (positive = home leading).</param>
        /// <param name="eventPositionX">X position of the event on the pitch.</param>
        /// <param name="eventPositionZ">Z position of the event on the pitch.</param>
        public void HandleEvent(
            CrowdGameEvent gameEvent,
            TeamIdentifier scoringTeam = TeamIdentifier.TeamA,
            int scoreDifference = 0,
            float eventPositionX = 0f,
            float eventPositionZ = 0f)
        {
            switch (gameEvent)
            {
                case CrowdGameEvent.Goal:
                    HandleGoal(scoringTeam, scoreDifference, eventPositionX, eventPositionZ);
                    break;
                case CrowdGameEvent.NearMiss:
                    HandleNearMiss(eventPositionX, eventPositionZ);
                    break;
                case CrowdGameEvent.Foul:
                    HandleFoul(eventPositionX, eventPositionZ);
                    break;
                case CrowdGameEvent.Kickoff:
                    HandleKickoff();
                    break;
                case CrowdGameEvent.HalfTime:
                    HandleHalfTime();
                    break;
                case CrowdGameEvent.FullTime:
                    HandleFullTime(scoreDifference);
                    break;
                case CrowdGameEvent.BigSave:
                    HandleBigSave(eventPositionX, eventPositionZ);
                    break;
                case CrowdGameEvent.MexicanWaveTrigger:
                    _waveOrchestrator.StartWave(_config.MexicanWaveClockwise);
                    break;
            }
        }

        /// <summary>
        /// Advance all section states by deltaTime.
        /// </summary>
        public void Update(float deltaTime)
        {
            _waveOrchestrator.Update(deltaTime);

            for (int i = 0; i < _sections.Count; i++)
            {
                _sections[i].Update(deltaTime, _config.TransitionBlendTime);
            }
        }

        private void HandleGoal(TeamIdentifier scoringTeam, int scoreDifference, float eventX, float eventZ)
        {
            bool homeScored = (scoringTeam == TeamIdentifier.TeamA);

            for (int i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];
                float delay = CalculateReactionDelay(i, eventX, eventZ);
                float duration = RandomInRange(_config.CelebratingDurationMin, _config.CelebratingDurationMax);

                bool sectionSupportsScorer = IsAlignedWithTeam(section.Affiliation, homeScored);

                if (sectionSupportsScorer)
                {
                    // Scoring team's fans: celebrate
                    float intensity = CalculateGoalIntensity(section.Affiliation, true, scoreDifference);
                    section.QueueState(CrowdAnimState.Celebrating, delay, duration, intensity);
                }
                else
                {
                    // Conceding team's fans: dejected
                    float intensity = CalculateGoalIntensity(section.Affiliation, false, scoreDifference);
                    section.QueueState(CrowdAnimState.Dejected, delay, _config.DejectedDuration, intensity);
                }
            }
        }

        private void HandleNearMiss(float eventX, float eventZ)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                float delay = CalculateReactionDelay(i, eventX, eventZ);
                float intensity = _proximityReactor.CalculateIntensity(i, _config.SectionCount, eventX, eventZ);
                _sections[i].QueueState(CrowdAnimState.Standing, delay, _config.StandingDuration, intensity);
            }
        }

        private void HandleFoul(float eventX, float eventZ)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                float delay = CalculateReactionDelay(i, eventX, eventZ);
                float intensity = _proximityReactor.CalculateIntensity(i, _config.SectionCount, eventX, eventZ);
                _sections[i].QueueState(CrowdAnimState.Booing, delay, _config.BooingDuration, intensity);
            }
        }

        private void HandleKickoff()
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                _sections[i].SetState(CrowdAnimState.Idle, 0f, 0.3f);
            }
        }

        private void HandleHalfTime()
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                _sections[i].SetState(CrowdAnimState.Seated, 0f, 0.2f);
            }
        }

        private void HandleFullTime(int scoreDifference)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];
                bool homeWon = scoreDifference > 0;
                bool awayWon = scoreDifference < 0;

                if (scoreDifference == 0)
                {
                    // Draw: mild cheering
                    section.SetState(CrowdAnimState.Cheering, _config.CheeringDurationMax, 0.5f);
                }
                else if (IsAlignedWithTeam(section.Affiliation, homeWon))
                {
                    // Winning team fans celebrate
                    float duration = RandomInRange(_config.CelebratingDurationMin, _config.CelebratingDurationMax);
                    section.SetState(CrowdAnimState.Celebrating, duration, _config.MaxIntensity);
                }
                else
                {
                    // Losing team fans dejected
                    section.SetState(CrowdAnimState.Dejected, _config.DejectedDuration, 0.6f);
                }
            }
        }

        private void HandleBigSave(float eventX, float eventZ)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                float delay = CalculateReactionDelay(i, eventX, eventZ);
                float duration = RandomInRange(_config.CheeringDurationMin, _config.CheeringDurationMax);
                float intensity = _proximityReactor.CalculateIntensity(i, _config.SectionCount, eventX, eventZ);
                _sections[i].QueueState(CrowdAnimState.Cheering, delay, duration, intensity);
            }
        }

        /// <summary>
        /// Calculate reaction delay for a section based on its distance from the event.
        /// Sections are arranged around the pitch; closer ones react sooner.
        /// </summary>
        private float CalculateReactionDelay(int sectionIndex, float eventX, float eventZ)
        {
            float sectionX, sectionZ;
            GetSectionPosition(sectionIndex, _config.SectionCount, out sectionX, out sectionZ);

            float dx = sectionX - eventX;
            float dz = sectionZ - eventZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            return distance * _config.ReactionDelayPerUnit;
        }

        /// <summary>
        /// Get approximate world position of a section center for delay calculation.
        /// Uses standard stadium layout: 8 sections around the pitch.
        /// </summary>
        internal static void GetSectionPosition(int sectionIndex, int totalSections, out float x, out float z)
        {
            // Standard 8-section layout positions (pitch ~50x30):
            // 0=North, 1=South, 2=East, 3=West, 4=NE, 5=NW, 6=SE, 7=SW
            float halfLength = 25f;
            float halfWidth = 15f;
            float standDist = 20f;

            switch (sectionIndex % 8)
            {
                case 0: x = 0f; z = halfWidth + standDist; break;          // North
                case 1: x = 0f; z = -(halfWidth + standDist); break;       // South
                case 2: x = halfLength + standDist; z = 0f; break;         // East
                case 3: x = -(halfLength + standDist); z = 0f; break;      // West
                case 4: x = halfLength + standDist; z = halfWidth + standDist; break;   // NE
                case 5: x = -(halfLength + standDist); z = halfWidth + standDist; break; // NW
                case 6: x = halfLength + standDist; z = -(halfWidth + standDist); break; // SE
                case 7: x = -(halfLength + standDist); z = -(halfWidth + standDist); break; // SW
                default: x = 0f; z = 0f; break;
            }
        }

        private float CalculateGoalIntensity(SectionAffiliation affiliation, bool scoredForThem, int scoreDiff)
        {
            float baseIntensity = scoredForThem ? 0.8f : 0.5f;

            // Home fans react more intensely
            if (affiliation == SectionAffiliation.Home && scoredForThem)
                baseIntensity = 0.95f;

            // Boost intensity based on score closeness (close game = more exciting)
            int absDiff = Math.Abs(scoreDiff);
            if (absDiff <= 1)
                baseIntensity = Math.Min(1f, baseIntensity + 0.1f);

            return Math.Min(1f, baseIntensity);
        }

        private bool IsAlignedWithTeam(SectionAffiliation affiliation, bool isHomeTeamAction)
        {
            if (affiliation == SectionAffiliation.Neutral)
                return true; // Neutral sections respond positively to either team
            if (affiliation == SectionAffiliation.Home && isHomeTeamAction)
                return true;
            if (affiliation == SectionAffiliation.Away && !isHomeTeamAction)
                return true;
            return false;
        }

        private static SectionAffiliation DefaultAffiliation(int index, int totalSections)
        {
            // Default: first ~37% home, next ~37% away, rest neutral
            float ratio = (float)index / totalSections;
            if (ratio < 0.375f) return SectionAffiliation.Home;
            if (ratio < 0.75f) return SectionAffiliation.Away;
            return SectionAffiliation.Neutral;
        }

        private float RandomInRange(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }
    }

    /// <summary>
    /// Orchestrates Mexican wave across stadium sections with timing offsets.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class WaveOrchestrator
    {
        private readonly CrowdAnimationConfig _config;
        private readonly List<CrowdSectionBehavior> _sections;
        private bool _waveActive;
        private float _waveElapsed;
        private bool _waveClockwise;
        private readonly List<int> _waveOrder;
        private int _waveCurrentIndex;

        /// <summary>Whether a wave is currently in progress.</summary>
        public bool IsWaveActive => _waveActive;

        /// <summary>Time elapsed since wave started.</summary>
        public float WaveElapsed => _waveElapsed;

        /// <summary>The ordered list of section indices for the current wave.</summary>
        public IReadOnlyList<int> WaveOrder => _waveOrder;

        /// <summary>Index into WaveOrder for the next section to trigger.</summary>
        public int WaveCurrentIndex => _waveCurrentIndex;

        public WaveOrchestrator(CrowdAnimationConfig config, List<CrowdSectionBehavior> sections)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _waveOrder = new List<int>();
        }

        /// <summary>
        /// Start a Mexican wave across all sections.
        /// </summary>
        /// <param name="clockwise">Direction of the wave.</param>
        public void StartWave(bool clockwise)
        {
            if (_waveActive) return;

            _waveActive = true;
            _waveElapsed = 0f;
            _waveClockwise = clockwise;
            _waveCurrentIndex = 0;

            _waveOrder.Clear();
            BuildWaveOrder(clockwise);
        }

        /// <summary>
        /// Stop the current wave immediately.
        /// </summary>
        public void StopWave()
        {
            _waveActive = false;
            _waveElapsed = 0f;
            _waveCurrentIndex = 0;
        }

        /// <summary>
        /// Update the wave, triggering sections at the appropriate time.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_waveActive) return;

            _waveElapsed += deltaTime;

            // Check if next section should be triggered
            while (_waveCurrentIndex < _waveOrder.Count)
            {
                float triggerTime = _waveCurrentIndex * _config.MexicanWaveDelay;
                if (_waveElapsed >= triggerTime)
                {
                    int sectionIdx = _waveOrder[_waveCurrentIndex];
                    if (sectionIdx < _sections.Count)
                    {
                        var waveState = _waveClockwise ? CrowdAnimState.WaveRight : CrowdAnimState.WaveLeft;
                        _sections[sectionIdx].SetState(waveState, _config.MexicanWaveUpDuration, 1f);
                    }
                    _waveCurrentIndex++;
                }
                else
                {
                    break;
                }
            }

            // Check if wave is complete
            float totalDuration = _config.MexicanWaveTotalDuration;
            if (_waveElapsed >= totalDuration)
            {
                _waveActive = false;
            }
        }

        /// <summary>
        /// Get the timing offset for a specific section in the wave order.
        /// </summary>
        /// <param name="sectionIndex">The section index.</param>
        /// <returns>Time offset in seconds, or -1 if section is not in wave.</returns>
        public float GetSectionTimingOffset(int sectionIndex)
        {
            for (int i = 0; i < _waveOrder.Count; i++)
            {
                if (_waveOrder[i] == sectionIndex)
                    return i * _config.MexicanWaveDelay;
            }
            return -1f;
        }

        private void BuildWaveOrder(bool clockwise)
        {
            // Standard order: 0(N), 4(NE), 2(E), 6(SE), 1(S), 7(SW), 3(W), 5(NW) - clockwise
            // This traces around the stadium perimeter
            int[] clockwiseOrder = { 0, 4, 2, 6, 1, 7, 3, 5 };

            int count = Math.Min(_config.SectionCount, clockwiseOrder.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = clockwise ? i : (count - 1 - i);
                if (clockwiseOrder[idx] < _sections.Count)
                    _waveOrder.Add(clockwiseOrder[idx]);
            }
        }
    }

    /// <summary>
    /// Calculates crowd intensity based on section proximity to the event location.
    /// Sections closer to the action react more intensely.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ProximityReactor
    {
        private readonly CrowdAnimationConfig _config;

        /// <summary>Maximum distance from event to section (for normalization).</summary>
        private const float MaxDistance = 80f;

        public ProximityReactor(CrowdAnimationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Calculate animation intensity for a section based on its distance to the event.
        /// Closer sections get higher intensity.
        /// </summary>
        /// <param name="sectionIndex">The section to calculate for.</param>
        /// <param name="totalSections">Total sections in the stadium.</param>
        /// <param name="eventX">X position of the event.</param>
        /// <param name="eventZ">Z position of the event.</param>
        /// <returns>Intensity value between MinIntensity and MaxIntensity.</returns>
        public float CalculateIntensity(int sectionIndex, int totalSections, float eventX, float eventZ)
        {
            float sectionX, sectionZ;
            CrowdDirector.GetSectionPosition(sectionIndex, totalSections, out sectionX, out sectionZ);

            float dx = sectionX - eventX;
            float dz = sectionZ - eventZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            // Normalize distance: 0 at event, 1 at max distance
            float normalizedDist = Math.Min(1f, distance / MaxDistance);

            // Invert: closer = higher intensity
            float intensityRange = _config.MaxIntensity - _config.MinIntensity;
            float intensity = _config.MaxIntensity - normalizedDist * intensityRange;

            return Math.Max(_config.MinIntensity, Math.Min(_config.MaxIntensity, intensity));
        }

        /// <summary>
        /// Calculate reaction delay for a section based on distance from event.
        /// </summary>
        public float CalculateDelay(int sectionIndex, int totalSections, float eventX, float eventZ)
        {
            float sectionX, sectionZ;
            CrowdDirector.GetSectionPosition(sectionIndex, totalSections, out sectionX, out sectionZ);

            float dx = sectionX - eventX;
            float dz = sectionZ - eventZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            return distance * _config.ReactionDelayPerUnit;
        }
    }
}
