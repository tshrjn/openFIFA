using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>
    /// Single-player tuning: kicks against a presser on the ball, firm passes led like the sim leads them,
    /// sim-matched tackle decisions and the difficulty curves on top of theta.
    /// </summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAIPressResistanceTests
    {
        private static readonly SimVec3 North = new SimVec3(0f, 0f, 1f);

        [Test]
        public void BlockRisk_OpponentOnTheKickLine_IsBlocked()
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 5, 0f, 0.7f);
            float r = TeamAIOnBall.BlockRisk(w, 0, SimVec3.Zero, North, TeamAIOnBall.PassLaunchDelay);
            Assert.GreaterOrEqual(r, 0.95f);
        }

        [Test]
        public void BlockRisk_KickAwayFromAPresserAMetreBehind_IsClear()
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 5, 0f, -1.1f);
            float r = TeamAIOnBall.BlockRisk(w, 0, SimVec3.Zero, North, TeamAIOnBall.PassLaunchDelay);
            Assert.AreEqual(0f, r, 1e-4f);
        }

        [Test]
        public void BlockRisk_ReleaseFromHands_OpponentBesideTheKeeperDoesNotBlock()
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, 0.6f, 0f);
            float feet = TeamAIOnBall.BlockRisk(w, 0, SimVec3.Zero, North, TeamAIOnBall.LobLaunchDelay, false);
            float hands = TeamAIOnBall.BlockRisk(w, 0, SimVec3.Zero, North, TeamAIOnBall.LobLaunchDelay, true);
            Assert.Greater(feet, 0.9f, "from the feet the sim hands him the ball");
            Assert.AreEqual(0f, hands, 1e-4f, "Law 12: he may not take a release from the hands");
        }

        /// <summary>The original exploit: a defender on the ball near his own goal with the user's player standing on
        /// it lobbed it straight into him. With only blocked kicks on, he keeps the ball instead.</summary>
        [Test]
        public void OnBall_PresserStandingOnTheBallNearOwnGoal_DoesNotKickIntoHim()
        {
            var w = TeamAITestWorlds.Empty();
            int owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, -47f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 3, -8f, -40f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 10f, -20f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -51f);
            // Presser on the ball's far side (the ball is 0.6 m ahead of the owner), facing back at it.
            int presser = TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, 8f, -45.8f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 51f);
            TeamAITestWorlds.GiveBall(w, owner);
            var ob = new TeamAIOnBall();
            for (int seed = 1; seed <= 20; seed++)
            {
                var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
                var ctx = new TeamAIOnBallContext { OffsideDepth = 0.5f };
                ob.Decide(w, owner, TeamAIPhase.BuildUp, new TeamAIDifficulty(1f), new TeamAIRandom((ulong)seed), ctx, ref cmd);
                bool kick = cmd.Action == TeamAIAction.Pass || cmd.Action == TeamAIAction.LobPass ||
                            cmd.Action == TeamAIAction.ThroughPass || cmd.Action == TeamAIAction.Cross;
                if (kick)
                    Assert.Less(TeamAIOnBall.BlockRisk(w, 0, TeamAIMath.Flat(w.Ball.Position), cmd.Aim, TeamAIOnBall.PassLaunchDelay),
                        0.6f, $"seed {seed}: kicked into the presser ({cmd.Action}, {cmd.Intent})");
            }
            Assert.AreEqual(1, w.Players[presser].Team);
        }

        private static bool IsKick(TeamAIAction a) =>
            a == TeamAIAction.Pass || a == TeamAIAction.LobPass || a == TeamAIAction.ThroughPass || a == TeamAIAction.Cross ||
            a == TeamAIAction.Shoot || a == TeamAIAction.FinesseShot || a == TeamAIAction.DrivenShot;

        /// <summary>
        /// The exploit setup above, held still: a presser who never leaves the ball. Returns the decision time (s) of the
        /// first kick, or +inf if the carrier still has it after 8 s.
        /// </summary>
        private static float FirstReleaseAgainstConstantPresser(float theta, ulong seed, out TeamAIIntent firstKeep)
        {
            var w = TeamAITestWorlds.Empty();
            int owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, -47f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 3, -8f, -40f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 10f, -20f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -51f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, 8f, -45.8f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 51f);
            TeamAITestWorlds.GiveBall(w, owner);
            var ob = new TeamAIOnBall();
            var rng = new TeamAIRandom(seed);
            var ctx = new TeamAIOnBallContext { OffsideDepth = 0.5f };
            firstKeep = TeamAIIntent.Idle;
            for (int k = 0; k <= 80; k++)
            {
                w.Time = k * 0.1f;
                var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
                ob.Decide(w, owner, TeamAIPhase.BuildUp, new TeamAIDifficulty(theta), rng, ctx, ref cmd);
                if (IsKick(cmd.Action)) return w.Time;
                if (k == 0) firstKeep = cmd.Intent;
            }
            return float.PositiveInfinity;
        }

        [Test]
        public void OnBall_ShieldingAgainstAConstantPresser_ReleasesTheBallWithinTheKeepLimit()
        {
            for (int lvl = 0; lvl <= 2; lvl++)
            {
                float theta = lvl * 0.5f;
                float bound = TeamAIOnBall.KeepLimit(theta) + TeamAIOnBall.KeepTurnWindow + 0.15f;
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    float t = FirstReleaseAgainstConstantPresser(theta, seed, out var firstKeep);
                    Assert.IsTrue(firstKeep == TeamAIIntent.Shield || firstKeep == TeamAIIntent.Carry,
                        $"theta {theta} seed {seed}: expected him to keep the ball first, got {firstKeep}");
                    Assert.LessOrEqual(t, bound, $"theta {theta} seed {seed}: still on the ball after {bound} s");
                    Assert.GreaterOrEqual(t, TeamAIOnBall.KeepLimit(theta) - 0.05f,
                        $"theta {theta} seed {seed}: gave up the shield before the keep limit");
                }
            }
            Assert.Less(TeamAIOnBall.KeepLimit(1f), TeamAIOnBall.KeepLimit(0f), "a strong side moves it on sooner");
        }

        [Test]
        public void LeadPoint_RunningReceiver_LedByTheFlightTime()
        {
            var w = TeamAITestWorlds.Empty();
            int rec = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 0f, 20f);
            w.Players[rec].Velocity = new SimVec3(6f, 0f, 0f);
            var ob = new TeamAIOnBall();
            var lead = ob.LeadPoint(w.Players[rec], SimVec3.Zero, TeamAIAction.Pass, North, w.Pitch);
            float t = ob.Model.GroundPassTime(TeamAIMath.FlatDistance(SimVec3.Zero, lead));
            Assert.AreEqual(6f * t, lead.X, 0.25f, "lead = velocity x flight time");
            Assert.AreEqual(20f, lead.Z, 1e-3f);
            var through = ob.LeadPoint(w.Players[rec], SimVec3.Zero, TeamAIAction.ThroughPass, North, w.Pitch);
            Assert.Greater(through.X, lead.X + ob.Model.ThroughLeadDistance * 0.9f, "through ball adds the sim's lead along the run");
        }

        [Test]
        public void GroundPassPower_GoodReceiver_IsFirm()
        {
            var m = TeamAIBallModel.Default;
            for (float d = 5f; d <= TeamAIOnBall.MaxGroundPass; d += 5f)
            {
                float arrive = m.ArrivalSpeedForPower(m.GroundPassPower(d, 0.8f));
                Assert.GreaterOrEqual(arrive, 6.5f - 1e-3f, $"{d} m pass is too soft");
                Assert.AreEqual(TeamAIBallModel.GroundPassPower(d), m.GroundPassPower(d, 0.8f), 1e-5f,
                    "a good first touch takes the full firm pass");
            }
            Assert.Less(m.GroundPassTime(15f), 1.6f, "a 15 m pass should take well under the old 1.9 s");
        }

        [Test]
        public void GroundPassPower_EveryReceiverControl_ArrivesUnderHisCleanFirstTouchWithMargin()
        {
            // The sim's clean first touch is 6 + 12 x control (m/s of ball speed relative to the receiver): a
            // 0-control player only kills a 6 m/s ball cleanly, so the firm 6.5-9 m/s pass would bounce off him.
            var m = TeamAIBallModel.Default;
            var ac = new SimActionConfig();
            for (int c = 0; c <= 10; c++)
            {
                float control = c / 10f;
                float clean = ac.FirstTouchCleanSpeedMin + ac.FirstTouchCleanSpeedRange * control;
                for (float d = 5f; d <= TeamAIOnBall.MaxGroundPass; d += 5f)
                {
                    float arrive = m.ArrivalSpeedForPower(m.GroundPassPower(d, control));
                    Assert.LessOrEqual(arrive, clean - TeamAIBallModel.FirstTouchMargin + 1e-3f,
                        $"{d} m pass too hot for control {control}");
                    Assert.GreaterOrEqual(arrive, ac.PassArrivalSpeedMin - 1e-3f);
                }
            }
            Assert.AreEqual(4.5f, m.ArrivalSpeedForPower(m.GroundPassPower(30f, 0f)), 1e-3f);
            Assert.Greater(m.GroundPassTimeTo(20f, 0f), m.GroundPassTimeTo(20f, 1f), "a softer pass takes longer");
        }

        [Test]
        public void OnBall_PassToAPoorFirstTouch_IsPlayedSofter()
        {
            var w = TeamAITestWorlds.Empty();
            int owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, -10f);
            int rec = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 0f, 15f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -51f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 51f);
            TeamAITestWorlds.GiveBall(w, owner);
            var ob = new TeamAIOnBall();
            var ctx = new TeamAIOnBallContext { OffsideDepth = 0.9f };
            float[] power = new float[2];
            for (int k = 0; k < 2; k++)
            {
                w.Players[rec].BallControl = k == 0 ? 0.9f : 0f;
                var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
                ob.Decide(w, owner, TeamAIPhase.Progression, new TeamAIDifficulty(1f), new TeamAIRandom(7), ctx, ref cmd);
                Assert.AreEqual(rec, ob.LastBestPass.TargetIndex);
                var a = ob.LastBestPass.Action;
                Assert.IsTrue(a == TeamAIAction.Pass || a == TeamAIAction.ThroughPass, $"expected a ground pass, got {a}");
                power[k] = ob.LastBestPass.Power;
            }
            Assert.Less(power[1], power[0] - 0.3f, "the 0-control striker should get a much softer ball");
            Assert.LessOrEqual(ob.Model.ArrivalSpeedForPower(power[1]), new SimActionConfig().FirstTouchCleanSpeedMin - 1f);
        }

        [Test]
        public void Tackle_ChanceFallsWithBallDistance_AndStrongSidesCommitEarlier()
        {
            var w = TeamAITestWorlds.Empty();
            int t = TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 0f, 0f);
            int c = TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, 0f, 1f);
            w.Players[t].Defending = 0.7f;
            w.Players[c].Dribbling = 0.7f;
            Assert.Greater(TeamAIBrain.TackleWinChance(w.Players[t], w.Players[c], 0.5f),
                TeamAIBrain.TackleWinChance(w.Players[t], w.Players[c], 1.5f));
            Assert.Less(TeamAIBrain.TackleThreshold(1f), TeamAIBrain.TackleThreshold(0f));
            // Equal skill, ball at 1.1 m: a top side goes in, a weak one jockeys.
            float p = TeamAIBrain.TackleWinChance(w.Players[t], w.Players[c], 1.1f);
            Assert.Greater(p, TeamAIBrain.TackleThreshold(1f));
            Assert.Less(p, TeamAIBrain.TackleThreshold(0f));
        }

        [Test]
        public void DifficultyCurves_AmbitionAndPressAwarenessRiseWithTheta()
        {
            float prevA = 0f, prevP = 0f;
            for (int i = 0; i <= 10; i++)
            {
                float th = i / 10f;
                float a = TeamAIOnBall.Ambition(th), p = TeamAIOnBall.PressAwareness(th);
                Assert.GreaterOrEqual(a, prevA);
                Assert.GreaterOrEqual(p, prevP);
                prevA = a;
                prevP = p;
            }
            Assert.AreEqual(1.2f, TeamAIOnBall.Ambition(1f), 1e-4f);
            Assert.AreEqual(1f, TeamAIOnBall.PressAwareness(1f), 1e-4f);
        }
    }
}
