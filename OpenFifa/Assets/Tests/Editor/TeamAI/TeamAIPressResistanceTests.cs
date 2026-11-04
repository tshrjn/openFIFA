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
        public void GroundPassPower_IsFirmButNeverBeyondCleanFirstTouch()
        {
            var m = TeamAIBallModel.Default;
            var ac = new SimActionConfig();
            for (float d = 5f; d <= TeamAIOnBall.MaxGroundPass; d += 5f)
            {
                float arrive = m.ArrivalSpeedForPower(TeamAIBallModel.GroundPassPower(d));
                Assert.GreaterOrEqual(arrive, 6.5f - 1e-3f, $"{d} m pass is too soft");
                Assert.LessOrEqual(arrive, ac.FirstTouchCleanSpeedMin + 3f, $"{d} m pass is too hot for a poor first touch");
            }
            Assert.Less(m.GroundPassTime(15f), 1.6f, "a 15 m pass should take well under the old 1.9 s");
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
