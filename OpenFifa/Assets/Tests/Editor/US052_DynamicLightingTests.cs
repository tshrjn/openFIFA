using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US052")]
    [Category("Lighting")]
    public class US052_DynamicLightingTests
    {
        // ================================================================
        // TimeOfDay tests
        // ================================================================

        [Test]
        public void TimeOfDay_DefaultNoon_IsDaytime()
        {
            var tod = new TimeOfDay(12f);
            Assert.IsTrue(tod.IsDaytime,
                $"Hour {tod.Hour} should be daytime");
            Assert.IsFalse(tod.IsNightTime);
            Assert.IsFalse(tod.IsDawn);
            Assert.IsFalse(tod.IsDusk);
        }

        [Test]
        public void TimeOfDay_Midnight_IsNightTime()
        {
            var tod = new TimeOfDay(2f);
            Assert.IsTrue(tod.IsNightTime,
                $"Hour {tod.Hour} should be night time (before dawn at sunrise {tod.SunriseHour} - {tod.DawnDuration})");
            Assert.IsFalse(tod.IsDaytime);
        }

        [Test]
        public void TimeOfDay_LateNight_IsNightTime()
        {
            var tod = new TimeOfDay(22f);
            Assert.IsTrue(tod.IsNightTime,
                $"Hour {tod.Hour} should be night (after dusk ends at {tod.SunsetHour + tod.DuskDuration})");
        }

        [Test]
        public void TimeOfDay_DawnPeriod_IsDawn()
        {
            // Default sunrise at 6.0, dawn duration 1.0, so dawn = 5.0-6.0
            var tod = new TimeOfDay(5.5f);
            Assert.IsTrue(tod.IsDawn,
                $"Hour {tod.Hour} should be dawn (sunrise={tod.SunriseHour}, dawnDuration={tod.DawnDuration})");
            Assert.IsFalse(tod.IsNightTime);
            Assert.IsFalse(tod.IsDaytime);
            Assert.IsFalse(tod.IsDusk);
        }

        [Test]
        public void TimeOfDay_DuskPeriod_IsDusk()
        {
            // Default sunset at 20.0, dusk duration 1.0, so dusk = 20.0-21.0
            var tod = new TimeOfDay(20.5f);
            Assert.IsTrue(tod.IsDusk,
                $"Hour {tod.Hour} should be dusk (sunset={tod.SunsetHour}, duskDuration={tod.DuskDuration})");
            Assert.IsFalse(tod.IsDaytime);
            Assert.IsFalse(tod.IsNightTime);
        }

        [Test]
        public void TimeOfDay_SunElevationFactor_ZeroAtNight()
        {
            var tod = new TimeOfDay(2f);
            Assert.AreEqual(0f, tod.SunElevationFactor, 0.001f,
                "Sun elevation factor should be 0 at night");
        }

        [Test]
        public void TimeOfDay_SunElevationFactor_PeakAtNoon()
        {
            // Solar noon = (6+20)/2 = 13.0
            var tod = new TimeOfDay(13f);
            Assert.Greater(tod.SunElevationFactor, 0.9f,
                $"Sun elevation factor at solar noon (hour {tod.Hour}) should be near peak, got {tod.SunElevationFactor}");
        }

        [Test]
        public void TimeOfDay_NormalizedTime_CorrectFraction()
        {
            var tod = new TimeOfDay(6f);
            Assert.AreEqual(0.25f, tod.NormalizedTime, 0.001f,
                "6:00 = 6/24 = 0.25 normalized");

            var noon = new TimeOfDay(12f);
            Assert.AreEqual(0.5f, noon.NormalizedTime, 0.001f,
                "12:00 = 0.5 normalized");
        }

        [Test]
        public void TimeOfDay_Advance_WrapsAt24()
        {
            var tod = new TimeOfDay(23f);
            var advanced = tod.Advance(2f);
            Assert.AreEqual(1f, advanced.Hour, 0.001f,
                $"23 + 2 = 25 -> should wrap to 1, got {advanced.Hour}");
        }

        [Test]
        public void TimeOfDay_InvalidHour_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TimeOfDay(-1f));
            Assert.Throws<ArgumentException>(() => new TimeOfDay(24f));
        }

        [Test]
        public void TimeOfDay_InvalidSunriseSunset_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TimeOfDay(12f, sunriseHour: 20f, sunsetHour: 6f),
                "Sunset must be after sunrise");
        }

        // ================================================================
        // FloodlightBehavior tests
        // ================================================================

        [Test]
        public void FloodlightBehavior_DefaultValues_AreReasonable()
        {
            var behavior = new FloodlightBehavior();
            Assert.Greater(behavior.WarmUpDuration, 0f, "Warm-up should take time");
            Assert.Greater(behavior.ColorTemperatureFull, 0f);
            Assert.Greater(behavior.ColorTemperatureWarmUp, 0f);
            Assert.That(behavior.DimmedIntensityFactor, Is.InRange(0f, 1f));
            Assert.That(behavior.EmergencyIntensityFactor, Is.InRange(0f, 1f));
        }

        [Test]
        public void FloodlightBehavior_WarmUpIntensity_ZeroAtStart()
        {
            var behavior = new FloodlightBehavior();
            float intensity = behavior.CalculateWarmUpIntensity(0f);
            Assert.AreEqual(0f, intensity, 0.01f,
                $"Intensity at time 0 should be ~0, got {intensity}");
        }

        [Test]
        public void FloodlightBehavior_WarmUpIntensity_OneAtCompletion()
        {
            var behavior = new FloodlightBehavior(warmUpFlickerAmplitude: 0f); // No flicker for predictable test
            float intensity = behavior.CalculateWarmUpIntensity(behavior.WarmUpDuration);
            Assert.AreEqual(1f, intensity, 0.01f,
                $"Intensity at warm-up completion should be 1, got {intensity}");
        }

        [Test]
        public void FloodlightBehavior_WarmUpIntensity_IncreasesOverTime()
        {
            var behavior = new FloodlightBehavior(warmUpFlickerAmplitude: 0f);
            float early = behavior.CalculateWarmUpIntensity(0.5f);
            float late = behavior.CalculateWarmUpIntensity(behavior.WarmUpDuration * 0.9f);
            Assert.Greater(late, early,
                $"Intensity should increase: early={early}, late={late}");
        }

        [Test]
        public void FloodlightBehavior_ColorTemperature_InterpolatesCorrectly()
        {
            var behavior = new FloodlightBehavior(
                colorTemperatureWarmUp: 3000f,
                colorTemperatureFull: 6000f);

            float atStart = behavior.InterpolateColorTemperature(0f);
            Assert.AreEqual(3000f, atStart, 1f, "At progress 0 should be warm-up temp");

            float atEnd = behavior.InterpolateColorTemperature(1f);
            Assert.AreEqual(6000f, atEnd, 1f, "At progress 1 should be full temp");

            float atMiddle = behavior.InterpolateColorTemperature(0.5f);
            Assert.AreEqual(4500f, atMiddle, 1f, "At progress 0.5 should be midpoint");
        }

        [Test]
        public void FloodlightBehavior_InvalidWarmUpDuration_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FloodlightBehavior(warmUpDuration: -1f));
        }

        [Test]
        public void FloodlightBehavior_InvalidAmplitude_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FloodlightBehavior(warmUpFlickerAmplitude: 1.5f));
        }

        // ================================================================
        // LightingDirector state machine tests
        // ================================================================

        [Test]
        public void LightingDirector_InitialPhase_IsPregame()
        {
            var director = new LightingDirector();
            Assert.AreEqual(LightingPhase.Pregame, director.CurrentPhase);
        }

        [Test]
        public void LightingDirector_AdvancePhase_FollowsCorrectSequence()
        {
            var director = new LightingDirector();

            Assert.IsTrue(director.AdvancePhase());
            Assert.AreEqual(LightingPhase.MatchStart, director.CurrentPhase);

            Assert.IsTrue(director.AdvancePhase());
            Assert.AreEqual(LightingPhase.HalfTime, director.CurrentPhase);

            Assert.IsTrue(director.AdvancePhase());
            Assert.AreEqual(LightingPhase.SecondHalf, director.CurrentPhase);

            Assert.IsTrue(director.AdvancePhase());
            Assert.AreEqual(LightingPhase.PostMatch, director.CurrentPhase);

            // Cannot advance past PostMatch
            Assert.IsFalse(director.AdvancePhase());
            Assert.AreEqual(LightingPhase.PostMatch, director.CurrentPhase);
        }

        [Test]
        public void LightingDirector_SetPhase_FiresEvent()
        {
            var director = new LightingDirector();
            LightingPhase? receivedPrevious = null;
            LightingPhase? receivedNew = null;

            director.OnPhaseChanged += (prev, next) =>
            {
                receivedPrevious = prev;
                receivedNew = next;
            };

            director.SetPhase(LightingPhase.HalfTime);

            Assert.AreEqual(LightingPhase.Pregame, receivedPrevious);
            Assert.AreEqual(LightingPhase.HalfTime, receivedNew);
        }

        [Test]
        public void LightingDirector_GetRecommendedFloodlightState_NightMatchStart_IsFullPower()
        {
            var director = new LightingDirector();
            director.SetPhase(LightingPhase.MatchStart);

            var nightTime = new TimeOfDay(22f);
            var state = director.GetRecommendedFloodlightState(nightTime);
            Assert.AreEqual(FloodlightState.FullPower, state,
                "Night + MatchStart should give full power floodlights");
        }

        [Test]
        public void LightingDirector_GetRecommendedFloodlightState_DayMatchStart_IsOff()
        {
            var director = new LightingDirector();
            director.SetPhase(LightingPhase.MatchStart);

            var dayTime = new TimeOfDay(12f);
            var state = director.GetRecommendedFloodlightState(dayTime);
            Assert.AreEqual(FloodlightState.Off, state,
                "Daytime + MatchStart should not need floodlights");
        }

        [Test]
        public void LightingDirector_GetRecommendedFloodlightState_NightPregame_IsWarmingUp()
        {
            var director = new LightingDirector();
            // Default phase is Pregame

            var nightTime = new TimeOfDay(22f);
            var state = director.GetRecommendedFloodlightState(nightTime);
            Assert.AreEqual(FloodlightState.WarmingUp, state,
                "Night + Pregame should warm up floodlights");
        }

        // ================================================================
        // TimeOfDaySimulator tests
        // ================================================================

        [Test]
        public void TimeOfDaySimulator_InitialTime_IsConfigured()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(15f));
            Assert.AreEqual(15f, sim.CurrentTime.Hour, 0.001f);
        }

        [Test]
        public void TimeOfDaySimulator_Update_AdvancesTime()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(12f), timeScale: 3600f); // 1hr per second
            sim.Update(1f); // 1 second = 1 hour at this scale

            Assert.AreEqual(13f, sim.CurrentTime.Hour, 0.01f,
                $"Expected hour to advance to 13, got {sim.CurrentTime.Hour}");
        }

        [Test]
        public void TimeOfDaySimulator_Paused_DoesNotAdvance()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(12f), timeScale: 3600f);
            sim.IsPaused = true;
            sim.Update(10f);

            Assert.AreEqual(12f, sim.CurrentTime.Hour, 0.001f,
                "Paused simulator should not advance time");
        }

        [Test]
        public void TimeOfDaySimulator_SunElevation_PositiveDuringDay()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(12f));
            float elevation = sim.GetSunElevation();
            Assert.Greater(elevation, 0f,
                $"Sun elevation at noon should be positive, got {elevation}");
        }

        [Test]
        public void TimeOfDaySimulator_SunElevation_ZeroAtNight()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(2f));
            float elevation = sim.GetSunElevation();
            Assert.AreEqual(0f, elevation, 0.001f,
                "Sun elevation should be 0 at night");
        }

        [Test]
        public void TimeOfDaySimulator_SunIntensity_PositiveDuringDay()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(12f));
            float intensity = sim.GetSunIntensity();
            Assert.Greater(intensity, 0f,
                $"Sun intensity at noon should be positive, got {intensity}");
        }

        [Test]
        public void TimeOfDaySimulator_AmbientColor_ValidRange()
        {
            var sim = new TimeOfDaySimulator(new TimeOfDay(12f));
            sim.GetAmbientColor(out float r, out float g, out float b);

            Assert.That(r, Is.InRange(0f, 1f), $"Ambient R={r} out of range");
            Assert.That(g, Is.InRange(0f, 1f), $"Ambient G={g} out of range");
            Assert.That(b, Is.InRange(0f, 1f), $"Ambient B={b} out of range");
        }

        // ================================================================
        // FloodlightController tests
        // ================================================================

        [Test]
        public void FloodlightController_InitialState_AllOff()
        {
            var controller = new FloodlightController(4);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(FloodlightState.Off, controller.GetState(i),
                    $"Floodlight {i} should start Off");
            }
        }

        [Test]
        public void FloodlightController_SetAllState_SetsAll()
        {
            var controller = new FloodlightController(4);
            controller.SetAllState(FloodlightState.FullPower);

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(FloodlightState.FullPower, controller.GetState(i),
                    $"Floodlight {i} should be FullPower");
            }
        }

        [Test]
        public void FloodlightController_WarmUp_TransitionsToFullPower()
        {
            var behavior = new FloodlightBehavior(warmUpDuration: 1f);
            var controller = new FloodlightController(1, behavior);

            controller.SetAllState(FloodlightState.WarmingUp);
            Assert.AreEqual(FloodlightState.WarmingUp, controller.GetState(0));

            // Simulate time passing beyond warm-up duration
            controller.Update(1.5f);

            Assert.AreEqual(FloodlightState.FullPower, controller.GetState(0),
                "Floodlight should transition to FullPower after warm-up completes");
        }

        [Test]
        public void FloodlightController_IntensityFactor_ZeroWhenOff()
        {
            var controller = new FloodlightController(1);
            Assert.AreEqual(0f, controller.GetIntensityFactor(0), 0.001f,
                "Off floodlight should have 0 intensity");
        }

        [Test]
        public void FloodlightController_IntensityFactor_OneWhenFullPower()
        {
            var controller = new FloodlightController(1);
            controller.SetState(0, FloodlightState.FullPower);
            Assert.AreEqual(1f, controller.GetIntensityFactor(0), 0.001f,
                "FullPower floodlight should have intensity 1.0");
        }

        [Test]
        public void FloodlightController_SimulateFailure_SetsEmergency()
        {
            var controller = new FloodlightController(4);
            controller.SetAllState(FloodlightState.FullPower);
            controller.SimulateFailure(2);

            Assert.AreEqual(FloodlightState.Emergency, controller.GetState(2),
                "Failed floodlight should be in Emergency state");
            Assert.AreEqual(FloodlightState.FullPower, controller.GetState(0),
                "Unaffected floodlight should remain FullPower");
        }

        [Test]
        public void FloodlightController_InvalidIndex_Throws()
        {
            var controller = new FloodlightController(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.GetState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.GetState(4));
        }

        [Test]
        public void FloodlightController_StateChangedEvent_Fires()
        {
            var controller = new FloodlightController(1);
            int? receivedIndex = null;
            FloodlightState? receivedState = null;

            controller.OnFloodlightStateChanged += (idx, state) =>
            {
                receivedIndex = idx;
                receivedState = state;
            };

            controller.SetState(0, FloodlightState.FullPower);

            Assert.AreEqual(0, receivedIndex);
            Assert.AreEqual(FloodlightState.FullPower, receivedState);
        }

        // ================================================================
        // LightingBlender tests
        // ================================================================

        [Test]
        public void LightingBlender_NotBlending_Initially()
        {
            var blender = new LightingBlender();
            Assert.IsFalse(blender.IsBlending);
        }

        [Test]
        public void LightingBlender_StartBlend_IsBlending()
        {
            var blender = new LightingBlender();
            var from = new LightingSnapshot { AmbientR = 0f };
            var to = new LightingSnapshot { AmbientR = 1f };

            blender.StartBlend(from, to, 1f);
            Assert.IsTrue(blender.IsBlending);
        }

        [Test]
        public void LightingBlender_CompletedBlend_IsNotBlending()
        {
            var blender = new LightingBlender();
            var from = new LightingSnapshot { AmbientR = 0f };
            var to = new LightingSnapshot { AmbientR = 1f };

            blender.StartBlend(from, to, 1f);
            blender.Update(2f); // Exceed duration

            Assert.IsFalse(blender.IsBlending);
        }

        [Test]
        public void LightingBlender_Interpolation_MidpointIsMiddle()
        {
            var blender = new LightingBlender();
            var from = new LightingSnapshot
            {
                AmbientR = 0f, AmbientG = 0f, AmbientB = 0f,
                SunElevation = 0f, SunAzimuth = 0f, SunIntensity = 0f,
                FloodlightIntensityFactor = 0f, FloodlightColorTemperature = 3000f,
                ShadowMaxDistance = 40f, ShadowCascadeCount = 2,
            };
            var to = new LightingSnapshot
            {
                AmbientR = 1f, AmbientG = 1f, AmbientB = 1f,
                SunElevation = 60f, SunAzimuth = 180f, SunIntensity = 2f,
                FloodlightIntensityFactor = 1f, FloodlightColorTemperature = 6000f,
                ShadowMaxDistance = 80f, ShadowCascadeCount = 4,
            };

            blender.StartBlend(from, to, 2f);
            blender.Update(1f); // Halfway

            var snapshot = blender.GetCurrentSnapshot();
            Assert.IsNotNull(snapshot);

            // SmoothStep at t=0.5 gives exactly 0.5
            Assert.That(snapshot.AmbientR, Is.InRange(0.4f, 0.6f),
                $"Ambient R at midpoint should be ~0.5, got {snapshot.AmbientR}");
            Assert.That(snapshot.FloodlightColorTemperature, Is.InRange(4000f, 5000f),
                $"Color temp at midpoint should be ~4500, got {snapshot.FloodlightColorTemperature}");
        }

        [Test]
        public void LightingBlender_NullSnapshots_Throws()
        {
            var blender = new LightingBlender();
            Assert.Throws<ArgumentNullException>(() => blender.StartBlend(null, new LightingSnapshot(), 1f));
            Assert.Throws<ArgumentNullException>(() => blender.StartBlend(new LightingSnapshot(), null, 1f));
        }

        // ================================================================
        // ShadowCascadeConfig tests
        // ================================================================

        [Test]
        public void ShadowCascadeConfig_DefaultValues_AreValid()
        {
            var config = new ShadowCascadeConfig();
            Assert.AreEqual(4, config.CascadeCount);
            Assert.AreEqual(3, config.SplitRatios.Count, "4 cascades needs 3 split ratios");
            Assert.AreEqual(80f, config.MaxDistance, 0.001f);
            Assert.IsTrue(config.IsValid);
        }

        [Test]
        public void ShadowCascadeConfig_TwoCascades_HasOneSplitRatio()
        {
            var config = new ShadowCascadeConfig(cascadeCount: 2);
            Assert.AreEqual(2, config.CascadeCount);
            Assert.AreEqual(1, config.SplitRatios.Count);
            Assert.IsTrue(config.IsValid);
        }

        [Test]
        public void ShadowCascadeConfig_SingleCascade_NoSplitRatios()
        {
            var config = new ShadowCascadeConfig(cascadeCount: 1);
            Assert.AreEqual(1, config.CascadeCount);
            Assert.AreEqual(0, config.SplitRatios.Count);
            Assert.IsTrue(config.IsValid);
        }

        [Test]
        public void ShadowCascadeConfig_InvalidCascadeCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ShadowCascadeConfig(cascadeCount: 3));
            Assert.Throws<ArgumentException>(() => new ShadowCascadeConfig(cascadeCount: 0));
        }

        [Test]
        public void ShadowCascadeConfig_MismatchedSplitRatios_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ShadowCascadeConfig(cascadeCount: 4, splitRatios: new List<float> { 0.1f }));
        }

        [Test]
        public void ShadowCascadeConfig_InvalidMaxDistance_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ShadowCascadeConfig(maxDistance: 0f));
        }

        // ================================================================
        // LightingTransitionConfig tests
        // ================================================================

        [Test]
        public void LightingTransitionConfig_DefaultAmbientKeyframes_HasEntries()
        {
            var config = new LightingTransitionConfig();
            Assert.Greater(config.AmbientKeyframes.Count, 0,
                "Default config should have ambient keyframes");
            Assert.AreEqual(0f, config.AmbientKeyframes[0].Time, 0.001f,
                "First keyframe should be at time 0");
            Assert.AreEqual(1f, config.AmbientKeyframes[config.AmbientKeyframes.Count - 1].Time, 0.001f,
                "Last keyframe should be at time 1");
        }

        [Test]
        public void LightingTransitionConfig_EvaluateAmbientColor_MiddayIsBright()
        {
            var config = new LightingTransitionConfig();
            config.EvaluateAmbientColor(0.5f, out float r, out float g, out float b);

            Assert.Greater(r, 0.5f, $"Midday ambient R should be bright, got {r}");
            Assert.Greater(g, 0.5f, $"Midday ambient G should be bright, got {g}");
            Assert.Greater(b, 0.5f, $"Midday ambient B should be bright, got {b}");
        }

        [Test]
        public void LightingTransitionConfig_EvaluateAmbientColor_MidnightIsDark()
        {
            var config = new LightingTransitionConfig();
            config.EvaluateAmbientColor(0f, out float r, out float g, out float b);

            Assert.Less(r, 0.2f, $"Midnight ambient R should be dark, got {r}");
            Assert.Less(g, 0.2f, $"Midnight ambient G should be dark, got {g}");
        }

        [Test]
        public void LightingTransitionConfig_SunElevation_ZeroAtNight()
        {
            var config = new LightingTransitionConfig();
            float elevation = config.EvaluateSunElevation(0f, 0.25f, 0.833f); // night
            Assert.AreEqual(0f, elevation, 0.001f);
        }

        [Test]
        public void LightingTransitionConfig_SunElevation_PeakAtNoon()
        {
            var config = new LightingTransitionConfig(sunAngleAtNoon: 60f);
            float sunriseNorm = 6f / 24f;  // 0.25
            float sunsetNorm = 20f / 24f;   // 0.833
            float noonNorm = (sunriseNorm + sunsetNorm) / 2f; // ~0.5417

            float elevation = config.EvaluateSunElevation(noonNorm, sunriseNorm, sunsetNorm);
            Assert.AreEqual(60f, elevation, 1f,
                $"Sun elevation at solar noon should be ~60, got {elevation}");
        }

        [Test]
        public void LightingTransitionConfig_InvalidSunAngle_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new LightingTransitionConfig(sunAngleAtNoon: 95f));
        }

        // ================================================================
        // AmbientColorKeyframe tests
        // ================================================================

        [Test]
        public void AmbientColorKeyframe_ValidValues_AreStored()
        {
            var kf = new AmbientColorKeyframe(0.5f, 0.8f, 0.6f, 0.4f);
            Assert.AreEqual(0.5f, kf.Time, 0.001f);
            Assert.AreEqual(0.8f, kf.R, 0.001f);
            Assert.AreEqual(0.6f, kf.G, 0.001f);
            Assert.AreEqual(0.4f, kf.B, 0.001f);
        }

        [Test]
        public void AmbientColorKeyframe_InvalidTime_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AmbientColorKeyframe(-0.1f, 0.5f, 0.5f, 0.5f));
            Assert.Throws<ArgumentException>(() => new AmbientColorKeyframe(1.1f, 0.5f, 0.5f, 0.5f));
        }

        [Test]
        public void AmbientColorKeyframe_InvalidColor_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AmbientColorKeyframe(0.5f, -0.1f, 0.5f, 0.5f));
            Assert.Throws<ArgumentException>(() => new AmbientColorKeyframe(0.5f, 0.5f, 1.1f, 0.5f));
        }

        // ================================================================
        // PitchLightmapConfig tests
        // ================================================================

        [Test]
        public void PitchLightmapConfig_DefaultValues_CenterIsBrightest()
        {
            var config = new PitchLightmapConfig();
            Assert.AreEqual(1.0f, config.CenterIntensityMultiplier, 0.001f);
            Assert.Less(config.CornerIntensityMultiplier, config.CenterIntensityMultiplier,
                "Corners should be slightly dimmer than center");
        }

        [Test]
        public void PitchLightmapConfig_GetMultiplierForZone_ReturnsCorrect()
        {
            var config = new PitchLightmapConfig(
                centerIntensityMultiplier: 1.0f,
                cornerIntensityMultiplier: 0.8f,
                goalAreaIntensityMultiplier: 0.9f,
                touchlineIntensityMultiplier: 0.85f);

            Assert.AreEqual(1.0f, config.GetMultiplierForZone("center"), 0.001f);
            Assert.AreEqual(0.8f, config.GetMultiplierForZone("corner"), 0.001f);
            Assert.AreEqual(0.9f, config.GetMultiplierForZone("goal"), 0.001f);
            Assert.AreEqual(0.85f, config.GetMultiplierForZone("touchline"), 0.001f);
            Assert.AreEqual(1f, config.GetMultiplierForZone("unknown"), 0.001f);
        }

        [Test]
        public void PitchLightmapConfig_NegativeMultiplier_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PitchLightmapConfig(centerIntensityMultiplier: -0.1f));
        }

        // ================================================================
        // ShadowManager tests
        // ================================================================

        [Test]
        public void ShadowManager_DaytimeConfig_Is4Cascades()
        {
            var manager = new ShadowManager();
            var dayConfig = manager.GetConfigForTime(new TimeOfDay(12f));
            Assert.AreEqual(4, dayConfig.CascadeCount);
        }

        [Test]
        public void ShadowManager_NighttimeConfig_Is2Cascades()
        {
            var manager = new ShadowManager();
            var nightConfig = manager.GetConfigForTime(new TimeOfDay(2f));
            Assert.AreEqual(2, nightConfig.CascadeCount);
        }

        [Test]
        public void ShadowManager_InterpolatedMaxDistance_DayIsLarger()
        {
            var manager = new ShadowManager();
            float dayDist = manager.GetInterpolatedMaxDistance(new TimeOfDay(12f));
            float nightDist = manager.GetInterpolatedMaxDistance(new TimeOfDay(2f));

            Assert.Greater(dayDist, nightDist,
                $"Day shadow distance ({dayDist}) should exceed night ({nightDist})");
        }
    }
}
