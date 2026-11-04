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
        public void DifficultyProfile_EveryLevel_UsesLadderThetasAndEveryKnobIsMonotonic()
        {
            for (int i = 0; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var level = (TeamAIDifficultyLevel)i;
                var p = SinglePlayerDifficulty.For(level);
                var l = TeamAIDifficultyLadder.For(level);
                // The user's AI teammates are never weaker than a Semi-Pro opponent, at any level.
                Assert.GreaterOrEqual(p.TeammateTheta, TeamAIDifficultyLadder.For(TeamAIDifficultyLevel.SemiPro).OpponentTheta,
                    level + " teammate theta below the Semi-Pro opponent");
                Assert.AreEqual(l.OpponentTheta, p.OpponentTheta);
                Assert.AreEqual(l.TeammateTheta, p.TeammateTheta);
                if (i > 0)
                {
                    var prev = SinglePlayerDifficulty.For((TeamAIDifficultyLevel)(i - 1));
                    // Every knob moves one way up the ladder (harder for the user), so no lever can undo another.
                    Assert.Greater(p.OpponentTheta, prev.OpponentTheta, level + " opponent theta");
                    Assert.LessOrEqual(p.TeammateTheta, prev.TeammateTheta, level + " teammate theta");
                    Assert.GreaterOrEqual(p.Ladder.OpponentReactionBonus, prev.Ladder.OpponentReactionBonus, level + " reaction bonus");
                    Assert.GreaterOrEqual(p.OpponentOutfieldDelta, prev.OpponentOutfieldDelta, level + " opponent outfield");
                    Assert.GreaterOrEqual(p.OpponentKeeperReflexDelta, prev.OpponentKeeperReflexDelta);
                    Assert.LessOrEqual(p.HumanKeeperReflexDelta, prev.HumanKeeperReflexDelta, level + " human keeper");
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
            // Beginner weakens the AI's outfielders (not its keeper's ratings) and leaves the user's side alone.
            Assert.Less(prof.OpponentOutfieldDelta, 0);
            Assert.AreEqual(src.Away[5].Pace + prof.OpponentOutfieldDelta, s.Away[5].Pace);
            Assert.AreEqual(src.Away[5].Finishing + prof.OpponentOutfieldDelta, s.Away[5].Finishing);
            Assert.AreEqual(src.Away[0].GkHandling, s.Away[0].GkHandling);
            Assert.AreEqual(src.Home[5].Pace, s.Home[5].Pace);
            Assert.AreEqual(before, src.Away[0].GkReflexes);
            Assert.AreEqual(src.Away[5].Pace, MatchSetup.CreateDefault(4).Away[5].Pace, "caller setup untouched");
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
            /// <summary>Shots on target faced by each keeper: saved, or scored (a shot followed by a save or a goal
            /// before any other pass, tackle, interception, woodwork or out-of-play).</summary>
            public int UserGkSaved, UserGkConceded, AIGkSaved, AIGkConceded;
            public float AIPossession;
            public float AIPassRate => AIPasses > 0 ? AIPassesCompleted / (float)AIPasses : 0f;
        }

        /// <summary>One full match against the scripted user; <c>finite</c> is false if the sim ever went NaN.</summary>
        private static LevelStats PlayOne(TeamAIDifficultyLevel level, ulong seed, float half, out bool finite)
        {
            var st = new LevelStats();
            var s = NewSession(level, seed, half);
            var bot = new ScriptedGamepadBot();
            finite = true;
            bool userShotLive = false, aiShotLive = false;
            int guard = 0;
            while (!s.IsFinished)
            {
                var ev = s.StepOnce(bot);
                for (int i = 0; i < ev.Count; i++)
                {
                    TrackShot(ev[i], ref st, ref userShotLive, ref aiShotLive);
                    if (ev[i].Team != SimTeam.Away) continue;
                    if (ev[i].Type == MatchEventType.Pass) st.AIPasses++;
                    else if (ev[i].Type == MatchEventType.PassCompleted) st.AIPassesCompleted++;
                }
                if ((++guard & 255) == 0 && !IsFinite(s)) { finite = false; break; }
            }
            if (!IsFinite(s)) finite = false;
            var r = s.Result;
            st.HumanGoals = r.HumanGoals;
            st.AIGoals = r.AIGoals;
            st.AIShots = r.AwayShots;
            st.HumanShots = r.HomeShots;
            st.AIPossession = 1f - r.HomePossession;
            return st;
        }

        /// <summary>Shot-outcome bookkeeping for the keeper save rate (the user is Home, the AI Away).</summary>
        private static void TrackShot(in MatchEvent e, ref LevelStats st, ref bool userShotLive, ref bool aiShotLive)
        {
            switch (e.Type)
            {
                case MatchEventType.Shot:
                    if (e.Team == SimTeam.Home) userShotLive = true; else if (e.Team == SimTeam.Away) aiShotLive = true;
                    break;
                case MatchEventType.Save:
                    if (e.Team == SimTeam.Away && userShotLive) { st.AIGkSaved++; userShotLive = false; }
                    else if (e.Team == SimTeam.Home && aiShotLive) { st.UserGkSaved++; aiShotLive = false; }
                    break;
                case MatchEventType.Goal:
                    if (e.Team == SimTeam.Home && userShotLive) st.AIGkConceded++;
                    else if (e.Team == SimTeam.Away && aiShotLive) st.UserGkConceded++;
                    userShotLive = aiShotLive = false;
                    break;
                case MatchEventType.Pass:
                case MatchEventType.OutOfPlay:
                case MatchEventType.Interception:
                case MatchEventType.Tackle:
                case MatchEventType.PostHit:
                case MatchEventType.CrossbarHit:
                    userShotLive = aiShotLive = false;
                    break;
            }
        }

        private static bool IsFinite(SinglePlayerMatchSession s)
        {
            var st = s.Sim.State;
            if (Bad(st.Ball.Position) || Bad(st.Ball.Velocity)) return false;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                if (Bad(st.Players[i].Position) || Bad(st.Players[i].Velocity)) return false;
            return true;
        }

        /// <summary>
        /// Plays seeds 1..<paramref name="seeds"/> for every level. Matches are independent sessions with no shared
        /// mutable state, so they run in parallel; each result lands in its own slot and the totals are summed in a fixed
        /// order, so the outcome is identical to a sequential run (checked against single-process runs of each seed).
        /// </summary>
        private static LevelStats[] PlayLadder(int seeds, float half)
        {
            int levels = TeamAIDifficultyLadder.LevelCount;
            var games = new LevelStats[levels * seeds];
            var finite = new bool[levels * seeds];
            System.Threading.Tasks.Parallel.For(0, games.Length,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                k => games[k] = PlayOne((TeamAIDifficultyLevel)(k / seeds), (ulong)(k % seeds + 1), half, out finite[k]));
            var totals = new LevelStats[levels];
            for (int k = 0; k < games.Length; k++)
            {
                Assert.IsTrue(finite[k], $"level {k / seeds} seed {k % seeds + 1}: sim went NaN");
                ref var t = ref totals[k / seeds];
                var g = games[k];
                t.HumanGoals += g.HumanGoals;
                t.AIGoals += g.AIGoals;
                t.AIShots += g.AIShots;
                t.HumanShots += g.HumanShots;
                t.AIPasses += g.AIPasses;
                t.AIPassesCompleted += g.AIPassesCompleted;
                t.UserGkSaved += g.UserGkSaved;
                t.UserGkConceded += g.UserGkConceded;
                t.AIGkSaved += g.AIGkSaved;
                t.AIGkConceded += g.AIGkConceded;
                t.AIPossession += g.AIPossession / seeds;
            }
            return totals;
        }

        /// <summary>
        /// Behaviour-level difficulty check over the whole seven-level ladder: against the same scripted user, every level
        /// must go clearly better for the AI than the one below, in goal difference (AI minus user) and in balance (goal
        /// difference plus 0.2 per shot of shot difference). Any inversion fails.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Fixed, representative seed set: seeds 1..24, full-length 2 x 6-minute halves, all seven levels (168 matches).
        /// Outcomes are chaotic in the seed (any parameter change reshuffles every match), so fewer seeds or shorter halves
        /// are not representative: with 4-minute halves World Class and Legendary tie on goal difference, and with seeds
        /// 1..16 some steps shrink to +10 goals. Measured on this set (Nov 2026 ladder retune, keeper deltas after the
        /// save-rate review), totals over 24 matches: goal difference -68 / -29 / 1 / 18 / 35 / 65 / 94 (steps 39 30 17 17
        /// 30 29) and balance -101.0 / -39.2 / 8.2 / 49.0 / 82.4 / 135.6 / 185.2 (steps 61.8 47.4 40.8 33.4 53.2 49.6).
        /// Over 48 seeds every step is at least 2.5 standard errors of the paired difference in goal difference and 3.7
        /// in balance. The thresholds (each step at least 0.25 goals and 0.5 balance per match) sit at about half the
        /// smallest measured step.
        /// </para>
        /// <para>
        /// Keeper save rate (on-target shots saved / (saved + scored)) targets 55-80% for both keepers at every level; over
        /// 48 seeds every level sits in that band (55.1-78.1%). Each keeper is judged on this test's own seeds only when it
        /// faced at least <see cref="MinKeeperSample"/> shots on target (Ultimate's and Legendary's AI keepers face 10-13,
        /// so they are skipped and logged). The assertion allows for sampling error: the observed rate must lie within two
        /// binomial standard errors (at the band edge) of the band, so a keeper only fails when its rate is clearly outside
        /// it. Measured on seeds 1..24: user keeper 85.0% (17/20), 70.0, 54.5, 67.0, 66.1, 62.2, 60.3%; AI keeper 50.0%
        /// (62/124), 51.3 (40/78), 63.8, 60.0 (18/30), 76.9 (20/26), skipped, skipped.
        /// </para>
        /// <para>
        /// The matches run in parallel on all cores: 3.2 minutes on 12 cores under Mono (26 CPU-minutes, so about half an hour on one
        /// core). Tagged Slow (skip it in the fast suite with a category filter) with a 60-minute timeout.
        /// </para>
        /// </remarks>
        [Test]
        [Category("Slow")]
        [Timeout(3600000)]
        public void Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI()
        {
            const int seeds = 24;
            const float half = 360f;
            var t = PlayLadder(seeds, half);
            for (int i = 0; i < t.Length; i++)
                Log(Describe(TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)i), t[i]));

            for (int i = 1; i < t.Length; i++)
            {
                string step = TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)(i - 1)) + " -> " +
                              TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)i);
                int gdStep = GoalDifference(t[i]) - GoalDifference(t[i - 1]);
                float balStep = Balance(t[i]) - Balance(t[i - 1]);
                Assert.GreaterOrEqual(gdStep, (int)Math.Ceiling(0.25f * seeds),
                    $"{step}: AI goal difference should rise by at least 0.25 per match (got {gdStep} over {seeds})");
                Assert.GreaterOrEqual(balStep, 0.5f * seeds,
                    $"{step}: balance should rise by at least 0.5 per match (got {balStep:F1} over {seeds})");
            }

            var beg = t[(int)TeamAIDifficultyLevel.Beginner];
            var ult = t[(int)TeamAIDifficultyLevel.Ultimate];
            // Beginner: the user clearly wins (measured 72 goals to 4); Ultimate: the AI clearly does (measured 105 to 11).
            Assert.Greater(beg.HumanGoals, 2 * beg.AIGoals + seeds / 2, "the user should clearly outscore the Beginner AI");
            Assert.Greater(ult.AIGoals, 2 * ult.HumanGoals + seeds, "the Ultimate AI should clearly outscore the user");
            // Threat and the user's chances move the right way end to end.
            Assert.GreaterOrEqual(ult.AIShots, 4 * beg.AIShots, "Ultimate AI should be far more threatening than Beginner");
            Assert.Greater(beg.HumanShots, 2 * ult.HumanShots, "the user should get far more shots against Beginner");

            for (int i = 0; i < t.Length; i++)
            {
                string name = TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)i);
                AssertSaveRate(name + " user keeper", t[i].UserGkSaved, t[i].UserGkConceded);
                AssertSaveRate(name + " AI keeper", t[i].AIGkSaved, t[i].AIGkConceded);
            }
        }

        /// <summary>Smallest number of on-target shots a keeper must face for its save rate to be judged.</summary>
        private const int MinKeeperSample = 20;
        private const float SaveRateLow = 0.55f, SaveRateHigh = 0.80f;

        private static void AssertSaveRate(string who, int saved, int conceded)
        {
            int n = saved + conceded;
            if (n < MinKeeperSample)
            {
                Log($"{who}: {saved}/{n} on-target shots saved, too few to judge (< {MinKeeperSample})");
                return;
            }
            float rate = saved / (float)n;
            float seLow = (float)Math.Sqrt(SaveRateLow * (1f - SaveRateLow) / n);
            float seHigh = (float)Math.Sqrt(SaveRateHigh * (1f - SaveRateHigh) / n);
            Log($"{who}: {saved}/{n} = {rate:P1} saved");
            Assert.GreaterOrEqual(rate, SaveRateLow - 2f * seLow, $"{who} saves too few: {saved}/{n} = {rate:P1}");
            Assert.LessOrEqual(rate, SaveRateHigh + 2f * seHigh, $"{who} saves too many: {saved}/{n} = {rate:P1}");
        }

        private static int GoalDifference(in LevelStats s) => s.AIGoals - s.HumanGoals;

        private static float Balance(in LevelStats s) =>
            s.AIGoals - s.HumanGoals + 0.2f * (s.AIShots - s.HumanShots);

        private static string Describe(string name, in LevelStats s) =>
            $"{name}: user {s.HumanGoals} goals / {s.HumanShots} shots, AI {s.AIGoals} goals / {s.AIShots} shots, " +
            $"AI pass {s.AIPassRate:P0}, AI poss {s.AIPossession:F2}, balance {Balance(s):F1}";
    }
}
