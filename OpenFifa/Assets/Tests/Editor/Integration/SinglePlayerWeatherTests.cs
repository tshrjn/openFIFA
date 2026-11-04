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
    }
}
