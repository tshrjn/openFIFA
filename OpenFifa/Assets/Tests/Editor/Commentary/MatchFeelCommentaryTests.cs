using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Open-play build-up commentary (switches, progressive passes, dribbles, pressing, possession spells), line variety
    /// across matches (<see cref="CommentaryLineMemory"/>), and the fast-forward commentary clock
    /// (<see cref="SinglePlayerMatchSession.FastForward"/>).
    /// </summary>
    [TestFixture]
    [Category("FID_COMMENTARY")]
    public class MatchFeelCommentaryTests
    {
        private static CommentaryLineBank Shipped()
        {
            var bank = CommentaryLineBank.Load(File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("; ", errors));
            return bank;
        }

        private static CommentaryAudioIndex ShippedIndex()
        {
            string path = Path.Combine(RosterTestData.StreamingAssets, "Commentary", "audio", "en", "index.json");
            var index = CommentaryAudioIndex.Parse(File.ReadAllText(path), out string error);
            Assert.IsNotNull(index, error);
            return index;
        }

        private static MatchSim NewSim() => new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(1));

        private static MatchEvent Pass(int from, SimVec3 at) =>
            new MatchEvent { Type = MatchEventType.Pass, PlayerId = from, Team = SimTeam.Home, Position = at, Action = IntentAction.Pass };

        private static MatchEvent Received(int player, int from, SimVec3 at) =>
            new MatchEvent { Type = MatchEventType.PassCompleted, PlayerId = player, OtherPlayerId = from, Team = SimTeam.Home, Position = at };

        private static CommentaryEventType Only(IReadOnlyList<CommentaryEvent> outp)
        {
            Assert.AreEqual(1, outp.Count, "one commentary event per reception");
            return outp[0].Type;
        }

        private static string[] Names()
        {
            var n = new string[MatchSimState.PlayerCount];
            for (int i = 0; i < n.Length; i++) n[i] = "Player" + i;
            return n;
        }

        // ---- build-up detection (SimCommentaryAdapter) ----

        [Test]
        public void Adapter_PassAcrossThePitch_IsSwitchOfPlayNamingTheReceiver()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            a.Convert(new List<MatchEvent> { Pass(4, new SimVec3(-20f, 0f, -5f)) }, 1.0);
            var outp = a.Convert(new List<MatchEvent> { Received(7, 4, new SimVec3(12f, 0f, 0f)) }, 2.0);
            Assert.AreEqual(CommentaryEventType.SwitchOfPlay, Only(outp));
            Assert.AreEqual("Player7", outp[0].PlayerName);
        }

        [Test]
        public void Adapter_PassForwardIntoTheAttackingHalf_IsProgressive()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            // Home attacks +Z: from 10 m inside its own half to 15 m into the opponents' half.
            a.Convert(new List<MatchEvent> { Pass(4, new SimVec3(0f, 0f, -10f)) }, 1.0);
            Assert.AreEqual(CommentaryEventType.ProgressivePass, Only(a.Convert(new List<MatchEvent> { Received(9, 4, new SimVec3(3f, 0f, 15f)) }, 2.0)));
        }

        [Test]
        public void Adapter_ShortSquarePass_IsANameCall()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            a.Convert(new List<MatchEvent> { Pass(4, new SimVec3(0f, 0f, 0f)) }, 1.0);
            Assert.AreEqual(CommentaryEventType.OnTheBall, Only(a.Convert(new List<MatchEvent> { Received(5, 4, new SimVec3(8f, 0f, 2f)) }, 2.0)));
        }

        [Test]
        public void Adapter_WrongFootedDefender_IsDribblePastNamingTheCarrier()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            var outp = a.Convert(new List<MatchEvent>
            {
                new MatchEvent { Type = MatchEventType.WrongFooted, PlayerId = 14, OtherPlayerId = 8, Team = SimTeam.Away, Position = new SimVec3(0f, 0f, 20f) },
            }, 1.0);
            Assert.AreEqual(CommentaryEventType.DribblePast, Only(outp));
            Assert.AreEqual("Player8", outp[0].PlayerName);
            Assert.AreEqual("Player14", outp[0].SecondaryPlayerName);
            Assert.AreEqual(0, outp[0].Team, "the carrier's side (home)");
        }

        [Test]
        public void Adapter_CarrierRunsPastHisGoalSideMarker_IsDribblePast()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            var st = sim.State;
            // Park everyone far away, then carrier 8 (home, attacking +Z) at z 10 with defender 14 two metres goal-side.
            for (int i = 0; i < MatchSimState.PlayerCount; i++) st.Players[i].Position = new SimVec3(-30f + i, 0f, -40f);
            st.Players[8].Position = new SimVec3(0f, 0f, 10f);
            st.Players[14].Position = new SimVec3(0.5f, 0f, 12f);
            var touch = new MatchEvent { Type = MatchEventType.DribbleTouch, PlayerId = 8, Team = SimTeam.Home };
            Assert.AreEqual(0, a.Convert(new List<MatchEvent> { touch }, 1.0).Count);
            st.Tick += 60;
            st.Players[8].Position = new SimVec3(0f, 0f, 15f); // past him
            var outp = a.Convert(new List<MatchEvent> { touch }, 2.0);
            Assert.AreEqual(CommentaryEventType.DribblePast, Only(outp));
            Assert.AreEqual("Player8", outp[0].PlayerName);
            Assert.AreEqual("Player14", outp[0].SecondaryPlayerName);
        }

        [Test]
        public void Adapter_BallWonInTheAttackingThird_IsPressWonButNotInOwnHalf()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 13, Team = SimTeam.Away, Position = new SimVec3(0f, 0f, 0f) } }, 1.0);
            var high = a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.Interception, PlayerId = 9, Team = SimTeam.Home, Position = new SimVec3(5f, 0f, 35f) } }, 2.0);
            Assert.AreEqual(CommentaryEventType.PressWon, Only(high));
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 13, Team = SimTeam.Away, Position = new SimVec3(0f, 0f, 0f) } }, 3.0);
            var deep = a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.Interception, PlayerId = 3, Team = SimTeam.Home, Position = new SimVec3(5f, 0f, -30f) } }, 4.0);
            Assert.AreEqual(0, deep.Count);
        }

        [Test]
        public void Adapter_BallWonBackHighAfterTheirThrowIn_IsPressWon()
        {
            // Home wins the ball, Away gets a throw-in (possession changes hands with no PossessionChange event), then
            // Home presses and wins it back in the attacking third: that is a turnover, so PressWon.
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 9, Team = SimTeam.Home, Position = new SimVec3(0f, 0f, 0f) } }, 1.0);
            sim.State.Tick += 120;
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.RestartTaken, Tick = sim.State.Tick, PlayerId = 15, Team = SimTeam.Away, Detail = (int)RestartType.ThrowIn, Position = new SimVec3(34f, 0f, 30f) } }, 3.0);
            sim.State.Tick += 120;
            var won = a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 9, Team = SimTeam.Home, Position = new SimVec3(20f, 0f, 36f) } }, 5.0);
            Assert.AreEqual(CommentaryEventType.PressWon, Only(won));
        }

        [Test]
        public void Adapter_KickOffBySideThatEndedTheHalfOnTheBall_StartsANewPossessionSpell()
        {
            // Home won the ball 30 s before half time without a completed pass; Home also kicks off the second half.
            // Its first pass after the kick-off is not "they've had the ball for a long while".
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            int ticksPerSecond = (int)Math.Round(1.0 / sim.Dt);
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 6, Team = SimTeam.Home, Position = new SimVec3(0f, 0f, -10f) } }, 1.0);
            sim.State.Tick = 40 * ticksPerSecond;
            var ko = a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.KickOff, Tick = sim.State.Tick, PlayerId = 9, Team = SimTeam.Home, Detail = (int)RestartType.KickOff } }, 41.0);
            Assert.AreEqual(CommentaryEventType.KickOff, Only(ko));
            sim.State.Tick = 42 * ticksPerSecond;
            a.Convert(new List<MatchEvent> { Pass(9, new SimVec3(0f, 0f, 0f)) }, 42.0);
            Assert.AreEqual(CommentaryEventType.OnTheBall, Only(a.Convert(new List<MatchEvent> { Received(8, 9, new SimVec3(3f, 0f, -4f)) }, 43.0)));
        }

        [Test]
        public void Adapter_ThrowInToTheSideOnTheBall_RestartsThePossessionSpell()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            int ticksPerSecond = (int)Math.Round(1.0 / sim.Dt);
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 6, Team = SimTeam.Home, Position = new SimVec3(0f, 0f, -10f) } }, 1.0);
            sim.State.Tick = 18 * ticksPerSecond;
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.RestartTaken, Tick = sim.State.Tick, PlayerId = 7, Team = SimTeam.Home, Detail = (int)RestartType.ThrowIn, Position = new SimVec3(34f, 0f, 0f) } }, 19.0);
            sim.State.Tick = 25 * ticksPerSecond;
            a.Convert(new List<MatchEvent> { Pass(7, new SimVec3(0f, 0f, 0f)) }, 25.0);
            Assert.AreEqual(CommentaryEventType.OnTheBall, Only(a.Convert(new List<MatchEvent> { Received(8, 7, new SimVec3(3f, 0f, -4f)) }, 26.0)),
                "7 s since the throw-in is not a long spell");
        }

        [Test]
        public void Adapter_RestartInAFastForwardBatch_TimesTheSpellFromTheEventTick()
        {
            // A fast-forward batch converts its events after the sim has run every tick of it, so State.Tick is later than
            // the throw-in's own tick. The spell starts at the throw-in (5 s), not at the end of the batch (15 s): 21 s
            // later it is a long spell; timed from the batch end it would be only 11 s.
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names());
            int ticksPerSecond = (int)Math.Round(1.0 / sim.Dt);
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.PossessionChange, PlayerId = 6, Team = SimTeam.Away, Position = new SimVec3(0f, 0f, -10f) } }, 1.0);
            sim.State.Tick = 15 * ticksPerSecond;
            a.Convert(new List<MatchEvent> { new MatchEvent { Type = MatchEventType.RestartTaken, Tick = 5 * ticksPerSecond, PlayerId = 7, Team = SimTeam.Home, Detail = (int)RestartType.ThrowIn, Position = new SimVec3(34f, 0f, 0f) } }, 15.0);
            sim.State.Tick = 26 * ticksPerSecond;
            a.Convert(new List<MatchEvent> { Pass(7, new SimVec3(0f, 0f, 0f)) }, 26.0);
            Assert.AreEqual(CommentaryEventType.PossessionSpell, Only(a.Convert(new List<MatchEvent> { Received(8, 7, new SimVec3(3f, 0f, -4f)) }, 26.5)),
                "21 s since the throw-in's own tick");
        }

        [Test]
        public void Adapter_LongSpellOnTheBall_CallsPossessionSpellOncePerSpell()
        {
            var sim = NewSim();
            var a = new SimCommentaryAdapter(sim, Names(), null, new[] { "Homeside", "Awayside" });
            int ticksPerSecond = (int)Math.Round(1.0 / sim.Dt);
            var types = new List<CommentaryEventType>();
            for (int k = 0; k < 16; k++)
            {
                sim.State.Tick = k * 2 * ticksPerSecond; // a pass every 2 s, 30 s in all
                a.Convert(new List<MatchEvent> { Pass(4 + k % 3, new SimVec3(0f, 0f, -5f)) }, k * 2.0);
                foreach (var c in a.Convert(new List<MatchEvent> { Received(4 + (k + 1) % 3, 4, new SimVec3(4f, 0f, -3f)) }, k * 2.0 + 1)) types.Add(c.Type);
            }
            Assert.AreEqual(1, types.FindAll(t => t == CommentaryEventType.PossessionSpell).Count, string.Join(",", types));
        }

        // ---- engine: build-up categories and the name-call fallback ----

        [Test]
        public void ShippedBank_EveryBuildUpEvent_HasALeadCategoryWithRenderedOrRenderableLines()
        {
            var bank = Shipped();
            foreach (var t in new[] { CommentaryEventType.SwitchOfPlay, CommentaryEventType.ProgressivePass, CommentaryEventType.DribblePast,
                         CommentaryEventType.PressWon, CommentaryEventType.PossessionSpell })
            {
                var cats = bank.For(t, CommentaryVoice.PlayByPlay);
                Assert.IsNotEmpty(cats, t.ToString());
                foreach (var c in cats)
                {
                    Assert.Greater(c.Cooldown, 8.0, c.Id + ": build-up lines need a cooldown so they do not chatter");
                    Assert.Less(c.Priority, new CommentaryConfig().AnalystYieldPriority, c.Id + ": build-up never cuts the analyst");
                }
            }
        }

        [Test]
        public void Engine_BuildUpCategoryOnCooldown_FallsBackToANameCall()
        {
            var engine = new CommentaryEngine(Shipped(), null, 7u);
            engine.Push(new CommentaryEvent { Type = CommentaryEventType.KickOff, Time = 0 });
            var cats = new List<string>();
            double t = 5;
            for (int i = 0; i < 4; i++, t += 6)
            {
                engine.Tick(t);
                foreach (var cue in engine.Push(new CommentaryEvent
                         { Type = CommentaryEventType.SwitchOfPlay, Time = t, PlayerName = "Okafor", Minute = 10 }))
                    if (cue.Kind == CommentaryCueKind.Start) cats.Add(cue.Utterance.Category.Id);
            }
            Assert.AreEqual("build_up.switch", cats[0]);
            Assert.Contains("on_the_ball", cats, "a switch inside the cooldown is still a reception: name call");
        }

        // ---- line variety across matches ----

        [Test]
        public void MatchSeed_DiffersByFixtureAndMatchIndex_AndIsStable()
        {
            uint a = CommentaryLineMemory.MatchSeed(4, "chelsea", "arsenal");
            Assert.AreEqual(a, CommentaryLineMemory.MatchSeed(4, "chelsea", "arsenal"));
            Assert.AreNotEqual(a, CommentaryLineMemory.MatchSeed(4, "arsenal", "chelsea"));
            Assert.AreNotEqual(a, CommentaryLineMemory.MatchSeed(5, "chelsea", "arsenal"));
            Assert.AreNotEqual(a, CommentaryLineMemory.MatchSeed(4, "chelsea", "arsenal", 1));
            Assert.AreNotEqual(0u, a);
        }

        private static string FirstAnalystGoalLine(CommentaryEngine engine)
        {
            engine.Push(new CommentaryEvent { Type = CommentaryEventType.KickOff, Time = 0, Minute = 0 });
            engine.Push(new CommentaryEvent
            {
                Type = CommentaryEventType.Goal, Time = 100, Minute = 30, Team = 0, HomeScore = 1, AwayScore = 0,
                PlayerName = "Okafor", PlayerId = "p9", Situation = GoalSituation.OpenPlay
            });
            for (double t = 100; t < 125; t += 0.1)
                foreach (var cue in engine.Tick(t))
                    if (cue.Kind == CommentaryCueKind.Start && cue.Utterance.Voice == CommentaryVoice.Analyst)
                        return cue.Utterance.Line.Id;
            Assert.Fail("the analyst never spoke about the goal");
            return null;
        }

        [Test]
        public void SessionMemory_TwelveMatchesWithTheSameSeed_NeverRepeatTheAnalystGoalLine()
        {
            // Worst case for variety: every match on the same seed. With one session memory the analyst works through
            // the open-play goal lines instead of opening with the same one each time.
            var bank = Shipped();
            var memory = new CommentaryLineMemory();
            var seen = new HashSet<string>();
            for (int m = 0; m < 12; m++)
            {
                var engine = new CommentaryEngine(bank, null, 3u) { SessionMemory = memory };
                Assert.IsTrue(seen.Add(FirstAnalystGoalLine(engine)), "repeat in match " + m + ": " + string.Join(", ", seen));
            }
        }

        [Test]
        public void WithoutSessionMemory_SameSeed_RepeatsTheLine()
        {
            // Documents the old behaviour the memory fixes: a fresh engine on the same seed says the same thing.
            var bank = Shipped();
            Assert.AreEqual(FirstAnalystGoalLine(new CommentaryEngine(bank, null, 3u)), FirstAnalystGoalLine(new CommentaryEngine(bank, null, 3u)));
        }

        [Test]
        public void SessionMemory_EveryLineHeard_PicksTheOneHeardLongestAgo()
        {
            var memory = new CommentaryLineMemory();
            memory.Note("a");
            memory.Note("b");
            memory.Note("a");
            Assert.Less(memory.LastUse("b"), memory.LastUse("a"));
            Assert.AreEqual(0, memory.LastUse("c"));
            Assert.IsTrue(memory.Heard("b"));
            memory.Clear();
            Assert.AreEqual(0, memory.Count);
        }

        // ---- fast-forward clock (capture skipping to its window) ----

        private static SinglePlayerMatchSession Session(ulong seed, out CommentaryEngine engine)
        {
            var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions
            {
                Seed = seed, Config = new MatchSimConfig { HalfDurationSeconds = 360f }, PlayerNames = Names(),
                TeamNames = new[] { "Homeside", "Awayside" },
            });
            engine = new CommentaryEngine(Shipped(), null, 11u);
            s.AttachCommentaryEngine(engine);
            s.HumanSlots[0].Input = new ScriptedGamepadBot();
            return s;
        }

        /// <summary>Seconds until the first line starts after the window opens, playing 40 s from the commentary clock given.</summary>
        private static double FirstLineAfter(SinglePlayerMatchSession s, double clock)
        {
            double start = clock;
            for (int i = 0; i < 40 * 60 && !s.IsFinished; i++)
            {
                clock += 1.0 / 60;
                s.Advance(1.0 / 60, null, clock);
                foreach (var c in s.FrameCues)
                    if (c.Kind == CommentaryCueKind.Start) return clock - start;
            }
            return double.PositiveInfinity;
        }

        [Test]
        public void FastForward_ThenPlayOnTheRealClock_CommentaryCarriesOnAtOnce()
        {
            var s = Session(2, out _);
            double clock = 0.5; // a few frames of real time before the skip, as in a capture
            clock = s.FastForward(90 * s.Sim.Config.TickRate, clock);
            Assert.GreaterOrEqual(clock, 90.0, "the clock moved on with the skipped sim time");
            Assert.Less(FirstLineAfter(s, clock), 12.0, "commentary resumes within a lull after the skip");
        }

        [Test]
        public void StepOnceSkip_ThenTheOldRealClock_GoesQuiet_RegressionOfTheCaptureGlitch()
        {
            // What captures did before: skip with StepOnce (commentary stamped with sim time, ~90 s) and then carry on
            // with the real clock (~0.5 s). The engine sees time run backwards and stays silent far longer.
            var s = Session(2, out _);
            while (s.Sim.Tick < 90 * s.Sim.Config.TickRate) s.StepOnce(null);
            Assert.Greater(FirstLineAfter(s, 0.5), 15.0);
        }
    }
}
