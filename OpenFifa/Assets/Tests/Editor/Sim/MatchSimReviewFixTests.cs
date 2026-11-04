using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Regression tests for the PR #5 review findings (docs/review/PR5_REVIEW.md) in the match sim.</summary>
    [TestFixture]
    [Category("FID-SIM")]
    public class MatchSimReviewFixTests
    {
        private const float HL = 52.5f;
        private const float HW = 34f;

        // ---- Review item 1: a penalty at the whistle is played out. ----

        [Test]
        public void Step_PenaltyTakenAfterWhistle_HalfEndsOnlyAfterTheOutcome()
        {
            var cfg = new MatchSimConfig { HalfDurationSeconds = 2f };
            var sim = SimTestHelpers.NewSim(11, cfg);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ScheduleRestart(sim, RestartType.Penalty, SimTeam.Home, new SimVec3(0f, 0f, HL - 11f));
            sim.State.Clock = 1.9f;
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 30; t++) sim.Step(intents);
            Assert.AreEqual(SimMatchPhase.AwaitingRestart, sim.Phase, "the penalty must still be awaited after the whistle");
            Assert.Greater(sim.State.Clock, cfg.HalfDurationSeconds);

            int taker = sim.State.RestartTakerId;
            intents[taker] = PlayerIntent.Kick(IntentAction.Shoot, 0.6f, new SimVec3(0.25f, 0f, 1f).Normalized);
            int taken = -1, outcome = -1, whistle = -1;
            for (int t = 0; t < 900 && whistle < 0; t++)
            {
                foreach (var e in sim.Step(intents))
                {
                    if (e.Type == MatchEventType.RestartTaken && taken < 0) taken = e.Tick;
                    if ((e.Type == MatchEventType.Goal || e.Type == MatchEventType.Save || e.Type == MatchEventType.OutOfPlay)
                        && outcome < 0) outcome = e.Tick;
                    if (e.Type == MatchEventType.HalfTime) whistle = e.Tick;
                }

                intents[taker] = PlayerIntent.Idle;
            }

            Assert.GreaterOrEqual(taken, 0, "penalty taken");
            Assert.GreaterOrEqual(whistle, 0, "half time eventually");
            Assert.Greater(whistle, taken, "the half must not end on the contact frame");
            Assert.GreaterOrEqual(outcome, taken, "goal, save or out of play before the whistle");
            Assert.GreaterOrEqual(whistle, outcome);
        }

        // ---- Review item 4: saves at closest approach, no teleport into the hands. ----

        private static MatchSim ShotAtKeeper(ulong seed, float lateral)
        {
            var sim = SimTestHelpers.NewSim(seed);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -30f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 10f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
            sim.State.Players[11].Facing = SimMath.Pi;
            SimTestHelpers.SetBall(sim, new SimVec3(lateral, 1f, HL - 16f), new SimVec3(0f, 3.5f, 20f));
            sim.State.LastTouchPlayer = 9;
            sim.State.LastTouchTeam = SimTeam.Home;
            sim.State.KickSerial = 1;
            sim.State.ShotLive = true;
            sim.State.Shooter = 9;
            return sim;
        }

        [Test]
        public void Save_ShotAtKeeper_ResolvedAtClosestApproachNotAtReachDistance()
        {
            int saves = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sim = ShotAtKeeper(seed, 0.8f);
                if (!SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.Save, 90, out var e)) continue;
                saves++;
                Assert.AreEqual(HL - 1f, e.Position.Z, 0.45f, $"seed {seed}: save must happen where the ball passes the keeper");
            }

            Assert.Greater(saves, 10);
        }

        [Test]
        public void Save_Caught_BallMovesSmoothlyIntoTheHands()
        {
            int caught = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = ShotAtKeeper(seed, 1.6f);
                var intents = SimTestHelpers.IdleIntents();
                SimVec3 prev = sim.State.Ball.Position;
                bool held = false;
                for (int t = 0; t < 120; t++)
                {
                    var ev = sim.Step(intents);
                    for (int i = 0; i < ev.Count; i++) if (ev[i].Type == MatchEventType.Save && ev[i].Detail == 1) { held = true; caught++; }
                    float jump = SimVec3.Distance(prev, sim.State.Ball.Position);
                    Assert.Less(jump, 0.4f, $"seed {seed} tick {t}: ball jumped {jump:F2} m in one tick");
                    prev = sim.State.Ball.Position;
                }

                if (held) Assert.IsTrue(sim.State.KeeperHolding, $"seed {seed}");
            }

            Assert.Greater(caught, 0, "some catches expected");
        }

        // ---- Review item 6: a parry that still goes in is the shooter's goal. ----

        [Test]
        public void Goal_AfterKeeperParry_CreditedToShooterNotOwnGoal()
        {
            int checkedGoals = 0;
            for (ulong seed = 1; seed <= 60 && checkedGoals < 3; seed++)
            {
                var sim = ShotAtKeeper(seed, 1.4f);
                sim.State.Ball = new SimBall
                {
                    Position = sim.State.Ball.Position,
                    Velocity = new SimVec3(0f, 3.5f, 28f),
                    Grounded = false,
                };
                var intents = SimTestHelpers.IdleIntents();
                if (!SimTestHelpers.StepUntil(sim, intents, MatchEventType.Save, 90, out var save) || save.Detail != 0) continue;
                Assert.AreEqual(9, save.OtherPlayerId, "save names the shooter");

                // The parry spins back toward goal; the keeper is out of the way.
                SimTestHelpers.Place(sim, 11, 12f, HL - 1f);
                var b = sim.State.Ball;
                b.Position = new SimVec3(0f, 1f, HL - 2f);
                b.Velocity = new SimVec3(0f, 0f, 8f);
                sim.State.Ball = b;
                Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.Goal, 120, out var goal), $"seed {seed}");
                Assert.AreEqual(9, goal.PlayerId, "scorer");
                Assert.AreEqual(0, goal.Detail, "not a keeper own goal");
                Assert.AreEqual(SimTeam.Home, goal.Team);
                checkedGoals++;
            }

            Assert.Greater(checkedGoals, 0, "need at least one parry");
        }

        // ---- Review item 5: ball-vs-body deflection and player separation. ----

        private static MatchSim BallAtPlayer(float height, float speed)
        {
            var sim = SimTestHelpers.NewSim(21);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -40f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 40f);
            SimTestHelpers.Place(sim, 5, 0f, 0f);
            // Launched so that the apex of the flight (the given height) is right at the player.
            float t = 3f / speed;
            float vy = 9.81f * t;
            float y0 = height - 0.5f * 9.81f * t * t;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, y0, -3f), new SimVec3(0f, vy, speed));
            sim.State.LastTouchPlayer = 15;
            sim.State.LastTouchTeam = SimTeam.Away;
            return sim;
        }

        [Test]
        public void Ball_AtHeadHeightAboveControlHeight_DeflectsOffThePlayer()
        {
            var sim = BallAtPlayer(1.97f, 20f);
            var intents = SimTestHelpers.IdleIntents();
            bool deflected = false;
            for (int t = 0; t < 30 && !deflected; t++)
                foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.Deflection && e.PlayerId == 5) deflected = true;
            Assert.IsTrue(deflected, "the ball must hit the head, not pass through it");
            Assert.Less(sim.State.Ball.Velocity.Z, 12f, "a header-height hit takes most of the pace off");
            Assert.Greater(sim.State.Ball.Velocity.Y, 0f, "a ball clipping the top of the head pops up");
            Assert.AreEqual(5, sim.State.LastTouchPlayer, "a deflection is a touch");
        }

        [Test]
        public void Ball_WellAbovePlayer_PassesOver()
        {
            var sim = BallAtPlayer(2.4f, 20f);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 40; t++) sim.Step(intents);
            Assert.Greater(sim.State.Ball.Position.Z, 3f);
        }

        [Test]
        public void Ball_RollingIntoGroundedPlayer_Deflects()
        {
            var sim = BallAtPlayer(0.11f, 10f);
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, -3f), new SimVec3(0f, 0f, 10f));
            sim.State.Ball = new SimBall { Position = new SimVec3(0f, 0.11f, -3f), Velocity = new SimVec3(0f, 0f, 10f),
                Spin = BallIntegrator.RollingSpin(new SimVec3(0f, 0f, 10f), 0.11f, 0f), Grounded = true };
            sim.State.Players[5].ActionState = SimPlayerActionState.Grounded;
            sim.State.Players[5].StateEndTick = 100000;
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 40; t++) sim.Step(intents);
            Assert.Less(sim.State.Ball.Position.Z, 0.5f, "a grounded player is still an obstacle");
        }

        [Test]
        public void Players_Overlapping_AreSeparated()
        {
            var sim = SimTestHelpers.NewSim(22);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.Place(sim, 3, 0f, 0f);
            SimTestHelpers.Place(sim, 15, 0.1f, 0.05f);
            SimTestHelpers.SetBall(sim, new SimVec3(20f, 0.11f, 20f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 5; t++) sim.Step(intents);
            float d = SimMath.FlatDistance(sim.State.Players[3].Position, sim.State.Players[15].Position);
            Assert.GreaterOrEqual(d, 2f * sim.Config.Actions.PlayerBodyRadius - 1e-3f);
        }

        // ---- Review item 8: no direct goal from an indirect free kick or a throw-in. ----

        private static int GoalsAfterRestart(RestartType type, SimVec3 spot, out MatchEvent restartAward)
        {
            var sim = SimTestHelpers.NewSim(31);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 20f);
            SimTestHelpers.Place(sim, 11, 25f, HL - 2f);
            SimTestHelpers.ScheduleRestart(sim, type, SimTeam.Home, spot);
            var intents = SimTestHelpers.IdleIntents();
            sim.Step(intents);
            int taker = sim.State.RestartTakerId;
            intents[taker] = PlayerIntent.Kick(IntentAction.Pass, 0.3f, new SimVec3(0f, 0f, 1f));
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.RestartTaken, 60, out _));
            intents[taker] = PlayerIntent.Idle;
            SimTestHelpers.Place(sim, 11, 25f, HL - 2f);
            // Nobody else touches it: the ball flies straight into the +Z net.
            var b = sim.State.Ball;
            b.Position = new SimVec3(0f, 1f, HL - 4f);
            b.Velocity = new SimVec3(0f, 1f, 15f);
            b.Spin = SimVec3.Zero;
            b.Grounded = false;
            sim.State.Ball = b;
            sim.State.Players[taker].Position = new SimVec3(-15f, 0f, HL - 20f);
            int goals = 0;
            restartAward = default;
            for (int t = 0; t < 120; t++)
            {
                foreach (var e in sim.Step(intents))
                {
                    if (e.Type == MatchEventType.Goal) goals++;
                    if (e.Type == MatchEventType.GoalKick || e.Type == MatchEventType.Corner) restartAward = e;
                }
            }

            return goals;
        }

        [Test]
        public void Goal_DirectFromIndirectFreeKick_NotAllowedGoalKickInstead()
        {
            Assert.AreEqual(0, GoalsAfterRestart(RestartType.IndirectFreeKick, new SimVec3(0f, 0f, HL - 25f), out var award));
            Assert.AreEqual(MatchEventType.GoalKick, award.Type);
            Assert.AreEqual(SimTeam.Away, award.Team);
        }

        [Test]
        public void Goal_DirectFromThrowIn_NotAllowedGoalKickInstead()
        {
            Assert.AreEqual(0, GoalsAfterRestart(RestartType.ThrowIn, new SimVec3(HW, 0f, HL - 12f), out var award));
            Assert.AreEqual(MatchEventType.GoalKick, award.Type);
        }

        [Test]
        public void Goal_DirectFromDirectFreeKick_Counts()
        {
            Assert.AreEqual(1, GoalsAfterRestart(RestartType.DirectFreeKick, new SimVec3(0f, 0f, HL - 25f), out _));
        }

        // ---- Review item 9: the wall stays on the pitch. ----

        [Test]
        public void Restart_FreeKickNearGoalLine_OpponentsStayOnPitchAndBackTheDistance()
        {
            var sim = SimTestHelpers.NewSim(41);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, 0f);
            for (int i = 1; i < 11; i++) SimTestHelpers.Place(sim, 11 + i, -6f + i * 1.2f, HL - 0.5f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 0.2f);
            var spot = new SimVec3(3f, 0f, HL - 1.5f);
            SimTestHelpers.ScheduleRestart(sim, RestartType.IndirectFreeKick, SimTeam.Home, spot);
            var intents = SimTestHelpers.IdleIntents();
            for (int t = 0; t < 3; t++) sim.Step(intents);
            Assert.AreEqual(SimMatchPhase.AwaitingRestart, sim.Phase);
            for (int i = 11; i < 22; i++)
            {
                var p = sim.State.Players[i].Position;
                Assert.LessOrEqual(SimMath.Abs(p.Z), HL + 1e-3f, $"player {i} pushed off the pitch: {p}");
                Assert.LessOrEqual(SimMath.Abs(p.X), HW + 1e-3f, $"player {i}: {p}");
                Assert.GreaterOrEqual(SimMath.FlatDistance(p, spot), sim.Config.RestartDistance - 1e-2f, $"player {i}: {p}");
            }
        }

        // ---- Review item 14: possession ties are not always Home's. ----

        [Test]
        public void Possession_ExactTie_SeededNotAlwaysHome()
        {
            int home = 0, away = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Home, -40f);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, 40f);
                SimTestHelpers.Place(sim, 5, -0.5f, 0f);
                SimTestHelpers.Place(sim, 16, 0.5f, 0f);
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
                sim.Step(SimTestHelpers.IdleIntents());
                if (sim.State.Owner == 5) home++;
                else if (sim.State.Owner == 16) away++;
            }

            Assert.AreEqual(40, home + away);
            Assert.Greater(home, 5);
            Assert.Greater(away, 5);
        }
    }
}
