using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Serializable configuration for a group of objects sharing LOD settings.
    /// Used to batch-apply LOD behavior to stadium sections, crowd blocks, etc.
    /// </summary>
    public class LODGroupConfig
    {
        /// <summary>Group identifier (e.g., "NorthStand", "CrowdSection3").</summary>
        public string GroupId { get; }

        /// <summary>The LOD profile governing this group's transitions.</summary>
        public LODProfile Profile { get; }

        /// <summary>Number of objects in this group.</summary>
        public int ObjectCount { get; set; }

        /// <summary>Combined triangle count for all objects in this group at their current LOD.</summary>
        public int CurrentTriangles { get; set; }

        public LODGroupConfig(string groupId, LODProfile profile, int objectCount = 0)
        {
            if (string.IsNullOrEmpty(groupId))
                throw new ArgumentException("Group ID must not be null or empty.", nameof(groupId));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (objectCount < 0)
                throw new ArgumentException("Object count must be non-negative.", nameof(objectCount));

            GroupId = groupId;
            Profile = profile;
            ObjectCount = objectCount;
            CurrentTriangles = 0;
        }
    }

    /// <summary>
    /// Statistics tracking for the LOD system.
    /// Provides real-time data on LOD distribution, triangle usage, and budget.
    /// </summary>
    public struct LODStatistics
    {
        /// <summary>Number of objects at each LOD level. Index maps to (int)LODLevel.</summary>
        public int[] ObjectCountPerLevel;

        /// <summary>Total triangles across all visible LOD-managed objects.</summary>
        public int TotalTriangles;

        /// <summary>Maximum triangle budget from LODBudgetConfig.</summary>
        public int MaxTriangleBudget;

        /// <summary>Number of characters currently at LOD0.</summary>
        public int ActiveLOD0Characters;

        /// <summary>Current LOD bias applied by the dynamic adjuster.</summary>
        public float CurrentBias;

        /// <summary>Current frame time in seconds.</summary>
        public float CurrentFrameTime;

        /// <summary>Budget utilization as a percentage (0-100).</summary>
        public float BudgetUtilizationPercent
        {
            get
            {
                if (MaxTriangleBudget <= 0) return 0f;
                return ((float)TotalTriangles / MaxTriangleBudget) * 100f;
            }
        }

        /// <summary>Average LOD level across all visible objects (0 = all at LOD0, higher = lower quality).</summary>
        public float AverageLODLevel
        {
            get
            {
                if (ObjectCountPerLevel == null) return 0f;

                int totalObjects = 0;
                float weightedSum = 0f;

                for (int i = 0; i < ObjectCountPerLevel.Length; i++)
                {
                    totalObjects += ObjectCountPerLevel[i];
                    weightedSum += i * ObjectCountPerLevel[i];
                }

                if (totalObjects <= 0) return 0f;
                return weightedSum / totalObjects;
            }
        }

        /// <summary>Total number of visible (non-culled) objects.</summary>
        public int TotalVisibleObjects
        {
            get
            {
                if (ObjectCountPerLevel == null) return 0;

                int total = 0;
                // Sum all levels except Culled (index 4)
                int limit = ObjectCountPerLevel.Length < 4 ? ObjectCountPerLevel.Length : 4;
                for (int i = 0; i < limit; i++)
                {
                    total += ObjectCountPerLevel[i];
                }
                return total;
            }
        }

        /// <summary>Creates a fresh statistics struct with zeroed counters.</summary>
        public static LODStatistics Create(int maxBudget)
        {
            return new LODStatistics
            {
                ObjectCountPerLevel = new int[5], // LOD0..LOD3 + Culled
                TotalTriangles = 0,
                MaxTriangleBudget = maxBudget,
                ActiveLOD0Characters = 0,
                CurrentBias = 0f,
                CurrentFrameTime = 0f
            };
        }
    }

    /// <summary>
    /// Computes screen-relative height from camera distance and object bounds.
    /// Uses a perspective projection model: screenHeight = objectHeight / (2 * distance * tan(fov/2)).
    /// Pure C# — no engine dependencies.
    /// </summary>
    public static class LODDistanceCalculator
    {
        /// <summary>
        /// Computes the screen-relative height (0-1) of an object based on its
        /// world-space height, distance from camera, and camera vertical FOV.
        /// </summary>
        /// <param name="objectHeight">World-space height of the object's bounding box.</param>
        /// <param name="distance">Distance from camera to object center.</param>
        /// <param name="verticalFOVDegrees">Camera vertical field of view in degrees.</param>
        /// <returns>Screen-relative height in [0, 1]. Returns 1 if distance is very small.</returns>
        public static float ComputeScreenHeight(float objectHeight, float distance, float verticalFOVDegrees)
        {
            if (objectHeight <= 0f) return 0f;
            if (distance <= 0.001f) return 1f;
            if (verticalFOVDegrees <= 0f || verticalFOVDegrees >= 180f) return 0f;

            float halfFOVRadians = (verticalFOVDegrees * 0.5f) * ((float)Math.PI / 180f);
            float halfViewHeight = distance * (float)Math.Tan(halfFOVRadians);

            if (halfViewHeight <= 0f) return 1f;

            float screenHeight = objectHeight / (2f * halfViewHeight);
            return screenHeight > 1f ? 1f : screenHeight;
        }

        /// <summary>
        /// Computes the distance at which an object of given height would have
        /// the specified screen-relative height. Useful for determining LOD transition distances.
        /// </summary>
        /// <param name="objectHeight">World-space height of the object.</param>
        /// <param name="targetScreenHeight">Desired screen-relative height (0-1).</param>
        /// <param name="verticalFOVDegrees">Camera vertical field of view in degrees.</param>
        /// <returns>Distance at which the object would have the target screen height.</returns>
        public static float ComputeDistanceForScreenHeight(float objectHeight, float targetScreenHeight, float verticalFOVDegrees)
        {
            if (objectHeight <= 0f || targetScreenHeight <= 0f) return float.MaxValue;
            if (verticalFOVDegrees <= 0f || verticalFOVDegrees >= 180f) return float.MaxValue;

            float halfFOVRadians = (verticalFOVDegrees * 0.5f) * ((float)Math.PI / 180f);
            float tanHalfFOV = (float)Math.Tan(halfFOVRadians);

            if (tanHalfFOV <= 0f) return float.MaxValue;

            // screenHeight = objectHeight / (2 * distance * tanHalfFOV)
            // => distance = objectHeight / (2 * screenHeight * tanHalfFOV)
            return objectHeight / (2f * targetScreenHeight * tanHalfFOV);
        }
    }

    /// <summary>
    /// Selects the appropriate LOD level for an object based on distance, budget constraints,
    /// and optional speed-based quality reduction.
    /// </summary>
    public class LODSelector
    {
        private readonly LODBudgetConfig _budget;
        private readonly LODTransitionConfig _transition;

        public LODSelector(LODBudgetConfig budget, LODTransitionConfig transition)
        {
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        /// <summary>
        /// Picks the LOD level for a single object given its profile and screen height.
        /// </summary>
        /// <param name="profile">The object's LOD profile.</param>
        /// <param name="screenHeight">Current screen-relative height of the object.</param>
        /// <param name="objectSpeed">Current movement speed of the object (units/sec). 0 for static.</param>
        /// <returns>Selected LOD level.</returns>
        public LODLevel SelectLevel(LODProfile profile, float screenHeight, float objectSpeed = 0f)
        {
            if (profile == null) return LODLevel.Culled;

            float effectiveScreenHeight = screenHeight;

            // Apply speed-based quality reduction
            if (objectSpeed > _transition.SpeedThreshold && _transition.SpeedQualityReduction > 0f)
            {
                float speedFactor = 1f - _transition.SpeedQualityReduction;
                effectiveScreenHeight *= speedFactor;
            }

            return profile.GetLODLevel(effectiveScreenHeight);
        }

        /// <summary>
        /// Picks the LOD level with budget enforcement. If the budget is exceeded,
        /// forces a lower LOD than what distance would suggest.
        /// </summary>
        /// <param name="profile">The object's LOD profile.</param>
        /// <param name="screenHeight">Current screen-relative height.</param>
        /// <param name="currentLOD0Count">Current number of characters at LOD0.</param>
        /// <param name="currentTotalTriangles">Current total triangle count across all objects.</param>
        /// <param name="objectTrianglesAtLOD0">Triangle count of this object at LOD0.</param>
        /// <param name="objectSpeed">Current movement speed of the object.</param>
        /// <returns>Budget-constrained LOD level.</returns>
        public LODLevel SelectLevelWithBudget(
            LODProfile profile,
            float screenHeight,
            int currentLOD0Count,
            int currentTotalTriangles,
            int objectTrianglesAtLOD0,
            float objectSpeed = 0f)
        {
            var desired = SelectLevel(profile, screenHeight, objectSpeed);

            // Enforce LOD0 character limit
            if (desired == LODLevel.LOD0_High && currentLOD0Count >= _budget.MaxActiveLOD0Characters)
            {
                desired = LODLevel.LOD1_Medium;
            }

            // Enforce total triangle budget
            if (currentTotalTriangles + objectTrianglesAtLOD0 > _budget.MaxTotalTriangles
                && desired == LODLevel.LOD0_High)
            {
                desired = LODLevel.LOD1_Medium;
            }

            return desired;
        }

        /// <summary>
        /// Computes the cross-fade amount (0-1) for a transition between two LOD levels.
        /// Returns 0 if not in a transition zone, 1 if fully transitioned.
        /// </summary>
        /// <param name="profile">The object's LOD profile.</param>
        /// <param name="screenHeight">Current screen-relative height.</param>
        /// <param name="fromLevel">The LOD level being transitioned from.</param>
        /// <returns>Cross-fade amount [0, 1].</returns>
        public float ComputeCrossFadeAmount(LODProfile profile, float screenHeight, LODLevel fromLevel)
        {
            if (profile == null || profile.CrossFadeWidth <= 0f) return 0f;

            float threshold = profile.GetThreshold(fromLevel);
            float fadeStart = threshold + profile.CrossFadeWidth;
            float fadeEnd = threshold;

            if (screenHeight >= fadeStart) return 0f;
            if (screenHeight <= fadeEnd) return 1f;

            return 1f - ((screenHeight - fadeEnd) / (fadeStart - fadeEnd));
        }
    }

    /// <summary>
    /// Tracks LOD0/LOD1/LOD2 counts and enforces triangle budget.
    /// Central accounting for the LOD system.
    /// </summary>
    public class LODBudgetManager
    {
        private readonly LODBudgetConfig _config;
        private readonly int[] _levelCounts;
        private int _totalTriangles;
        private int _lod0CharacterCount;

        public LODBudgetConfig Config => _config;

        /// <summary>Current total triangle count across all tracked objects.</summary>
        public int TotalTriangles => _totalTriangles;

        /// <summary>Current number of characters at LOD0.</summary>
        public int LOD0CharacterCount => _lod0CharacterCount;

        /// <summary>Current object count per LOD level.</summary>
        public int GetCountAtLevel(LODLevel level) => _levelCounts[(int)level];

        /// <summary>Whether the triangle budget is exceeded.</summary>
        public bool IsOverBudget => _totalTriangles > _config.MaxTotalTriangles;

        /// <summary>Whether the LOD0 character limit is reached.</summary>
        public bool IsLOD0Full => _lod0CharacterCount >= _config.MaxActiveLOD0Characters;

        /// <summary>Budget utilization as a percentage (0-100).</summary>
        public float BudgetUtilizationPercent
        {
            get
            {
                if (_config.MaxTotalTriangles <= 0) return 0f;
                return ((float)_totalTriangles / _config.MaxTotalTriangles) * 100f;
            }
        }

        /// <summary>Remaining triangle budget.</summary>
        public int RemainingTriangles => Math.Max(0, _config.MaxTotalTriangles - _totalTriangles);

        /// <summary>Remaining LOD0 character slots.</summary>
        public int RemainingLOD0Slots => Math.Max(0, _config.MaxActiveLOD0Characters - _lod0CharacterCount);

        public LODBudgetManager(LODBudgetConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _levelCounts = new int[5]; // LOD0..LOD3 + Culled
            _totalTriangles = 0;
            _lod0CharacterCount = 0;
        }

        /// <summary>
        /// Registers an object at the given LOD level with the specified triangle count.
        /// </summary>
        /// <param name="level">LOD level the object is rendered at.</param>
        /// <param name="triangleCount">Triangle count at that level.</param>
        /// <param name="isCharacter">Whether this object is a character (for LOD0 limit tracking).</param>
        public void Register(LODLevel level, int triangleCount, bool isCharacter = false)
        {
            if (triangleCount < 0) triangleCount = 0;

            _levelCounts[(int)level]++;
            if (level != LODLevel.Culled)
            {
                _totalTriangles += triangleCount;
            }
            if (isCharacter && level == LODLevel.LOD0_High)
            {
                _lod0CharacterCount++;
            }
        }

        /// <summary>
        /// Unregisters an object from the budget tracker.
        /// </summary>
        /// <param name="level">LOD level the object was at.</param>
        /// <param name="triangleCount">Triangle count that was allocated.</param>
        /// <param name="isCharacter">Whether this object is a character.</param>
        public void Unregister(LODLevel level, int triangleCount, bool isCharacter = false)
        {
            if (triangleCount < 0) triangleCount = 0;

            _levelCounts[(int)level] = Math.Max(0, _levelCounts[(int)level] - 1);
            if (level != LODLevel.Culled)
            {
                _totalTriangles = Math.Max(0, _totalTriangles - triangleCount);
            }
            if (isCharacter && level == LODLevel.LOD0_High)
            {
                _lod0CharacterCount = Math.Max(0, _lod0CharacterCount - 1);
            }
        }

        /// <summary>
        /// Updates tracking when an object transitions from one LOD level to another.
        /// </summary>
        /// <param name="fromLevel">Previous LOD level.</param>
        /// <param name="toLevel">New LOD level.</param>
        /// <param name="fromTriangles">Triangle count at previous level.</param>
        /// <param name="toTriangles">Triangle count at new level.</param>
        /// <param name="isCharacter">Whether this object is a character.</param>
        public void Transition(LODLevel fromLevel, LODLevel toLevel, int fromTriangles, int toTriangles, bool isCharacter = false)
        {
            Unregister(fromLevel, fromTriangles, isCharacter);
            Register(toLevel, toTriangles, isCharacter);
        }

        /// <summary>
        /// Resets all tracking counters. Call at the beginning of each frame's LOD evaluation.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _levelCounts.Length; i++)
                _levelCounts[i] = 0;
            _totalTriangles = 0;
            _lod0CharacterCount = 0;
        }

        /// <summary>
        /// Returns a snapshot of the current LOD statistics.
        /// </summary>
        public LODStatistics GetStatistics(float currentBias = 0f, float currentFrameTime = 0f)
        {
            var stats = LODStatistics.Create(_config.MaxTotalTriangles);
            Array.Copy(_levelCounts, stats.ObjectCountPerLevel, _levelCounts.Length);
            stats.TotalTriangles = _totalTriangles;
            stats.ActiveLOD0Characters = _lod0CharacterCount;
            stats.CurrentBias = currentBias;
            stats.CurrentFrameTime = currentFrameTime;
            return stats;
        }
    }

    /// <summary>
    /// Dynamically adjusts the global LOD bias based on frame time performance.
    /// If fps drops below target, reduces bias (lowers quality). If fps is good, restores bias.
    /// </summary>
    public class DynamicLODAdjuster
    {
        private readonly LODBudgetConfig _budget;
        private readonly float _biasStep;
        private readonly float _minBias;
        private readonly float _maxBias;
        private float _currentBias;

        /// <summary>Current LOD bias offset applied to all profiles.</summary>
        public float CurrentBias => _currentBias;

        /// <summary>
        /// Creates a dynamic LOD adjuster.
        /// </summary>
        /// <param name="budget">Budget config with target FPS.</param>
        /// <param name="biasStep">Amount to adjust bias per frame when over/under budget.</param>
        /// <param name="minBias">Minimum bias (most aggressive quality reduction).</param>
        /// <param name="maxBias">Maximum bias (highest quality boost).</param>
        public DynamicLODAdjuster(
            LODBudgetConfig budget,
            float biasStep = 0.005f,
            float minBias = -0.15f,
            float maxBias = 0.10f)
        {
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _biasStep = biasStep;
            _minBias = minBias;
            _maxBias = maxBias;
            _currentBias = 0f;
        }

        /// <summary>
        /// Updates the bias based on the current frame time.
        /// Call once per frame after measuring frame time.
        /// </summary>
        /// <param name="frameTime">Duration of the last frame in seconds.</param>
        public void Update(float frameTime)
        {
            if (frameTime <= 0f) return;

            float targetFrameTime = _budget.FrameTimeBudget;

            if (frameTime > targetFrameTime)
            {
                // Frame took too long — reduce quality (lower bias)
                _currentBias -= _biasStep;
                if (_currentBias < _minBias) _currentBias = _minBias;
            }
            else
            {
                // Frame was fast enough — restore quality (raise bias)
                _currentBias += _biasStep * 0.5f; // Recover more slowly than degradation
                if (_currentBias > _maxBias) _currentBias = _maxBias;
            }
        }

        /// <summary>
        /// Applies the current bias to a LOD profile.
        /// </summary>
        /// <param name="profile">Profile to adjust.</param>
        public void ApplyBias(LODProfile profile)
        {
            if (profile != null)
            {
                profile.Bias = _currentBias;
            }
        }

        /// <summary>
        /// Resets the bias to zero.
        /// </summary>
        public void ResetBias()
        {
            _currentBias = 0f;
        }

        /// <summary>
        /// Whether the adjuster is currently reducing quality (bias is negative).
        /// </summary>
        public bool IsReducingQuality => _currentBias < 0f;
    }
}
