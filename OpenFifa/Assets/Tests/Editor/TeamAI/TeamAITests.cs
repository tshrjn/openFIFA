using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAITests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        private static float CentroidX(SimVec3[] a)
        {
            float s = 0f;
            for (int i = 1; i < a.Length; i++) s += a[i].X;
            return s / (a.Length - 1);
        }

        private static float CentroidZ(SimVec3[] a)
        {
            float s = 0f;
            for (int i = 1; i < a.Length; i++) s += a[i].Z;
            return s / (a.Length - 1);
        }

        private static float Spread(SimVec3[] a)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 1; i < a.Length; i++) { lo = Math.Min(lo, a[i].X); hi = Math.Max(hi, a[i].X); }
            return hi - lo;
        }

        // ------------------------------------------------------------ formation

        [Test]
        public void ComputeAnchors_BallMovesToRightTouchline_BlockShiftsTowardBall()
        {
            var tac = new TeamAITactics();
            var left = new SimVec3[11];
            var right = new SimVec3[11];
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, new SimVec3(-25f, 0f, 0f), 1, Pitch, left);
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, new SimVec3(25f, 0f, 0f), 1, Pitch, right);
            Assert.Greater(CentroidX(right) - CentroidX(left), 15f, "block should slide at least 15 m with the ball");
        }

        [Test]
        public void ComputeAnchors_BallMovesUpfield_BlockAdvances()
        {
            var tac = new TeamAITactics();
            foreach (TeamAIPhase phase in new[] { TeamAIPhase.Progression, TeamAIPhase.MidBlock })
            {
                var deep = new SimVec3[11];
                var high = new SimVec3[11];
                TeamAIFormationShape.ComputeAnchors(phase, tac, new SimVec3(0f, 0f, -20f), 1, Pitch, deep);
                TeamAIFormationShape.ComputeAnchors(phase, tac, new SimVec3(0f, 0f, 20f), 1, Pitch, high);
                Assert.Greater(CentroidZ(high), CentroidZ(deep) + 5f, phase.ToString());
            }
        }

        [Test]
        public void ComputeAnchors_OutOfPossession_NarrowerAndMoreCompactThanInPossession()
        {
            var tac = new TeamAITactics();
            var inPos = new SimVec3[11];
            var outPos = new SimVec3[11];
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.Progression, tac, SimVec3.Zero, 1, Pitch, inPos);
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, SimVec3.Zero, 1, Pitch, outPos);
            Assert.Less(Spread(outPos), Spread(inPos));
        }

        [Test]
        public void ComputeAnchors_AwayTeam_IsMirrorOfHomeTeam()
        {
            var tac = new TeamAITactics(TeamAIFormation.F4231, 0.5f, 0.5f, 0.5f);
            var home = new SimVec3[11];
            var away = new SimVec3[11];
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, new SimVec3(10f, 0f, 5f), 1, Pitch, home);
            TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, new SimVec3(-10f, 0f, -5f), -1, Pitch, away);
            for (int i = 0; i < 11; i++)
            {
                Assert.AreEqual(home[i].X, -away[i].X, 1e-3f);
                Assert.AreEqual(home[i].Z, -away[i].Z, 1e-3f);
            }
        }

        [Test]
        public void ComputeAnchors_DepthSlider_RaisesTheLine()
        {
            float low = TeamAIFormationShape.BackLineDepth(TeamAIPhase.MidBlock, new TeamAITactics(TeamAIFormation.F433, 0.5f, 0f, 0.5f), 0.6f);
            float high = TeamAIFormationShape.BackLineDepth(TeamAIPhase.MidBlock, new TeamAITactics(TeamAIFormation.F433, 0.5f, 1f, 0.5f), 0.6f);
            Assert.Greater(high, low);
        }

        [Test]
        public void Formations_AllPresets_HaveElevenSlotsWithGoalkeeperFirst()
        {
            foreach (TeamAIFormation f in Enum.GetValues(typeof(TeamAIFormation)))
            {
                Assert.AreEqual(TeamAIRole.GK, TeamAIFormations.RoleOf(f, 0));
                int defenders = 0;
                for (int s = 1; s < 11; s++)
                {
                    Assert.AreNotEqual(TeamAIRole.GK, TeamAIFormations.RoleOf(f, s));
                    if (TeamAIFormations.RoleOf(f, s) == TeamAIRole.CB) defenders++;
                }
                Assert.GreaterOrEqual(defenders, 2, f.ToString());
            }
        }

        // ------------------------------------------------------------ phase machine

        [Test]
        public void PhaseMachine_LosePossessionWithPressingTeam_CounterPressesThenSettles()
        {
            var tac = new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.8f);
            var m = new TeamAIPhaseMachine();
            m.Update(0.1f, tac, true, 0.6f, 2f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.Progression, m.Phase);
            m.Update(0.1f, tac, false, 0.6f, 3f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.CounterPress, m.Phase);
            for (int i = 0; i < 80; i++) m.Update(0.1f, tac, false, 0.6f, 3f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreNotEqual(TeamAIPhase.CounterPress, m.Phase);
            Assert.IsFalse(TeamAIFormationShape.InPossession(m.Phase));
        }

        [Test]
        public void PhaseMachine_PassiveTeamLosesBall_Recovers()
        {
            var tac = new TeamAITactics(TeamAIFormation.F442, 0.5f, 0.5f, 0.1f);
            var m = new TeamAIPhaseMachine();
            m.Update(0.1f, tac, true, 0.5f, 2f, TeamAIMatchPhase.InPlay, -1, 0);
            m.Update(0.1f, tac, false, 0.5f, 2f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.Recovery, m.Phase);
        }

        [Test]
        public void PhaseMachine_InPossession_PhaseFollowsBallDepth()
        {
            var tac = new TeamAITactics();
            var m = new TeamAIPhaseMachine();
            m.Update(0.1f, tac, true, 0.2f, 1f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.BuildUp, m.Phase);
            m.Update(0.1f, tac, true, 0.5f, 1f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.Progression, m.Phase);
            m.Update(0.1f, tac, true, 0.8f, 1f, TeamAIMatchPhase.InPlay, -1, 0);
            Assert.AreEqual(TeamAIPhase.FinalThird, m.Phase);
        }

        [Test]
        public void PhaseMachine_SettledBlock_DependsOnBallDepthAndPressing()
        {
            var presser = new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.9f);
            var passive = new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, 0.1f);
            Assert.AreEqual(TeamAIPhase.HighPress, TeamAIPhaseMachine.SettledBlock(presser, 0.8f, TeamAIPhase.MidBlock, true));
            Assert.AreEqual(TeamAIPhase.MidBlock, TeamAIPhaseMachine.SettledBlock(passive, 0.8f, TeamAIPhase.MidBlock, true));
            Assert.AreEqual(TeamAIPhase.LowBlock, TeamAIPhaseMachine.SettledBlock(presser, 0.15f, TeamAIPhase.MidBlock, true));
        }

        [Test]
        public void PhaseMachine_RestartForUs_IsSetPieceFor()
        {
            var m = new TeamAIPhaseMachine();
            m.Update(0.1f, new TeamAITactics(), false, 0.5f, 5f, TeamAIMatchPhase.Corner, 1, 1);
            Assert.AreEqual(TeamAIPhase.SetPieceFor, m.Phase);
            m.Update(0.1f, new TeamAITactics(), false, 0.5f, 5f, TeamAIMatchPhase.Corner, 1, 0);
            Assert.AreEqual(TeamAIPhase.SetPieceAgainst, m.Phase);
        }

        // ------------------------------------------------------------ pitch control

        [Test]
        public void PitchControl_SlicedUpdates_MatchFullRebuild()
        {
            var world = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var full = new TeamAIPitchControl(Pitch);
            full.RebuildAll(world);
            var sliced = new TeamAIPitchControl(Pitch);
            for (int i = 0; i < 3; i++) sliced.UpdateSlice(world, 7);
            Assert.AreEqual(1, sliced.Sweeps);
            for (int r = 0; r < full.Rows; r++)
                for (int c = 0; c < full.Cols; c++)
                    Assert.AreEqual(full.CellValue(c, r), sliced.CellValue(c, r), 1e-6f);
        }

        [Test]
        public void PitchControl_NearOwnGoal_ControlledByDefendingTeam()
        {
            var world = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var pc = new TeamAIPitchControl(Pitch);
            pc.RebuildAll(world);
            Assert.Greater(pc.ControlAt(0, new SimVec3(0f, 0f, -45f)), 0.8f);
            Assert.Greater(pc.ControlAt(1, new SimVec3(0f, 0f, 45f)), 0.8f);
            Assert.AreEqual(32 * 21, pc.Cols * pc.Rows);
        }

        // ------------------------------------------------------------ on-ball

        private static TeamAIWorld PassScenario(out int owner, out int open, out int marked)
        {
            var w = TeamAITestWorlds.Empty();
            owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, -5f);
            open = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 7, -16f, 2f);
            marked = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 5, 16f, 2f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 6, 16.8f, 3.2f);   // tight on the marked player
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 7, 8f, -1.5f);     // sitting in the right-hand lane
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 50f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -50f);
            TeamAITestWorlds.GiveBall(w, owner);
            return w;
        }

        [Test]
        public void OnBall_OneTeammateMarked_PassesToOpenTeammate()
        {
            var w = PassScenario(out int owner, out int open, out _);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            var ctx = new TeamAIOnBallContext { OffsideDepth = 0.9f };
            ob.Decide(w, owner, TeamAIPhase.Progression, new TeamAIDifficulty(1f), new TeamAIRandom(7), ctx, ref cmd);
            Assert.AreEqual(open, ob.LastBestPass.TargetIndex, "best pass should go to the open teammate");
            Assert.Greater(ob.LastBestPass.Control, 0.7f);
        }

        [Test]
        public void OnBall_OpenLaneVsBlockedLane_RiskIsHigherForBlockedLane()
        {
            var w = PassScenario(out int owner, out int open, out int marked);
            var from = w.Ball.Position;
            float tOpen = TeamAIOnBall.GroundPassTime(TeamAIMath.FlatDistance(from, w.Players[open].Position));
            float riskOpen = TeamAIOnBall.InterceptionRisk(w, 0, from, w.Players[open].Position, tOpen, false);
            float riskMarked = TeamAIOnBall.InterceptionRisk(w, 0, from, w.Players[marked].Position, tOpen, false);
            Assert.Less(riskOpen, riskMarked);
        }

        [Test]
        public void OnBall_CentralUnmarkedNearGoal_Shoots()
        {
            var w = TeamAITestWorlds.Empty();
            int owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 0f, 41f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 9, 25f, 20f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0.5f, 51.5f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 1, 15f, 35f);
            TeamAITestWorlds.GiveBall(w, owner);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            var ctx = new TeamAIOnBallContext { OffsideDepth = 0.98f };
            ob.Decide(w, owner, TeamAIPhase.FinalThird, new TeamAIDifficulty(1f), new TeamAIRandom(3), ctx, ref cmd);
            Assert.AreEqual(TeamAIIntent.Shoot, cmd.Intent);
            Assert.Greater(cmd.Aim.Z, 0.9f);
        }

        [Test]
        public void OnBall_DeepAndPressedWithNoOptions_Clears()
        {
            var w = TeamAITestWorlds.Empty();
            int owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 1, 0f, -42f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 10, 0.5f, -43.5f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 9, -1.5f, -40f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.ST, 8, 1.5f, -40f);
            TeamAITestWorlds.GiveBall(w, owner, 0.5f);
            var ob = new TeamAIOnBall();
            var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
            ob.Decide(w, owner, TeamAIPhase.BuildUp, new TeamAIDifficulty(1f), new TeamAIRandom(1),
                new TeamAIOnBallContext { OffsideDepth = 0.5f }, ref cmd);
            Assert.AreEqual(TeamAIIntent.Clear, cmd.Intent);
            Assert.AreEqual(TeamAIAction.LobPass, cmd.Action);
        }

        [Test]
        public void ExpectedGoals_CloserAndMoreCentral_IsHigher()
        {
            float close = TeamAIOnBall.ExpectedGoals(new SimVec3(0f, 0f, 44f), 1, Pitch);
            float far = TeamAIOnBall.ExpectedGoals(new SimVec3(0f, 0f, 25f), 1, Pitch);
            float wide = TeamAIOnBall.ExpectedGoals(new SimVec3(18f, 0f, 44f), 1, Pitch);
            Assert.Greater(close, far);
            Assert.Greater(close, wide);
        }

        // ------------------------------------------------------------ defending

        private static TeamAIWorld PressScenario(bool heavyTouch, out int presser)
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            // Away central midfielder has the ball just inside the away half; home team defends.
            int owner = -1;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 1 && w.Players[i].Role == TeamAIRole.DM) owner = i;
            w.Players[owner].Position = new SimVec3(0f, 0f, 6f);
            w.Players[owner].Facing = new SimVec3(0f, 0f, -1f);
            TeamAITestWorlds.GiveBall(w, owner);
            if (heavyTouch)
                w.Ball.Position = new SimVec3(0f, 0.11f, 6f - 3f); // knocked 3 m ahead
            presser = -1;
            return w;
        }

        private static TeamAIDirector RunTicks(TeamAIWorld w, int ticks, float pressing = 0.5f)
        {
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(TeamAIFormation.F433, 0.5f, 0.5f, pressing),
                new TeamAITactics(), 1f, 1f, 42);
            for (int i = 0; i < ticks; i++)
            {
                dir.DecideNow(w, 0.1f);
                w.Time += 0.1f;
            }
            return dir;
        }

        [Test]
        public void Brain_HeavyTouch_TriggersPress()
        {
            var w = PressScenario(true, out _);
            var dir = RunTicks(w, 3);
            var brain = dir.Brains[0];
            Assert.IsTrue(brain.PressTriggerActive);
            Assert.GreaterOrEqual(brain.PresserIndex, 0);
            var cmd = dir.Commands[brain.PresserIndex];
            Assert.AreEqual(TeamAIIntent.Press, cmd.Intent);
            Assert.IsTrue(cmd.Sprint);
            Assert.GreaterOrEqual(brain.SecondPresserIndex, 0, "a trigger should bring a second presser");
        }

        private static int CountIntent(TeamAIWorld w, TeamAIDirector dir, int team, TeamAIIntent intent)
        {
            int n = 0;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == team && dir.Commands[i].Intent == intent) n++;
            return n;
        }

        [Test]
        public void Brain_CleanTouchInMidBlock_OnlyZonePlayerEngages()
        {
            var clean = PressScenario(false, out _);
            var cleanDir = RunTicks(clean, 3);
            var heavy = PressScenario(true, out _);
            var heavyDir = RunTicks(heavy, 3);
            Assert.AreEqual(TeamAIPhase.MidBlock, cleanDir.Brains[0].Phase);
            Assert.IsFalse(cleanDir.Brains[0].PressTriggerActive);
            Assert.AreEqual(-1, cleanDir.Brains[0].SecondPresserIndex);
            Assert.LessOrEqual(CountIntent(clean, cleanDir, 0, TeamAIIntent.Press), 1);
            Assert.GreaterOrEqual(CountIntent(heavy, heavyDir, 0, TeamAIIntent.Press), 2);
        }

        [Test]
        public void Brain_HeavyTouch_LowDifficultyReactsLater()
        {
            var w = PressScenario(true, out _);
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 0f, 0f, 42);
            dir.DecideNow(w, 0.1f);
            w.Time += 0.1f;
            dir.DecideNow(w, 0.1f);
            Assert.IsTrue(dir.Brains[0].PressTriggerSeen);
            Assert.IsFalse(dir.Brains[0].PressTriggerActive, "theta 0 should not react within 0.1 s");
        }

        [Test]
        public void Brain_OutOfPossession_BackFourHoldOneLine()
        {
            var w = PressScenario(false, out _);
            var dir = RunTicks(w, 2);
            float? lineZ = null;
            int count = 0;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                var p = w.Players[i];
                if (p.Team != 0 || (p.Role != TeamAIRole.CB && p.Role != TeamAIRole.FB)) continue;
                var c = dir.Commands[i];
                Assert.AreEqual(TeamAIIntent.KeepLine, c.Intent);
                if (lineZ == null) lineZ = c.MoveTarget.Z;
                Assert.AreEqual(lineZ.Value, c.MoveTarget.Z, 0.01f);
                count++;
            }
            Assert.AreEqual(4, count);
        }

        [Test]
        public void Brain_BallPlayedUpfield_LineStepsUp()
        {
            var w = PressScenario(false, out _);
            var dir = RunTicks(w, 2);
            float before = dir.Brains[0].BackLineDepth;
            int owner = w.OwnerIndex;
            w.Players[owner].Position = new SimVec3(0f, 0f, 30f);
            TeamAITestWorlds.GiveBall(w, owner);
            for (int i = 0; i < 2; i++) { dir.DecideNow(w, 0.1f); w.Time += 0.1f; }
            Assert.Greater(dir.Brains[0].BackLineDepth, before);
        }

        [Test]
        public void Brain_AttackerBeyondLine_DefenderTracksGoalSide()
        {
            var w = PressScenario(false, out _);
            // Push the away striker in behind the home back line.
            int st = -1;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 1 && w.Players[i].Role == TeamAIRole.ST) st = i;
            w.Players[st].Position = new SimVec3(2f, 0f, -38f);
            var dir = RunTicks(w, 2);
            bool tracked = false;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                var c = dir.Commands[i];
                if (w.Players[i].Team == 0 && c.Intent == TeamAIIntent.Mark && c.TargetPlayerId == w.Players[st].Id)
                {
                    tracked = true;
                    Assert.Less(c.MoveTarget.Z, -38f, "marker stays goal-side");
                }
            }
            Assert.IsTrue(tracked);
        }

        // ------------------------------------------------------------ goalkeeper

        [Test]
        public void Goalkeeper_PositionTarget_StaysOnAngleBisector()
        {
            var balls = new[]
            {
                new SimVec3(0f, 0f, -30f), new SimVec3(-15f, 0f, -35f), new SimVec3(20f, 0f, -40f),
                new SimVec3(-8f, 0f, -45f), new SimVec3(25f, 0f, -20f)
            };
            foreach (var b in balls)
            {
                var p = TeamAIGoalkeeper.BisectorOnGoalLine(b, 1, Pitch);
                var gk = TeamAIGoalkeeper.PositionTarget(b, 1, Pitch, 0.3f);
                Assert.Less(TeamAIMath.DistanceToLine(p, b, gk), 0.05f, b.ToString());
                Assert.Greater(gk.Z, -Pitch.HalfLength, "keeper stays in front of the goal line");
                // Bisector property: equal angles to both posts.
                var lp = new SimVec3(-Pitch.GoalWidth / 2f, 0f, -Pitch.HalfLength);
                var rp = new SimVec3(Pitch.GoalWidth / 2f, 0f, -Pitch.HalfLength);
                var tb = TeamAIMath.FlatDirection(b, p);
                float aL = (float)Math.Acos(SimVec3.Dot(tb, TeamAIMath.FlatDirection(b, lp)));
                float aR = (float)Math.Acos(SimVec3.Dot(tb, TeamAIMath.FlatDirection(b, rp)));
                Assert.AreEqual(aL, aR, 0.01f, b.ToString());
            }
        }

        [Test]
        public void Goalkeeper_BallInShootingRange_StandsOneToFiveMetresOffLine()
        {
            foreach (float z in new[] { -30f, -36f, -42f })
            {
                var gk = TeamAIGoalkeeper.PositionTarget(new SimVec3(0f, 0f, z), 1, Pitch, 0.3f);
                float off = gk.Z + Pitch.HalfLength;
                Assert.Greater(off, 1f, z.ToString());
                Assert.Less(off, 5f, z.ToString());
            }
            // Point-blank: the keeper narrows the angle but stays between ball and goal.
            var close = TeamAIGoalkeeper.PositionTarget(new SimVec3(0f, 0f, -47f), 1, Pitch, 0.3f);
            Assert.Less(close.Z, -47f);
        }

        [Test]
        public void Goalkeeper_InMatch_CommandStaysOnBisector()
        {
            var w = PressScenario(false, out _);
            int owner = w.OwnerIndex;
            w.Players[owner].Position = new SimVec3(-14f, 0f, -28f);
            TeamAITestWorlds.GiveBall(w, owner);
            var dir = RunTicks(w, 2);
            int gk = -1;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 0 && w.Players[i].Role == TeamAIRole.GK) gk = i;
            var c = dir.Commands[gk];
            Assert.AreEqual(TeamAIIntent.GKPosition, c.Intent);
            var b = TeamAIMath.Flat(w.Ball.Position);
            var p = TeamAIGoalkeeper.BisectorOnGoalLine(b, 1, Pitch);
            Assert.Less(TeamAIMath.DistanceToLine(p, b, c.MoveTarget), 0.05f);
        }

        [Test]
        public void Goalkeeper_HighBallIntoBox_Claims()
        {
            var w = PressScenario(false, out _);
            w.Ball.OwnerId = -1;
            w.Ball.LastTouchTeam = 1;
            w.Ball.Position = new SimVec3(-25f, 3f, -42f);
            w.Ball.Velocity = new SimVec3(18f, 5f, -3f);
            int gk = -1;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 0 && w.Players[i].Role == TeamAIRole.GK) gk = i;
            var cmd = AIPlayerCommand.Idle(w.Players[gk].Id, w.Players[gk].Position);
            TeamAIGoalkeeper.Decide(w, gk, 0.2f, false, ref cmd);
            Assert.AreEqual(TeamAIIntent.GKClaim, cmd.Intent);
        }

        // ------------------------------------------------------------ difficulty, RNG, determinism

        [Test]
        public void Difficulty_HigherTheta_FasterAndLessNoisy()
        {
            var lo = new TeamAIDifficulty(0f);
            var hi = new TeamAIDifficulty(1f);
            Assert.Greater(lo.ReactionDelay, hi.ReactionDelay);
            Assert.Greater(lo.DecisionNoise, hi.DecisionNoise);
            Assert.Greater(lo.AimError, hi.AimError);
            Assert.Greater(lo.PowerError, hi.PowerError);
        }

        [Test]
        public void Random_SameSeed_SameSequence()
        {
            var a = new TeamAIRandom(123);
            var b = new TeamAIRandom(123);
            var c = new TeamAIRandom(124);
            bool differs = false;
            for (int i = 0; i < 100; i++)
            {
                float x = a.NextFloat();
                Assert.AreEqual(x, b.NextFloat());
                Assert.GreaterOrEqual(x, 0f);
                Assert.Less(x, 1f);
                if (x != c.NextFloat()) differs = true;
            }
            Assert.IsTrue(differs);
        }

        private static ulong RunMatchHash(ulong seed, float theta, int steps)
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics(TeamAIFormation.F442, 0.5f, 0.5f, 0.7f));
            TeamAITestWorlds.GiveBall(w, 5); // home DM kicks off the sim
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(),
                new TeamAITactics(TeamAIFormation.F442, 0.5f, 0.5f, 0.7f), theta, theta, seed);
            ulong h = 1469598103934665603UL;
            for (int s = 0; s < steps; s++)
            {
                dir.Tick(w, 1f / 60f);
                TeamAITestWorlds.Step(w, dir.Commands, 1f / 60f);
                if (s % 6 == 0)
                {
                    for (int i = 0; i < w.PlayerCount; i++)
                    {
                        var c = dir.Commands[i];
                        h = (h ^ (ulong)(long)(c.MoveTarget.X * 1000f)) * 1099511628211UL;
                        h = (h ^ (ulong)(long)(c.MoveTarget.Z * 1000f)) * 1099511628211UL;
                        h = (h ^ (ulong)c.Action) * 1099511628211UL;
                        h = (h ^ (ulong)(long)(c.Power * 1000f)) * 1099511628211UL;
                    }
                }
            }
            return h;
        }

        [Test]
        public void Director_SameSeed_IdenticalCommands()
        {
            Assert.AreEqual(RunMatchHash(99, 0.3f, 1200), RunMatchHash(99, 0.3f, 1200));
        }

        [Test]
        public void Director_DifferentSeedAtLowDifficulty_Diverges()
        {
            Assert.AreNotEqual(RunMatchHash(1, 0f, 1200), RunMatchHash(2, 0f, 1200));
        }

        [Test]
        public void Director_SixtyHertzSteps_DecidesAtTenHertz()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 0.5f, 0.5f, 5);
            for (int s = 0; s < 600; s++) dir.Tick(w, 1f / 60f);
            Assert.AreEqual(100, dir.DecisionTicks, 1);
        }

        [Test]
        public void Director_SteadyStateTick_AllocatesNothing()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            TeamAITestWorlds.GiveBall(w, 5);
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 0.5f, 0.5f, 5);
            for (int s = 0; s < 120; s++) { dir.Tick(w, 1f / 60f); TeamAITestWorlds.Step(w, dir.Commands, 1f / 60f); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int s = 0; s < 600; s++) { dir.Tick(w, 1f / 60f); TeamAITestWorlds.Step(w, dir.Commands, 1f / 60f); }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before);
        }
    }
}
