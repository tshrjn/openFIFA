using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// AI movement commands are refreshed at 10 Hz; the adapter must steer to the target every sim tick and brake on
    /// arrival, so players settle on formation spots instead of running past them and turning back.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class AIArrivalTests
    {
        /// <summary>Runs player 5 at a spot from <paramref name="start"/>, commands refreshed every 6 ticks like the director.</summary>
        private static void RunToSpot(SimVec3 start, SimVec3 initialVelocity, SimVec3 spot, out float overshoot,
            out float finalDistance, out float finalSpeed)
        {
            var sim = new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(3));
            var s = sim.State;
            s.Phase = SimMatchPhase.InPlay;
            s.PendingRestart = RestartType.None;
            s.RestartTakerId = -1;
            s.Owner = -1;
            s.KeeperHolding = false;
            s.PassLive = false;
            s.ShotLive = false;
            // Everyone else out of the way along the far goal line; the ball at rest in the other half.
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                s.Players[i].Position = new SimVec3(-30f + i * 2.5f, 0f, 50f);
                s.Players[i].Velocity = SimVec3.Zero;
            }
            var b = s.Ball;
            b.Position = new SimVec3(-20f, 0.11f, 30f);
            b.Velocity = SimVec3.Zero;
            b.Spin = SimVec3.Zero;
            b.Grounded = true;
            b.InNet = 0;
            s.Ball = b;
            const int id = 5;
            s.Players[id].Position = start;
            s.Players[id].Velocity = initialVelocity;

            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            SimVec3 approach = (spot - start).Flat.Normalized;
            var cmd = AIPlayerCommand.Idle(id, start);
            overshoot = float.MinValue;
            int ticks = sim.Config.SecondsToTicks(7f);
            for (int t = 0; t < ticks; t++)
            {
                var p = s.Players[id];
                if (t % 6 == 0)
                {
                    // What TeamAIBrain.SetMove writes at a decision tick.
                    float d = SimMath.FlatDistance(p.Position, spot);
                    cmd.MoveTarget = spot;
                    cmd.MoveDirection = d > 0.6f ? (spot - p.Position).Flat.Normalized : SimVec3.Zero;
                    cmd.Sprint = d > 1.5f;
                    cmd.Intent = TeamAIIntent.HoldShape;
                }
                intents[id] = SimTeamAIAdapter.ToIntent(cmd, p, t % 6 == 0);
                sim.Step(intents);
                float past = SimVec3.Dot((s.Players[id].Position - spot).Flat, approach);
                if (past > overshoot) overshoot = past;
            }
            finalDistance = SimMath.FlatDistance(s.Players[id].Position, spot);
            finalSpeed = s.Players[id].Velocity.Flat.Magnitude;
        }

        [Test]
        public void ToIntent_SprintToFormationSpot_SettlesWithoutOvershoot()
        {
            RunToSpot(new SimVec3(10f, 0f, -40f), SimVec3.Zero, new SimVec3(10f, 0f, -10f),
                out float overshoot, out float dist, out float speed);
            Assert.LessOrEqual(overshoot, 0.2f, "ran past the spot");
            Assert.LessOrEqual(dist, SimTeamAIAdapter.ArriveRadius + 0.2f, "did not settle on the spot");
            Assert.Less(speed, 0.5f, "still moving after arriving");
        }

        [Test]
        public void ToIntent_ArrivingAtFullSprintFromTheSide_SettlesWithoutOvershoot()
        {
            // Already at top speed and running across the line to the spot: the stale 10 Hz direction used to carry
            // him ~0.8 m past it.
            RunToSpot(new SimVec3(-12f, 0f, -20f), new SimVec3(7.5f, 0f, 0f), new SimVec3(0f, 0f, -18f),
                out float overshoot, out float dist, out float speed);
            Assert.LessOrEqual(overshoot, 0.2f, "ran past the spot");
            Assert.LessOrEqual(dist, SimTeamAIAdapter.ArriveRadius + 0.2f, "did not settle on the spot");
            Assert.Less(speed, 0.5f, "still moving after arriving");
        }
    }
}
