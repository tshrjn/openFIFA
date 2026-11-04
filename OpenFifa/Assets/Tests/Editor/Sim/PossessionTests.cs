using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID_SIM")]
    public class PossessionTests
    {
        private const float HL = 52.5f;

        private static MatchSim Dribbler(ulong seed, int dribbling)
        {
            var setup = MatchSetup.CreateDefault(seed);
            setup.Home[9].Dribbling = dribbling;
            var sim = new MatchSim(new MatchSimConfig(), setup);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, -45f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -30f);
            SimTestHelpers.Place(sim, 9, 20f, -20f);
            sim.State.Owner = 9;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, new SimVec3(20f, 0.11f, -19.5f), SimVec3.Zero);
            return sim;
        }

        private static void RunDribble(MatchSim sim, bool sprint, int ticks, out int touches, out float meanLead, out bool kept)
        {
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0f, 0f, 1f), sprint);
            touches = 0;
            float leadSum = 0f;
            int samples = 0;
            kept = true;
            for (int t = 0; t < ticks; t++)
            {
                foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.DribbleTouch && e.PlayerId == 9) touches++;
                if (sim.State.Owner != 9) kept = false;
                if (t > 60)
                {
                    leadSum += SimMath.FlatDistance(sim.State.Ball.Position, sim.State.Players[9].Position);
                    samples++;
                }
            }

            meanLead = leadSum / samples;
        }

        [Test]
        public void Dribble_JogFor4Seconds_KeepsBallWithRegularTouches()
        {
            RunDribble(Dribbler(1, 80), false, 240, out int touches, out float lead, out bool kept);
            Assert.IsTrue(kept);
            Assert.That(touches, Is.InRange(6, 16));
            Assert.Less(lead, 1.2f);
        }

        [Test]
        public void Dribble_Sprint_FewerTouchesAndBallFurtherAhead()
        {
            RunDribble(Dribbler(1, 75), false, 240, out int jogTouches, out float jogLead, out _);
            RunDribble(Dribbler(1, 75), true, 240, out int sprintTouches, out float sprintLead, out _);
            Assert.Less(sprintTouches, jogTouches);
            Assert.Greater(sprintLead, jogLead + 0.3f);
        }

        [Test]
        public void Dribble_BallSpeed_NeverExceedsPlayerSpeedPlusCap()
        {
            var sim = Dribbler(3, 60);
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.MoveOnly(new SimVec3(0.3f, 0f, 1f), true);
            float cap = sim.Config.Actions.MaxTouchSpeedOverPlayer;
            for (int t = 0; t < 300; t++)
            {
                foreach (var e in sim.Step(intents))
                    if (e.Type == MatchEventType.DribbleTouch)
                        Assert.LessOrEqual(e.Speed, sim.State.Players[9].Speed + cap + 0.01f);
            }
        }

        private static float FirstTouchQuality(float incomingSpeed)
        {
            var sim = SimTestHelpers.NewSim(4);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, -45f);
            SimTestHelpers.Place(sim, 9, 0f, 10f);
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 10f - 0.84f), new SimVec3(0f, 0f, incomingSpeed));
            var b = sim.State.Ball;
            b.Spin = BallIntegrator.RollingSpin(b.Velocity, 0.11f, 0f);
            sim.State.Ball = b;
            SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.FirstTouch, 10, out var e);
            return e.Value;
        }

        [Test]
        public void FirstTouch_HardBall_WorseThanSoftBall()
        {
            Assert.Greater(FirstTouchQuality(4f), FirstTouchQuality(24f) + 0.2f);
        }

        [Test]
        public void Keeper_ShotStraightAtHim_UsuallySaved()
        {
            int saves = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Home, -30f);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, 10f);
                SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
                SimTestHelpers.SetBall(sim, new SimVec3(0.5f, 1f, HL - 16f), new SimVec3(0f, 0f, 20f));
                sim.State.LastTouchPlayer = 9;
                sim.State.LastTouchTeam = SimTeam.Home;
                sim.State.KickSerial = 1;
                var intents = SimTestHelpers.IdleIntents();
                for (int t = 0; t < 90; t++)
                    foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.Save && e.PlayerId == 11) saves++;
            }

            Assert.Greater(saves, 12);
        }

        [Test]
        public void Keeper_ShotIntoTopCorner_UsuallyScores()
        {
            int goals = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Home, -30f);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, 10f);
                SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 1.6f, HL - 16f), new SimVec3(3.2f / 16f * 28f, 0.6f, 28f));
                sim.State.LastTouchPlayer = 9;
                sim.State.LastTouchTeam = SimTeam.Home;
                sim.State.KickSerial = 1;
                var intents = SimTestHelpers.IdleIntents();
                for (int t = 0; t < 90; t++)
                    foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.Goal) goals++;
            }

            Assert.Greater(goals, 10);
        }
    }
}
