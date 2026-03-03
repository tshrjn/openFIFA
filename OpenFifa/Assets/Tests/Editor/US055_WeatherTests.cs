using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US055")]
    public class US055_WeatherTests
    {
        // ============================================================
        // WeatherConfig defaults and ranges
        // ============================================================

        [Test]
        public void RainConfig_Defaults_DropletCount500()
        {
            var config = new RainConfig();
            Assert.AreEqual(500, config.DropletCount, "Default rain droplet count should be 500");
        }

        [Test]
        public void RainConfig_Constructor_ClampsPuddleDensityTo01()
        {
            var config = new RainConfig(100, 5f, 10f, 20f, 1.5f);
            Assert.AreEqual(1f, config.PuddleDensity, 0.001f,
                "Puddle density should be clamped to 1.0 max");
        }

        [Test]
        public void RainConfig_Constructor_ClampsDropletCountToMax()
        {
            var config = new RainConfig(10000, 5f, 10f, 20f, 0.5f);
            Assert.AreEqual(RainConfig.MaxDropletCount, config.DropletCount,
                $"Droplet count should be clamped to {RainConfig.MaxDropletCount}");
        }

        [Test]
        public void SnowConfig_Defaults_FlakeCount300()
        {
            var config = new SnowConfig();
            Assert.AreEqual(300, config.FlakeCount, "Default snow flake count should be 300");
        }

        [Test]
        public void SnowConfig_Constructor_ClampsAccumulationRate()
        {
            var config = new SnowConfig(200, 2f, 1f, 0.5f);
            Assert.AreEqual(SnowConfig.MaxAccumulationRate, config.AccumulationRate, 0.001f,
                $"Accumulation rate should be clamped to {SnowConfig.MaxAccumulationRate}");
        }

        [Test]
        public void FogConfig_Defaults_Density002()
        {
            var config = new FogConfig();
            Assert.AreEqual(0.02f, config.Density, 0.001f, "Default fog density should be 0.02");
        }

        [Test]
        public void FogConfig_Constructor_ClampsDensityToRange()
        {
            var config = new FogConfig(0.5f, 0.7f, 0.7f, 0.75f, 10f, 80f);
            Assert.AreEqual(FogConfig.MaxDensity, config.Density, 0.001f,
                $"Fog density should be clamped to {FogConfig.MaxDensity}");
        }

        [Test]
        public void FogConfig_ColorValues_ClampedTo01()
        {
            var config = new FogConfig(0.02f, 1.5f, -0.5f, 0.5f, 10f, 80f);
            Assert.AreEqual(1f, config.ColorR, 0.001f, "ColorR should be clamped to 1.0");
            Assert.AreEqual(0f, config.ColorG, 0.001f, "ColorG should be clamped to 0.0");
            Assert.AreEqual(0.5f, config.ColorB, 0.001f, "ColorB should remain 0.5");
        }

        // ============================================================
        // WindConfig
        // ============================================================

        [Test]
        public void WindConfig_Constructor_NormalizesDirection()
        {
            var config = new WindConfig(3f, 0f, 4f, 5f, 1f);
            float mag = config.DirectionMagnitude();
            Assert.AreEqual(1f, mag, 0.01f,
                $"Wind direction magnitude should be ~1.0 after normalization, got {mag}");
        }

        [Test]
        public void WindConfig_ZeroDirection_DefaultsToUnitX()
        {
            var config = new WindConfig(0f, 0f, 0f, 5f, 1f);
            Assert.AreEqual(1f, config.DirectionX, 0.001f,
                "Zero direction should default to (1,0,0)");
            Assert.AreEqual(0f, config.DirectionY, 0.001f);
            Assert.AreEqual(0f, config.DirectionZ, 0.001f);
        }

        [Test]
        public void WindConfig_Strength_ClampedToMax()
        {
            var config = new WindConfig(1f, 0f, 0f, 50f, 1f);
            Assert.AreEqual(WindConfig.MaxStrength, config.Strength, 0.001f,
                $"Wind strength should be clamped to {WindConfig.MaxStrength}");
        }

        [Test]
        public void WindConfig_GustFrequency_ClampedToMax()
        {
            var config = new WindConfig(1f, 0f, 0f, 5f, 10f);
            Assert.AreEqual(WindConfig.MaxGustFrequency, config.GustFrequency, 0.001f,
                $"Gust frequency should be clamped to {WindConfig.MaxGustFrequency}");
        }

        // ============================================================
        // WeatherTransitionConfig
        // ============================================================

        [Test]
        public void WeatherTransitionConfig_Default_BlendDuration5()
        {
            var config = new WeatherTransitionConfig();
            Assert.AreEqual(5f, config.BlendDuration, 0.001f,
                "Default blend duration should be 5 seconds");
        }

        [Test]
        public void WeatherTransitionConfig_ClampsMin()
        {
            var config = new WeatherTransitionConfig(0.01f);
            Assert.AreEqual(WeatherTransitionConfig.MinBlendDuration, config.BlendDuration, 0.001f,
                "Blend duration should be clamped to minimum");
        }

        // ============================================================
        // WeatherConfigData presets
        // ============================================================

        [Test]
        public void WeatherConfigData_ClearPreset_NoRain()
        {
            var config = WeatherConfigData.CreatePreset(WeatherType.Clear);
            Assert.AreEqual(0, config.Rain.DropletCount,
                "Clear weather should have zero rain droplets");
        }

        [Test]
        public void WeatherConfigData_HeavyRainPreset_HighDropletCount()
        {
            var config = WeatherConfigData.CreatePreset(WeatherType.HeavyRain);
            Assert.Greater(config.Rain.DropletCount, 500,
                "Heavy rain should have > 500 droplets");
        }

        [Test]
        public void WeatherConfigData_SnowPreset_HasFlakes()
        {
            var config = WeatherConfigData.CreatePreset(WeatherType.Snow);
            Assert.Greater(config.Snow.FlakeCount, 0,
                "Snow preset should have > 0 flakes");
        }

        [Test]
        public void WeatherConfigData_FogPreset_HighDensity()
        {
            var config = WeatherConfigData.CreatePreset(WeatherType.Fog);
            Assert.Greater(config.Fog.Density, 0.03f,
                "Fog preset should have density > 0.03");
        }

        // ============================================================
        // WeatherLogic state transitions
        // ============================================================

        [Test]
        public void WeatherStateMachine_InitialState_Clear()
        {
            var sm = new WeatherStateMachine();
            Assert.AreEqual(WeatherType.Clear, sm.CurrentWeatherType,
                "Initial weather should be Clear");
            Assert.IsFalse(sm.IsTransitioning, "Should not be transitioning initially");
        }

        [Test]
        public void WeatherStateMachine_TransitionTo_StartsTransition()
        {
            var sm = new WeatherStateMachine();
            sm.TransitionTo(WeatherType.HeavyRain);
            Assert.IsTrue(sm.IsTransitioning, "Should be transitioning after TransitionTo");
            Assert.AreEqual(0f, sm.TransitionProgress, 0.001f,
                "Transition progress should start at 0");
        }

        [Test]
        public void WeatherStateMachine_TransitionToSameType_NoTransition()
        {
            var sm = new WeatherStateMachine(WeatherType.Clear);
            sm.TransitionTo(WeatherType.Clear);
            Assert.IsFalse(sm.IsTransitioning,
                "Transitioning to same weather type should be a no-op");
        }

        [Test]
        public void WeatherStateMachine_Update_AdvancesProgress()
        {
            var sm = new WeatherStateMachine();
            sm.TransitionTo(WeatherType.Snow);
            float initialProgress = sm.TransitionProgress;
            sm.Update(1f);
            Assert.Greater(sm.TransitionProgress, initialProgress,
                "Update should advance transition progress");
        }

        [Test]
        public void WeatherStateMachine_TransitionCompletes_UpdatesCurrentConfig()
        {
            var sm = new WeatherStateMachine();
            sm.TransitionTo(WeatherType.Snow);

            // Advance beyond blend duration
            float blendDuration = sm.TargetConfig.Transition.BlendDuration;
            sm.Update(blendDuration + 1f);

            Assert.IsFalse(sm.IsTransitioning, "Transition should be complete");
            Assert.AreEqual(WeatherType.Snow, sm.CurrentWeatherType,
                "Current weather should now be Snow");
        }

        [Test]
        public void WeatherStateMachine_SetImmediate_NoTransition()
        {
            var sm = new WeatherStateMachine();
            sm.SetImmediate(WeatherType.HeavyRain);
            Assert.AreEqual(WeatherType.HeavyRain, sm.CurrentWeatherType);
            Assert.IsFalse(sm.IsTransitioning,
                "SetImmediate should not trigger a transition");
        }

        // ============================================================
        // PitchConditionTracker
        // ============================================================

        [Test]
        public void PitchConditionTracker_Initial_AllZero()
        {
            var tracker = new PitchConditionTracker();
            Assert.AreEqual(0f, tracker.Wetness, 0.001f);
            Assert.AreEqual(0f, tracker.SnowCoverage, 0.001f);
            Assert.AreEqual(0f, tracker.MudLevel, 0.001f);
        }

        [Test]
        public void PitchConditionTracker_Rain_IncreasesWetness()
        {
            var tracker = new PitchConditionTracker();
            var config = WeatherConfigData.CreatePreset(WeatherType.LightRain);
            tracker.Update(config, 5f);
            Assert.Greater(tracker.Wetness, 0f,
                "Wetness should increase during rain");
        }

        [Test]
        public void PitchConditionTracker_HeavyRain_IncreasesWetnessFaster()
        {
            var lightTracker = new PitchConditionTracker();
            var heavyTracker = new PitchConditionTracker();
            var lightConfig = WeatherConfigData.CreatePreset(WeatherType.LightRain);
            var heavyConfig = WeatherConfigData.CreatePreset(WeatherType.HeavyRain);

            lightTracker.Update(lightConfig, 5f);
            heavyTracker.Update(heavyConfig, 5f);

            Assert.Greater(heavyTracker.Wetness, lightTracker.Wetness,
                "Heavy rain should increase wetness faster than light rain");
        }

        [Test]
        public void PitchConditionTracker_Snow_IncreasesSnowCoverage()
        {
            var tracker = new PitchConditionTracker();
            var config = WeatherConfigData.CreatePreset(WeatherType.Snow);
            tracker.Update(config, 10f);
            Assert.Greater(tracker.SnowCoverage, 0f,
                "Snow coverage should increase during snow weather");
        }

        [Test]
        public void PitchConditionTracker_SnowCoverage_CappedByMax()
        {
            var tracker = new PitchConditionTracker();
            var config = WeatherConfigData.CreatePreset(WeatherType.Snow);

            // Update for a very long time
            for (int i = 0; i < 100; i++)
                tracker.Update(config, 10f);

            Assert.LessOrEqual(tracker.SnowCoverage, config.PitchEffect.SnowCoverageMax,
                $"Snow coverage should not exceed max ({config.PitchEffect.SnowCoverageMax})");
        }

        [Test]
        public void PitchConditionTracker_HighWetness_IncreasesMud()
        {
            var tracker = new PitchConditionTracker();
            var config = WeatherConfigData.CreatePreset(WeatherType.HeavyRain);

            // First build up wetness above the mud threshold
            for (int i = 0; i < 50; i++)
                tracker.Update(config, 1f);

            Assert.Greater(tracker.MudLevel, 0f,
                $"Mud should accumulate when wetness ({tracker.Wetness}) exceeds threshold ({config.PitchEffect.MudSplatterThreshold})");
        }

        [Test]
        public void PitchConditionTracker_Reset_AllZero()
        {
            var tracker = new PitchConditionTracker();
            var config = WeatherConfigData.CreatePreset(WeatherType.HeavyRain);
            tracker.Update(config, 10f);
            tracker.Reset();

            Assert.AreEqual(0f, tracker.Wetness, 0.001f, "Wetness should be 0 after reset");
            Assert.AreEqual(0f, tracker.SnowCoverage, 0.001f, "Snow should be 0 after reset");
            Assert.AreEqual(0f, tracker.MudLevel, 0.001f, "Mud should be 0 after reset");
        }

        [Test]
        public void PitchConditionTracker_ClearWeather_DecreasesWetness()
        {
            var tracker = new PitchConditionTracker();
            var rainConfig = WeatherConfigData.CreatePreset(WeatherType.HeavyRain);
            var clearConfig = WeatherConfigData.CreatePreset(WeatherType.Clear);

            // Build up wetness
            tracker.Update(rainConfig, 10f);
            float wetAfterRain = tracker.Wetness;

            // Let it dry
            tracker.Update(clearConfig, 10f);
            Assert.Less(tracker.Wetness, wetAfterRain,
                "Wetness should decrease during clear weather");
        }

        // ============================================================
        // Weather blend interpolation
        // ============================================================

        [Test]
        public void WeatherStateMachine_GetParticleParams_InterpolatesDuringTransition()
        {
            var sm = new WeatherStateMachine(WeatherType.Clear);
            sm.TransitionTo(WeatherType.HeavyRain);

            // Advance halfway
            float blendDuration = sm.TargetConfig.Transition.BlendDuration;
            sm.Update(blendDuration * 0.5f);

            var p = sm.GetCurrentParticleParams();

            // At 50% blend, rain droplet count should be between clear (0) and heavy rain (1000)
            Assert.Greater(p.RainDropletCount, 0,
                "Rain droplets should be > 0 at 50% transition from clear to heavy rain");
            Assert.Less(p.RainDropletCount, 1000,
                "Rain droplets should be < 1000 at 50% transition to heavy rain");
        }

        [Test]
        public void WeatherStateMachine_GetParticleParams_NoTransition_ReturnsCurrentConfig()
        {
            var sm = new WeatherStateMachine(WeatherType.Snow);
            var p = sm.GetCurrentParticleParams();

            var snowConfig = WeatherConfigData.CreatePreset(WeatherType.Snow);
            Assert.AreEqual(snowConfig.Snow.FlakeCount, p.SnowFlakeCount,
                "Without transition, params should match current config");
        }

        // ============================================================
        // Fog density ranges
        // ============================================================

        [Test]
        public void WeatherStateMachine_GetFogDensity_ClearWeather_Low()
        {
            var sm = new WeatherStateMachine(WeatherType.Clear);
            float density = sm.GetFogDensity();
            Assert.LessOrEqual(density, 0.001f,
                "Clear weather should have near-zero fog density");
        }

        [Test]
        public void WeatherStateMachine_GetFogDensity_FogWeather_High()
        {
            var sm = new WeatherStateMachine(WeatherType.Fog);
            float density = sm.GetFogDensity();
            Assert.Greater(density, 0.03f,
                "Fog weather should have density > 0.03");
            Assert.LessOrEqual(density, FogConfig.MaxDensity,
                $"Fog density should not exceed {FogConfig.MaxDensity}");
        }

        // ============================================================
        // WeatherLogic composite
        // ============================================================

        [Test]
        public void WeatherLogic_UpdateWeather_AdvancesTransition()
        {
            var logic = new WeatherLogic(WeatherType.Clear);
            logic.TransitionTo(WeatherType.Snow);
            Assert.IsTrue(logic.IsTransitioning);

            // Complete transition
            float blend = logic.StateMachine.TargetConfig.Transition.BlendDuration;
            logic.UpdateWeather(blend + 1f);

            Assert.IsFalse(logic.IsTransitioning);
            Assert.AreEqual(WeatherType.Snow, logic.CurrentWeather);
        }

        [Test]
        public void WeatherLogic_ShouldShowPuddles_TrueForHeavyRain()
        {
            var logic = new WeatherLogic();
            logic.SetImmediate(WeatherType.HeavyRain);
            Assert.IsTrue(logic.ShouldShowPuddles,
                "Heavy rain should show puddles (puddle density > 0.1)");
        }

        [Test]
        public void WeatherLogic_ShouldShowPuddles_FalseForClear()
        {
            var logic = new WeatherLogic(WeatherType.Clear);
            Assert.IsFalse(logic.ShouldShowPuddles,
                "Clear weather should not show puddles");
        }

        [Test]
        public void WeatherLogic_ShouldShowSnowCoverage_TrueForSnow()
        {
            var logic = new WeatherLogic();
            logic.SetImmediate(WeatherType.Snow);
            Assert.IsTrue(logic.ShouldShowSnowCoverage,
                "Snow weather should show snow coverage");
        }

        [Test]
        public void WeatherLogic_ShouldShowSnowCoverage_FalseForRain()
        {
            var logic = new WeatherLogic();
            logic.SetImmediate(WeatherType.HeavyRain);
            Assert.IsFalse(logic.ShouldShowSnowCoverage,
                "Rain weather should not show snow coverage");
        }

        [Test]
        public void WeatherLogic_ZeroDeltaTime_NoChange()
        {
            var logic = new WeatherLogic(WeatherType.Clear);
            logic.TransitionTo(WeatherType.Snow);
            logic.UpdateWeather(0f);
            Assert.IsTrue(logic.IsTransitioning,
                "Zero deltaTime should not advance transition");
            Assert.AreEqual(0f, logic.StateMachine.TransitionProgress, 0.001f);
        }

        [Test]
        public void WeatherLogic_NegativeDeltaTime_NoChange()
        {
            var logic = new WeatherLogic(WeatherType.Clear);
            logic.TransitionTo(WeatherType.HeavyRain);
            logic.UpdateWeather(-1f);
            Assert.IsTrue(logic.IsTransitioning,
                "Negative deltaTime should not advance transition");
        }

        // ============================================================
        // PitchEffectConfig
        // ============================================================

        [Test]
        public void PitchEffectConfig_Defaults_ValidRanges()
        {
            var config = new PitchEffectConfig();
            Assert.That(config.WetReflectivity, Is.InRange(0f, 1f),
                "Wet reflectivity should be in 0-1 range");
            Assert.That(config.MudSplatterThreshold, Is.InRange(0f, 1f),
                "Mud threshold should be in 0-1 range");
            Assert.That(config.SnowCoverageMax, Is.InRange(0f, 1f),
                "Snow coverage max should be in 0-1 range");
        }

        [Test]
        public void PitchEffectConfig_Constructor_ClampsValues()
        {
            var config = new PitchEffectConfig(2f, -1f, 5f);
            Assert.AreEqual(1f, config.WetReflectivity, 0.001f,
                "Reflectivity should be clamped to 1.0");
            Assert.AreEqual(0f, config.MudSplatterThreshold, 0.001f,
                "Mud threshold should be clamped to 0.0");
            Assert.AreEqual(1f, config.SnowCoverageMax, 0.001f,
                "Snow coverage max should be clamped to 1.0");
        }

        // ============================================================
        // Wind strength via WeatherLogic
        // ============================================================

        [Test]
        public void WeatherLogic_WindStrength_HeavyRain_StrongerThanClear()
        {
            var clearLogic = new WeatherLogic(WeatherType.Clear);
            var rainLogic = new WeatherLogic();
            rainLogic.SetImmediate(WeatherType.HeavyRain);

            Assert.Greater(rainLogic.WindStrength, clearLogic.WindStrength,
                "Heavy rain should have stronger wind than clear weather");
        }
    }
}
