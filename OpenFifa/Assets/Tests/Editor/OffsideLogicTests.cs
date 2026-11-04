using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Law 11 (Offside). TeamA attacks +X, TeamB attacks -X, halfway line at X = 0.
    /// Default config here: body radius 0.25 m, ball radius 0, level tolerance 0.02 m.
    /// </summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsideLogicTests
    {
        private const TeamIdentifier A = TeamIdentifier.TeamA;
        private const TeamIdentifier B = TeamIdentifier.TeamB;

        private static OffsideLogic CreateLogic(float bodyRadius = 0.25f, float ballRadius = 0f,
            float tolerance = 0.02f, float interferenceRadius = 2f)
        {
            var config = new OffsideConfigData(
                bodyRadius: bodyRadius, ballRadius: ballRadius,
                levelTolerance: tolerance, interferenceRadius: interferenceRadius);
            return new OffsideLogic(config, playersPerSide: 11);
        }

        private static OffsidePlayerSnapshot P(TeamIdentifier team, int id, float x, float z = 0f)
        {
            return new OffsidePlayerSnapshot(team, id, x, z);
        }

        /// <summary>
        /// Standard attacking scene for TeamA: passer A0 at x=5 with the ball,
        /// keeper B0 on the goal line (x=24), last defender B1 at x=15, B2 at x=5.
        /// Attacker A1 at <paramref name="attackerX"/>.
        /// </summary>
        private static List<OffsidePlayerSnapshot> Scene(float attackerX, float a2X = 0f)
        {
            return new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 5f), P(A, 1, attackerX, 3f), P(A, 2, a2X, -3f),
                P(B, 0, 24f), P(B, 1, 15f), P(B, 2, 5f, 6f)
            };
        }

        // === Offside position ===

        [Test]
        public void EvaluatePositions_AttackerBeyondSecondLastAndBall_IsFlagged()
        {
            var logic = CreateLogic();
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, 5f, Scene(17f)),
                "Attacker at 17 is beyond the second-last opponent (15) and the ball (5)");
        }

        [Test]
        public void EvaluatePositions_AttackerBehindSecondLast_IsOnside()
        {
            var logic = CreateLogic();
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 5f, Scene(13f)));
        }

        [Test]
        public void EvaluatePositions_AttackerLevelWithSecondLast_IsOnside()
        {
            var logic = CreateLogic();
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 5f, Scene(15f)),
                "Level with the second-last opponent is onside");
        }

        [Test]
        public void EvaluatePositions_AttackerWithinLevelTolerance_IsOnside()
        {
            var logic = CreateLogic(tolerance: 0.02f);
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 5f, Scene(15.015f)),
                "1.5 cm ahead is within the 2 cm level tolerance");
        }

        [Test]
        public void EvaluatePositions_AttackerJustBeyondTolerance_IsFlagged()
        {
            var logic = CreateLogic(tolerance: 0.02f);
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, 5f, Scene(15.05f)));
        }

        [Test]
        public void EvaluatePositions_AttackerBehindBall_IsOnside()
        {
            var logic = CreateLogic();
            // Ball played from x=20 (cut-back), attacker at 17 is behind the ball.
            var players = Scene(17f);
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 20f, players));
        }

        [Test]
        public void EvaluatePositions_AttackerLevelWithBall_IsOnside()
        {
            // Ball at 17.25 equals the attacker's foremost point (17 + 0.25)
            var logic = CreateLogic();
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 17.25f, Scene(17f)));
        }

        [Test]
        public void EvaluatePositions_BallRadius_CountsTowardBallLine()
        {
            // Attacker front 17.25; ball centre 17.1 + radius 0.11 = 17.21 -> 4 cm ahead, offside.
            // With a ball radius of 0.2 the ball line is 17.3 -> onside.
            Assert.IsTrue(CreateLogic(ballRadius: 0.11f).IsInOffsidePosition(A, 1, 0, 17.1f, Scene(17f)));
            Assert.IsFalse(CreateLogic(ballRadius: 0.2f).IsInOffsidePosition(A, 1, 0, 17.1f, Scene(17f)));
        }

        [Test]
        public void EvaluatePositions_AttackerInOwnHalf_IsOnside()
        {
            var logic = CreateLogic();
            // Defenders pushed up into TeamA's half, ball deep in TeamA's half.
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, -20f), P(A, 1, -2f),
                P(B, 0, -8f), P(B, 1, -10f), P(B, 2, -12f)
            };
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, -20f, players),
                "A player entirely in their own half cannot be in an offside position");
        }

        [Test]
        public void EvaluatePositions_BodyPartOverHalfwayLine_IsFlagged()
        {
            var logic = CreateLogic(bodyRadius: 0.25f);
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, -20f), P(A, 1, -0.1f),
                P(B, 0, -8f), P(B, 1, -10f), P(B, 2, -12f)
            };
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, -20f, players),
                "Centre 10 cm short of halfway but body (0.25 m) reaches into the opponents' half");
        }

        [Test]
        public void EvaluatePositions_ZeroBodyRadius_CentreShortOfHalfway_IsOnside()
        {
            var logic = CreateLogic(bodyRadius: 0f);
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, -20f), P(A, 1, -0.1f),
                P(B, 0, -8f), P(B, 1, -10f), P(B, 2, -12f)
            };
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, -20f, players));
        }

        [Test]
        public void EvaluatePositions_BodyExactlyOnHalfwayLine_IsOnside()
        {
            var logic = CreateLogic(bodyRadius: 0.25f);
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, -20f), P(A, 1, -0.25f),
                P(B, 0, -8f), P(B, 1, -10f), P(B, 2, -12f)
            };
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, -20f, players),
                "The halfway line itself is not part of the opponents' half");
        }

        [Test]
        public void EvaluatePositions_KeeperOutOfGoal_SecondLastIsOutfieldDefender()
        {
            var logic = CreateLogic();
            // Keeper rushed out to x=10; defenders at 20 and 15. Second-last opponent = 15.
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 5f), P(A, 1, 18f), P(A, 2, 14f),
                P(B, 0, 10f), P(B, 1, 20f), P(B, 2, 15f)
            };
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, 5f, players), "18 is beyond second-last (15)");
            Assert.IsFalse(logic.IsInOffsidePosition(A, 2, 0, 5f, players), "14 is behind second-last (15)");
        }

        [Test]
        public void EvaluatePositions_KeeperAndOneDefenderBehindAttacker_IsOnside()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 5f), P(A, 1, 18f),
                P(B, 0, 24f), P(B, 1, 20f), P(B, 2, 5f)
            };
            Assert.IsFalse(logic.IsInOffsidePosition(A, 1, 0, 5f, players),
                "Keeper (24) and a defender (20) are nearer the goal line: second-last is 20");
        }

        [Test]
        public void EvaluatePositions_KeeperCountsAsOpponent_OnlyKeeperBehind_IsFlagged()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 5f), P(A, 1, 18f),
                P(B, 0, 24f), P(B, 1, 15f), P(B, 2, 5f)
            };
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, 5f, players));
        }

        [Test]
        public void EvaluatePositions_MultipleAttackers_FlagsOnlyThoseOffside()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 5f), P(A, 1, 17f), P(A, 2, 19f, 4f), P(A, 3, 12f, -4f), P(A, 4, 15f, 8f),
                P(B, 0, 24f), P(B, 1, 15f), P(B, 2, 5f)
            };
            var phase = logic.EvaluatePositions(A, 0, 5f, players);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, phase.FlaggedPlayerIds,
                "A1 (17) and A2 (19) are offside; A3 (12) is behind and A4 (15) is level");
        }

        [Test]
        public void EvaluatePositions_PasserNeverFlagged()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot>
            {
                P(A, 0, 20f), P(B, 0, 24f), P(B, 1, 15f)
            };
            var phase = logic.EvaluatePositions(A, 0, 20f, players);
            Assert.AreEqual(0, phase.FlaggedCount);
        }

        [Test]
        public void EvaluatePositions_TeamBAttacksNegativeX_Mirrored()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot>
            {
                P(B, 0, -5f), P(B, 1, -17f), P(B, 2, -13f),
                P(A, 0, -24f), P(A, 1, -15f), P(A, 2, -5f)
            };
            var phase = logic.EvaluatePositions(B, 0, -5f, players);
            Assert.IsTrue(phase.IsFlagged(1), "B1 at -17 is beyond A1 at -15");
            Assert.IsFalse(phase.IsFlagged(2), "B2 at -13 is behind A1");
            Assert.AreEqual(-15.25f, phase.DefenderLineX, 1e-4f);
            Assert.AreEqual(-17.25f, phase.GetAttackerLineX(1), 1e-4f);
        }

        [Test]
        public void EvaluatePositions_ConfiguredDirectionSwap_Mirrors()
        {
            var config = new OffsideConfigData(bodyRadius: 0.25f, ballRadius: 0f, teamAAttacksPositiveX: false);
            var logic = new OffsideLogic(config);
            Assert.AreEqual(-1f, logic.AttackSign(A));
            Assert.AreEqual(1f, logic.AttackSign(B));
        }

        [Test]
        public void EvaluatePositions_FewerThanTwoOpponents_OnlyBallAndHalfwayApply()
        {
            var logic = CreateLogic();
            var players = new List<OffsidePlayerSnapshot> { P(A, 0, 5f), P(A, 1, 10f), P(B, 0, 20f) };
            Assert.IsTrue(logic.IsInOffsidePosition(A, 1, 0, 5f, players));
        }

        [Test]
        public void EvaluatePositions_LineValues_IncludeBodyRadius()
        {
            var logic = CreateLogic(bodyRadius: 0.25f);
            var phase = logic.EvaluatePositions(A, 0, 5f, Scene(17f));
            Assert.AreEqual(17.25f, phase.GetAttackerLineX(1), 1e-4f);
            Assert.AreEqual(15.25f, phase.DefenderLineX, 1e-4f);
            Assert.AreEqual(5f, phase.BallLineX, 1e-4f);
        }

        // === Offence: involvement ===

        [Test]
        public void RegisterTouch_FlaggedPlayerTouchesNext_IsOffence()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));

            var receive = Scene(19f); // attacker has run on to the ball
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 19f, 3f, receive);

            Assert.IsTrue(decision.IsOffence);
            Assert.AreEqual(OffsideReason.PlayingBall, decision.Reason);
            Assert.AreEqual(A, decision.OffenderTeam);
            Assert.AreEqual(B, decision.DefendingTeam);
            Assert.AreEqual(1, decision.OffenderId);
            Assert.AreEqual(0, decision.PasserId);
        }

        [Test]
        public void RegisterTouch_Offence_FreeKickAtOffenderPositionAndLinesFromSnapshot()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 19.5f, 3f, Scene(19.4f));

            Assert.AreEqual(19.4f, decision.FreeKickX, 1e-4f, "IFK where the offender became involved");
            Assert.AreEqual(3f, decision.FreeKickZ, 1e-4f);
            Assert.AreEqual(17.25f, decision.AttackerLineX, 1e-4f, "Attacker line from the moment of the pass");
            Assert.AreEqual(15.25f, decision.DefenderLineX, 1e-4f);
        }

        [Test]
        public void RegisterTouch_OffsidePositionButNotInvolved_NoOffence()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f, a2X: 10f));

            // Onside team-mate A2 receives instead.
            var decision = logic.RegisterTouch(new BallTouch(A, 2), 10f, -3f, Scene(17f, a2X: 10f));
            Assert.IsFalse(decision.IsOffence, "Being in an offside position is not an offence");
        }

        [Test]
        public void RegisterTouch_OnsideTeammateTouches_StartsNewPhase()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f, a2X: 10f));
            Assert.IsTrue(logic.IsFlagged(A, 1));

            // A2 receives at 16 (ahead of A1 at 14 who has dropped back) and lays it off.
            var players = Scene(14f, a2X: 16f);
            logic.RegisterTouch(new BallTouch(A, 2), 16f, -3f, players);
            Assert.IsFalse(logic.IsFlagged(A, 1), "A1 is behind the ball at the new touch");

            var decision = logic.RegisterTouch(new BallTouch(A, 1), 14f, 3f, players);
            Assert.IsFalse(decision.IsOffence);
        }

        [Test]
        public void RegisterTouch_OnsideAtPass_RunsOffsideAfterwards_NoOffence()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(13f));
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 20f, 3f, Scene(20f));
            Assert.IsFalse(decision.IsOffence, "Judged at the moment the ball is played");
        }

        [Test]
        public void RegisterTouch_SamePasserTouchesAgain_NoOffenceAndPhaseRefreshed()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            var decision = logic.RegisterTouch(new BallTouch(A, 0), 6f, 0f, Scene(17f));
            Assert.IsFalse(decision.IsOffence);
            Assert.IsTrue(logic.IsFlagged(A, 1), "Re-evaluated: A1 still offside relative to the new touch");
        }

        // === Resets ===

        [Test]
        public void RegisterTouch_DefenderDeliberatePlay_ResetsOffside()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            logic.RegisterTouch(new BallTouch(B, 1, BallTouchKind.Deliberate), 15f, 0f, Scene(17f));

            var decision = logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));
            Assert.IsFalse(decision.IsOffence, "Deliberate play by an opponent resets offside");
        }

        [Test]
        public void RegisterTouch_DefenderDeflection_DoesNotReset()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            logic.RegisterTouch(new BallTouch(B, 1, BallTouchKind.Deflection), 15f, 0f, Scene(17f));

            var decision = logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));
            Assert.IsTrue(decision.IsOffence, "A deflection does not reset offside");
        }

        [Test]
        public void RegisterTouch_GoalkeeperSave_DoesNotReset()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            logic.RegisterTouch(new BallTouch(B, 0, BallTouchKind.Save), 23f, 0f, Scene(17f));

            var decision = logic.RegisterTouch(new BallTouch(A, 1), 20f, 3f, Scene(20f));
            Assert.IsTrue(decision.IsOffence, "Rebound from a deliberate save does not reset offside");
        }

        [Test]
        public void RegisterTouch_DefenderInterception_StartsPhaseForDefendingTeam()
        {
            var logic = CreateLogic();
            // B0 intercepts at x=0; B1 is goal-hanging at -20 beyond A's second-last defender.
            var players = new List<OffsidePlayerSnapshot>
            {
                P(B, 0, 0f), P(B, 1, -20f),
                P(A, 0, -24f), P(A, 1, -15f), P(A, 2, 2f)
            };
            logic.RegisterTouch(new BallTouch(B, 0), 0f, 0f, players);
            Assert.IsTrue(logic.IsFlagged(B, 1));
            var decision = logic.RegisterTouch(new BallTouch(B, 1), -20f, 0f, players);
            Assert.IsTrue(decision.IsOffence);
            Assert.AreEqual(A, decision.DefendingTeam);
        }

        // === Restarts ===

        [TestCase(RestartType.GoalKick)]
        [TestCase(RestartType.ThrowIn)]
        [TestCase(RestartType.Corner)]
        public void RegisterTouch_ExemptRestart_NoOffenceDirectly(RestartType restart)
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0, BallTouchKind.Deliberate, restart), 5f, 0f, Scene(17f));
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));
            Assert.IsFalse(decision.IsOffence, $"No offside offence directly from a {restart}");
        }

        [TestCase(RestartType.IndirectFreeKick)]
        [TestCase(RestartType.DirectFreeKick)]
        [TestCase(RestartType.KickOff)]
        [TestCase(RestartType.None)]
        public void RegisterTouch_NonExemptRestart_OffsideApplies(RestartType restart)
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0, BallTouchKind.Deliberate, restart), 5f, 0f, Scene(17f));
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));
            Assert.IsTrue(decision.IsOffence);
        }

        [Test]
        public void RegisterTouch_AfterExemptRestart_NextTeammatePassIsJudged()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0, BallTouchKind.Deliberate, RestartType.ThrowIn), 5f, 0f, Scene(17f));
            logic.RegisterTouch(new BallTouch(A, 2), 0f, -3f, Scene(17f));
            var decision = logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));
            Assert.IsTrue(decision.IsOffence, "Exemption only covers the restart itself");
        }

        // === Interfering with an opponent ===

        [Test]
        public void RegisterChallenge_FlaggedPlayerNearBall_IsOffence()
        {
            var logic = CreateLogic(interferenceRadius: 2f);
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            var decision = logic.RegisterChallenge(A, 1, 16f, 0f, 15f, 0f);
            Assert.IsTrue(decision.IsOffence);
            Assert.AreEqual(OffsideReason.ChallengingOpponent, decision.Reason);
            Assert.AreEqual(16f, decision.FreeKickX, 1e-4f);
        }

        [Test]
        public void RegisterChallenge_FlaggedPlayerFarFromBall_NoOffence()
        {
            var logic = CreateLogic(interferenceRadius: 2f);
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            Assert.IsFalse(logic.RegisterChallenge(A, 1, 20f, 0f, 15f, 0f).IsOffence);
            Assert.IsTrue(logic.IsFlagged(A, 1), "Phase remains live");
        }

        [Test]
        public void RegisterChallenge_OnsidePlayer_NoOffence()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f, a2X: 10f));
            Assert.IsFalse(logic.RegisterChallenge(A, 2, 15f, 0f, 15f, 0f).IsOffence);
        }

        [Test]
        public void RegisterChallenge_ZeroRadius_RuleDisabled()
        {
            var logic = CreateLogic(interferenceRadius: 0f);
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            Assert.IsFalse(logic.RegisterChallenge(A, 1, 15f, 0f, 15f, 0f).IsOffence);
        }

        // === Enable / events / reset ===

        [Test]
        public void Constructor_SmallSidedDisabled_IsEnabledFalse()
        {
            var config = new OffsideConfigData(enabledFullSided: true, enabledSmallSided: false);
            Assert.IsFalse(new OffsideLogic(config, playersPerSide: 5).IsEnabled);
            Assert.IsTrue(new OffsideLogic(config, playersPerSide: 11).IsEnabled);
        }

        [Test]
        public void Constructor_SmallSidedEnabled_IsEnabledTrue()
        {
            var config = new OffsideConfigData(enabledFullSided: true, enabledSmallSided: true);
            Assert.IsTrue(new OffsideLogic(config, playersPerSide: 4).IsEnabled);
        }

        [Test]
        public void RegisterTouch_Disabled_NeverOffence()
        {
            var logic = CreateLogic();
            logic.SetEnabled(false);
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            Assert.IsFalse(logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f)).IsOffence);
        }

        [Test]
        public void RegisterTouch_Offence_FiresEventAndClearsPhases()
        {
            var logic = CreateLogic();
            OffsideDecision fired = null;
            logic.OnOffence += d => fired = d;

            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f));

            Assert.IsNotNull(fired);
            Assert.IsTrue(fired.IsOffence);
            Assert.IsNull(logic.GetPhase(A));
        }

        [Test]
        public void Reset_ClearsLivePhase()
        {
            var logic = CreateLogic();
            logic.RegisterTouch(new BallTouch(A, 0), 5f, 0f, Scene(17f));
            logic.Reset();
            Assert.IsFalse(logic.RegisterTouch(new BallTouch(A, 1), 17f, 3f, Scene(17f)).IsOffence);
        }

        [Test]
        public void OffsideDecision_None_IsNotOffence()
        {
            Assert.IsFalse(OffsideDecision.None.IsOffence);
            Assert.AreEqual(OffsideReason.None, OffsideDecision.None.Reason);
        }

        [TestCase(RestartType.GoalKick, true)]
        [TestCase(RestartType.ThrowIn, true)]
        [TestCase(RestartType.Corner, true)]
        [TestCase(RestartType.DirectFreeKick, false)]
        [TestCase(RestartType.IndirectFreeKick, false)]
        [TestCase(RestartType.Penalty, false)]
        [TestCase(RestartType.DropBall, false)]
        public void IsExemptRestart_MatchesLaw11(RestartType restart, bool expected)
        {
            Assert.AreEqual(expected, OffsideLogic.IsExemptRestart(restart));
        }
    }
}
