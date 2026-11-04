using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Formations, team styles, the AI manager's mentality and how they reach the shape and the brain.</summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAITacticsStyleTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        // ------------------------------------------------------------------ formations

        [Test]
        public void EveryFormation_HasKeeperFirst_TenOutfielders_AndIsMirrorSymmetric()
        {
            foreach (var f in TeamAIFormations.All)
            {
                Assert.AreEqual(TeamAIRole.GK, TeamAIFormations.RoleOf(f, 0), f.ToString());
                float lateralSum = 0f;
                for (int s = 1; s < TeamAIFormations.SlotCount; s++)
                {
                    var slot = TeamAIFormations.GetSlot(f, s);
                    Assert.AreNotEqual(TeamAIRole.GK, slot.Role, f + " slot " + s);
                    Assert.That(slot.Lateral, Is.InRange(-1f, 1f));
                    Assert.That(slot.BlockDepth, Is.InRange(0f, 1f));
                    lateralSum += slot.Lateral;
                }
                Assert.AreEqual(0f, lateralSum, 1e-4f, f + " is lopsided");
            }
        }

        [Test]
        public void EveryFormation_DefenderCount_MatchesItsName()
        {
            foreach (var f in TeamAIFormations.All)
            {
                int defenders = 0;
                for (int s = 1; s < TeamAIFormations.SlotCount; s++)
                {
                    var r = TeamAIFormations.RoleOf(f, s);
                    if (r == TeamAIRole.CB || (r == TeamAIRole.FB && TeamAIFormations.GetSlot(f, s).BlockDepth < 0.2f)) defenders++;
                }
                int named = TeamAIFormations.Name(f)[0] - '0';
                Assert.AreEqual(named, defenders, TeamAIFormations.Name(f));
            }
        }

        [Test]
        public void FormationNames_RoundTripThroughTheRosterParser()
        {
            foreach (var f in TeamAIFormations.All)
                Assert.AreEqual(f, RosterMatchBuilder.ParseFormation(TeamAIFormations.Name(f)));
            Assert.AreEqual(TeamAIFormation.F4141, RosterMatchBuilder.ParseFormation("4-5-1"));
            Assert.AreEqual(TeamAIFormation.F532, RosterMatchBuilder.ParseFormation("5-4-1"));
        }

        [Test]
        public void KickOffShapeOfEachNewFormation_FitsItsOwnTemplateBest()
        {
            var spots = new SimVec3[TeamAIFormations.SlotCount];
            foreach (var f in TeamAIFormations.All)
            {
                for (int s = 0; s < spots.Length; s++)
                {
                    var slot = TeamAIFormations.GetSlot(f, s);
                    spots[s] = new SimVec3(slot.Lateral * 25f, 0f, -45f + slot.BlockDepth * 35f);
                }
                Assert.AreEqual(f, SimFormationMapper.BestFit(spots), TeamAIFormations.Name(f));
            }
        }

        // ------------------------------------------------------------------ styles

        [Test]
        public void DefaultTactics_AreNeutral()
        {
            var t = new TeamAITactics();
            Assert.AreEqual(1f, TeamAIOnBall.DirectProgress(t.Directness), 1e-6f);
            Assert.AreEqual(1f, TeamAIOnBall.DirectRisk(t.Directness), 1e-6f);
            Assert.AreEqual(2, t.MaxRunners);
            Assert.AreEqual(0f, t.CounterBoost);
            Assert.AreEqual(TeamAIMentality.Balanced, t.Mentality);
            var b = TeamAITactics.FromStyle(TeamAIStyle.Balanced);
            Assert.AreEqual(t.Depth, b.Depth);
            Assert.AreEqual(t.PressingIntensity, b.PressingIntensity);
            Assert.AreEqual(t.Directness, b.Directness);
        }

        [Test]
        public void Styles_DifferWhereTheyShould()
        {
            var bus = TeamAITactics.FromStyle(TeamAIStyle.ParkTheBus);
            var press = TeamAITactics.FromStyle(TeamAIStyle.HighPress);
            var poss = TeamAITactics.FromStyle(TeamAIStyle.Possession);
            var longBall = TeamAITactics.FromStyle(TeamAIStyle.LongBall);
            var counter = TeamAITactics.FromStyle(TeamAIStyle.CounterAttack);
            var wing = TeamAITactics.FromStyle(TeamAIStyle.WingPlay);
            Assert.Less(bus.Depth, 0.5f);
            Assert.Greater(press.Depth, 0.5f);
            Assert.Greater(press.PressingIntensity, poss.PressingIntensity);
            Assert.Less(bus.PressingIntensity, 0.3f);
            Assert.Greater(longBall.Directness, 0.9f);
            Assert.Less(poss.Directness, 0.3f);
            Assert.AreEqual(1f, counter.CounterBoost, 1e-6f);
            Assert.AreEqual(1, bus.MaxRunners);
            Assert.AreEqual(3, wing.MaxRunners);
            Assert.Greater(wing.Width, 0.9f);
        }

        [Test]
        public void StyleNames_Parse()
        {
            Assert.IsTrue(TeamAITactics.TryParseStyle("Park The Bus", out var a));
            Assert.AreEqual(TeamAIStyle.ParkTheBus, a);
            Assert.IsTrue(TeamAITactics.TryParseStyle("high-press", out var b));
            Assert.AreEqual(TeamAIStyle.HighPress, b);
            Assert.IsTrue(TeamAITactics.TryParseStyle("gegenpress", out var c));
            Assert.AreEqual(TeamAIStyle.HighPress, c);
            Assert.IsTrue(TeamAITactics.TryParseStyle("long_ball", out var d));
            Assert.AreEqual(TeamAIStyle.LongBall, d);
            Assert.IsFalse(TeamAITactics.TryParseStyle("catenaccio-plus", out _));
            Assert.IsFalse(TeamAITactics.TryParseStyle(null, out _));
        }

        [Test]
        public void Directness_TradesRiskForProgress()
        {
            Assert.Greater(TeamAIOnBall.DirectProgress(1f), TeamAIOnBall.DirectProgress(0f));
            Assert.Less(TeamAIOnBall.DirectRisk(1f), TeamAIOnBall.DirectRisk(0f));
        }

        // ------------------------------------------------------------------ manager

        [Test]
        public void Manager_LevelEarly_StaysBalanced()
        {
            Assert.AreEqual(TeamAIMentality.Balanced, TeamAIManager.Choose(0, 0.2f, 0));
            Assert.AreEqual(TeamAIMentality.Balanced, TeamAIManager.Choose(0, 0.95f, 0));
        }

        [Test]
        public void Manager_ChasingLate_GoesAllOut_AndProtectingANarrowLead_ParksTheBus()
        {
            Assert.AreEqual(TeamAIMentality.Attacking, TeamAIManager.Choose(-1, 0.6f, 0));
            Assert.AreEqual(TeamAIMentality.AllOutAttack, TeamAIManager.Choose(-1, 0.9f, 0));
            Assert.AreEqual(TeamAIMentality.AllOutAttack, TeamAIManager.Choose(-2, 0.65f, 0));
            Assert.AreEqual(TeamAIMentality.Defensive, TeamAIManager.Choose(1, 0.7f, 0));
            Assert.AreEqual(TeamAIMentality.UltraDefensive, TeamAIManager.Choose(1, 0.9f, 0));
            Assert.AreEqual(TeamAIMentality.Defensive, TeamAIManager.Choose(3, 0.8f, 0));
        }

        [Test]
        public void Manager_ManDown_DropsOneStep_ManUpWhenNotWinning_AddsOne()
        {
            Assert.AreEqual(TeamAIMentality.Defensive, TeamAIManager.Choose(0, 0.3f, -1));
            Assert.AreEqual(TeamAIMentality.Attacking, TeamAIManager.Choose(0, 0.3f, 1));
            Assert.AreEqual(TeamAIMentality.Balanced, TeamAIManager.Choose(1, 0.3f, 1));
        }

        [Test]
        public void Manager_ChangesAtAStoppage_OrAfterTheDelayInOpenPlay()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var m = new TeamAIManager { Enabled = true };
            w.Score[1] = 1;
            w.MatchProgress = 0.9f;
            w.MatchPhase = TeamAIMatchPhase.InPlay;
            w.Time = 100f;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.AllOutAttack, m.Wanted);
            Assert.AreEqual(TeamAIMentality.Balanced, m.Current, "no change mid-move");
            w.Time = 100f + TeamAIManager.ChangeDelay - 1f;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Balanced, m.Current);
            w.Time = 100f + TeamAIManager.ChangeDelay + 0.1f;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.AllOutAttack, m.Current);

            // A goal back at a stoppage: the change goes in at once.
            w.Score[0] = 1;
            w.MatchProgress = 0.92f;
            w.MatchPhase = TeamAIMatchPhase.Kickoff;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Balanced, m.Current);
            Assert.AreEqual(2, m.Changes);
        }

        [Test]
        public void Manager_Disabled_NeverChanges()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var m = new TeamAIManager();
            w.Score[1] = 3;
            w.MatchProgress = 0.95f;
            w.MatchPhase = TeamAIMatchPhase.Kickoff;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Balanced, m.Current);
        }

        [Test]
        public void Apply_ShiftsLineAndPress_AddsBaseMentality_AndClamps()
        {
            var baseT = new TeamAITactics();
            var outT = new TeamAITactics();
            TeamAIManager.Apply(baseT, TeamAIMentality.Attacking, outT);
            Assert.Greater(outT.Depth, baseT.Depth);
            Assert.Greater(outT.PressingIntensity, baseT.PressingIntensity);
            Assert.Greater(outT.ForwardRuns, baseT.ForwardRuns);
            Assert.AreEqual(0.5f, baseT.Depth, "base untouched");

            TeamAIManager.Apply(baseT, TeamAIMentality.UltraDefensive, outT);
            Assert.Less(outT.Depth, baseT.Depth);
            Assert.AreEqual(1, outT.MaxRunners);

            var bus = TeamAITactics.FromStyle(TeamAIStyle.ParkTheBus);
            bus.Mentality = TeamAIMentality.Defensive;
            TeamAIManager.Apply(bus, TeamAIMentality.UltraDefensive, outT);
            Assert.AreEqual(TeamAIMentality.UltraDefensive, outT.Mentality, "clamped at -2");
            Assert.That(outT.Depth, Is.InRange(0f, 1f));
        }

        // ------------------------------------------------------------------ shape

        private static int CountNearLine(SimVec3[] anchors, float lineZ, float tol)
        {
            int n = 0;
            for (int s = 1; s < anchors.Length; s++) if (Math.Abs(anchors[s].Z - lineZ) < tol) n++;
            return n;
        }

        [Test]
        public void BackFive_InALowBlock_WingBacksJoinTheLine()
        {
            var t = new TeamAITactics { Formation = TeamAIFormation.F532 };
            var anchors = new SimVec3[TeamAIFormations.SlotCount];
            var ball = new SimVec3(0f, 0f, -10f);
            float line = TeamAIFormationShape.ComputeAnchors(TeamAIPhase.LowBlock, t, ball, 1, Pitch, anchors);
            float lineZ = TeamAIMath.DepthToZ(line, 1, Pitch);
            Assert.AreEqual(5, CountNearLine(anchors, lineZ, 1.5f));

            // In possession the wing-backs push on.
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.Progression, t, ball, 1, Pitch, anchors);
            float fbDepth = TeamAIMath.Depth(anchors[4].Z, 1, Pitch);
            float cbDepth = TeamAIMath.Depth(anchors[2].Z, 1, Pitch);
            Assert.Greater(fbDepth - cbDepth, 0.15f);
        }

        [Test]
        public void FourThreeThree_DefendsAsFourFiveOne_WingersTrackBack()
        {
            var t = new TeamAITactics { Formation = TeamAIFormation.F433 };
            var anchors = new SimVec3[TeamAIFormations.SlotCount];
            var ball = new SimVec3(0f, 0f, -5f);
            float line = TeamAIFormationShape.ComputeAnchors(TeamAIPhase.LowBlock, t, ball, 1, Pitch, anchors);
            float wingerDepth = TeamAIMath.Depth(anchors[8].Z, 1, Pitch);
            float cmDepth = TeamAIMath.Depth(anchors[6].Z, 1, Pitch);
            float stDepth = TeamAIMath.Depth(anchors[10].Z, 1, Pitch);
            Assert.AreEqual(cmDepth, wingerDepth, 0.02f, "winger level with the midfield");
            Assert.Greater(stDepth, wingerDepth + 0.05f, "the striker stays up");

            // A high press keeps the front three high.
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.HighPress, t, ball, 1, Pitch, anchors);
            Assert.AreEqual(TeamAIMath.Depth(anchors[10].Z, 1, Pitch), TeamAIMath.Depth(anchors[8].Z, 1, Pitch), 0.04f);
            Assert.Greater(line, 0f);
        }

        [Test]
        public void PossessionBuildUp_HolderDropsBetweenCentreBacks()
        {
            var poss = TeamAITactics.FromStyle(TeamAIStyle.Possession, TeamAIFormation.F433);
            var bal = new TeamAITactics { Formation = TeamAIFormation.F433 };
            var a = new SimVec3[TeamAIFormations.SlotCount];
            var b = new SimVec3[TeamAIFormations.SlotCount];
            var ball = new SimVec3(0f, 0f, -35f);
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.BuildUp, poss, ball, 1, Pitch, a);
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.BuildUp, bal, ball, 1, Pitch, b);
            // Slot 5 is the 4-3-3's holding midfielder; slots 1-2 the centre-backs.
            Assert.AreEqual(a[1].Z, a[5].Z, 1.5f, "holder on the centre-backs' line");
            Assert.Less(a[1].X, b[1].X, "left centre-back splits wider");
            Assert.Greater(b[5].Z - b[1].Z, 5f, "balanced side keeps the holder in midfield");
        }

        // ------------------------------------------------------------------ brain

        [Test]
        public void Brain_PlaysTheManagersMentality()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 0.6f, 0.6f, 3);
            dir.Brains[0].Manager.Enabled = true;
            w.Score[1] = 1;
            w.MatchProgress = 0.9f;
            w.MatchPhase = TeamAIMatchPhase.Kickoff;
            w.RestartTeam = 0;
            dir.DecideNow(w, 0.1f);
            Assert.AreEqual(TeamAIMentality.AllOutAttack, dir.Brains[0].Effective.Mentality);
            Assert.Greater(dir.Brains[0].Effective.Depth, dir.Brains[0].Tactics.Depth);
            Assert.AreEqual(TeamAIMentality.Balanced, dir.Brains[1].Effective.Mentality, "manager off for team 1");
        }

        private static bool CounterAfterRegain(TeamAITactics home)
        {
            var w = TeamAITestWorlds.FullMatch(home, new TeamAITactics());
            var dir = new TeamAIDirector(Pitch, home, new TeamAITactics(), 0.7f, 0.7f, 5);
            // Team 1 has it in our half...
            TeamAITestWorlds.GiveBall(w, 18);
            dir.DecideNow(w, 0.1f);
            // ...and our midfielder wins it.
            w.Players[6].Position = TeamAIMath.Flat(w.Ball.Position) + new SimVec3(0f, 0f, -0.8f);
            TeamAITestWorlds.GiveBall(w, 6);
            w.Time += 0.1f;
            dir.DecideNow(w, 0.1f);
            return dir.Brains[0].CounterActive;
        }

        [Test]
        public void CounterAttackingSide_BreaksAfterWinningTheBall_BalancedDoesNot()
        {
            Assert.IsTrue(CounterAfterRegain(TeamAITactics.FromStyle(TeamAIStyle.CounterAttack)));
            Assert.IsFalse(CounterAfterRegain(new TeamAITactics()));
        }

        [Test]
        public void CounterAttack_RaisesOnBallDirectness()
        {
            var home = TeamAITactics.FromStyle(TeamAIStyle.CounterAttack);
            var w = TeamAITestWorlds.FullMatch(home, new TeamAITactics());
            var dir = new TeamAIDirector(Pitch, home, new TeamAITactics(), 0.7f, 0.7f, 5);
            TeamAITestWorlds.GiveBall(w, 18);
            dir.DecideNow(w, 0.1f);
            TeamAITestWorlds.GiveBall(w, 6);
            w.Time += 0.1f;
            dir.DecideNow(w, 0.1f);
            Assert.Greater(dir.Brains[0].OnBall.Directness, home.Directness);
            // After the window the side settles into its normal game.
            w.Time += TeamAIBrain.CounterWindow + 1f;
            dir.DecideNow(w, 0.1f);
            Assert.IsFalse(dir.Brains[0].CounterActive);
            Assert.AreEqual(home.Directness, dir.Brains[0].OnBall.Directness, 1e-6f);
        }

        // ------------------------------------------------------------------ rosters

        [Test]
        public void RosterStyle_PicksTheStylesTactics_OtherwiseReputationSliders()
        {
            var players = new RosterPlayer[0];
            var counter = new RosterClub("c", "C", "C", "#000000", "#ffffff", "#ff0000", "S", 10000, "5-3-2", 3, players, "counter");
            var plain = new RosterClub("p", "P", "P", "#000000", "#ffffff", "#ff0000", "S", 10000, "4-3-3", 5, players);
            var tc = RosterMatchBuilder.TacticsFor(counter, TeamAIFormation.F532);
            Assert.AreEqual(TeamAIStyle.CounterAttack, tc.Style);
            Assert.AreEqual(TeamAIFormation.F532, tc.Formation);
            var tp = RosterMatchBuilder.TacticsFor(plain, TeamAIFormation.F433);
            Assert.AreEqual(TeamAIStyle.Balanced, tp.Style);
            Assert.AreEqual(0.85f, tp.PressingIntensity, 1e-5f);
        }
    }
}
