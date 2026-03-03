using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// LOD level identifiers from highest detail to fully culled.
    /// </summary>
    public enum LODLevel
    {
        LOD0_High = 0,
        LOD1_Medium = 1,
        LOD2_Low = 2,
        LOD3_Billboard = 3,
        Culled = 4
    }

    /// <summary>
    /// Per-object LOD profile defining screen-height thresholds for each level,
    /// cross-fade width, and quality bias. Pure C# with no engine dependencies.
    /// </summary>
    public class LODProfile
    {
        /// <summary>Screen-height threshold (0-1) below which LOD0 transitions to LOD1.</summary>
        public float LOD0ScreenHeight { get; }

        /// <summary>Screen-height threshold (0-1) below which LOD1 transitions to LOD2.</summary>
        public float LOD1ScreenHeight { get; }

        /// <summary>Screen-height threshold (0-1) below which LOD2 transitions to LOD3 (billboard).</summary>
        public float LOD2ScreenHeight { get; }

        /// <summary>Screen-height threshold (0-1) below which the object is fully culled.</summary>
        public float CullScreenHeight { get; }

        /// <summary>Cross-fade width as a fraction of the threshold (0-1).</summary>
        public float CrossFadeWidth { get; }

        /// <summary>
        /// Quality bias offset applied to screen height before LOD selection.
        /// Positive values increase quality (delay LOD transitions), negative reduce it.
        /// </summary>
        public float Bias { get; set; }

        /// <summary>Human-readable profile name.</summary>
        public string ProfileName { get; }

        public LODProfile(
            string profileName,
            float lod0ScreenHeight = 0.40f,
            float lod1ScreenHeight = 0.15f,
            float lod2ScreenHeight = 0.05f,
            float cullScreenHeight = 0.02f,
            float crossFadeWidth = 0.05f,
            float bias = 0f)
        {
            if (string.IsNullOrEmpty(profileName))
                throw new ArgumentException("Profile name must not be null or empty.", nameof(profileName));
            if (lod0ScreenHeight <= 0f || lod0ScreenHeight > 1f)
                throw new ArgumentException("LOD0 screen height must be in (0, 1].", nameof(lod0ScreenHeight));
            if (lod1ScreenHeight < 0f || lod1ScreenHeight >= lod0ScreenHeight)
                throw new ArgumentException("LOD1 screen height must be in [0, LOD0).", nameof(lod1ScreenHeight));
            if (lod2ScreenHeight < 0f || lod2ScreenHeight >= lod1ScreenHeight)
                throw new ArgumentException("LOD2 screen height must be in [0, LOD1).", nameof(lod2ScreenHeight));
            if (cullScreenHeight < 0f || cullScreenHeight >= lod2ScreenHeight)
                throw new ArgumentException("Cull screen height must be in [0, LOD2).", nameof(cullScreenHeight));
            if (crossFadeWidth < 0f || crossFadeWidth > 1f)
                throw new ArgumentException("Cross-fade width must be in [0, 1].", nameof(crossFadeWidth));

            ProfileName = profileName;
            LOD0ScreenHeight = lod0ScreenHeight;
            LOD1ScreenHeight = lod1ScreenHeight;
            LOD2ScreenHeight = lod2ScreenHeight;
            CullScreenHeight = cullScreenHeight;
            CrossFadeWidth = crossFadeWidth;
            Bias = bias;
        }

        /// <summary>
        /// Returns the LOD level appropriate for the given screen height,
        /// accounting for the current bias.
        /// </summary>
        public LODLevel GetLODLevel(float screenHeight)
        {
            float biased = screenHeight + Bias;

            if (biased >= LOD0ScreenHeight) return LODLevel.LOD0_High;
            if (biased >= LOD1ScreenHeight) return LODLevel.LOD1_Medium;
            if (biased >= LOD2ScreenHeight) return LODLevel.LOD2_Low;
            if (biased >= CullScreenHeight) return LODLevel.LOD3_Billboard;
            return LODLevel.Culled;
        }

        /// <summary>
        /// Returns the screen-height threshold for the given LOD level.
        /// </summary>
        public float GetThreshold(LODLevel level)
        {
            switch (level)
            {
                case LODLevel.LOD0_High: return LOD0ScreenHeight;
                case LODLevel.LOD1_Medium: return LOD1ScreenHeight;
                case LODLevel.LOD2_Low: return LOD2ScreenHeight;
                case LODLevel.LOD3_Billboard: return CullScreenHeight;
                case LODLevel.Culled: return 0f;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// Pre-built LOD profile for character models.
    /// LOD0=30K tris, LOD1=5K, LOD2=500 (billboard), Culled at less than 2% screen.
    /// </summary>
    public class CharacterLODProfile : LODProfile
    {
        /// <summary>Triangle budget for LOD0 (full detail).</summary>
        public int LOD0Triangles { get; }

        /// <summary>Triangle budget for LOD1 (medium detail).</summary>
        public int LOD1Triangles { get; }

        /// <summary>Triangle budget for LOD2 (billboard/low detail).</summary>
        public int LOD2Triangles { get; }

        public CharacterLODProfile(
            int lod0Triangles = 30000,
            int lod1Triangles = 5000,
            int lod2Triangles = 500,
            float lod0ScreenHeight = 0.40f,
            float lod1ScreenHeight = 0.15f,
            float lod2ScreenHeight = 0.05f,
            float cullScreenHeight = 0.02f,
            float crossFadeWidth = 0.05f,
            float bias = 0f)
            : base("Character", lod0ScreenHeight, lod1ScreenHeight, lod2ScreenHeight,
                   cullScreenHeight, crossFadeWidth, bias)
        {
            if (lod0Triangles <= 0) throw new ArgumentException("LOD0 triangles must be positive.", nameof(lod0Triangles));
            if (lod1Triangles <= 0) throw new ArgumentException("LOD1 triangles must be positive.", nameof(lod1Triangles));
            if (lod2Triangles <= 0) throw new ArgumentException("LOD2 triangles must be positive.", nameof(lod2Triangles));
            if (lod1Triangles >= lod0Triangles) throw new ArgumentException("LOD1 triangles must be less than LOD0.", nameof(lod1Triangles));
            if (lod2Triangles >= lod1Triangles) throw new ArgumentException("LOD2 triangles must be less than LOD1.", nameof(lod2Triangles));

            LOD0Triangles = lod0Triangles;
            LOD1Triangles = lod1Triangles;
            LOD2Triangles = lod2Triangles;
        }

        /// <summary>Returns the triangle budget for the given LOD level.</summary>
        public int GetTriangleBudget(LODLevel level)
        {
            switch (level)
            {
                case LODLevel.LOD0_High: return LOD0Triangles;
                case LODLevel.LOD1_Medium: return LOD1Triangles;
                case LODLevel.LOD2_Low: return LOD2Triangles;
                case LODLevel.LOD3_Billboard: return LOD2Triangles;
                case LODLevel.Culled: return 0;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// Pre-built LOD profile for stadium geometry (stands, structures).
    /// Uses wider thresholds since stadium elements are larger and mostly static.
    /// </summary>
    public class StadiumLODProfile : LODProfile
    {
        /// <summary>LOD0: full detail geometry with all features.</summary>
        public int LOD0Triangles { get; }

        /// <summary>LOD1: simplified geometry.</summary>
        public int LOD1Triangles { get; }

        /// <summary>LOD2: impostor or very low detail.</summary>
        public int LOD2Triangles { get; }

        public StadiumLODProfile(
            int lod0Triangles = 50000,
            int lod1Triangles = 15000,
            int lod2Triangles = 2000,
            float lod0ScreenHeight = 0.50f,
            float lod1ScreenHeight = 0.20f,
            float lod2ScreenHeight = 0.08f,
            float cullScreenHeight = 0.03f,
            float crossFadeWidth = 0.04f,
            float bias = 0f)
            : base("Stadium", lod0ScreenHeight, lod1ScreenHeight, lod2ScreenHeight,
                   cullScreenHeight, crossFadeWidth, bias)
        {
            if (lod0Triangles <= 0) throw new ArgumentException("LOD0 triangles must be positive.", nameof(lod0Triangles));
            if (lod1Triangles <= 0) throw new ArgumentException("LOD1 triangles must be positive.", nameof(lod1Triangles));
            if (lod2Triangles <= 0) throw new ArgumentException("LOD2 triangles must be positive.", nameof(lod2Triangles));

            LOD0Triangles = lod0Triangles;
            LOD1Triangles = lod1Triangles;
            LOD2Triangles = lod2Triangles;
        }
    }

    /// <summary>
    /// Pre-built LOD profile for crowd sections.
    /// LOD0: individual meshes, LOD1: merged batches, LOD2: billboard sheets.
    /// </summary>
    public class CrowdLODProfile : LODProfile
    {
        /// <summary>LOD0 mode description: individual meshes per crowd member.</summary>
        public string LOD0Mode { get; }

        /// <summary>LOD1 mode description: merged batch meshes.</summary>
        public string LOD1Mode { get; }

        /// <summary>LOD2 mode description: billboard texture sheets.</summary>
        public string LOD2Mode { get; }

        /// <summary>Max individual crowd meshes at LOD0 before forcing merge.</summary>
        public int MaxIndividualMeshes { get; }

        /// <summary>Triangles per individual crowd member at LOD0.</summary>
        public int TrianglesPerIndividual { get; }

        /// <summary>Triangles per batch row at LOD1.</summary>
        public int TrianglesPerBatch { get; }

        public CrowdLODProfile(
            int maxIndividualMeshes = 200,
            int trianglesPerIndividual = 500,
            int trianglesPerBatch = 100,
            float lod0ScreenHeight = 0.30f,
            float lod1ScreenHeight = 0.12f,
            float lod2ScreenHeight = 0.04f,
            float cullScreenHeight = 0.01f,
            float crossFadeWidth = 0.03f,
            float bias = 0f)
            : base("Crowd", lod0ScreenHeight, lod1ScreenHeight, lod2ScreenHeight,
                   cullScreenHeight, crossFadeWidth, bias)
        {
            if (maxIndividualMeshes <= 0) throw new ArgumentException("Max individual meshes must be positive.", nameof(maxIndividualMeshes));
            if (trianglesPerIndividual <= 0) throw new ArgumentException("Triangles per individual must be positive.", nameof(trianglesPerIndividual));
            if (trianglesPerBatch <= 0) throw new ArgumentException("Triangles per batch must be positive.", nameof(trianglesPerBatch));

            MaxIndividualMeshes = maxIndividualMeshes;
            TrianglesPerIndividual = trianglesPerIndividual;
            TrianglesPerBatch = trianglesPerBatch;
            LOD0Mode = "Individual";
            LOD1Mode = "MergedBatch";
            LOD2Mode = "BillboardSheet";
        }
    }

    /// <summary>
    /// Budget constraints for the LOD system.
    /// Enforces maximum triangle counts and active LOD0 character limits.
    /// </summary>
    public class LODBudgetConfig
    {
        /// <summary>Maximum characters that can be rendered at LOD0 simultaneously.</summary>
        public int MaxActiveLOD0Characters { get; }

        /// <summary>Maximum total triangles for all LOD-managed objects combined.</summary>
        public int MaxTotalTriangles { get; }

        /// <summary>Target frame rate in fps. LOD bias adjusts if frame time exceeds this.</summary>
        public float TargetFPS { get; }

        /// <summary>Frame time budget in seconds (1 / TargetFPS).</summary>
        public float FrameTimeBudget => 1f / TargetFPS;

        public LODBudgetConfig(
            int maxActiveLOD0Characters = 6,
            int maxTotalTriangles = 500000,
            float targetFPS = 60f)
        {
            if (maxActiveLOD0Characters <= 0) throw new ArgumentException("Max active LOD0 characters must be positive.", nameof(maxActiveLOD0Characters));
            if (maxTotalTriangles <= 0) throw new ArgumentException("Max total triangles must be positive.", nameof(maxTotalTriangles));
            if (targetFPS <= 0f) throw new ArgumentException("Target FPS must be positive.", nameof(targetFPS));

            MaxActiveLOD0Characters = maxActiveLOD0Characters;
            MaxTotalTriangles = maxTotalTriangles;
            TargetFPS = targetFPS;
        }
    }

    /// <summary>
    /// Transition settings for LOD switching: cross-fade duration, dither pattern ID,
    /// and speed-based quality reduction factor.
    /// </summary>
    public class LODTransitionConfig
    {
        /// <summary>Duration in seconds for cross-fade between LOD levels.</summary>
        public float CrossFadeDuration { get; }

        /// <summary>
        /// Dither pattern identifier.
        /// 0 = Bayer 4x4, 1 = Bayer 8x8, 2 = Blue noise.
        /// </summary>
        public int DitherPatternId { get; }

        /// <summary>
        /// Speed-based quality reduction factor (0-1).
        /// When an object is moving fast, reduce LOD quality by this factor
        /// (lower LOD sooner) since detail is less visible in motion.
        /// </summary>
        public float SpeedQualityReduction { get; }

        /// <summary>Minimum speed (units/sec) before speed-based reduction kicks in.</summary>
        public float SpeedThreshold { get; }

        public LODTransitionConfig(
            float crossFadeDuration = 0.5f,
            int ditherPatternId = 0,
            float speedQualityReduction = 0.2f,
            float speedThreshold = 5f)
        {
            if (crossFadeDuration < 0f) throw new ArgumentException("Cross-fade duration must be non-negative.", nameof(crossFadeDuration));
            if (ditherPatternId < 0 || ditherPatternId > 2) throw new ArgumentException("Dither pattern ID must be 0, 1, or 2.", nameof(ditherPatternId));
            if (speedQualityReduction < 0f || speedQualityReduction > 1f) throw new ArgumentException("Speed quality reduction must be in [0, 1].", nameof(speedQualityReduction));
            if (speedThreshold < 0f) throw new ArgumentException("Speed threshold must be non-negative.", nameof(speedThreshold));

            CrossFadeDuration = crossFadeDuration;
            DitherPatternId = ditherPatternId;
            SpeedQualityReduction = speedQualityReduction;
            SpeedThreshold = speedThreshold;
        }
    }

    /// <summary>
    /// Master configuration aggregating all LOD profiles, budget, and transition settings.
    /// </summary>
    public class LODSystemConfig
    {
        public CharacterLODProfile CharacterProfile { get; }
        public StadiumLODProfile StadiumProfile { get; }
        public CrowdLODProfile CrowdProfile { get; }
        public LODBudgetConfig Budget { get; }
        public LODTransitionConfig Transition { get; }

        public LODSystemConfig(
            CharacterLODProfile characterProfile = null,
            StadiumLODProfile stadiumProfile = null,
            CrowdLODProfile crowdProfile = null,
            LODBudgetConfig budget = null,
            LODTransitionConfig transition = null)
        {
            CharacterProfile = characterProfile ?? new CharacterLODProfile();
            StadiumProfile = stadiumProfile ?? new StadiumLODProfile();
            CrowdProfile = crowdProfile ?? new CrowdLODProfile();
            Budget = budget ?? new LODBudgetConfig();
            Transition = transition ?? new LODTransitionConfig();
        }

        /// <summary>Creates a default LOD system config suitable for 5v5 matches at 60fps.</summary>
        public static LODSystemConfig CreateDefault()
        {
            return new LODSystemConfig();
        }

        /// <summary>Creates a performance-focused config with more aggressive LOD transitions.</summary>
        public static LODSystemConfig CreatePerformance()
        {
            return new LODSystemConfig(
                characterProfile: new CharacterLODProfile(
                    lod0ScreenHeight: 0.50f,
                    lod1ScreenHeight: 0.25f,
                    lod2ScreenHeight: 0.10f,
                    cullScreenHeight: 0.04f),
                budget: new LODBudgetConfig(
                    maxActiveLOD0Characters: 4,
                    maxTotalTriangles: 300000,
                    targetFPS: 60f));
        }
    }
}
