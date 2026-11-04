using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Presentation
{
    /// <summary>Broadcast weather visuals: fog at broadcast distance, the rain box, particle budgets and the wet-turf look.</summary>
    [TestFixture]
    [Category("FID_PRESENTATION")]
    public class WeatherPresentationTests
    {
        // SinglePlayerCamera defaults: 19 m up, 28 m back from the near touchline, 68 m pitch, yaw up to 25 deg.
        private const float CamHeight = 19f, Setback = 28f, PitchWidth = 68f, HalfLength = 52.5f, MaxYaw = 25f;
        private static readonly WeatherType[] AllWeather =
            { WeatherType.Clear, WeatherType.Overcast, WeatherType.LightRain, WeatherType.Snow, WeatherType.HeavyRain, WeatherType.Fog };

        private static float PresetDensity(WeatherType w) => WeatherConfigData.CreatePreset(w).Fog.Density;
        private static float Near => BroadcastFog.NearTouchlineDistance(CamHeight, Setback);
        private static float Far => BroadcastFog.FarTouchlineDistance(CamHeight, Setback, PitchWidth);

        [Test]
        public void FogFactorExp2_PresetDensityAtBroadcastDistance_WashesOutTheNearTouchline()
        {
            // The bug: the close-camera Fog preset used as is leaves the near touchline ~89% fogged.
            Assert.AreEqual(33.8f, Near, 0.1f);
            Assert.AreEqual(97.9f, Far, 0.1f);
            Assert.Greater(BroadcastFog.FogFactorExp2(PresetDensity(WeatherType.Fog), Near), 0.85f);
        }

        [Test]
        public void Density_EveryPreset_NearTouchlineAtMost15PercentFarAtMost40()
        {
            foreach (var w in AllWeather)
            {
                float d = BroadcastFog.Density(PresetDensity(w), Far);
                float near = BroadcastFog.FogFactorExp2(d, Near), far = BroadcastFog.FogFactorExp2(d, Far);
                Assert.LessOrEqual(near, 0.15f, w + " near touchline");
                Assert.LessOrEqual(far, 0.40f, w + " far touchline");
                Assert.LessOrEqual(near, far, w.ToString());
            }
        }

        [Test]
        public void Density_FogAndHeavyRain_StillReadAsFoggy()
        {
            float fog = BroadcastFog.FogFactorExp2(BroadcastFog.Density(PresetDensity(WeatherType.Fog), Far), Far);
            float rain = BroadcastFog.FogFactorExp2(BroadcastFog.Density(PresetDensity(WeatherType.HeavyRain), Far), Far);
            Assert.AreEqual(BroadcastFog.MaxFarTouchlineFog, fog, 1e-3f);
            Assert.Greater(fog, 0.3f);
            Assert.Greater(rain, 0.15f, "heavy rain haze is visible across the pitch");
            // The stands beyond the far touchline fade further (atmosphere), the near touchline stays crisp.
            float stand = BroadcastFog.FogFactorExp2(BroadcastFog.Density(PresetDensity(WeatherType.Fog), Far), 130f);
            Assert.Greater(stand, fog);
        }

        [Test]
        public void Density_HeavierWeather_FogsMore_ClearHasNone()
        {
            Assert.AreEqual(0f, BroadcastFog.Density(PresetDensity(WeatherType.Clear), Far));
            var order = new[] { WeatherType.Overcast, WeatherType.LightRain, WeatherType.Snow, WeatherType.HeavyRain, WeatherType.Fog };
            for (int i = 1; i < order.Length; i++)
                Assert.Greater(BroadcastFog.Density(PresetDensity(order[i]), Far), BroadcastFog.Density(PresetDensity(order[i - 1]), Far),
                    order[i] + " vs " + order[i - 1]);
        }

        [Test]
        public void Density_NoReferenceDistance_KeepsThePreset()
        {
            Assert.AreEqual(0.05f, BroadcastFog.Density(0.05f, 0f));
        }

        /// <summary>Smallest horizontal distance from (px, pz) to the box's footprint (0 inside).</summary>
        private static float DistanceToBox(PrecipitationBox b, float fwdX, float fwdZ, float px, float pz)
        {
            float len = (float)Math.Sqrt(fwdX * fwdX + fwdZ * fwdZ); fwdX /= len; fwdZ /= len;
            float dx = px - b.CenterX, dz = pz - b.CenterZ;
            float along = dx * fwdX + dz * fwdZ, across = -dx * fwdZ + dz * fwdX;
            float oa = Math.Max(0f, Math.Abs(along) - b.Depth * 0.5f), oc = Math.Max(0f, Math.Abs(across) - b.Width * 0.5f);
            return (float)Math.Sqrt(oa * oa + oc * oc);
        }

        private static void YawForward(float yawDeg, out float fx, out float fz)
        {
            // Camera on the +x side looking towards -x, yawed towards a goal (+-z).
            double r = yawDeg * Math.PI / 180.0;
            fx = (float)-Math.Cos(r); fz = (float)Math.Sin(r);
        }

        [TestCase(0f)]
        [TestCase(25f)]
        [TestCase(-25f)]
        public void PrecipitationBox_StaysClearOfTheCamera_AndCoversTheVisiblePitch(float yaw)
        {
            float farReach = (Setback + PitchWidth) / (float)Math.Cos(MaxYaw * Math.PI / 180.0);
            float camX = PitchWidth * 0.5f + Setback, camZ = yaw == 0f ? 0f : Math.Sign(yaw) * (HalfLength - 14f);
            YawForward(yaw, out float fx, out float fz);
            float tilt = WeatherConfigData.CreatePreset(WeatherType.HeavyRain).Rain.Angle;
            var b = PrecipitationVolume.Compute(camX, CamHeight, camZ, fx, fz, farReach, tilt);

            // Clearance: the falling prism (box footprint, drift included in NearEdge) is >= 10 m from the camera.
            float drift = PrecipitationVolume.WindDrift(b.EmitterHeight, tilt);
            Assert.GreaterOrEqual(DistanceToBox(b, fx, fz, camX, camZ) - drift, PrecipitationVolume.MinCameraClearance);
            Assert.Greater(b.EmitterHeight, CamHeight, "drops are born above the camera");

            // Coverage: every pitch point inside the broadcast view's horizontal FOV (27 deg vertical at 16:9) is under the box.
            double halfH = Math.Atan(Math.Tan(13.5 * Math.PI / 180.0) * 16.0 / 9.0);
            int inView = 0;
            for (float x = -PitchWidth * 0.5f; x <= PitchWidth * 0.5f; x += 4f)
                for (float z = -HalfLength; z <= HalfLength; z += 5f)
                {
                    float dx = x - camX, dz = z - camZ;
                    double ang = Math.Acos((dx * fx + dz * fz) / Math.Sqrt(dx * dx + dz * dz));
                    if (ang > halfH) continue;
                    inView++;
                    Assert.AreEqual(0f, DistanceToBox(b, fx, fz, x, z), 1e-3f, "pitch point (" + x + ", " + z + ") yaw " + yaw);
                }
            Assert.Greater(inView, 50);
            Assert.GreaterOrEqual(b.Width, 110f);
        }

        [Test]
        public void FallTime_DropsLiveUntilTheyReachTheGround()
        {
            const float h = 23f, v = 10f, g = 1.5f * 9.81f;
            float t = PrecipitationVolume.FallTime(h, v, 0f, g, 3f);
            Assert.AreEqual(h, v * t + 0.5f * g * t * t, 0.05f);
            Assert.Less(t, 2f, "heavy rain no longer lives 2 s and falls through the pitch");
            Assert.AreEqual(3f, PrecipitationVolume.FallTime(500f, v, 0f, g, 3f), "capped");
            float snow = PrecipitationVolume.FallTime(h, 1.5f, 0f, 0.1f * 9.81f, 12f);
            Assert.Greater(snow, t);
            Assert.LessOrEqual(snow, 12f);
        }

        [TestCase(RenderQualityTier.High)]
        [TestCase(RenderQualityTier.Mobile)]
        public void EmissionRate_StaysInsideTheTierParticleBudget(RenderQualityTier tier)
        {
            var q = RenderQualityTiers.For(tier);
            var b = PrecipitationVolume.Compute(62f, CamHeight, 0f, -1f, 0f, 106f, 15f);
            foreach (var w in AllWeather)
            {
                var p = WeatherConfigData.CreatePreset(w);
                float rainLife = PrecipitationVolume.FallTime(b.EmitterHeight, p.Rain.Speed, p.Rain.Angle, 1.5f * 9.81f, 3f);
                float rain = PrecipitationVolume.EmissionRate(p.Rain.DropletCount, 4f, b.Width * b.Depth, rainLife, q.MaxRainParticles);
                Assert.LessOrEqual(rain * rainLife, q.MaxRainParticles + 1f, w.ToString());
                float snowLife = PrecipitationVolume.FallTime(b.EmitterHeight, p.Snow.Speed, 0f, 0.1f * 9.81f, 12f);
                float snow = PrecipitationVolume.EmissionRate(p.Snow.FlakeCount, 4f, b.Width * b.Depth, snowLife, q.MaxSnowParticles);
                Assert.LessOrEqual(snow * snowLife, q.MaxSnowParticles + 1f, w.ToString());
            }
        }

        [Test]
        public void ParticleBudget_MobileIsUnderHalfOfDesktop()
        {
            var hi = RenderQualityTiers.For(RenderQualityTier.High);
            var lo = RenderQualityTiers.For(RenderQualityTier.Mobile);
            Assert.Greater(lo.MaxRainParticles, 0);
            Assert.LessOrEqual(lo.MaxRainParticles * 2, hi.MaxRainParticles);
            Assert.LessOrEqual(lo.MaxSnowParticles * 2, hi.MaxSnowParticles);
        }

        [Test]
        public void EmissionRate_BiggerBox_KeepsDensity()
        {
            float small = PrecipitationVolume.EmissionRate(1000f, 4f, PrecipitationVolume.ReferenceArea, 1f, 0);
            float big = PrecipitationVolume.EmissionRate(1000f, 4f, 2f * PrecipitationVolume.ReferenceArea, 1f, 0);
            Assert.AreEqual(4000f, small, 1e-2f);
            Assert.AreEqual(2f * small, big, 1e-2f);
            Assert.AreEqual(0f, PrecipitationVolume.EmissionRate(0f, 4f, 1000f, 1f, 100));
        }

        private static PitchWetnessLook LookFor(WeatherType w, float trackedWetness) =>
            PitchWetnessLook.For(w, WeatherConfigData.CreatePreset(w).PitchEffect.WetReflectivity, trackedWetness);

        [Test]
        public void WetnessLook_Clear_IsTheDryTurf()
        {
            var dry = LookFor(WeatherType.Clear, 0f);
            Assert.AreEqual(0f, dry.Amount);
            Assert.AreEqual(1f, dry.SmoothnessMultiplier);
            Assert.AreEqual(1f, dry.AlbedoScale);
        }

        [Test]
        public void WetnessLook_HeavierRain_ShinierAndDarker_WithinTurfLimits()
        {
            var heavy = LookFor(WeatherType.HeavyRain, 1f);
            var light = LookFor(WeatherType.LightRain, 1f);
            var fog = LookFor(WeatherType.Fog, 0f); // dew, no tracked rain
            Assert.Greater(heavy.SmoothnessMultiplier, light.SmoothnessMultiplier);
            Assert.Greater(light.SmoothnessMultiplier, fog.SmoothnessMultiplier);
            Assert.Greater(fog.SmoothnessMultiplier, 1f);
            Assert.Less(heavy.AlbedoScale, light.AlbedoScale);
            Assert.Less(light.AlbedoScale, 1f);
            // Turf base-map smoothness tops out at Smoothness + StripeSmoothness + noise (~0.31): stays clearly < 1.
            var t = new TurfSettings();
            float maxAlpha = t.Smoothness + t.StripeSmoothness + 0.02f;
            Assert.Less(maxAlpha * heavy.SmoothnessMultiplier, 0.7f);
            Assert.GreaterOrEqual(heavy.AlbedoScale, 0.75f, "slightly darker, not black");
            Assert.AreEqual(1.8f, heavy.SmoothnessMultiplier, 1e-3f);
            Assert.AreEqual(0.8f, heavy.AlbedoScale, 1e-3f);
        }

        [Test]
        public void WetnessLook_ScalesWithTrackedWetness()
        {
            Assert.AreEqual(0f, LookFor(WeatherType.LightRain, 0f).Amount, "rain on a dry pitch starts dry");
            Assert.Less(LookFor(WeatherType.HeavyRain, 0.5f).Amount, LookFor(WeatherType.HeavyRain, 1f).Amount);
            Assert.AreEqual(1f, PitchWetnessLook.FromAmount(5f).Amount);
            Assert.AreEqual(0f, PitchWetnessLook.FromAmount(float.NaN).Amount);
        }

        [Test]
        public void PitchCondition_SetWetness_PrimesAndClamps()
        {
            var logic = new WeatherLogic(WeatherType.HeavyRain);
            logic.PitchCondition.SetWetness(1f);
            Assert.AreEqual(1f, logic.PitchCondition.Wetness);
            logic.PitchCondition.SetWetness(-2f);
            Assert.AreEqual(0f, logic.PitchCondition.Wetness);
            logic.PitchCondition.SetWetness(1f);
            logic.UpdateWeather(1f);
            Assert.AreEqual(1f, logic.PitchCondition.Wetness, "stays saturated while it rains");
        }

        [Test]
        public void WeatherLook_AmbientScale_DimsTheTrilightALittle_HeavyRainMost()
        {
            foreach (var w in AllWeather)
            {
                float a = MatchWeatherOption.LookFor(w).AmbientScale;
                Assert.LessOrEqual(a, 1f, w.ToString());
                Assert.GreaterOrEqual(a, 0.8f, w.ToString());
            }
            Assert.Less(MatchWeatherOption.LookFor(WeatherType.HeavyRain).AmbientScale, MatchWeatherOption.LookFor(WeatherType.LightRain).AmbientScale);
            Assert.Less(MatchWeatherOption.LookFor(WeatherType.LightRain).AmbientScale, 1f);
        }
    }
}
