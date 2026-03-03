using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US051")]
    [Category("Art")]
    public class US051_LODTests
    {
        // ================================================================
        // LODLevel enum
        // ================================================================

        [Test]
        public void LODLevel_HasFiveLevels()
        {
            var values = Enum.GetValues(typeof(LODLevel));
            Assert.AreEqual(5, values.Length,
                "LODLevel should have 5 values: LOD0_High, LOD1_Medium, LOD2_Low, LOD3_Billboard, Culled.");
        }

        [Test]
        public void LODLevel_ValuesAreOrdered()
        {
            Assert.Less((int)LODLevel.LOD0_High, (int)LODLevel.LOD1_Medium);
            Assert.Less((int)LODLevel.LOD1_Medium, (int)LODLevel.LOD2_Low);
            Assert.Less((int)LODLevel.LOD2_Low, (int)LODLevel.LOD3_Billboard);
            Assert.Less((int)LODLevel.LOD3_Billboard, (int)LODLevel.Culled);
        }

        // ================================================================
        // LODProfile defaults and screen height thresholds
        // ================================================================

        [Test]
        public void LODProfile_DefaultThresholds_AreDecreasing()
        {
            var profile = new CharacterLODProfile();
            Assert.Greater(profile.LOD0ScreenHeight, profile.LOD1ScreenHeight,
                "LOD0 threshold must be higher than LOD1.");
            Assert.Greater(profile.LOD1ScreenHeight, profile.LOD2ScreenHeight,
                "LOD1 threshold must be higher than LOD2.");
            Assert.Greater(profile.LOD2ScreenHeight, profile.CullScreenHeight,
                "LOD2 threshold must be higher than Cull.");
        }

        [Test]
        public void LODProfile_ProfileName_IsSet()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual("Character", profile.ProfileName);
        }

        [Test]
        public void LODProfile_GetLODLevel_HighScreenHeight_ReturnsLOD0()
        {
            var profile = new CharacterLODProfile();
            var level = profile.GetLODLevel(0.50f);
            Assert.AreEqual(LODLevel.LOD0_High, level,
                "Screen height 0.50 (above LOD0 threshold 0.40) should return LOD0.");
        }

        [Test]
        public void LODProfile_GetLODLevel_MediumScreenHeight_ReturnsLOD1()
        {
            var profile = new CharacterLODProfile();
            var level = profile.GetLODLevel(0.20f);
            Assert.AreEqual(LODLevel.LOD1_Medium, level,
                "Screen height 0.20 (between LOD0=0.40 and LOD1=0.15) should return LOD1.");
        }

        [Test]
        public void LODProfile_GetLODLevel_LowScreenHeight_ReturnsLOD2()
        {
            var profile = new CharacterLODProfile();
            var level = profile.GetLODLevel(0.08f);
            Assert.AreEqual(LODLevel.LOD2_Low, level,
                "Screen height 0.08 (between LOD1=0.15 and LOD2=0.05) should return LOD2.");
        }

        [Test]
        public void LODProfile_GetLODLevel_VeryLowScreenHeight_ReturnsBillboard()
        {
            var profile = new CharacterLODProfile();
            var level = profile.GetLODLevel(0.03f);
            Assert.AreEqual(LODLevel.LOD3_Billboard, level,
                "Screen height 0.03 (between LOD2=0.05 and Cull=0.02) should return LOD3 Billboard.");
        }

        [Test]
        public void LODProfile_GetLODLevel_TinyScreenHeight_ReturnsCulled()
        {
            var profile = new CharacterLODProfile();
            var level = profile.GetLODLevel(0.01f);
            Assert.AreEqual(LODLevel.Culled, level,
                "Screen height 0.01 (below Cull=0.02) should be culled.");
        }

        [Test]
        public void LODProfile_GetLODLevel_WithPositiveBias_IncreasesQuality()
        {
            var profile = new CharacterLODProfile();
            // Without bias, 0.35 would be LOD1 (below 0.40)
            // With +0.10 bias, effective = 0.45, which is >= 0.40 => LOD0
            profile.Bias = 0.10f;
            var level = profile.GetLODLevel(0.35f);
            Assert.AreEqual(LODLevel.LOD0_High, level,
                "Positive bias should push screen height up, keeping higher quality.");
        }

        [Test]
        public void LODProfile_GetLODLevel_WithNegativeBias_ReducesQuality()
        {
            var profile = new CharacterLODProfile();
            // Without bias, 0.42 would be LOD0 (>= 0.40)
            // With -0.10 bias, effective = 0.32, which is < 0.40 => LOD1
            profile.Bias = -0.10f;
            var level = profile.GetLODLevel(0.42f);
            Assert.AreEqual(LODLevel.LOD1_Medium, level,
                "Negative bias should reduce effective screen height, lowering quality.");
        }

        [Test]
        public void LODProfile_GetThreshold_ReturnsCorrectValues()
        {
            var profile = new CharacterLODProfile(
                lod0ScreenHeight: 0.40f,
                lod1ScreenHeight: 0.15f,
                lod2ScreenHeight: 0.05f,
                cullScreenHeight: 0.02f);

            Assert.AreEqual(0.40f, profile.GetThreshold(LODLevel.LOD0_High), 0.001f);
            Assert.AreEqual(0.15f, profile.GetThreshold(LODLevel.LOD1_Medium), 0.001f);
            Assert.AreEqual(0.05f, profile.GetThreshold(LODLevel.LOD2_Low), 0.001f);
            Assert.AreEqual(0.02f, profile.GetThreshold(LODLevel.LOD3_Billboard), 0.001f);
            Assert.AreEqual(0f, profile.GetThreshold(LODLevel.Culled), 0.001f);
        }

        [Test]
        public void LODProfile_InvalidThresholds_Throws()
        {
            // LOD1 >= LOD0 should fail
            Assert.Throws<ArgumentException>(() => new CharacterLODProfile(
                lod0ScreenHeight: 0.20f, lod1ScreenHeight: 0.30f));
        }

        // ================================================================
        // CharacterLODProfile specifics
        // ================================================================

        [Test]
        public void CharacterLODProfile_Defaults_LOD0Is30K()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual(30000, profile.LOD0Triangles,
                "Character LOD0 should default to 30K triangles.");
        }

        [Test]
        public void CharacterLODProfile_Defaults_LOD1Is5K()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual(5000, profile.LOD1Triangles,
                "Character LOD1 should default to 5K triangles.");
        }

        [Test]
        public void CharacterLODProfile_Defaults_LOD2Is500()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual(500, profile.LOD2Triangles,
                "Character LOD2 (billboard) should default to 500 triangles.");
        }

        [Test]
        public void CharacterLODProfile_Defaults_CullAt2Percent()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual(0.02f, profile.CullScreenHeight, 0.001f,
                "Characters should be culled below 2% screen height.");
        }

        [Test]
        public void CharacterLODProfile_GetTriangleBudget_AllLevels()
        {
            var profile = new CharacterLODProfile();
            Assert.AreEqual(30000, profile.GetTriangleBudget(LODLevel.LOD0_High));
            Assert.AreEqual(5000, profile.GetTriangleBudget(LODLevel.LOD1_Medium));
            Assert.AreEqual(500, profile.GetTriangleBudget(LODLevel.LOD2_Low));
            Assert.AreEqual(500, profile.GetTriangleBudget(LODLevel.LOD3_Billboard));
            Assert.AreEqual(0, profile.GetTriangleBudget(LODLevel.Culled));
        }

        [Test]
        public void CharacterLODProfile_TrianglesSortedDescending()
        {
            var profile = new CharacterLODProfile();
            Assert.Greater(profile.LOD0Triangles, profile.LOD1Triangles,
                "LOD0 triangles must exceed LOD1.");
            Assert.Greater(profile.LOD1Triangles, profile.LOD2Triangles,
                "LOD1 triangles must exceed LOD2.");
        }

        [Test]
        public void CharacterLODProfile_InvalidTriangles_LOD1GreaterThanLOD0_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CharacterLODProfile(
                lod0Triangles: 5000, lod1Triangles: 10000, lod2Triangles: 500));
        }

        // ================================================================
        // StadiumLODProfile specifics
        // ================================================================

        [Test]
        public void StadiumLODProfile_Defaults_HasCorrectName()
        {
            var profile = new StadiumLODProfile();
            Assert.AreEqual("Stadium", profile.ProfileName);
        }

        [Test]
        public void StadiumLODProfile_Defaults_HigherThresholdsThanCharacter()
        {
            var stadium = new StadiumLODProfile();
            var character = new CharacterLODProfile();
            Assert.Greater(stadium.LOD0ScreenHeight, character.LOD0ScreenHeight,
                "Stadium LOD0 threshold should be higher (larger objects fill more screen).");
        }

        [Test]
        public void StadiumLODProfile_LOD0Triangles_HigherThanCharacter()
        {
            var stadium = new StadiumLODProfile();
            var character = new CharacterLODProfile();
            Assert.Greater(stadium.LOD0Triangles, character.LOD0Triangles,
                "Stadium sections have more geometry than individual characters.");
        }

        // ================================================================
        // CrowdLODProfile specifics
        // ================================================================

        [Test]
        public void CrowdLODProfile_Defaults_HasCorrectModes()
        {
            var profile = new CrowdLODProfile();
            Assert.AreEqual("Individual", profile.LOD0Mode);
            Assert.AreEqual("MergedBatch", profile.LOD1Mode);
            Assert.AreEqual("BillboardSheet", profile.LOD2Mode);
        }

        [Test]
        public void CrowdLODProfile_Defaults_MaxIndividualMeshesIsPositive()
        {
            var profile = new CrowdLODProfile();
            Assert.Greater(profile.MaxIndividualMeshes, 0,
                "Max individual meshes must be positive.");
            Assert.AreEqual(200, profile.MaxIndividualMeshes);
        }

        [Test]
        public void CrowdLODProfile_Defaults_TrianglesPerIndividual()
        {
            var profile = new CrowdLODProfile();
            Assert.AreEqual(500, profile.TrianglesPerIndividual);
            Assert.AreEqual(100, profile.TrianglesPerBatch);
        }

        [Test]
        public void CrowdLODProfile_InvalidMaxMeshes_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CrowdLODProfile(maxIndividualMeshes: 0));
        }

        // ================================================================
        // LODBudgetConfig
        // ================================================================

        [Test]
        public void LODBudgetConfig_Defaults_AreReasonable()
        {
            var budget = new LODBudgetConfig();
            Assert.AreEqual(6, budget.MaxActiveLOD0Characters,
                "Default max LOD0 characters should be 6 (for 5v5 + ball focus).");
            Assert.AreEqual(500000, budget.MaxTotalTriangles,
                "Default total triangle budget should be 500K.");
            Assert.AreEqual(60f, budget.TargetFPS, 0.001f,
                "Default target FPS should be 60.");
        }

        [Test]
        public void LODBudgetConfig_FrameTimeBudget_IsReciprocalOfFPS()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            Assert.AreEqual(1f / 60f, budget.FrameTimeBudget, 0.0001f,
                "Frame time budget should be 1/60 for 60fps target.");
        }

        [Test]
        public void LODBudgetConfig_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => new LODBudgetConfig(maxActiveLOD0Characters: 0));
            Assert.Throws<ArgumentException>(() => new LODBudgetConfig(maxTotalTriangles: -1));
            Assert.Throws<ArgumentException>(() => new LODBudgetConfig(targetFPS: 0f));
        }

        // ================================================================
        // LODTransitionConfig
        // ================================================================

        [Test]
        public void LODTransitionConfig_Defaults_AreReasonable()
        {
            var transition = new LODTransitionConfig();
            Assert.AreEqual(0.5f, transition.CrossFadeDuration, 0.001f,
                "Default cross-fade duration should be 0.5 seconds.");
            Assert.AreEqual(0, transition.DitherPatternId,
                "Default dither pattern should be 0 (Bayer 4x4).");
            Assert.That(transition.SpeedQualityReduction, Is.InRange(0f, 1f),
                "Speed quality reduction must be in [0, 1].");
            Assert.AreEqual(5f, transition.SpeedThreshold, 0.001f);
        }

        [Test]
        public void LODTransitionConfig_InvalidDitherPattern_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LODTransitionConfig(ditherPatternId: 3));
            Assert.Throws<ArgumentException>(() => new LODTransitionConfig(ditherPatternId: -1));
        }

        [Test]
        public void LODTransitionConfig_NegativeCrossFade_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LODTransitionConfig(crossFadeDuration: -0.1f));
        }

        // ================================================================
        // LODBudgetManager tracking
        // ================================================================

        [Test]
        public void LODBudgetManager_InitialState_IsZero()
        {
            var budget = new LODBudgetConfig();
            var manager = new LODBudgetManager(budget);

            Assert.AreEqual(0, manager.TotalTriangles);
            Assert.AreEqual(0, manager.LOD0CharacterCount);
            Assert.IsFalse(manager.IsOverBudget);
            Assert.IsFalse(manager.IsLOD0Full);
        }

        [Test]
        public void LODBudgetManager_Register_TracksTriangles()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 100000);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000, isCharacter: true);
            Assert.AreEqual(30000, manager.TotalTriangles);
            Assert.AreEqual(1, manager.LOD0CharacterCount);
            Assert.AreEqual(1, manager.GetCountAtLevel(LODLevel.LOD0_High));
        }

        [Test]
        public void LODBudgetManager_Register_CulledDoesNotAddTriangles()
        {
            var budget = new LODBudgetConfig();
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.Culled, 30000, isCharacter: true);
            Assert.AreEqual(0, manager.TotalTriangles,
                "Culled objects should not contribute to triangle count.");
            Assert.AreEqual(0, manager.LOD0CharacterCount,
                "Culled characters should not count as LOD0.");
        }

        [Test]
        public void LODBudgetManager_Unregister_RemovesTriangles()
        {
            var budget = new LODBudgetConfig();
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000, isCharacter: true);
            manager.Unregister(LODLevel.LOD0_High, 30000, isCharacter: true);

            Assert.AreEqual(0, manager.TotalTriangles);
            Assert.AreEqual(0, manager.LOD0CharacterCount);
        }

        [Test]
        public void LODBudgetManager_Transition_UpdatesCounts()
        {
            var budget = new LODBudgetConfig();
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000, isCharacter: true);
            manager.Transition(LODLevel.LOD0_High, LODLevel.LOD1_Medium, 30000, 5000, isCharacter: true);

            Assert.AreEqual(5000, manager.TotalTriangles,
                "After LOD0->LOD1 transition, triangles should be LOD1 budget.");
            Assert.AreEqual(0, manager.LOD0CharacterCount,
                "Character should no longer be at LOD0.");
            Assert.AreEqual(1, manager.GetCountAtLevel(LODLevel.LOD1_Medium));
        }

        [Test]
        public void LODBudgetManager_IsOverBudget_WhenExceeded()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 50000);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000);
            Assert.IsFalse(manager.IsOverBudget);

            manager.Register(LODLevel.LOD0_High, 30000);
            Assert.IsTrue(manager.IsOverBudget,
                $"60K triangles should exceed 50K budget. Total: {manager.TotalTriangles}");
        }

        [Test]
        public void LODBudgetManager_IsLOD0Full_WhenLimitReached()
        {
            var budget = new LODBudgetConfig(maxActiveLOD0Characters: 2);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 1000, isCharacter: true);
            Assert.IsFalse(manager.IsLOD0Full);

            manager.Register(LODLevel.LOD0_High, 1000, isCharacter: true);
            Assert.IsTrue(manager.IsLOD0Full,
                "2 LOD0 characters should fill the 2-character limit.");
        }

        [Test]
        public void LODBudgetManager_BudgetUtilization_CalculatesCorrectly()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 100000);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 50000);
            Assert.AreEqual(50f, manager.BudgetUtilizationPercent, 0.1f,
                "50K / 100K = 50% utilization.");
        }

        [Test]
        public void LODBudgetManager_RemainingTriangles_CalculatesCorrectly()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 100000);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000);
            Assert.AreEqual(70000, manager.RemainingTriangles);
        }

        [Test]
        public void LODBudgetManager_RemainingLOD0Slots_CalculatesCorrectly()
        {
            var budget = new LODBudgetConfig(maxActiveLOD0Characters: 6);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 1000, isCharacter: true);
            manager.Register(LODLevel.LOD0_High, 1000, isCharacter: true);
            Assert.AreEqual(4, manager.RemainingLOD0Slots);
        }

        [Test]
        public void LODBudgetManager_Reset_ClearsAllCounts()
        {
            var budget = new LODBudgetConfig();
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000, isCharacter: true);
            manager.Register(LODLevel.LOD1_Medium, 5000);
            manager.Reset();

            Assert.AreEqual(0, manager.TotalTriangles);
            Assert.AreEqual(0, manager.LOD0CharacterCount);
            Assert.AreEqual(0, manager.GetCountAtLevel(LODLevel.LOD0_High));
            Assert.AreEqual(0, manager.GetCountAtLevel(LODLevel.LOD1_Medium));
        }

        [Test]
        public void LODBudgetManager_GetStatistics_ReturnsSnapshot()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 200000);
            var manager = new LODBudgetManager(budget);

            manager.Register(LODLevel.LOD0_High, 30000, isCharacter: true);
            manager.Register(LODLevel.LOD1_Medium, 5000);

            var stats = manager.GetStatistics(currentBias: -0.05f, currentFrameTime: 0.018f);
            Assert.AreEqual(1, stats.ObjectCountPerLevel[0], "1 object at LOD0");
            Assert.AreEqual(1, stats.ObjectCountPerLevel[1], "1 object at LOD1");
            Assert.AreEqual(35000, stats.TotalTriangles);
            Assert.AreEqual(1, stats.ActiveLOD0Characters);
            Assert.AreEqual(-0.05f, stats.CurrentBias, 0.001f);
            Assert.AreEqual(0.018f, stats.CurrentFrameTime, 0.001f);
            Assert.AreEqual(200000, stats.MaxTriangleBudget);
        }

        // ================================================================
        // LODDistanceCalculator screen height computation
        // ================================================================

        [Test]
        public void LODDistanceCalculator_CloseDistance_ReturnsHighScreenHeight()
        {
            float screenHeight = LODDistanceCalculator.ComputeScreenHeight(1.8f, 2f, 60f);
            Assert.Greater(screenHeight, 0.3f,
                $"A 1.8m object at 2m distance should fill >30% of screen. Got {screenHeight}");
        }

        [Test]
        public void LODDistanceCalculator_FarDistance_ReturnsLowScreenHeight()
        {
            float screenHeight = LODDistanceCalculator.ComputeScreenHeight(1.8f, 100f, 60f);
            Assert.Less(screenHeight, 0.02f,
                $"A 1.8m object at 100m should fill <2% of screen. Got {screenHeight}");
        }

        [Test]
        public void LODDistanceCalculator_ZeroDistance_ReturnsOne()
        {
            float screenHeight = LODDistanceCalculator.ComputeScreenHeight(1.8f, 0f, 60f);
            Assert.AreEqual(1f, screenHeight, 0.001f,
                "At zero distance, screen height should be 1 (full screen).");
        }

        [Test]
        public void LODDistanceCalculator_ZeroObjectHeight_ReturnsZero()
        {
            float screenHeight = LODDistanceCalculator.ComputeScreenHeight(0f, 10f, 60f);
            Assert.AreEqual(0f, screenHeight, 0.001f,
                "Zero-height object should have zero screen height.");
        }

        [Test]
        public void LODDistanceCalculator_InvalidFOV_ReturnsZero()
        {
            float sh1 = LODDistanceCalculator.ComputeScreenHeight(1.8f, 10f, 0f);
            float sh2 = LODDistanceCalculator.ComputeScreenHeight(1.8f, 10f, 180f);
            Assert.AreEqual(0f, sh1, 0.001f);
            Assert.AreEqual(0f, sh2, 0.001f);
        }

        [Test]
        public void LODDistanceCalculator_ComputeDistanceForScreenHeight_RoundTrips()
        {
            float objectHeight = 1.8f;
            float fov = 60f;
            float targetScreenHeight = 0.15f;

            float distance = LODDistanceCalculator.ComputeDistanceForScreenHeight(
                objectHeight, targetScreenHeight, fov);

            float actualScreenHeight = LODDistanceCalculator.ComputeScreenHeight(
                objectHeight, distance, fov);

            Assert.AreEqual(targetScreenHeight, actualScreenHeight, 0.001f,
                $"Round-trip distance={distance}, expected sh={targetScreenHeight}, got {actualScreenHeight}");
        }

        [Test]
        public void LODDistanceCalculator_ScreenHeightDecreasesWithDistance()
        {
            float sh5 = LODDistanceCalculator.ComputeScreenHeight(1.8f, 5f, 60f);
            float sh10 = LODDistanceCalculator.ComputeScreenHeight(1.8f, 10f, 60f);
            float sh50 = LODDistanceCalculator.ComputeScreenHeight(1.8f, 50f, 60f);

            Assert.Greater(sh5, sh10, "Closer objects should have larger screen height.");
            Assert.Greater(sh10, sh50, "Closer objects should have larger screen height.");
        }

        // ================================================================
        // LODSelector level picking
        // ================================================================

        [Test]
        public void LODSelector_SelectLevel_HighScreenHeight_ReturnsLOD0()
        {
            var budget = new LODBudgetConfig();
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);
            var profile = new CharacterLODProfile();

            var level = selector.SelectLevel(profile, 0.50f);
            Assert.AreEqual(LODLevel.LOD0_High, level);
        }

        [Test]
        public void LODSelector_SelectLevel_NullProfile_ReturnsCulled()
        {
            var budget = new LODBudgetConfig();
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);

            var level = selector.SelectLevel(null, 0.50f);
            Assert.AreEqual(LODLevel.Culled, level,
                "Null profile should be treated as culled.");
        }

        [Test]
        public void LODSelector_SelectLevel_WithSpeed_ReducesQuality()
        {
            var budget = new LODBudgetConfig();
            var transition = new LODTransitionConfig(speedQualityReduction: 0.5f, speedThreshold: 3f);
            var selector = new LODSelector(budget, transition);
            var profile = new CharacterLODProfile();

            // 0.42 screen height would normally be LOD0
            // With speed > threshold and 0.5 reduction: effective = 0.42 * 0.5 = 0.21 => LOD1
            var level = selector.SelectLevel(profile, 0.42f, objectSpeed: 10f);
            Assert.AreEqual(LODLevel.LOD1_Medium, level,
                "Fast-moving objects should be rendered at lower LOD.");
        }

        [Test]
        public void LODSelector_SelectLevelWithBudget_LOD0Full_ForcesLOD1()
        {
            var budget = new LODBudgetConfig(maxActiveLOD0Characters: 2);
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);
            var profile = new CharacterLODProfile();

            // LOD0 limit is 2, current count is already 2
            var level = selector.SelectLevelWithBudget(
                profile, screenHeight: 0.50f,
                currentLOD0Count: 2,
                currentTotalTriangles: 0,
                objectTrianglesAtLOD0: 30000);

            Assert.AreEqual(LODLevel.LOD1_Medium, level,
                "When LOD0 slots are full, should force LOD1.");
        }

        [Test]
        public void LODSelector_SelectLevelWithBudget_TriangleBudgetExceeded_ForcesLOD1()
        {
            var budget = new LODBudgetConfig(maxTotalTriangles: 50000);
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);
            var profile = new CharacterLODProfile();

            var level = selector.SelectLevelWithBudget(
                profile, screenHeight: 0.50f,
                currentLOD0Count: 0,
                currentTotalTriangles: 45000,
                objectTrianglesAtLOD0: 30000);

            Assert.AreEqual(LODLevel.LOD1_Medium, level,
                "When adding LOD0 would exceed triangle budget, should force LOD1.");
        }

        [Test]
        public void LODSelector_ComputeCrossFadeAmount_InTransitionZone()
        {
            var budget = new LODBudgetConfig();
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);

            var profile = new CharacterLODProfile(crossFadeWidth: 0.05f);

            // LOD0 threshold = 0.40, crossFadeWidth = 0.05
            // Transition zone: 0.40 to 0.45
            // At 0.425 (midpoint): should be ~0.5
            float fade = selector.ComputeCrossFadeAmount(profile, 0.425f, LODLevel.LOD0_High);
            Assert.That(fade, Is.InRange(0.4f, 0.6f),
                $"Cross-fade at midpoint of transition zone should be ~0.5. Got {fade}");
        }

        [Test]
        public void LODSelector_ComputeCrossFadeAmount_OutsideZone_ReturnsZero()
        {
            var budget = new LODBudgetConfig();
            var transition = new LODTransitionConfig();
            var selector = new LODSelector(budget, transition);

            var profile = new CharacterLODProfile(crossFadeWidth: 0.05f);

            // Well above the transition zone
            float fade = selector.ComputeCrossFadeAmount(profile, 0.60f, LODLevel.LOD0_High);
            Assert.AreEqual(0f, fade, 0.001f,
                "Outside transition zone should have zero cross-fade.");
        }

        // ================================================================
        // DynamicLODAdjuster bias changes
        // ================================================================

        [Test]
        public void DynamicLODAdjuster_InitialBias_IsZero()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget);
            Assert.AreEqual(0f, adjuster.CurrentBias, 0.001f);
            Assert.IsFalse(adjuster.IsReducingQuality);
        }

        [Test]
        public void DynamicLODAdjuster_SlowFrame_ReducesBias()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.01f);

            // Frame time of 0.02s > 0.01667s (60fps target)
            adjuster.Update(0.02f);
            Assert.Less(adjuster.CurrentBias, 0f,
                "Slow frame should reduce bias (lower quality).");
            Assert.IsTrue(adjuster.IsReducingQuality);
        }

        [Test]
        public void DynamicLODAdjuster_FastFrame_RestoresBias()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.01f);

            // First, reduce quality
            adjuster.Update(0.02f);
            float reducedBias = adjuster.CurrentBias;

            // Then, fast frame should restore
            adjuster.Update(0.008f);
            Assert.Greater(adjuster.CurrentBias, reducedBias,
                "Fast frame should increase bias (restore quality).");
        }

        [Test]
        public void DynamicLODAdjuster_BiasClampedToMin()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.1f, minBias: -0.15f);

            // Many slow frames
            for (int i = 0; i < 100; i++)
                adjuster.Update(0.03f);

            Assert.AreEqual(-0.15f, adjuster.CurrentBias, 0.001f,
                "Bias should not go below minBias.");
        }

        [Test]
        public void DynamicLODAdjuster_BiasClampedToMax()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.1f, maxBias: 0.10f);

            // Many fast frames
            for (int i = 0; i < 100; i++)
                adjuster.Update(0.005f);

            Assert.AreEqual(0.10f, adjuster.CurrentBias, 0.001f,
                "Bias should not go above maxBias.");
        }

        [Test]
        public void DynamicLODAdjuster_ApplyBias_SetsProfileBias()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.01f);

            adjuster.Update(0.02f); // Reduce quality
            var profile = new CharacterLODProfile();
            adjuster.ApplyBias(profile);

            Assert.AreEqual(adjuster.CurrentBias, profile.Bias, 0.001f,
                "Profile bias should match adjuster's current bias.");
        }

        [Test]
        public void DynamicLODAdjuster_ResetBias_SetsToZero()
        {
            var budget = new LODBudgetConfig(targetFPS: 60f);
            var adjuster = new DynamicLODAdjuster(budget, biasStep: 0.01f);

            adjuster.Update(0.02f);
            adjuster.ResetBias();
            Assert.AreEqual(0f, adjuster.CurrentBias, 0.001f);
        }

        // ================================================================
        // LODStatistics calculations
        // ================================================================

        [Test]
        public void LODStatistics_BudgetUtilization_CalculatesCorrectly()
        {
            var stats = LODStatistics.Create(200000);
            stats.TotalTriangles = 100000;
            Assert.AreEqual(50f, stats.BudgetUtilizationPercent, 0.1f,
                "100K / 200K = 50% utilization.");
        }

        [Test]
        public void LODStatistics_BudgetUtilization_ZeroBudget_ReturnsZero()
        {
            var stats = new LODStatistics
            {
                ObjectCountPerLevel = new int[5],
                TotalTriangles = 1000,
                MaxTriangleBudget = 0
            };
            Assert.AreEqual(0f, stats.BudgetUtilizationPercent, 0.001f);
        }

        [Test]
        public void LODStatistics_AverageLODLevel_AllAtLOD0_IsZero()
        {
            var stats = LODStatistics.Create(500000);
            stats.ObjectCountPerLevel[0] = 10; // All at LOD0
            Assert.AreEqual(0f, stats.AverageLODLevel, 0.001f,
                "All objects at LOD0 should give average level of 0.");
        }

        [Test]
        public void LODStatistics_AverageLODLevel_MixedDistribution()
        {
            var stats = LODStatistics.Create(500000);
            stats.ObjectCountPerLevel[0] = 2; // 2 at LOD0 (weight 0)
            stats.ObjectCountPerLevel[1] = 2; // 2 at LOD1 (weight 2)
            stats.ObjectCountPerLevel[2] = 2; // 2 at LOD2 (weight 4)
            // Average = (0 + 2 + 4) / 6 = 1.0
            Assert.AreEqual(1f, stats.AverageLODLevel, 0.001f,
                "Average LOD should be 1.0 for equal distribution at LOD0/1/2.");
        }

        [Test]
        public void LODStatistics_AverageLODLevel_NoObjects_ReturnsZero()
        {
            var stats = LODStatistics.Create(500000);
            Assert.AreEqual(0f, stats.AverageLODLevel, 0.001f);
        }

        [Test]
        public void LODStatistics_TotalVisibleObjects_ExcludesCulled()
        {
            var stats = LODStatistics.Create(500000);
            stats.ObjectCountPerLevel[0] = 3; // LOD0
            stats.ObjectCountPerLevel[1] = 2; // LOD1
            stats.ObjectCountPerLevel[4] = 5; // Culled

            Assert.AreEqual(5, stats.TotalVisibleObjects,
                "Visible objects should exclude culled (3 + 2 = 5).");
        }

        // ================================================================
        // LODGroupConfig
        // ================================================================

        [Test]
        public void LODGroupConfig_ValidConstruction_StoresValues()
        {
            var profile = new StadiumLODProfile();
            var group = new LODGroupConfig("NorthStand", profile, objectCount: 5);

            Assert.AreEqual("NorthStand", group.GroupId);
            Assert.AreSame(profile, group.Profile);
            Assert.AreEqual(5, group.ObjectCount);
            Assert.AreEqual(0, group.CurrentTriangles);
        }

        [Test]
        public void LODGroupConfig_NullGroupId_Throws()
        {
            var profile = new StadiumLODProfile();
            Assert.Throws<ArgumentException>(() => new LODGroupConfig(null, profile));
        }

        [Test]
        public void LODGroupConfig_NullProfile_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LODGroupConfig("Test", null));
        }

        [Test]
        public void LODGroupConfig_NegativeObjectCount_Throws()
        {
            var profile = new StadiumLODProfile();
            Assert.Throws<ArgumentException>(() => new LODGroupConfig("Test", profile, objectCount: -1));
        }

        // ================================================================
        // LODSystemConfig factory methods
        // ================================================================

        [Test]
        public void LODSystemConfig_CreateDefault_HasAllProfiles()
        {
            var config = LODSystemConfig.CreateDefault();
            Assert.IsNotNull(config.CharacterProfile);
            Assert.IsNotNull(config.StadiumProfile);
            Assert.IsNotNull(config.CrowdProfile);
            Assert.IsNotNull(config.Budget);
            Assert.IsNotNull(config.Transition);
        }

        [Test]
        public void LODSystemConfig_CreatePerformance_HasLowerBudget()
        {
            var perf = LODSystemConfig.CreatePerformance();
            var def = LODSystemConfig.CreateDefault();

            Assert.Less(perf.Budget.MaxActiveLOD0Characters, def.Budget.MaxActiveLOD0Characters,
                "Performance preset should allow fewer LOD0 characters.");
            Assert.Less(perf.Budget.MaxTotalTriangles, def.Budget.MaxTotalTriangles,
                "Performance preset should have lower triangle budget.");
        }

        // ================================================================
        // Cross-fade config validation
        // ================================================================

        [Test]
        public void LODProfile_CrossFadeWidth_DefaultIsPositive()
        {
            var profile = new CharacterLODProfile();
            Assert.Greater(profile.CrossFadeWidth, 0f,
                "Default cross-fade width should be positive for smooth transitions.");
        }

        [Test]
        public void LODProfile_InvalidCrossFadeWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CharacterLODProfile(crossFadeWidth: -0.1f));
            Assert.Throws<ArgumentException>(() => new CharacterLODProfile(crossFadeWidth: 1.5f));
        }

        [Test]
        public void LODTransitionConfig_SpeedQualityReduction_InValidRange()
        {
            Assert.Throws<ArgumentException>(() => new LODTransitionConfig(speedQualityReduction: -0.1f));
            Assert.Throws<ArgumentException>(() => new LODTransitionConfig(speedQualityReduction: 1.1f));
        }
    }
}
