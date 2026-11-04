using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class SinglePlayerSessionTests
    {
        private static SinglePlayerMatchSession NewSession(TeamAIDifficultyLevel level, ulong seed, float half = 360f) =>
            new SinglePlayerMatchSession(new SinglePlayerMatchOptions
            {
                Level = level,
                Seed = seed,
                Config = new MatchSimConfig { HalfDurationSeconds = half },
            });

        private static SinglePlayerMatchResult PlayFull(SinglePlayerMatchSession s)
        {
            var bot = new ScriptedGamepadBot();
            int guard = 0;
            while (!s.IsFinished)
            {
                s.StepOnce(bot);
                if ((++guard & 63) == 0) AssertFinite(s);
                Assert.Less(guard, 200000, "match did not finish");
            }
            AssertFinite(s);
            return s.Result;
        }

        private static void AssertFinite(SinglePlayerMatchSession s)
        {
            var st = s.Sim.State;
            Assert.IsFalse(Bad(st.Ball.Position) || Bad(st.Ball.Velocity), "ball NaN at tick " + st.Tick);
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                Assert.IsFalse(Bad(st.Players[i].Position) || Bad(st.Players[i].Velocity), "player " + i + " NaN at tick " + st.Tick);
        }

        private static void Log(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (Exception) { }
            Console.WriteLine(line);
        }

        private static bool Bad(SimVec3 v) =>
            float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.X) || float.IsInfinity(v.Z);

        [Test]
        public void DifficultyProfile_EveryLevel_UsesLadderThetasAndOrderedAssists()
        {
            for (int i = 0; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var level = (TeamAIDifficultyLevel)i;
                var p = SinglePlayerDifficulty.For(level);
                var l = TeamAIDifficultyLadder.For(level);
                Assert.AreEqual(l.OpponentTheta, p.OpponentTheta);
                Assert.AreEqual(l.TeammateTheta, p.TeammateTheta);
                if (i > 0)
                {
                    var prev = SinglePlayerDifficulty.For((TeamAIDifficultyLevel)(i - 1));
                    Assert.GreaterOrEqual(p.OpponentKeeperReflexDelta, prev.OpponentKeeperReflexDelta);
                    Assert.LessOrEqual(p.PassAssist, prev.PassAssist);
                    Assert.LessOrEqual(p.ShotAssist, prev.ShotAssist);
                }
            }
            Assert.AreEqual("Semi-Pro", SinglePlayerDifficulty.For(TeamAIDifficultyLevel.SemiPro).DisplayName);
        }

        [Test]
        public void PrepareSetup_AppliesKeeperDeltas_WithoutTouchingCallerSetup()
        {
            var src = MatchSetup.CreateDefault(4);
            int before = src.Away[0].GkReflexes;
            var prof = SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Beginner);
            var s = SinglePlayerMatchSession.PrepareSetup(src, 4, SimTeam.Home, prof);
            Assert.AreEqual(before, src.Away[0].GkReflexes);
            Assert.AreEqual(before + prof.OpponentKeeperReflexDelta, s.Away[0].GkReflexes);
            Assert.AreEqual(src.Home[0].GkReflexes + prof.HumanKeeperReflexDelta, s.Home[0].GkReflexes);
        }

        [Test]
        public void Session_HumanAway_UsesOpponentThetaForHome()
        {
            var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Level = TeamAIDifficultyLevel.Legendary, HumanTeam = SimTeam.Away });
            var prof = SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Legendary);
            Assert.AreEqual(prof.OpponentTheta, s.Director.Brains[0].Difficulty.Theta, 1e-6f);
            Assert.AreEqual(prof.TeammateTheta, s.Director.Brains[1].Difficulty.Theta, 1e-6f);
            // Opponent reaction bonus reaches the AI (review item 7); teammates get none.
            Assert.AreEqual(prof.Ladder.OpponentReactionBonus, s.Director.Brains[0].Difficulty.ReactionBonus, 1e-6f);
            Assert.AreEqual(0f, s.Director.Brains[1].Difficulty.ReactionBonus, 1e-6f);
            Assert.GreaterOrEqual(s.ControlledId, 11);
        }

        [Test]
        public void Advance_FixedStep_AccumulatesAndPauses()
        {
            var s = NewSession(TeamAIDifficultyLevel.Professional, 2);
            var bot = new ScriptedGamepadBot();
            Assert.AreEqual(3, s.Advance(0.051, bot));
            Assert.AreEqual(0, s.Advance(0.010, bot));
            Assert.AreEqual(1, s.Advance(0.010, bot)); // 0.003 + 0.010 + 0.010 >= 1/60
            s.Pause();
            int tick = s.Sim.Tick;
            Assert.AreEqual(0, s.Advance(1.0, bot));
            Assert.AreEqual(tick, s.Sim.Tick);
            s.Resume();
            Assert.AreEqual(15, s.Advance(10.0, bot)); // clamped to MaxFrameTime 0.25 s
        }

        [Test]
        public void HumanMask_MasksControlledPlayer_AndInputlessSlotFallsBackToAI()
        {
            var s = NewSession(TeamAIDifficultyLevel.Professional, 2);
            Assert.AreEqual(0L, s.HumanMask); // no input yet: attract mode
            s.HumanSlots[0].Input = new ScriptedGamepadBot();
            Assert.AreEqual(1L << s.ControlledId, s.HumanMask);
            var second = s.AddHumanSlot(SimTeam.Home, new ScriptedGamepadBot());
            Assert.AreNotEqual(s.ControlledId, second.ControlledId);
            Assert.AreEqual((1L << s.ControlledId) | (1L << second.ControlledId), s.HumanMask);
        }

        [Test]
        public void Session_SameSeed_IsDeterministic()
        {
            var a = NewSession(TeamAIDifficultyLevel.WorldClass, 9);
            var b = NewSession(TeamAIDifficultyLevel.WorldClass, 9);
            var botA = new ScriptedGamepadBot();
            var botB = new ScriptedGamepadBot();
            for (int i = 0; i < 3600; i++)
            {
                a.StepOnce(botA);
                b.StepOnce(botB);
            }
            Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
            Assert.AreEqual(a.ControlledId, b.ControlledId);
            var c = NewSession(TeamAIDifficultyLevel.WorldClass, 10);
            var botC = new ScriptedGamepadBot();
            for (int i = 0; i < 3600; i++) c.StepOnce(botC);
            Assert.AreNotEqual(a.Sim.ComputeStateHash(), c.Sim.ComputeStateHash());
        }

        [Test]
        public void FullMatch_EveryDifficulty_RunsHeadlessWithoutNaN()
        {
            for (int i = 0; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var level = (TeamAIDifficultyLevel)i;
                var s = NewSession(level, 1);
                var r = PlayFull(s);
                Assert.IsTrue(r.Finished, level.ToString());
                Assert.AreEqual(2, s.Sim.State.Half);
                Assert.Greater(r.HomeShots + r.AwayShots, 0, level + ": no shots at all");
                Assert.Greater(s.Selector.SwitchCount, 10, level + ": user never changed player");
                Log($"{TeamAIDifficultyLadder.DisplayName(level)}: {r.HomeScore}-{r.AwayScore} shots {r.HomeShots}-{r.AwayShots} poss {r.HomePossession:F2}");
            }
        }

        private struct LevelStats
        {
            public int HumanGoals, AIGoals, AIShots, HumanShots, AIPasses, AIPassesCompleted;
            public float AIPossession;
            public float AIPassRate => AIPasses > 0 ? AIPassesCompleted / (float)AIPasses : 0f;
        }

        private static LevelStats PlaySeeds(TeamAIDifficultyLevel level, int seeds, float half = 360f)
        {
            var st = new LevelStats();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var s = NewSession(level, (ulong)seed, half);
                var bot = new ScriptedGamepadBot();
                while (!s.IsFinished)
                {
                    var ev = s.StepOnce(bot);
                    for (int i = 0; i < ev.Count; i++)
                    {
                        if (ev[i].Team != SimTeam.Away) continue;
                        if (ev[i].Type == MatchEventType.Pass) st.AIPasses++;
                        else if (ev[i].Type == MatchEventType.PassCompleted) st.AIPassesCompleted++;
                    }
                }
                AssertFinite(s);
                var r = s.Result;
                st.HumanGoals += r.HumanGoals;
                st.AIGoals += r.AIGoals;
                st.AIShots += r.AwayShots;
                st.HumanShots += r.HomeShots;
                st.AIPossession += (1f - r.HomePossession) / seeds;
            }
            return st;
        }

        /// <summary>
        /// Behaviour-level difficulty check: the AI's threat must rise with the level and the user's scoring fall, over
        /// seeds, against the same scripted user. Counts are totals over <c>seeds</c> matches of two 4-minute halves;
        /// thresholds sit well inside the 10-seed full-length numbers (AI shots per match 3.3 / 13.0 / 20.8, AI goals
        /// 0.4 / 2.2 / 2.4, user goals 2.1 / 1.0 / 0.6 for Beginner / Professional / Ultimate, after the moving-target
        /// and Beginner retune of Nov 2026) so they hold across seeds and tuning noise. This 5-seed, 4-minute-half run
        /// gave balances -12.0 / 12.4 / 21.0. Pass completion and possession are logged, not asserted: a stronger AI plays more
        /// forward passes, so its completion rate is not a measure of threat.
        /// </summary>
        /// <remarks>
        /// Fifteen 8-minute matches: about 5.5 minutes under Mono and far longer in the Unity editor, so it is tagged
        /// Slow (skip it in the fast suite with a category filter) and given a 30-minute timeout instead of Unity's
        /// default 180 s. Fewer seeds or shorter halves would make the ordering assertions flaky.
        /// </remarks>
        [Test]
        [Category("Slow")]
        [Timeout(1800000)]
        public void Difficulty_BeginnerProfessionalUltimate_AIThreatRisesAndUserScoringFallsOverSeeds()
        {
            const int seeds = 5;
            const float half = 240f;
            var b = PlaySeeds(TeamAIDifficultyLevel.Beginner, seeds, half);
            var p = PlaySeeds(TeamAIDifficultyLevel.Professional, seeds, half);
            var u = PlaySeeds(TeamAIDifficultyLevel.Ultimate, seeds, half);
            Log(Describe("Beginner", b));
            Log(Describe("Professional", p));
            Log(Describe("Ultimate", u));

            // The match swings toward the AI level by level: the balance (goal difference plus 0.2 per shot of shot
            // difference, AI minus user) is strictly ordered. Over four sets of five seeds it ran -9.8..-3.4
            // (Beginner), -1.2..6 (Professional) and 15..22 (Ultimate); the closest step was 2.2 apart.
            Assert.Less(Balance(b), Balance(p), "Professional should go better for the AI than Beginner");
            Assert.Less(Balance(p), Balance(u), "Ultimate should go better for the AI than Professional");
            // Shots: Ultimate well clear of both.
            Assert.Greater(u.AIShots, p.AIShots + 10, "Ultimate AI should shoot clearly more than Professional");
            Assert.GreaterOrEqual(u.AIShots, 2 * b.AIShots + 5, "Ultimate AI should be far more threatening than Beginner");
            // Goals: the top level outscores the bottom one, and Beginner still lets the user win.
            Assert.Greater(u.AIGoals, b.AIGoals, "Ultimate AI should score more than Beginner");
            Assert.Greater(b.HumanGoals, b.AIGoals, "the user should outscore the Beginner AI");
            // The user's scoring falls with the level.
            Assert.Greater(b.HumanGoals, u.HumanGoals + 2, "the user should score clearly more against Beginner than Ultimate");
            Assert.GreaterOrEqual(b.HumanShots, u.HumanShots, "the user should get at least as many shots against Beginner");
        }

        private static float Balance(in LevelStats s) =>
            s.AIGoals - s.HumanGoals + 0.2f * (s.AIShots - s.HumanShots);

        private static string Describe(string name, in LevelStats s) =>
            $"{name}: user {s.HumanGoals} goals / {s.HumanShots} shots, AI {s.AIGoals} goals / {s.AIShots} shots, " +
            $"AI pass {s.AIPassRate:P0}, AI poss {s.AIPossession:F2}, balance {Balance(s):F1}";
    }
}
