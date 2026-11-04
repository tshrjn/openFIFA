using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>
    /// Behaviour of the style sliders inside the brain (runs, overlaps, rest defence, crosses), the away-side mirror of
    /// the defensive shapes, the roster style checks, and how styles sit against the difficulty ladder.
    /// </summary>
    [TestFixture]
    [Category("FID_TEAMAI")]
    public class TeamAITacticsBehaviourTests
    {
        private static readonly PitchSpec Pitch = PitchSpec.Standard;

        private static float Z(float depth, int sign = 1) => TeamAIMath.DepthToZ(depth, sign, Pitch);

        /// <summary>
        /// Team 0 (attacking +Z) in the final third: carrier CM at depth 0.7 facing forward, unpressed. Team 1 holds a
        /// flat back four at depth 0.8 (the offside line) with its keeper behind. Optional forwards wait just onside.
        /// </summary>
        private sealed class Attack
        {
            public readonly TeamAIWorld World = TeamAITestWorlds.Empty();
            public int Carrier;

            public Attack(int behindBall = 5)
            {
                var w = World;
                TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
                // Rest defence: CBs, FBs, DM behind the ball.
                var rest = new[] { (TeamAIRole.CB, 1, -8f, 0.42f), (TeamAIRole.CB, 2, 8f, 0.42f), (TeamAIRole.FB, 3, -24f, 0.47f),
                                   (TeamAIRole.FB, 4, 24f, 0.47f), (TeamAIRole.DM, 5, 0f, 0.55f) };
                for (int k = 0; k < behindBall && k < rest.Length; k++)
                    TeamAITestWorlds.Add(w, 0, rest[k].Item1, rest[k].Item2, rest[k].Item3, Z(rest[k].Item4));
                Carrier = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, Z(0.7f));
                w.Players[Carrier].Facing = new SimVec3(0f, 0f, 1f);
                TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.99f));
                float[] xs = { -18f, -6f, 6f, 18f };
                for (int k = 0; k < 4; k++)
                    TeamAITestWorlds.Add(w, 1, k < 2 ? TeamAIRole.CB : TeamAIRole.FB, 1 + k, xs[k], Z(0.8f));
                TeamAITestWorlds.GiveBall(w, Carrier);
            }

            public int AddForward(TeamAIRole role, int slot, float x, float depth = 0.785f) =>
                TeamAITestWorlds.Add(World, 0, role, slot, x, Z(depth));

            public AIPlayerCommand[] Decide(TeamAITactics home, float time = 10f)
            {
                World.Time = time;
                var dir = new TeamAIDirector(Pitch, home, new TeamAITactics(), 1f, 1f, 11);
                dir.DecideNow(World, 0.1f);
                return dir.Commands;
            }

            public int Count(AIPlayerCommand[] cmds, TeamAIIntent intent)
            {
                int n = 0;
                for (int i = 0; i < World.PlayerCount; i++)
                    if (World.Players[i].Team == 0 && cmds[i].Intent == intent) n++;
                return n;
            }
        }

        private static TeamAITactics Runs(float forwardRuns) => new TeamAITactics { ForwardRuns = forwardRuns };

        [Test]
        public void MaxRunners_CapsRunsInBehind()
        {
            foreach (var (runs, expected) in new[] { (0f, 1), (0.5f, 2), (1f, 3) })
            {
                var a = new Attack();
                a.AddForward(TeamAIRole.W, 8, -22f);
                a.AddForward(TeamAIRole.ST, 10, -4f);
                a.AddForward(TeamAIRole.AM, 7, 4f);
                a.AddForward(TeamAIRole.W, 9, 22f);
                var cmds = a.Decide(Runs(runs));
                Assert.AreEqual(expected, a.Count(cmds, TeamAIIntent.RunInBehind), "ForwardRuns " + runs);
            }
        }

        [Test]
        public void LateMidfieldRun_OnlyWhenTheTeamCommitsPlayersForward()
        {
            int Late(float runs, int behind)
            {
                var a = new Attack(behind);
                int cm = a.AddForward(TeamAIRole.CM, 7, 10f);
                var cmds = a.Decide(Runs(runs));
                return cmds[cm].Intent == TeamAIIntent.RunInBehind ? 1 : 0;
            }
            Assert.AreEqual(0, Late(0.5f, 5), "a balanced side keeps its midfielders in shape");
            Assert.AreEqual(1, Late(1f, 5), "a commit-forward side sends the midfielder in late");
            Assert.AreEqual(0, Late(1f, 2), "rest defence: not with only two team-mates behind the ball");
        }

        private static int OverlapScene(float runs, float carrierDepth)
        {
            var w = TeamAITestWorlds.Empty();
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 1, -8f, Z(0.3f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CB, 2, 8f, Z(0.3f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.DM, 5, 0f, Z(0.35f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.FB, 3, -24f, Z(0.33f));
            int fb = TeamAITestWorlds.Add(w, 0, TeamAIRole.FB, 4, 26f, Z(carrierDepth - 0.08f));
            int carrier = TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 9, 24f, Z(carrierDepth));
            // Two closer team-mates take the support spots, so the full-back is free to overlap.
            TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 7, 17f, Z(carrierDepth - 0.03f));
            TeamAITestWorlds.Add(w, 0, TeamAIRole.AM, 8, 18f, Z(carrierDepth + 0.03f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.99f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 1, -6f, Z(0.8f));
            TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 2, 6f, Z(0.8f));
            TeamAITestWorlds.GiveBall(w, carrier);
            w.Time = 10f;
            var dir = new TeamAIDirector(Pitch, Runs(runs), new TeamAITactics(), 1f, 1f, 13);
            dir.DecideNow(w, 0.1f);
            return dir.Commands[fb].Intent == TeamAIIntent.Overlap ? 1 : 0;
        }

        [Test]
        public void FullBackOverlap_GatedByForwardRuns_AndStartsDeeperForAttackingSides()
        {
            Assert.AreEqual(1, OverlapScene(0.5f, 0.56f), "balanced side overlaps a winger in the opponent half");
            Assert.AreEqual(0, OverlapScene(0.1f, 0.56f), "a side that commits nobody keeps its full-back home");
            Assert.AreEqual(0, OverlapScene(0.5f, 0.45f), "balanced: not from his own half");
            Assert.AreEqual(1, OverlapScene(0.9f, 0.45f), "wing-play: the full-back goes from deeper");
        }

        [Test]
        public void CrossBias_TipsAWideCarrierIntoCrossing()
        {
            int crosses0 = 0, crosses1 = 0;
            for (int seed = 0; seed < 24; seed++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    var w = TeamAITestWorlds.Empty();
                    TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, Z(0.02f));
                    int carrier = TeamAITestWorlds.Add(w, 0, TeamAIRole.W, 9, 26f, Z(0.82f));
                    TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, -2f, Z(0.91f));
                    TeamAITestWorlds.Add(w, 0, TeamAIRole.FB, 4, 22f, Z(0.7f));
                    TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 6f, Z(0.72f));
                    TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, Z(0.99f));
                    TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 1, -4f, Z(0.93f));
                    TeamAITestWorlds.Add(w, 1, TeamAIRole.CB, 2, 3f, Z(0.9f));
                    TeamAITestWorlds.Add(w, 1, TeamAIRole.FB, 4, 20f, Z(0.84f));
                    TeamAITestWorlds.GiveBall(w, carrier);
                    var onBall = new TeamAIOnBall { CrossBias = pass == 0 ? 0f : 0.3f };
                    var cmd = AIPlayerCommand.Idle(w.Players[carrier].Id, w.Players[carrier].Position);
                    var ctx = new TeamAIOnBallContext { OffsideDepth = 0.93f, MatchPhase = TeamAIMatchPhase.InPlay };
                    onBall.Decide(w, carrier, TeamAIPhase.FinalThird, new TeamAIDifficulty(0.6f), new TeamAIRandom((ulong)(seed + 1)), ctx, ref cmd);
                    if (cmd.Action == TeamAIAction.Cross) { if (pass == 0) crosses0++; else crosses1++; }
                }
            }
            Assert.Greater(crosses1, crosses0, $"crosses without bias {crosses0}/24, with bias {crosses1}/24");
        }

        // ------------------------------------------------------------------ away side (attacks -Z)

        [Test]
        public void AwaySide_BackFive_WingerDrop_AndHolderDrop_Mirror()
        {
            var anchors = new SimVec3[TeamAIFormations.SlotCount];
            var home = new SimVec3[TeamAIFormations.SlotCount];
            var ball = new SimVec3(5f, 0f, 10f); // in the away side's own half
            var mirrorBall = new SimVec3(-5f, 0f, -10f);

            foreach (var (f, phase, tac) in new[]
            {
                (TeamAIFormation.F532, TeamAIPhase.LowBlock, new TeamAITactics { Formation = TeamAIFormation.F532 }),
                (TeamAIFormation.F433, TeamAIPhase.MidBlock, new TeamAITactics { Formation = TeamAIFormation.F433 }),
                (TeamAIFormation.F433, TeamAIPhase.BuildUp, TeamAITactics.FromStyle(TeamAIStyle.Possession, TeamAIFormation.F433)),
            })
            {
                float la = TeamAIFormationShape.ComputeAnchors(phase, tac, ball, -1, Pitch, anchors);
                float lh = TeamAIFormationShape.ComputeAnchors(phase, tac, mirrorBall, 1, Pitch, home);
                Assert.AreEqual(lh, la, 1e-4f, f + " " + phase + " line");
                for (int s = 0; s < anchors.Length; s++)
                {
                    Assert.AreEqual(-home[s].X, anchors[s].X, 1e-3f, $"{f} {phase} slot {s} x");
                    Assert.AreEqual(-home[s].Z, anchors[s].Z, 1e-3f, $"{f} {phase} slot {s} z");
                }
            }

            // And the shapes hold for the away side on their own terms.
            var t532 = new TeamAITactics { Formation = TeamAIFormation.F532 };
            float line = TeamAIFormationShape.ComputeAnchors(TeamAIPhase.LowBlock, t532, ball, -1, Pitch, anchors);
            float lineZ = Z(line, -1);
            int onLine = 0;
            for (int s = 1; s < anchors.Length; s++) if (System.Math.Abs(anchors[s].Z - lineZ) < 1.5f) onLine++;
            Assert.AreEqual(5, onLine, "away back five");
        }

        // ------------------------------------------------------------------ rosters

        [Test]
        public void Validator_RejectsAnUnknownStyle_AcceptsAKnownOne()
        {
            var root = RosterTestData.SmallLeague();
            root.Get("clubs")[0].Set("style", JsonNode.FromString("total-football-9000"));
            var bad = RosterTestData.Load(root);
            Assert.IsFalse(bad.Success);
            Assert.IsTrue(RosterTestData.HasError(bad, "clubs[0].style", "must be one of"), bad.Describe());

            root.Get("clubs")[0].Set("style", JsonNode.FromNumber(3));
            Assert.IsFalse(RosterTestData.Load(root).Success, "a number is not a style");

            root.Get("clubs")[0].Set("style", JsonNode.FromString("Gegenpress"));
            var ok = RosterTestData.Load(root);
            Assert.IsTrue(ok.Success, ok.Describe());
            Assert.AreEqual("Gegenpress", ok.League.Clubs[0].Style);
        }

        [Test]
        public void RosterStyle_KeepsReputation_SameStyleClubsDiffer()
        {
            var players = new RosterPlayer[0];
            RosterClub Club(int rep) => new RosterClub("c" + rep, "C", "C", "#000000", "#ffffff", "#ff0000", "S", 10000, "4-3-3", rep,
                players, style: "high-press");
            var big = RosterMatchBuilder.TacticsFor(Club(5), TeamAIFormation.F433);
            var mid = RosterMatchBuilder.TacticsFor(Club(3), TeamAIFormation.F433);
            var small = RosterMatchBuilder.TacticsFor(Club(1), TeamAIFormation.F433);
            var preset = TeamAITactics.FromStyle(TeamAIStyle.HighPress);
            Assert.AreEqual(preset.PressingIntensity, mid.PressingIntensity, 1e-6f, "3 stars = the preset");
            Assert.Greater(big.Depth, mid.Depth);
            Assert.Less(small.PressingIntensity, mid.PressingIntensity);
            Assert.Less(small.Depth, mid.Depth);
            Assert.That(big.PressingIntensity, Is.InRange(0f, 1f));
            Assert.AreEqual(TeamAIStyle.HighPress, small.Style);
        }

        // ------------------------------------------------------------------ styles vs the difficulty ladder

        [Test]
        public void SameLevel_DifferentStyles_SameDifficultyProfile()
        {
            var levels = new[] { TeamAIDifficultyLevel.Beginner, TeamAIDifficultyLevel.Professional, TeamAIDifficultyLevel.Ultimate };
            foreach (var level in levels)
            {
                TeamAIDifficulty? reference = null;
                foreach (TeamAIStyle st in System.Enum.GetValues(typeof(TeamAIStyle)))
                {
                    var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
                    {
                        Level = level, Seed = 3, AwayTactics = TeamAITactics.FromStyle(st, TeamAIFormation.F442)
                    });
                    var brain = s.Director.Brains[1];
                    var d = brain.Difficulty;
                    if (reference == null) reference = d;
                    Assert.AreEqual(reference.Value.Theta, d.Theta, 1e-6f, $"{level} {st} theta");
                    Assert.AreEqual(reference.Value.ReactionDelay, d.ReactionDelay, 1e-6f, $"{level} {st} reaction");
                    Assert.AreEqual(reference.Value.DecisionNoise, d.DecisionNoise, 1e-6f, $"{level} {st} noise");
                    Assert.AreEqual(reference.Value.PressAggression, d.PressAggression, 1e-6f, $"{level} {st} press aggression");
                    Assert.AreEqual(reference.Value.LapseChance, d.LapseChance, 1e-6f, $"{level} {st} lapses");
                    // The style itself is carried out in proportion to the level (neutral sliders stay neutral).
                    float exec = brain.Manager.StyleExecution;
                    Assert.AreEqual(TeamAIMath.Lerp(0.25f, 1f, d.Theta), exec, 1e-5f);
                }
            }
        }

        [Test]
        public void StyleExecution_PullsSlidersTowardNeutral_NeutralStaysNeutral()
        {
            var press = TeamAITactics.FromStyle(TeamAIStyle.HighPress);
            var o = new TeamAITactics();
            TeamAIManager.Apply(press, TeamAIMentality.Balanced, 0.4f, o);
            Assert.AreEqual(0.5f + (press.PressingIntensity - 0.5f) * 0.4f, o.PressingIntensity, 1e-5f);
            Assert.Less(o.Depth, press.Depth);
            TeamAIManager.Apply(new TeamAITactics(), TeamAIMentality.Balanced, 0.4f, o);
            Assert.AreEqual(0.5f, o.Depth, 1e-6f);
            Assert.AreEqual(0.5f, o.PressingIntensity, 1e-6f);
        }

        [Test]
        public void Manager_ForDifficulty_RangeScalesWithLevel()
        {
            var m = new TeamAIManager();
            m.ForDifficulty(0.05f);
            Assert.AreEqual(0, m.MaxAttack);
            Assert.AreEqual(1, m.MaxDefend);
            m.ForDifficulty(1f);
            Assert.AreEqual(2, m.MaxAttack);
            Assert.AreEqual(0, m.MaxDefend, "a top side keeps attacking with a lead");

            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            w.MatchPhase = TeamAIMatchPhase.Kickoff;
            w.MatchProgress = 0.95f;
            w.Score[1] = 1;
            m.ForDifficulty(0.05f);
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Balanced, m.Current, "Beginner chasing: no push");
            m.ForDifficulty(0.58f);
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Attacking, m.Current, "Professional chasing: one step");
            w.Score[0] = 2;
            m.Update(w, 0);
            Assert.AreEqual(TeamAIMentality.Defensive, m.Current, "leading late: one step only");
        }

        /// <summary>
        /// A style never lifts a level past the next one: the most aggressive style on Beginner still goes worse for the
        /// AI than the most defensive one on Ultimate. Same scripted user and balance measure as the ladder test.
        /// </summary>
        [Test]
        [Category("Slow")]
        [Timeout(1800000)]
        public void BeginnerHighPress_RanksBelow_UltimateParkTheBus()
        {
            float Balance(TeamAIDifficultyLevel level, TeamAIStyle style)
            {
                float bal = 0f;
                for (int seed = 1; seed <= 3; seed++)
                {
                    var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
                    {
                        Level = level, Seed = (ulong)seed, Config = new MatchSimConfig { HalfDurationSeconds = 240f },
                        AwayTactics = TeamAITactics.FromStyle(style, TeamAIFormation.F442)
                    });
                    var bot = new ScriptedGamepadBot();
                    while (!s.IsFinished) s.StepOnce(bot);
                    var r = s.Result;
                    bal += r.AIGoals - r.HumanGoals + 0.2f * (r.AwayShots - r.HomeShots);
                }
                return bal;
            }
            float beginnerPress = Balance(TeamAIDifficultyLevel.Beginner, TeamAIStyle.HighPress);
            float ultimateBus = Balance(TeamAIDifficultyLevel.Ultimate, TeamAIStyle.ParkTheBus);
            System.Console.WriteLine($"Beginner HighPress {beginnerPress:F1}, Ultimate ParkTheBus {ultimateBus:F1}");
            Assert.Less(beginnerPress, ultimateBus);
        }
    
        // ------------------------------------------------------------------ pressing (PPDA)

        /// <summary>Pressing numbers of the home side over AI-vs-AI matches (see <see cref="Press"/>).</summary>
        public struct PressStats
        {
            /// <summary>Opponent passes in their own defensive 60% of the pitch.</summary>
            public int Passes;
            /// <summary>Home tackles and interceptions in that zone (fouls excluded: a foul is not a press won).</summary>
            public int Actions;
            /// <summary>Home fouls anywhere.</summary>
            public int Fouls;
            public float Ppda => Passes / (float)System.Math.Max(1, Actions);
        }

        /// <summary>
        /// Home = <paramref name="style"/>, away = a balanced 4-3-3, both on Professional, no human input.
        /// </summary>
        public static PressStats Press(TeamAIStyle style, int seeds, float half)
        {
            var st = new PressStats();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
                {
                    Level = TeamAIDifficultyLevel.Professional, Seed = (ulong)seed,
                    Config = new MatchSimConfig { HalfDurationSeconds = half },
                    HomeTactics = TeamAITactics.FromStyle(style), AwayTactics = new TeamAITactics()
                });
                s.HumanSlots[0].Input = null;
                s.Director.Brains[1].Manager.Enabled = false;
                float hl = s.Sim.Config.Pitch.HalfLength;
                while (!s.IsFinished)
                {
                    var ev = s.StepOnce(null);
                    int sign = s.Sim.AttackSign(SimTeam.Home);
                    for (int i = 0; i < ev.Count; i++)
                    {
                        var e = ev[i];
                        if (e.Type == MatchEventType.Foul && e.Team == SimTeam.Home) st.Fouls++;
                        float depthFromHomeGoal = (e.Position.Z * sign + hl) / (2f * hl);
                        if (depthFromHomeGoal <= 0.4f) continue;
                        if (e.Type == MatchEventType.Pass && e.Team == SimTeam.Away) st.Passes++;
                        else if (e.Team == SimTeam.Home && (e.Type == MatchEventType.Tackle || e.Type == MatchEventType.Interception))
                            st.Actions++;
                    }
                }
            }
            return st;
        }

        /// <summary>
        /// The press is visible in the numbers and is not bought with fouls: HighPress allows the fewest opponent passes
        /// per tackle or interception (fouls excluded), ParkTheBus the most, and HighPress commits at most 1.5x the
        /// fouls of a balanced side. Measured (8 seeds, 4-minute halves): PPDA without fouls 3.09 / 4.27 / 11.45,
        /// fouls 48 / 35 / 27 (HighPress / Balanced / ParkTheBus); this test's
        /// 6 seeds of 3-minute halves gave 3.74 / 5.06 / 9.98 and fouls 27 / 24 / 14.
        /// </summary>
        [Test]
        [Category("Slow")]
        [Timeout(1800000)]
        public void Ppda_HighPress_Below_Balanced_Below_ParkTheBus_WithoutExtraFouls()
        {
            var press = Press(TeamAIStyle.HighPress, 6, 180f);
            var bal = Press(TeamAIStyle.Balanced, 6, 180f);
            var bus = Press(TeamAIStyle.ParkTheBus, 6, 180f);
            System.Console.WriteLine($"PPDA (no fouls) HighPress {press.Ppda:F2}, Balanced {bal.Ppda:F2}, ParkTheBus {bus.Ppda:F2}; " +
                                     $"fouls {press.Fouls} / {bal.Fouls} / {bus.Fouls}");
            Assert.Less(press.Ppda, bal.Ppda * 0.85f, "high press should allow clearly fewer passes per defensive action");
            Assert.Less(bal.Ppda, bus.Ppda, "park the bus should press least");
            Assert.LessOrEqual(press.Fouls, 1.5f * bal.Fouls + 2f, "the press should not be won by fouling");
        }

        [Test]
        public void PressSqueeze_OnlyForPressingSides_InPressingPhases()
        {
            var w = TeamAITestWorlds.FullMatch(new TeamAITactics(), new TeamAITactics());
            var press = TeamAITactics.FromStyle(TeamAIStyle.HighPress);
            var dir = new TeamAIDirector(Pitch, press, new TeamAITactics(), 0.7f, 0.7f, 9);
            // Team 1 builds up deep in its own half: team 0 is in its high press.
            int carrier = 18;
            w.Players[carrier].Position = new SimVec3(0f, 0f, Z(0.85f));
            TeamAITestWorlds.GiveBall(w, carrier);
            w.Time = 5f;
            dir.DecideNow(w, 0.1f);
            Assert.AreEqual(TeamAIPhase.HighPress, dir.Brains[0].Phase);
            Assert.Greater(dir.Brains[0].PressSqueeze, 0.8f);
            Assert.AreEqual(0f, dir.Brains[1].PressSqueeze, "the side on the ball does not squeeze");

            var neutral = new TeamAIDirector(Pitch, new TeamAITactics(), new TeamAITactics(), 0.7f, 0.7f, 9);
            neutral.DecideNow(w, 0.1f);
            Assert.AreEqual(0f, neutral.Brains[0].PressSqueeze, "neutral pressing intensity: unchanged behaviour");
        }

        // ------------------------------------------------------------------ style strength follows intent

        private static int AIGoalsFor(TeamAIStyle style, TeamAIDifficultyLevel level, int seeds)
        {
            int goals = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var setup = MatchSetup.CreateDefault((ulong)seed);
                var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
                {
                    Level = level, Seed = (ulong)seed, Config = new MatchSimConfig { HalfDurationSeconds = 360f },
                    AwayTactics = TeamAITactics.FromStyle(style, SimFormationMapper.BestFit(setup.AwayFormation))
                });
                var bot = new ScriptedGamepadBot();
                while (!s.IsFinished) s.StepOnce(bot);
                goals += s.Result.AIGoals;
            }
            return goals;
        }

        /// <summary>
        /// A defensive style must not be the strongest attack. At Ultimate against the scripted user (16 seeds of
        /// 6-minute halves): park the bus scores least, high press most and clearly more than balanced. Measured over
        /// 48 seeds: ParkTheBus 145, Possession 196, Balanced 203, HighPress 274 (before the calibration ParkTheBus
        /// scored 178 to Balanced's 105 over 24 seeds, the most of any style).
        /// </summary>
        [Test]
        [Category("Slow")]
        [Timeout(3600000)]
        public void StyleGoalsFor_FollowIntent_ParkTheBusLeast_HighPressMost()
        {
            const int seeds = 16;
            var level = TeamAIDifficultyLevel.Ultimate;
            int bus = AIGoalsFor(TeamAIStyle.ParkTheBus, level, seeds);
            int bal = AIGoalsFor(TeamAIStyle.Balanced, level, seeds);
            int poss = AIGoalsFor(TeamAIStyle.Possession, level, seeds);
            int press = AIGoalsFor(TeamAIStyle.HighPress, level, seeds);
            System.Console.WriteLine($"AI goals at Ultimate over {seeds}: ParkTheBus {bus}, Balanced {bal}, Possession {poss}, HighPress {press}");
            Assert.Less(bus, bal, "park the bus should score less than a balanced side");
            Assert.Less(bus, poss, "park the bus should score less than a possession side");
            Assert.Greater(press, bal, "a high press should score more than a balanced side");
            Assert.Greater(press, bus * 1.4f, "a high press should score far more than park the bus");
        }
    }
}
