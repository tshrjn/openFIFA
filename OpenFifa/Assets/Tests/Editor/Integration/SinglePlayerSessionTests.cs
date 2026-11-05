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
                    Assert.GreaterOrEqual(p.OpponentFinishingDelta, prev.OpponentFinishingDelta, level + " opponent finishing");
                    Assert.LessOrEqual(p.HumanKeeperReflexDelta, prev.HumanKeeperReflexDelta, level + " human keeper");
                    Assert.LessOrEqual(p.PassAssist, prev.PassAssist);
                    Assert.LessOrEqual(p.ShotAssist, prev.ShotAssist);
                }
            }
            Assert.AreEqual("Semi-Pro", SinglePlayerDifficulty.For(TeamAIDifficultyLevel.SemiPro).DisplayName);
            // The bottom three levels each have their own AI keeper (Beginner < Amateur < Semi-Pro).
            Assert.Less(SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Beginner).OpponentKeeperReflexDelta,
                SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Amateur).OpponentKeeperReflexDelta);
            Assert.Less(SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Amateur).OpponentKeeperReflexDelta,
                SinglePlayerDifficulty.For(TeamAIDifficultyLevel.SemiPro).OpponentKeeperReflexDelta);
        }

        [Test]
        public void Difficulty_EasyLevels_KeepTheUsersKeeper_AndLowerAIFinishingInstead()
        {
            for (var level = TeamAIDifficultyLevel.Beginner; level <= TeamAIDifficultyLevel.Professional; level++)
            {
                var p = SinglePlayerDifficulty.For(level);
                Assert.AreEqual(0, p.HumanKeeperReflexDelta, level + ": the user's own keeper is not weakened");
                Assert.Less(p.OpponentFinishingDelta, 0, level + ": AI finishing is lowered instead");
            }
            for (var level = TeamAIDifficultyLevel.WorldClass; level <= TeamAIDifficultyLevel.Ultimate; level++)
                Assert.AreEqual(0, SinglePlayerDifficulty.For(level).OpponentFinishingDelta, level.ToString());
        }

        [Test]
        public void PrepareSetup_FinishingDelta_OnlyShiftsAIOutfieldFinishing()
        {
            var src = MatchSetup.CreateDefault(4);
            var prof = SinglePlayerDifficulty.For(TeamAIDifficultyLevel.Amateur);
            int d = prof.OpponentOutfieldDelta + prof.OpponentFinishingDelta;
            foreach (var human in new[] { SimTeam.Home, SimTeam.Away })
            {
                var s = SinglePlayerMatchSession.PrepareSetup(src, 4, human, prof);
                var ai = human == SimTeam.Home ? s.Away : s.Home;
                var aiSrc = human == SimTeam.Home ? src.Away : src.Home;
                var me = human == SimTeam.Home ? s.Home : s.Away;
                var meSrc = human == SimTeam.Home ? src.Home : src.Away;
                for (int i = 1; i < MatchSetup.PlayersPerTeam; i++)
                {
                    Assert.AreEqual(Clamp99(aiSrc[i].Finishing + d), ai[i].Finishing);
                    Assert.AreEqual(Clamp99(aiSrc[i].LongShots + d), ai[i].LongShots);
                    Assert.AreEqual(Clamp99(aiSrc[i].Composure + d), ai[i].Composure);
                    Assert.AreEqual(Clamp99(aiSrc[i].ShortPassing + prof.OpponentOutfieldDelta), ai[i].ShortPassing,
                        "only the finishing ratings get the extra delta");
                    Assert.AreEqual(meSrc[i].Finishing, me[i].Finishing, "the user's side keeps its ratings");
                }
                Assert.AreEqual(aiSrc[0].Finishing, ai[0].Finishing, "the AI keeper is not an outfielder");
                Assert.AreEqual(meSrc[0].GkReflexes, me[0].GkReflexes, "Amateur leaves the user's keeper alone");
            }
        }

        private static int Clamp99(int v) => v < 1 ? 1 : (v > 99 ? 99 : v);

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
            Assert.AreEqual(Clamp99(src.Away[5].Finishing + prof.OpponentOutfieldDelta + prof.OpponentFinishingDelta), s.Away[5].Finishing);
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
                    TrackShot(ev[i], ref st, ref userShotLive, ref aiShotLive, IsKeeperId);
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

        /// <summary>Keepers are slot 0 of each side (player ids 0 and 11).</summary>
        private static bool IsKeeperId(int id) => id % MatchSetup.PlayersPerTeam == 0;

        /// <summary>
        /// Shot-outcome bookkeeping for the keeper save rate (the user is Home, the AI Away). A shot stays live only
        /// until something other than the keeper deals with it: a block or deflection off any player, an outfielder
        /// taking control of it, a pass, interception, tackle, woodwork or out-of-play clears it, so a later save or
        /// goal is never credited to that shot.
        /// </summary>
        private static void TrackShot(in MatchEvent e, ref LevelStats st, ref bool userShotLive, ref bool aiShotLive,
            Func<int, bool> isKeeper)
        {
            switch (e.Type)
            {
                case MatchEventType.Shot:
                    userShotLive = e.Team == SimTeam.Home;
                    aiShotLive = e.Team == SimTeam.Away;
                    break;
                case MatchEventType.Save:
                    if (e.Team == SimTeam.Away && userShotLive) st.AIGkSaved++;
                    else if (e.Team == SimTeam.Home && aiShotLive) st.UserGkSaved++;
                    userShotLive = aiShotLive = false;
                    break;
                case MatchEventType.Goal:
                    if (e.Team == SimTeam.Home && userShotLive) st.AIGkConceded++;
                    else if (e.Team == SimTeam.Away && aiShotLive) st.UserGkConceded++;
                    userShotLive = aiShotLive = false;
                    break;
                case MatchEventType.FirstTouch:
                case MatchEventType.PossessionChange:
                    // A keeper gathering the ball after his own save is not a block.
                    if (!isKeeper(e.PlayerId)) userShotLive = aiShotLive = false;
                    break;
                case MatchEventType.Deflection:
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

        private static MatchEvent Ev(MatchEventType type, SimTeam team, int player = 5) =>
            new MatchEvent { Type = type, Team = team, PlayerId = player };

        private static LevelStats Track(params MatchEvent[] events)
        {
            var st = new LevelStats();
            bool u = false, a = false;
            foreach (var e in events) TrackShot(e, ref st, ref u, ref a, IsKeeperId);
            return st;
        }

        [Test]
        public void TrackShot_SaveAfterABlockOrDeflection_IsNotCreditedToTheShot()
        {
            // User shot blocked by an AI defender's body, then the AI keeper saves the loose ball: not a save on that shot.
            var st = Track(Ev(MatchEventType.Shot, SimTeam.Home), Ev(MatchEventType.Deflection, SimTeam.Away, 14),
                Ev(MatchEventType.Save, SimTeam.Away, 11));
            Assert.AreEqual(0, st.AIGkSaved);
            // Deflection off the shooter's own team-mate also ends it.
            st = Track(Ev(MatchEventType.Shot, SimTeam.Away, 18), Ev(MatchEventType.Deflection, SimTeam.Away, 19),
                Ev(MatchEventType.Goal, SimTeam.Away, 19));
            Assert.AreEqual(0, st.UserGkConceded);
            // A defender controlling the shot (block and control) ends it too.
            st = Track(Ev(MatchEventType.Shot, SimTeam.Home), Ev(MatchEventType.FirstTouch, SimTeam.Away, 13),
                Ev(MatchEventType.Save, SimTeam.Away, 11));
            Assert.AreEqual(0, st.AIGkSaved);
        }

        [Test]
        public void TrackShot_CleanSaveAndGoal_AreCreditedToTheRightKeeper()
        {
            var st = Track(Ev(MatchEventType.Shot, SimTeam.Home), Ev(MatchEventType.Save, SimTeam.Away, 11),
                Ev(MatchEventType.FirstTouch, SimTeam.Away, 11), Ev(MatchEventType.Goal, SimTeam.Home));
            Assert.AreEqual(1, st.AIGkSaved);
            Assert.AreEqual(0, st.AIGkConceded, "the goal came without a new shot");
            st = Track(Ev(MatchEventType.Shot, SimTeam.Away, 18), Ev(MatchEventType.Goal, SimTeam.Away, 18),
                Ev(MatchEventType.Shot, SimTeam.Home), Ev(MatchEventType.Save, SimTeam.Away, 11));
            Assert.AreEqual(1, st.UserGkConceded);
            Assert.AreEqual(1, st.AIGkSaved);
            // A new shot by the other side replaces the live one: its save is not the first shot's.
            st = Track(Ev(MatchEventType.Shot, SimTeam.Home), Ev(MatchEventType.Shot, SimTeam.Away, 18),
                Ev(MatchEventType.Save, SimTeam.Away, 11));
            Assert.AreEqual(0, st.AIGkSaved);
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
        private static LevelStats[] PlayLadder(int seeds, float half, int firstSeed = 1)
        {
            int levels = TeamAIDifficultyLadder.LevelCount;
            var games = new LevelStats[levels * seeds];
            var finite = new bool[levels * seeds];
            System.Threading.Tasks.Parallel.For(0, games.Length,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                k => games[k] = PlayOne((TeamAIDifficultyLevel)(k / seeds), (ulong)(k % seeds + firstSeed), half, out finite[k]));
            var totals = new LevelStats[levels];
            for (int k = 0; k < games.Length; k++)
            {
                Assert.IsTrue(finite[k], $"level {k / seeds} seed {k % seeds + firstSeed}: sim went NaN");
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
        /// must go better for the AI than the one below, in goal difference (AI minus user) and in balance (goal
        /// difference plus 0.2 per shot of shot difference), by at least that step's floor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Fixed seed set: seeds 49..120 (72 per level), full-length 2 x 6-minute halves, all seven levels (504 matches).
        /// Outcomes are chaotic in the seed (any sim or AI change reshuffles every match), so the old 24-seed set flipped a
        /// different step red with each unrelated change. Each step's floor is its baseline on these seeds minus 2 standard
        /// errors of the per-seed paired difference (same seed, adjacent levels), clamped at 0 so no step may invert.
        /// Baseline: main at d762568 (Nov 2026, after player body contact #41); per-step numbers are in docs/DIFFICULTY.md.
        /// Re-baseline the floors there and here when a deliberate difficulty change moves a step.
        /// </para>
        /// <para>
        /// Keeper save rate (on-target shots saved / (saved + scored)) targets 55-80% for both keepers at every level, on
        /// this test's own seeds, when the keeper faced at least <see cref="MinKeeperSample"/> shots on target; the observed
        /// rate may sit outside the band by min(1.5 binomial standard errors, 4 points), so a 50% keeper fails. A shot stops
        /// counting at a block, deflection or outfielder's touch (TrackShot). Keepers short of the sample are judged on
        /// further seeds by <see cref="Difficulty_LowVolumeKeepers_SaveRateInBandOnMoreSeeds"/>.
        /// </para>
        /// <para>
        /// The matches run in parallel on all cores: about 10 minutes on 12 cores. Tagged Slow (skip it in the fast suite
        /// with a category filter) with a 60-minute timeout.
        /// </para>
        /// </remarks>
        [Test]
        [Category("Slow")]
        [Timeout(3600000)]
        public void Difficulty_AllSevenLevels_EachStepGoesClearlyBetterForTheAI()
        {
            const int firstSeed = LadderFirstSeed, seeds = LadderSeeds;
            const float half = 360f;
            var t = PlayLadder(seeds, half, firstSeed);
            for (int i = 0; i < t.Length; i++)
                Log(Describe(TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)i), t[i]));

            for (int i = 1; i < t.Length; i++)
            {
                string step = TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)(i - 1)) + " -> " +
                              TeamAIDifficultyLadder.DisplayName((TeamAIDifficultyLevel)i);
                int gdStep = GoalDifference(t[i]) - GoalDifference(t[i - 1]);
                float balStep = Balance(t[i]) - Balance(t[i - 1]);
                Log($"{step}: GD step {gdStep} (floor {StepGdFloor[i - 1] * seeds:F1}), balance step {balStep:F1} (floor {StepBalanceFloor[i - 1] * seeds:F1})");
                Assert.GreaterOrEqual(gdStep, StepGdFloor[i - 1] * seeds,
                    $"{step}: AI goal difference should rise by at least {StepGdFloor[i - 1]:F2} per match (got {gdStep} over {seeds})");
                Assert.GreaterOrEqual(balStep, StepBalanceFloor[i - 1] * seeds,
                    $"{step}: balance should rise by at least {StepBalanceFloor[i - 1]:F2} per match (got {balStep:F1} over {seeds})");
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

        /// <summary>
        /// The keepers the ladder test cannot judge on seeds 1..24 because they face under <see cref="MinKeeperSample"/>
        /// shots on target there: the user's keeper at Beginner and Amateur (the AI barely shoots), and the AI's keeper
        /// from Professional up (the scripted user barely shoots). Each is played on further seeds (25 onwards, 24 at a
        /// time, at most 144) until it has faced at least that many, then judged with the same 55-80% band.
        /// </summary>
        /// <remarks>
        /// Batches of 24 seeds for the cases still short of the sample, in parallel on all cores. Every case must reach
        /// the sample within 144 seeds, so none is silently skipped. Measured (Nov 2026): Beginner user keeper 73.5%
        /// (36/49, seeds 25..120), Amateur user 64.2% (52/81, 25..72), Professional AI 79.0% (49/62, 25..72), World Class
        /// AI 62.5% (25/40, 25..72), Legendary AI 68.1% (32/47, 25..96), Ultimate AI 66.0% (31/47, 25..120): 432 matches,
        /// 14.8 minutes on 12 cores under Mono. Tagged Slow with a 60-minute timeout, like the ladder test.
        /// </remarks>
        [Test]
        [Category("Slow")]
        [Timeout(3600000)]
        public void Difficulty_LowVolumeKeepers_SaveRateInBandOnMoreSeeds()
        {
            var cases = new (TeamAIDifficultyLevel Level, bool UserKeeper)[]
            {
                (TeamAIDifficultyLevel.Beginner, true), (TeamAIDifficultyLevel.Amateur, true),
                (TeamAIDifficultyLevel.Professional, false), (TeamAIDifficultyLevel.WorldClass, false),
                (TeamAIDifficultyLevel.Legendary, false), (TeamAIDifficultyLevel.Ultimate, false),
            };
            const int batch = 24, maxSeeds = 144;
            const float half = 360f;
            var saved = new int[cases.Length];
            var conceded = new int[cases.Length];
            var seedsUsed = new int[cases.Length];
            var pending = new System.Collections.Generic.List<int>();
            for (int first = 25; first < 25 + maxSeeds; first += batch)
            {
                pending.Clear();
                for (int c = 0; c < cases.Length; c++)
                    if (saved[c] + conceded[c] < MinKeeperSample) pending.Add(c);
                if (pending.Count == 0) break;
                var games = new LevelStats[pending.Count * batch];
                var finite = new bool[games.Length];
                int seed0 = first;
                System.Threading.Tasks.Parallel.For(0, games.Length,
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                    k => games[k] = PlayOne(cases[pending[k / batch]].Level, (ulong)(seed0 + k % batch), half, out finite[k]));
                for (int k = 0; k < games.Length; k++)
                {
                    int c = pending[k / batch];
                    Assert.IsTrue(finite[k], $"{cases[c].Level} seed {seed0 + k % batch}: sim went NaN");
                    saved[c] += cases[c].UserKeeper ? games[k].UserGkSaved : games[k].AIGkSaved;
                    conceded[c] += cases[c].UserKeeper ? games[k].UserGkConceded : games[k].AIGkConceded;
                }
                foreach (int c in pending) seedsUsed[c] += batch;
            }
            for (int c = 0; c < cases.Length; c++)
            {
                string who = TeamAIDifficultyLadder.DisplayName(cases[c].Level) + (cases[c].UserKeeper ? " user keeper" : " AI keeper");
                Log($"{who}: seeds 25..{24 + seedsUsed[c]}");
                Assert.GreaterOrEqual(saved[c] + conceded[c], MinKeeperSample,
                    $"{who}: only {saved[c] + conceded[c]} on-target shots in {seedsUsed[c]} seeds");
                AssertSaveRate(who, saved[c], conceded[c]);
            }
        }

        /// <summary>Seed set of the ladder test: seeds LadderFirstSeed .. LadderFirstSeed + LadderSeeds - 1.</summary>
        private const int LadderFirstSeed = 49, LadderSeeds = 72;

        /// <summary>
        /// Per-match floors for each ladder step (Beginner->Amateur first): baseline mean minus 2 standard errors of the
        /// paired per-seed difference on the ladder seeds, rounded down, never below 0. See docs/DIFFICULTY.md.
        /// </summary>
        private static readonly float[] StepGdFloor = { 0.84f, 0.65f, 0.25f, 0.38f, 0.85f, 0.03f };
        private static readonly float[] StepBalanceFloor = { 1.83f, 1.35f, 1.12f, 1.00f, 1.64f, 0.00f };

        /// <summary>Smallest number of on-target shots a keeper must face for its save rate to be judged.</summary>
        private const int MinKeeperSample = 40;
        private const float SaveRateLow = 0.55f, SaveRateHigh = 0.80f;
        /// <summary>Sampling allowance at the band edge: 1.5 binomial standard errors, but never more than this.</summary>
        private const float MaxSaveRateAllowance = 0.04f;

        /// <summary>
        /// Keeper save-rate band check. Null = in band (or too few shots to judge, see <paramref name="judged"/>);
        /// otherwise the failure message. The observed rate may sit outside 55-80% by min(1.5 SE, 4 points) at the
        /// band edge, so a 50% keeper fails even on ~120 shots (where 1.5 SE alone would still allow 48.6%).
        /// </summary>
        private static string SaveRateProblem(string who, int saved, int conceded, out bool judged)
        {
            int n = saved + conceded;
            judged = n >= MinKeeperSample;
            if (!judged) return null;
            float rate = saved / (float)n;
            float lo = SaveRateLow - Math.Min(1.5f * (float)Math.Sqrt(SaveRateLow * (1f - SaveRateLow) / n), MaxSaveRateAllowance);
            float hi = SaveRateHigh + Math.Min(1.5f * (float)Math.Sqrt(SaveRateHigh * (1f - SaveRateHigh) / n), MaxSaveRateAllowance);
            if (rate < lo) return $"{who} saves too few: {saved}/{n} = {rate:P1} (floor {lo:P1})";
            if (rate > hi) return $"{who} saves too many: {saved}/{n} = {rate:P1} (ceiling {hi:P1})";
            return null;
        }

        private static void AssertSaveRate(string who, int saved, int conceded)
        {
            int n = saved + conceded;
            string problem = SaveRateProblem(who, saved, conceded, out bool judged);
            if (!judged)
            {
                Log($"{who}: {saved}/{n} on-target shots saved, too few to judge (< {MinKeeperSample})");
                return;
            }
            Log($"{who}: {saved}/{n} = {saved / (float)n:P1} saved");
            Assert.IsNull(problem, problem);
        }

        [Test]
        public void SaveRateBand_RejectsAFiftyPercentKeeper_AcceptsTheBandAndSkipsSmallSamples()
        {
            // The old check (2 SE, n >= 20) passed the Beginner AI keeper at 62/124 = 50%.
            Assert.IsNotNull(SaveRateProblem("k", 62, 62, out bool judged));
            Assert.IsTrue(judged);
            Assert.IsNotNull(SaveRateProblem("k", 136, 136, out _), "50% on 272 shots");
            Assert.IsNotNull(SaveRateProblem("k", 25, 25, out _), "50% on 50 shots");
            Assert.IsNull(SaveRateProblem("k", 70, 54, out _), "56% is in the band");
            Assert.IsNull(SaveRateProblem("k", 66, 58, out _), "53% is within the allowance on 124 shots");
            Assert.IsNull(SaveRateProblem("k", 100, 24, out _), "81% is within the allowance");
            Assert.IsNotNull(SaveRateProblem("k", 110, 14, out _), "89% saves too many");
            Assert.IsNull(SaveRateProblem("k", 10, 29, out judged), "39 shots are too few to judge");
            Assert.IsFalse(judged);
        }

        private static int GoalDifference(in LevelStats s) => s.AIGoals - s.HumanGoals;

        private static float Balance(in LevelStats s) =>
            s.AIGoals - s.HumanGoals + 0.2f * (s.AIShots - s.HumanShots);

        private static string Describe(string name, in LevelStats s) =>
            $"{name}: user {s.HumanGoals} goals / {s.HumanShots} shots, AI {s.AIGoals} goals / {s.AIShots} shots, " +
            $"AI pass {s.AIPassRate:P0}, AI poss {s.AIPossession:F2}, balance {Balance(s):F1}";
    }
}
