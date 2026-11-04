using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Match weather reaches the ball config the sim and the team AI's ball model are built from.</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class SinglePlayerWeatherTests
    {
        private static SinglePlayerMatchSession NewSession(SinglePlayerMatchOptions o)
        {
            o.Seed = 4;
            o.Config ??= new MatchSimConfig { HalfDurationSeconds = 60f };
            return new SinglePlayerMatchSession(o);
        }

        [Test]
        public void Default_IsClearWeatherOnANormalPitch()
        {
            var o = new SinglePlayerMatchOptions();
            Assert.AreEqual(WeatherType.Clear, o.Weather);
            Assert.AreEqual(PitchSurface.Normal, o.EffectiveSurface);
            var s = NewSession(o);
            Assert.AreSame(o.Config, s.Sim.Config, "a normal pitch keeps the caller's config");
            Assert.AreEqual(PitchSurface.Normal, s.Sim.Config.Ball.Surface);
        }

        [TestCase(WeatherType.LightRain, PitchSurface.Wet)]
        [TestCase(WeatherType.HeavyRain, PitchSurface.Wet)]
        [TestCase(WeatherType.Snow, PitchSurface.Heavy)]
        [TestCase(WeatherType.Fog, PitchSurface.Normal)]
        public void Weather_SetsSimAndAIBallSurface(WeatherType weather, PitchSurface expected)
        {
            var cfg = new MatchSimConfig { HalfDurationSeconds = 60f };
            var s = NewSession(new SinglePlayerMatchOptions { Weather = weather, Config = cfg });
            Assert.AreEqual(expected, s.Sim.Config.Ball.Surface);
            Assert.AreSame(s.Sim.Config.Ball, s.Director.Brains[0].BallModel.Ball, "AI predicts with the match ball");
            Assert.AreSame(s.Sim.Config.Ball, s.Director.Brains[1].BallModel.Ball);
            Assert.AreEqual(PitchSurface.Normal, cfg.Ball.Surface, "the caller's config is not changed");
            Assert.AreEqual(60f, s.Sim.Config.HalfDurationSeconds);
        }

        [Test]
        public void WeatherMatrix_EachSurfaceReachesTheSim()
        {
            // Same as the TestCase above, as a plain [Test] for runners without parameterised-test support.
            foreach (WeatherType w in System.Enum.GetValues(typeof(WeatherType)))
            {
                var s = NewSession(new SinglePlayerMatchOptions { Weather = w });
                Assert.AreEqual(PitchSurfaces.ForWeather(w), s.Sim.Config.Ball.Surface, w.ToString());
                Assert.AreSame(s.Sim.Config.Ball, s.Director.Brains[0].BallModel.Ball, w.ToString());
            }
        }

        [Test]
        public void SurfaceOverride_WinsOverWeather()
        {
            var s = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.HeavyRain, Surface = PitchSurface.Dry });
            Assert.AreEqual(PitchSurface.Dry, s.Sim.Config.Ball.Surface);
            Assert.AreSame(s.Sim.Config.Ball, s.Director.Brains[1].BallModel.Ball);
        }

        [Test]
        public void WetPitch_BallRollsDifferentlyThanNormal()
        {
            var normal = new BallSimConfig();
            var wet = normal.WithSurface(PitchSurface.Wet);
            Assert.Less(wet.RollingResistance, normal.RollingResistance);
            Assert.Less(wet.GroundRestitution, normal.GroundRestitution);
        }

        [Test]
        public void ParseWeatherOption_AcceptsNamesAndPitchShorthands()
        {
            Assert.IsTrue(MatchWeatherOption.TryParse("dry", out var w, out var surf));
            Assert.AreEqual(WeatherType.Clear, w); Assert.AreEqual(PitchSurface.Dry, surf);
            Assert.IsTrue(MatchWeatherOption.TryParse("WET", out w, out surf));
            Assert.AreEqual(WeatherType.LightRain, w); Assert.IsNull(surf);
            Assert.IsTrue(MatchWeatherOption.TryParse("heavyrain", out w, out surf));
            Assert.AreEqual(WeatherType.HeavyRain, w);
            Assert.IsTrue(MatchWeatherOption.TryParse("heavy", out w, out _));
            Assert.AreEqual(WeatherType.Snow, w);
            Assert.IsTrue(MatchWeatherOption.TryParse("normal", out w, out surf));
            Assert.AreEqual(WeatherType.Clear, w); Assert.AreEqual(PitchSurface.Normal, surf);
            Assert.IsTrue(MatchWeatherOption.TryParse(" Overcast ", out w, out _));
            Assert.AreEqual(WeatherType.Overcast, w);
            Assert.IsFalse(MatchWeatherOption.TryParse("sunny", out _, out _));
            Assert.IsFalse(MatchWeatherOption.TryParse(null, out _, out _));
        }

        [Test]
        public void WetMatch_RunsDeterministically()
        {
            var a = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.HeavyRain });
            var b = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.HeavyRain });
            var botA = new ScriptedGamepadBot();
            var botB = new ScriptedGamepadBot();
            for (int i = 0; i < 600; i++) { a.StepOnce(botA); b.StepOnce(botB); }
            Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
        }

        // ---- The brain's own ball model reaches every AI timing path (follow-up to the weather commit) ----

        private const float BallDt = 1f / 120f;

        private static SimBall Rolling(BallSimConfig cfg, float speed) => new SimBall
        {
            Position = new SimVec3(0f, cfg.Radius, -30f),
            Velocity = new SimVec3(0f, 0f, speed),
            Grounded = true,
            Spin = BallIntegrator.RollingSpin(new SimVec3(0f, 0f, speed), cfg.Radius, 0f),
        };

        [Test]
        public void WetBrain_FreeBallPrediction_DiffersFromDefault_AndMatchesTheSim()
        {
            var s = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.HeavyRain });
            var brain = s.Director.Brains[1];
            var wet = s.Sim.Config.Ball;
            Assert.AreEqual(PitchSurface.Wet, wet.Surface);
            Assert.AreNotSame(TeamAIBallModel.Default, brain.BallModel);

            var b = Rolling(wet, 14f);
            foreach (float t in new[] { 1f, 2f, 3f, 4f, 6f })
            {
                var truth = BallIntegrator.PredictBall(b, wet, s.Sim.Config.Pitch, t).Position;
                // Keeper fallback path (no trajectory): TeamAIMath.PredictBall with the brain's model.
                var mine = TeamAIMath.PredictBall(b.Position, b.Velocity, t, brain.BallModel);
                var dflt = TeamAIMath.PredictBall(b.Position, b.Velocity, t);
                Assert.AreEqual(truth.Z, mine.Z, 0.05f, $"wet model matches the wet sim at {t} s");
                // The default model is measurably off on a wet pitch (standing water drags a fast-rolling ball).
                if (t >= 2f) Assert.Greater(System.Math.Abs(truth.Z - dflt.Z), 0.15f, $"default model misses the wet roll at {t} s");
            }
            // A null model still means the default ball.
            Assert.AreEqual(TeamAIMath.PredictBall(b.Position, b.Velocity, 2f).Z,
                TeamAIMath.PredictBall(b.Position, b.Velocity, 2f, null).Z, 1e-6f);
        }

        [Test]
        public void WetBrain_PassTimes_DifferFromDefault_AndMatchTheSim()
        {
            var s = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.HeavyRain });
            var m = s.Director.Brains[0].BallModel;
            var wet = s.Sim.Config.Ball;
            var act = s.Sim.Config.Actions;
            bool anyDiffers = false;
            for (float d = 15f; d <= 40f; d += 5f)
            {
                float arrive = act.PassArrivalSpeedMin + act.PassArrivalSpeedRange * TeamAIBallModel.GroundPassPower(d);
                float v0 = System.Math.Min(BallFlight.GroundPassSpeedFor(wet, d, arrive), act.PassMaxSpeed);
                var b = Rolling(wet, v0);
                b.Position = new SimVec3(0f, wet.Radius, 0f);
                float truth = float.PositiveInfinity;
                var free = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);
                for (int i = 1; i < 120 * 30; i++)
                {
                    BallIntegrator.Step(ref b, wet, free, BallDt);
                    if (b.Position.Z >= d) { truth = i * BallDt; break; }
                }
                Assert.AreEqual(truth, m.GroundPassTime(d), truth * 0.1f, $"wet ground pass {d} m");
                if (System.Math.Abs(m.GroundPassTime(d) - TeamAIBallModel.Default.GroundPassTime(d)) > 0.01f) anyDiffers = true;
            }
            Assert.IsTrue(anyDiffers, "wet pass timing differs from the default model");
        }

        [Test]
        public void Brain_SharesItsBallModelWithOnBallAndSetPieces()
        {
            var s = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.LightRain });
            foreach (var brain in s.Director.Brains)
            {
                Assert.AreSame(brain.BallModel, brain.OnBall.Model, "open-play passes");
                Assert.AreSame(brain.BallModel, brain.SetPieces.Model, "set-piece routines");
            }
            var fresh = new TeamAIBrain(0, new TeamAITactics(), 0.5f, 1UL);
            Assert.AreSame(TeamAIBallModel.Default, fresh.SetPieces.Model);
            fresh.BallModel = null;
            Assert.AreSame(TeamAIBallModel.Default, fresh.SetPieces.Model);
        }

        // ---- MatchSimConfig.ForSurface: the requested surface always wins ----

        [Test]
        public void ForSurface_SameSurface_ReturnsSameConfig()
        {
            var cfg = new MatchSimConfig();
            Assert.AreSame(cfg, cfg.ForSurface(PitchSurface.Normal));
            var wet = cfg.ForSurface(PitchSurface.Wet);
            Assert.AreSame(wet, wet.ForSurface(PitchSurface.Wet));
        }

        [Test]
        public void ForSurface_OnAWetConfig_AppliesTheRequestedSurface()
        {
            var normal = new MatchSimConfig();
            var wet = normal.ForSurface(PitchSurface.Wet);
            var dry = wet.ForSurface(PitchSurface.Dry);
            Assert.AreEqual(PitchSurface.Dry, dry.Ball.Surface, "requested surface wins over the config's");
            Assert.AreEqual(normal.Ball.WithSurface(PitchSurface.Dry).RollingResistance, dry.Ball.RollingResistance, 1e-6f,
                "re-surfaced from the Normal ball, not stacked on the wet one");
            Assert.AreSame(normal.Ball, wet.ForSurface(PitchSurface.Normal).Ball, "Normal gives the Normal ball back");
            Assert.AreEqual(PitchSurface.Wet, wet.Ball.Surface, "the source config is not changed");
        }

        [Test]
        public void ForSurface_HandSetSurfaceWithoutSource_Throws()
        {
            var cfg = new MatchSimConfig();
            cfg.Ball.Surface = PitchSurface.Heavy;
            Assert.Throws<System.InvalidOperationException>(() => cfg.ForSurface(PitchSurface.Wet));
            Assert.AreSame(cfg, cfg.ForSurface(PitchSurface.Heavy));
        }

        [Test]
        public void Session_WeatherWinsOverAWetConfig()
        {
            var wetCfg = new MatchSimConfig { HalfDurationSeconds = 60f }.ForSurface(PitchSurface.Wet);
            var s = NewSession(new SinglePlayerMatchOptions { Weather = WeatherType.Snow, Config = wetCfg });
            Assert.AreEqual(PitchSurface.Heavy, s.Sim.Config.Ball.Surface);
            Assert.AreSame(s.Sim.Config.Ball, s.Director.Brains[0].BallModel.Ball);
        }

        // ---- Weather visuals (-spWeather drives the WeatherSystem plus sky/lighting) ----

        [Test]
        public void WeatherLook_ClearIsTheUnchangedBroadcastLook()
        {
            var look = MatchWeatherOption.LookFor(WeatherType.Clear);
            Assert.AreEqual(1f, look.SunScale); Assert.AreEqual(1f, look.ShadowScale);
            Assert.AreEqual(1f, look.AmbientScale); Assert.AreEqual(0f, look.SkyGrey);
            Assert.IsFalse(look.Rain); Assert.IsFalse(look.Snow);
        }

        [Test]
        public void WeatherLook_RainShowsRain_AndGetsDarkerWithHeavierWeather()
        {
            var overcast = MatchWeatherOption.LookFor(WeatherType.Overcast);
            var light = MatchWeatherOption.LookFor(WeatherType.LightRain);
            var heavy = MatchWeatherOption.LookFor(WeatherType.HeavyRain);
            Assert.IsTrue(light.Rain); Assert.IsTrue(heavy.Rain); Assert.IsFalse(overcast.Rain);
            Assert.IsTrue(MatchWeatherOption.LookFor(WeatherType.Snow).Snow);
            Assert.Greater(1f, overcast.SunScale);
            Assert.Greater(overcast.SunScale, light.SunScale);
            Assert.Greater(light.SunScale, heavy.SunScale);
            Assert.Less(overcast.SkyGrey, light.SkyGrey);
            Assert.Less(light.SkyGrey, heavy.SkyGrey);
        }

        [Test]
        public void WeatherPresets_RainWeatherEmitsDroplets()
        {
            // The WeatherSystem's rain emitter runs off these preset counts; -spWeather rain must produce drops.
            foreach (var w in new[] { WeatherType.LightRain, WeatherType.HeavyRain })
            {
                var logic = new WeatherLogic(w);
                logic.SetImmediate(w);
                Assert.Greater(logic.GetCurrentParticleParams().RainDropletCount, 0, w.ToString());
                Assert.IsTrue(MatchWeatherOption.LookFor(w).Rain);
            }
            var clear = new WeatherLogic(WeatherType.Clear);
            Assert.AreEqual(0, clear.GetCurrentParticleParams().RainDropletCount);
        }
    }
}
