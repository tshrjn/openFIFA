using System;
using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Tests.Editor.TeamAI;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// Back-line motion through <see cref="SimTeamAIAdapter"/> and the full sim: a defender walking a drifting shape spot
    /// never parks and restarts (stop-go), a tired player keeps up with a moving spot, and a defender caught upfield
    /// sprints back whatever the tempo limit says.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class BackLineMotionTests
    {
        private static MatchSim OpenSim(int id, SimVec3 at)
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
            b.Position = new SimVec3(0f, 0.11f, 25f);
            b.Velocity = SimVec3.Zero;
            b.Spin = SimVec3.Zero;
            b.Grounded = true;
            b.InNet = 0;
            s.Ball = b;
            s.Players[id].Position = at;
            s.Players[id].Velocity = SimVec3.Zero;
            s.Players[id].Facing = 0f;
            return sim;
        }

        /// <summary>Longest time (s) a back-four player walking his drifting line spot for 30 s stands still while it drifts away.</summary>
        private static float LongestStop(int driftId, out float maxLag)
        {
            const int id = 2;
            var basePos = new SimVec3(-8f, 0f, -30f);
            var sim = OpenSim(id, basePos);
            var s = sim.State;
            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            float sprintSpeed = PlayerKinematics.SprintSpeed(sim.AttributesOf(id), sim.Config.Kinematics, s.Players[id].Stamina);
            float stopped = 0f, longest = 0f, prevGap = 0f;
            maxLag = 0f;
            for (int t = 0; t < 60 * 32; t++)
            {
                float time = t * sim.Dt;
                // Same composition as TeamAIBrain.SetHold for a line keeper: own drift along the line, the line's depth drift.
                TeamAIShapeMotion.Adjustment(driftId, time, true, false, out var off, out var offVel);
                TeamAIShapeMotion.Adjustment(TeamAIShapeMotion.LineDriftId, time, true, false, out var lineOff, out var lineVel);
                var spot = basePos + new SimVec3(off.X, 0f, lineOff.Z);
                var spotVel = new SimVec3(offVel.X, 0f, lineVel.Z);
                var cmd = AIPlayerCommand.Idle(id, spot);
                cmd.MoveTarget = spot;
                cmd.MoveTargetVelocity = spotVel;
                cmd.Intent = TeamAIIntent.KeepLine;
                cmd.LookAt = s.Ball.Position.Flat;
                cmd.FaceLookAt = true;
                cmd.SprintSpeed = sprintSpeed;
                cmd.SpeedLimit = TeamAITempo.WalkSpeed; // a holder on his spot walks
                intents[id] = SimTeamAIAdapter.ToIntent(cmd, s.Players[id], t % 6 == 0);
                sim.Step(intents);
                float gap = SimMath.FlatDistance(s.Players[id].Position, spot);
                if (time >= 2f) // after the walk-in from rest
                {
                    // Stop-go: standing while his spot drifts away from him. (Standing while the spot passes through
                    // its own turning point, or comes back to him, is what a real player does.)
                    float speed = s.Players[id].Velocity.Flat.Magnitude;
                    bool standing = speed < 0.1f && spotVel.Magnitude > 0.15f && gap > prevGap;
                    stopped = standing ? stopped + sim.Dt : 0f;
                    longest = Math.Max(longest, stopped);
                    maxLag = Math.Max(maxLag, gap);
                }
                prevGap = gap;
            }
            return longest;
        }

        [Test]
        public void ToIntent_BackFourOnDriftingLineSpot_NeverStopsLongerThanAThirdOfASecond()
        {
            foreach (int driftId in new[] { 1, 2, 3, 4, 13, 14 })
            {
                float longest = LongestStop(driftId, out float lag);
                Assert.LessOrEqual(longest, 0.3f, $"drift {driftId}: stood still for {longest:F2} s (stop-go)");
                Assert.Less(lag, 1.3f, $"drift {driftId}: fell {lag:F2} m behind his spot");
            }
        }

        [Test]
        public void ToIntent_FixedSpot_KeepsTheSettleDeadZone()
        {
            // An exactly fixed spot still gets the hysteresis: settled, a 1 m offset is not worth a step.
            var sim = OpenSim(2, SimVec3.Zero);
            var cmd = AIPlayerCommand.Idle(2, new SimVec3(1f, 0f, 0f));
            cmd.MoveTarget = new SimVec3(1f, 0f, 0f);
            cmd.Intent = TeamAIIntent.KeepLine;
            var intent = SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[2], true);
            Assert.Less(intent.Move.Magnitude, 0.01f);
        }

        [Test]
        public void ToIntent_TiredPlayerOnARecedingSpot_SwitchesToTheSprintGait()
        {
            var sim = OpenSim(2, SimVec3.Zero);
            var cmd = AIPlayerCommand.Idle(2, new SimVec3(0f, 0f, 6f));
            cmd.MoveTarget = new SimVec3(0f, 0f, 6f);
            cmd.MoveTargetVelocity = new SimVec3(0f, 0f, 3.8f); // block sliding at 3.8 m/s
            cmd.Intent = TeamAIIntent.HoldShape;
            cmd.SpeedLimit = 4.1f;
            cmd.SprintSpeed = 6f; // tired: jog top 3.72 m/s, cannot keep pace jogging
            var intent = SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[2], true);
            Assert.IsTrue(intent.Sprint, "a jog that cannot keep pace with the spot must switch to the sprint gait");
            cmd.SprintSpeed = 9.5f; // fresh: jog top 5.9 m/s is enough
            Assert.IsFalse(SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[2], true).Sprint);
        }

        private static TeamAIWorld CaughtUpfield(out int cb, out AIPlayerCommand cmd, float ballZ)
        {
            var w = TeamAITestWorlds.Empty();
            cb = TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, -6f, 10f);   // home attacks +Z; CB at +10
            int st = TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, 0f, ballZ);
            TeamAITestWorlds.GiveBall(w, st);                                 // away striker has it
            cmd = AIPlayerCommand.Idle(cb, new SimVec3(-6f, 0f, -30f));
            cmd.MoveTarget = new SimVec3(-6f, 0f, -30f);
            cmd.Intent = TeamAIIntent.KeepLine;
            cmd.SpeedLimit = 3.4f;
            cmd.SprintSpeed = 8f;
            return w;
        }

        [Test]
        public void ApplyRecoveryRun_CentreBackCaughtUpfield_SprintsBackWithoutTempoCap()
        {
            var w = CaughtUpfield(out int cb, out var cmd, -10f);
            Assert.IsTrue(SimTeamAIAdapter.ApplyRecoveryRun(ref cmd, w, cb));
            Assert.IsTrue(cmd.Sprint);
            Assert.AreEqual(0f, cmd.SpeedLimit);
            Assert.AreEqual(TeamAIIntent.Recover, cmd.Intent);
            var sim = OpenSim(2, new SimVec3(-6f, 0f, 10f));
            var intent = SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[2], true);
            Assert.IsTrue(intent.Sprint, "the sim intent must be a sprint");
            Assert.Greater(intent.Move.Magnitude, 0.99f, "full throttle, no tempo cap");
        }

        [Test]
        public void ApplyRecoveryRun_BallUpfieldOrOursOrDead_LeavesTheCommand()
        {
            var w = CaughtUpfield(out int cb, out var cmd, 30f); // ball ahead of him: not caught upfield
            var before = cmd;
            Assert.IsFalse(SimTeamAIAdapter.ApplyRecoveryRun(ref cmd, w, cb));
            Assert.AreEqual(before.SpeedLimit, cmd.SpeedLimit);

            w = CaughtUpfield(out cb, out cmd, -10f);
            TeamAITestWorlds.GiveBall(w, cb); // we have it
            Assert.IsFalse(SimTeamAIAdapter.ApplyRecoveryRun(ref cmd, w, cb));

            w = CaughtUpfield(out cb, out cmd, -10f);
            w.MatchPhase = TeamAIMatchPhase.FreeKick;
            Assert.IsFalse(SimTeamAIAdapter.ApplyRecoveryRun(ref cmd, w, cb));

            w = CaughtUpfield(out cb, out cmd, -10f);
            cmd.MoveTarget = new SimVec3(-6f, 0f, 5f); // spot only 5 m back
            Assert.IsFalse(SimTeamAIAdapter.ApplyRecoveryRun(ref cmd, w, cb));
        }
    }
}
