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

        /// <summary>
        /// Player 5 presses a carrier (a point running along +X at <paramref name="carrierSpeed"/> from 8 m ahead of
        /// him) with the command TeamAIBrain writes for a press: the goal-side point 1 m ahead of the carrier, moving
        /// with him. Returns the closest the presser got and his distance at the end.
        /// </summary>
        private static void PressRunningCarrier(float carrierSpeed, float stopAfter, bool trackVelocity, out float minDistance,
            out float finalDistance, out float overshoot)
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
            var run = new SimVec3(1f, 0f, 0f);
            s.Players[id].Position = new SimVec3(-30f, 0f, -10f);
            s.Players[id].Velocity = run * 6f;
            var carrier = new SimVec3(-22f, 0f, -10f);

            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            var cmd = AIPlayerCommand.Idle(id, s.Players[id].Position);
            minDistance = float.MaxValue;
            overshoot = float.MinValue;
            int ticks = sim.Config.SecondsToTicks(6f);
            int stopTick = sim.Config.SecondsToTicks(stopAfter);
            float speedNow = carrierSpeed;
            for (int t = 0; t < ticks; t++)
            {
                // After stopAfter the carrier pulls up at the sim's weakest braking (6 m/s^2).
                if (t >= stopTick) speedNow = SimMath.Max(0f, speedNow - 6f * sim.Dt);
                var vel = run * speedNow;
                var p = s.Players[id];
                if (t % 6 == 0)
                {
                    // What TeamAIBrain.DecidePresser writes: approach = ball + toGoal * 1 m, tracking the carrier.
                    var approach = carrier + run * 1f;
                    float d = SimMath.FlatDistance(p.Position, approach);
                    cmd.MoveTarget = approach;
                    cmd.MoveDirection = d > 0.6f ? (approach - p.Position).Flat.Normalized : SimVec3.Zero;
                    cmd.MoveTargetVelocity = trackVelocity ? vel : SimVec3.Zero;
                    cmd.Sprint = d > 1.5f;
                    cmd.Intent = TeamAIIntent.Press;
                }
                intents[id] = SimTeamAIAdapter.ToIntent(cmd, p, t % 6 == 0);
                sim.Step(intents);
                carrier += vel * sim.Dt;
                float dist = SimMath.FlatDistance(s.Players[id].Position, carrier);
                if (t > sim.Config.SecondsToTicks(1f) && dist < minDistance) minDistance = dist;
                if (t >= stopTick)
                {
                    float past = SimVec3.Dot((s.Players[id].Position - (carrier + run)).Flat, run);
                    if (past > overshoot) overshoot = past;
                }
            }
            finalDistance = SimMath.FlatDistance(s.Players[id].Position, carrier);
        }

        [Test]
        public void ToIntent_PressingACarrierRunningAway_ClosesToWithinOneAndAHalfMetres()
        {
            PressRunningCarrier(6f, 99f, true, out float min, out float final, out _);
            Assert.Less(min, 1.5f, "presser never got to the carrier");
            Assert.Less(final, 2.5f, "presser fell away from the carrier");
        }

        [Test]
        public void ToIntent_PressingWithoutTargetVelocity_PullsUpShort()
        {
            // Guard that the target velocity is what closes the gap: braking as if the goal-side point were fixed keeps
            // the sprinter about 3 m behind a carrier running away at 6 m/s, however long he chases.
            PressRunningCarrier(6f, 99f, false, out float min, out _, out _);
            Assert.Greater(min, 2.5f);
        }

        [Test]
        public void ToIntent_PressedCarrierStops_PresserSettlesWithoutOvershoot()
        {
            // The carrier pulls up after 3 s: the presser on his heels must stop at the goal-side point, not run on past.
            PressRunningCarrier(6f, 3f, true, out _, out float final, out float overshoot);
            Assert.LessOrEqual(overshoot, 0.8f, "ran past the goal-side point");
            Assert.Less(final, 2.2f, "did not settle at the carrier");
        }

        [Test]
        public void ToIntent_SettlingOnAJitteringFormationSpot_DoesNotReverseMoreThanOnce()
        {
            // The spot is recomputed at 10 Hz and jitters by up to ~0.6 m (anchors follow the ball). Arriving from 6 m at
            // a jog, the player must settle and hold: at most one reversal of his run, and his body must not flip.
            var sim = new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(3));
            var s = sim.State;
            s.Phase = SimMatchPhase.InPlay;
            s.PendingRestart = RestartType.None;
            s.RestartTakerId = -1;
            s.Owner = -1;
            s.KeeperHolding = false;
            s.PassLive = false;
            s.ShotLive = false;
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
            var spot = new SimVec3(0f, 0f, -10f);
            s.Players[id].Position = new SimVec3(0f, 0f, -16f);
            s.Players[id].Velocity = SimVec3.Zero;
            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            var cmd = AIPlayerCommand.Idle(id, s.Players[id].Position);
            float[] jitterX = { 0.45f, -0.3f, 0.15f, -0.45f, 0.35f, 0f, -0.25f, 0.45f };
            float[] jitterZ = { -0.35f, 0.4f, -0.15f, 0.3f, -0.45f, 0.25f, 0f, -0.4f };
            int reversals = 0, flips = 0;
            SimVec3 lastRun = SimVec3.Zero;
            float lastFacing = s.Players[id].Facing;
            int lastTurnSign = 0;
            int ticks = sim.Config.SecondsToTicks(5f);
            for (int t = 0; t < ticks; t++)
            {
                var p = s.Players[id];
                if (t % 6 == 0)
                {
                    int k = (t / 6) % jitterX.Length;
                    var target = spot + new SimVec3(jitterX[k], 0f, jitterZ[k]);
                    float d = SimMath.FlatDistance(p.Position, target);
                    cmd.MoveTarget = target;
                    cmd.MoveDirection = d > 0.6f ? (target - p.Position).Flat.Normalized : SimVec3.Zero;
                    cmd.Sprint = false;
                    cmd.Intent = TeamAIIntent.HoldShape;
                    cmd.LookAt = s.Ball.Position.Flat;
                    cmd.FaceLookAt = true;
                }
                intents[id] = SimTeamAIAdapter.ToIntent(cmd, p, t % 6 == 0);
                sim.Step(intents);
                var v = s.Players[id].Velocity.Flat;
                if (v.Magnitude > 0.1f)
                {
                    var run = v.Normalized;
                    if (lastRun.SqrMagnitude > 0f && SimVec3.Dot(run, lastRun) < -0.3f) reversals++;
                    lastRun = run;
                }
                if (t >= sim.Config.SecondsToTicks(3f))
                {
                    // Over the last 2 s (settled): the body may turn toward the ball, but never swing back and forth.
                    float turn = SimMath.DeltaAngle(lastFacing, s.Players[id].Facing);
                    int sign = turn > 0.002f ? 1 : (turn < -0.002f ? -1 : 0);
                    if (sign != 0 && lastTurnSign != 0 && sign != lastTurnSign) flips++;
                    if (sign != 0) lastTurnSign = sign;
                }
                lastFacing = s.Players[id].Facing;
            }
            Assert.LessOrEqual(reversals, 1, "reversed his run more than once while settling");
            Assert.AreEqual(0, flips, "body flipped while holding the spot");
            Assert.LessOrEqual(SimMath.FlatDistance(s.Players[id].Position, spot), SimTeamAIAdapter.ResumeRadius + 0.35f);
            Assert.Less(s.Players[id].Velocity.Flat.Magnitude, 0.3f, "still moving after settling");
        }
    }
}
