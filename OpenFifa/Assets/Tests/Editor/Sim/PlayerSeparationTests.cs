using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Player-player separation keeps the animated bodies (0.35-0.4 m reach) from interpenetrating.</summary>
    [TestFixture]
    [Category("FID_ANIM")]
    public class PlayerSeparationTests
    {
        [Test]
        public void SeparatePlayers_CloseOpponents_KeptAtTwiceSeparationRadius()
        {
            var sim = SimTestHelpers.NewSim(22);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.Place(sim, 3, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0.5f, 0f);
            SimTestHelpers.SetBall(sim, new SimVec3(20f, 0.11f, 20f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 5; t++) sim.Step(intents);
            float d = SimMath.FlatDistance(sim.State.Players[3].Position, sim.State.Players[15].Position);
            Assert.GreaterOrEqual(d, 2f * sim.Config.Actions.PlayerSeparationRadius - 1e-3f);
        }

        [Test]
        public void PlayerSeparationRadius_CoversAnimatedBodyReach()
        {
            var c = new SimActionConfig();
            Assert.GreaterOrEqual(c.PlayerSeparationRadius, 0.35f);
            Assert.LessOrEqual(c.PlayerSeparationRadius, 0.45f);
            // The ball-deflection capsule is separate and unchanged.
            Assert.AreEqual(0.3f, c.PlayerBodyRadius, 1e-6f);
        }
    }
}
