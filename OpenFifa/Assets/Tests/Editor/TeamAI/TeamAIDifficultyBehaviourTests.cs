using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Review item 7: the difficulty ladder (including OpponentReactionBonus) changes how the AI plays.</summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAIDifficultyBehaviourTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        [Test]
        public void Difficulty_ReactionBonus_ShortensReactionDelay()
        {
            var plain = new TeamAIDifficulty(0.9f);
            var sharp = new TeamAIDifficulty(0.9f, 0.02f);
            Assert.AreEqual(plain.ReactionDelay - 0.02f, sharp.ReactionDelay, 1e-5f);
            Assert.AreEqual(plain.AimError, sharp.AimError, 1e-6f, "the bonus only touches reactions");
            Assert.AreEqual(TeamAIDifficulty.MinReactionDelay, new TeamAIDifficulty(1f, 5f).ReactionDelay, 1e-6f);

            var ult = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Ultimate);
            Assert.AreEqual(new TeamAIDifficulty(1f).ReactionDelay - ult.OpponentReactionBonus, ult.OpponentDifficulty.ReactionDelay, 1e-5f);
            Assert.Greater(ult.OpponentReactionBonus, 0f);
            var beg = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Beginner);
            Assert.AreEqual(new TeamAIDifficulty(beg.OpponentTheta).ReactionDelay, beg.OpponentDifficulty.ReactionDelay, 1e-6f);
        }

        [Test]
        public void Director_ForSinglePlayer_OpponentGetsLadderDifficultyWithBonus()
        {
            var prof = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Legendary);
            var dir = TeamAIDirector.ForSinglePlayer(Pitch, new TeamAITactics(), new TeamAITactics(), 0, prof, 7);
            Assert.AreEqual(prof.OpponentReactionBonus, dir.Brains[1].Difficulty.ReactionBonus, 1e-6f);
            Assert.AreEqual(prof.OpponentTheta, dir.Brains[1].Difficulty.Theta, 1e-6f);
            Assert.AreEqual(prof.TeammateTheta, dir.Brains[0].Difficulty.Theta, 1e-6f);
            Assert.AreEqual(0f, dir.Brains[0].Difficulty.ReactionBonus, 1e-6f);
            var flipped = TeamAIDirector.ForSinglePlayer(Pitch, new TeamAITactics(), new TeamAITactics(), 1, prof, 7);
            Assert.AreEqual(prof.OpponentTheta, flipped.Brains[0].Difficulty.Theta, 1e-6f);
        }

        // ------------------------------------------------------------ pressing

        /// <summary>Away DM with a heavy touch in midfield; the home side (the AI under test) defends.</summary>
        private static TeamAIWorld HeavyTouch(ulong seed)
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            int owner = -1;
            for (int i = 0; i < w.PlayerCount; i++)
                if (w.Players[i].Team == 1 && w.Players[i].Role == TeamAIRole.DM) owner = i;
            // Vary the scenario per seed: where the carrier is and how far the touch ran.
            var r = new TeamAIRandom(seed);
            float x = r.Range(-8f, 8f), z = r.Range(3f, 9f);
            w.Players[owner].Position = new SimVec3(x, 0f, z);
            w.Players[owner].Facing = new SimVec3(0f, 0f, -1f);
            TeamAITestWorlds.GiveBall(w, owner);
            w.Ball.Position = new SimVec3(x, 0.11f, z - r.Range(2.2f, 3.5f));
            return w;
        }

        private static int CountPress(TeamAIWorld w, TeamAIDirector dir)
        {
            int n = 0;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (w.Players[i].Team != 0) continue;
                var it = dir.Commands[i].Intent;
                if ((it == TeamAIIntent.Press && dir.Commands[i].Sprint) || it == TeamAIIntent.Tackle) n++;
            }
            return n;
        }

        /// <summary>Seconds until the home side's press trigger fires and two men engage the ball (sprint press or tackle).</summary>
        private static float TimeToPress(TeamAIDifficulty home, ulong seed)
        {
            var w = HeavyTouch(seed);
            var dir = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), home, new TeamAIDifficulty(0.5f), seed);
            const float dt = 0.01f;
            for (int k = 0; k < 150; k++)
            {
                dir.DecideNow(w, dt);
                if (dir.Brains[0].PressTriggerActive && CountPress(w, dir) >= 2) return k * dt;
                w.Time += dt;
            }
            return 99f;
        }

        [Test]
        public void Ladder_UltimatePressesSoonerThanBeginner_OverSeededScenarios()
        {
            var ult = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Ultimate).OpponentDifficulty;
            var beg = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Beginner).OpponentDifficulty;
            float sumU = 0f, sumB = 0f;
            for (ulong s = 1; s <= 10; s++)
            {
                float tu = TimeToPress(ult, s), tb = TimeToPress(beg, s);
                Assert.Less(tu, 1.5f, $"seed {s}: ultimate never pressed");
                Assert.Less(tu, tb, $"seed {s}");
                sumU += tu;
                sumB += tb;
            }
            Assert.Greater(sumB / 10f - sumU / 10f, 0.3f, "beginner should be clearly slower to press");
        }

        [Test]
        public void Ladder_ReactionBonus_MakesLegendaryPressSoonerThanItsThetaAlone()
        {
            var prof = TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Legendary);
            float withBonus = TimeToPress(prof.OpponentDifficulty, 3);
            float withoutBonus = TimeToPress(new TeamAIDifficulty(prof.OpponentTheta), 3);
            Assert.Less(withBonus, withoutBonus);
        }

        // ------------------------------------------------------------ passing

        private static TeamAIWorld PassScenario(ulong seed, out int owner, out int open)
        {
            var r = new TeamAIRandom(seed * 7919UL);
            var w = TeamAITestWorlds.Empty();
            owner = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, r.Range(-4f, 4f), r.Range(-8f, -2f));
            open = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 7, r.Range(-20f, -12f), r.Range(0f, 6f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 5, 16f, 2f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 6, 16.8f, 3.2f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CM, 7, 8f, -1.5f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 50f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -50f);
            for (int i = 0; i < w.PlayerCount; i++) w.Players[i].Passing = 0.75f;
            TeamAITestWorlds.GiveBall(w, owner);
            return w;
        }

        private static void PassStats(TeamAIDifficulty diff, out float meanAimError, out float meanPowerError, out int toOpen)
        {
            float aim = 0f, pow = 0f;
            int n = 0;
            toOpen = 0;
            for (ulong s = 1; s <= 60; s++)
            {
                var w = PassScenario(s, out int owner, out int open);
                var ob = new TeamAIOnBall();
                var cmd = AIPlayerCommand.Idle(owner, w.Players[owner].Position);
                ob.Decide(w, owner, TeamAIPhase.Progression, diff, new TeamAIRandom(s), new TeamAIOnBallContext { OffsideDepth = 0.9f }, ref cmd);
                if (ob.LastChoice.TargetIndex == open) toOpen++;
                if (cmd.Action != TeamAIAction.Pass && cmd.Action != TeamAIAction.LobPass && cmd.Action != TeamAIAction.ThroughPass) continue;
                var ideal = TeamAIMath.FlatDirection(w.Ball.Position, cmd.ActionTarget);
                float cross = ideal.X * cmd.Aim.Z - ideal.Z * cmd.Aim.X;
                float dot = ideal.X * cmd.Aim.X + ideal.Z * cmd.Aim.Z;
                aim += Math.Abs((float)Math.Atan2(cross, dot));
                pow += Math.Abs(cmd.Power - TeamAIMath.Clamp01(ob.LastChoice.Power)) / Math.Max(ob.LastChoice.Power, 0.05f);
                n++;
            }
            Assert.Greater(n, 30, "most scenarios should produce a pass");
            meanAimError = aim / n;
            meanPowerError = pow / n;
        }

        [Test]
        public void Ladder_UltimatePassesMoreAccuratelyThanBeginner_OverSeededScenarios()
        {
            PassStats(TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Ultimate).OpponentDifficulty,
                out float aimU, out float powU, out int openU);
            PassStats(TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.Beginner).OpponentDifficulty,
                out float aimB, out float powB, out int openB);
            Assert.Less(aimU, aimB * 0.3f, $"aim error ultimate {aimU} vs beginner {aimB}");
            Assert.Less(powU, powB * 0.3f, $"power error ultimate {powU} vs beginner {powB}");
            Assert.GreaterOrEqual(openU, openB, "ultimate picks the open man at least as often");
        }
    }
}
