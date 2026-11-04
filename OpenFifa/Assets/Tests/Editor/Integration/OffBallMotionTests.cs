using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// Off-ball players in the full sim: small shape adjustments are walked facing the ball (no turning the back on play
    /// for a 1-2 m step), and in open play almost nobody stands still.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class OffBallMotionTests
    {
        private static MatchSim OpenSim(out int id)
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
            b.Position = new SimVec3(0f, 0.11f, 20f);
            b.Velocity = SimVec3.Zero;
            b.Spin = SimVec3.Zero;
            b.Grounded = true;
            b.InNet = 0;
            s.Ball = b;
            id = 5;
            s.Players[id].Position = new SimVec3(0f, 0f, 0f);
            s.Players[id].Velocity = SimVec3.Zero;
            s.Players[id].Facing = 0f; // facing +Z, the ball 20 m ahead
            return sim;
        }

        private static AIPlayerCommand Hold(int id, SimVec3 target, SimVec3 targetVel, SimVec3 ball)
        {
            var cmd = AIPlayerCommand.Idle(id, target);
            cmd.MoveTarget = target;
            cmd.MoveTargetVelocity = targetVel;
            cmd.Intent = TeamAIIntent.HoldShape;
            cmd.LookAt = ball.Flat;
            cmd.FaceLookAt = true;
            return cmd;
        }

        [Test]
        public void ToIntent_SmallSidewaysAdjustment_ShufflesFacingTheBall()
        {
            var sim = OpenSim(out int id);
            var s = sim.State;
            var intents = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < intents.Length; i++) intents[i] = PlayerIntent.Idle;
            var spot = new SimVec3(2f, 0f, 0f); // 2 m to his right
            float worstOff = 0f;
            float moved = 0f;
            var start = s.Players[id].Position;
            for (int t = 0; t < 90; t++)
            {
                var cmd = Hold(id, spot, SimVec3.Zero, s.Ball.Position);
                intents[id] = SimTeamAIAdapter.ToIntent(cmd, s.Players[id], t % 6 == 0);
                if (t == 0) Assert.AreEqual(IntentAction.Jockey, intents[id].Action, "a 2 m adjustment should be a shuffle");
                sim.Step(intents);
                var toBall = (s.Ball.Position - s.Players[id].Position).Flat;
                float off = System.Math.Abs(SimMath.DeltaAngle(s.Players[id].Facing, SimMath.Yaw(toBall)));
                if (off > worstOff) worstOff = off;
            }
            moved = SimMath.FlatDistance(start, s.Players[id].Position);
            Assert.Greater(moved, 1.0f, "did not adjust");
            Assert.Less(worstOff, 0.35f, "turned his body away from the ball for a small step");
            Assert.Less(s.Players[id].Velocity.Flat.Magnitude, 2.6f, "an adjustment is a shuffle, not a run");
        }

        [Test]
        public void ToIntent_FarSpot_TurnsAndRuns()
        {
            var sim = OpenSim(out int id);
            var cmd = Hold(id, new SimVec3(12f, 0f, 0f), SimVec3.Zero, sim.State.Ball.Position);
            var intent = SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[id], true);
            Assert.AreEqual(IntentAction.None, intent.Action);
            Assert.Greater(intent.Move.Magnitude, SimTeamAIAdapter.AdjustMaxThrottle);
        }

        [Test]
        public void ToIntent_FastMovingTarget_IsNotAShuffle()
        {
            var sim = OpenSim(out int id);
            var cmd = Hold(id, new SimVec3(1f, 0f, 0f), new SimVec3(4f, 0f, 0f), sim.State.Ball.Position);
            var intent = SimTeamAIAdapter.ToIntent(cmd, sim.State.Players[id], true);
            Assert.AreEqual(IntentAction.None, intent.Action, "tracking a runner is a run, not a shuffle");
        }

        [Test]
        public void SinglePlayerMatch_OpenPlay_OffBallPlayersAreRarelyStill()
        {
            // 90 s of match time with the scripted bot. Before the shape-motion change 3.4-4.8 % of AI outfield samples
            // were under 0.3 m/s in open play (6-8 % with the ball in a box); now holders walk their adjustments.
            var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
            {
                Level = TeamAIDifficultyLevel.Professional, Seed = 3UL, Config = new MatchSimConfig { HalfDurationSeconds = 45f }
            });
            var bot = new ScriptedGamepadBot { PressBall = true };
            long n = 0, still = 0;
            int ticks = 0;
            while (!s.IsFinished && ticks < 60 * 90)
            {
                s.StepOnce(bot);
                ticks++;
                var st = s.Sim.State;
                if (st.Phase != SimMatchPhase.InPlay || ticks < 120) continue;
                long hm = s.HumanMask;
                for (int i = 0; i < MatchSimState.PlayerCount; i++)
                {
                    if ((hm & (1L << i)) != 0 || i == st.Owner || st.Players[i].SentOff) continue;
                    if (s.AI.RoleOfPlayer[i] == TeamAIRole.GK) continue;
                    n++;
                    if (st.Players[i].Velocity.Flat.Magnitude < 0.3f) still++;
                }
            }
            Assert.Greater(n, 10000);
            float frac = (float)still / n;
            Assert.Less(frac, 0.025f, $"{frac:P1} of off-ball outfield samples standing still in open play");
        }
    }
}
