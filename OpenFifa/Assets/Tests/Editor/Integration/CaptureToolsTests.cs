using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>
    /// Engine-free helpers of the single-player review capture: clock and commentary text, club selection from the
    /// command line, highlight picking for frame sequences, and the headless tactics summary.
    /// </summary>
    [TestFixture]
    [Category("FID_INTEGRATION")] // Unity NUnit rejects "-" in category names
    public class CaptureToolsTests
    {
        // ------------------------------------------------------------------ text

        [Test]
        public void Clock_HalfwayThroughFirstHalf_Shows22m30()
        {
            Assert.AreEqual("22:30.0", CaptureText.Clock(180f, 1, 360f));
        }

        [Test]
        public void Clock_SecondHalfStart_Shows45m()
        {
            Assert.AreEqual("45:00.0", CaptureText.Clock(0f, 2, 360f));
        }

        [Test]
        public void Clock_JustBeforeAMinute_TruncatesTenthsNotSixty()
        {
            // 0.9999 of a 45-minute half: 44:59.9, never 44:60.0.
            Assert.AreEqual("44:59.9", CaptureText.Clock(359.999f, 1, 360f));
            Assert.AreEqual("00:07.5", CaptureText.Clock(1f, 1, 360f));
        }

        [Test]
        public void CommentaryLine_ClipSeparators_JoinedIntoFourFields()
        {
            string line = CaptureText.CommentaryLine("12:03.4", CommentaryEventType.Goal, CommentaryVoice.PlayByPlay, "Saka | sweeps it in!");
            Assert.AreEqual("12:03.4 | Goal | play-by-play | Saka sweeps it in!", line);
            Assert.AreEqual(4, line.Split('|').Length);
            StringAssert.Contains("| analyst |", CaptureText.CommentaryLine("00:00.0", CommentaryEventType.Foul, CommentaryVoice.Analyst, "x"));
        }

        // ------------------------------------------------------------------ club selection

        private static RosterClub Club(string id) =>
            new RosterClub(id, id.ToUpperInvariant(), id.Substring(0, 3).ToUpperInvariant(), "#ff0000", "#ffffff", "#0000ff", "Ground", 1000, "4-4-2", 3, null);

        private static List<RosterLeague> Leagues() => new List<RosterLeague>
        {
            new RosterLeague(1, "alpha", "Alpha", "ALP", "X", "2026/27", "test", "", true, new[] { Club("aaa"), Club("bbb"), Club("ccc") }),
            new RosterLeague(1, "beta", "Beta", "BET", "X", "2026/27", "test", "", true, new[] { Club("ddd"), Club("eee") }),
        };

        [Test]
        public void TryResolve_HomeAndAwayIds_SelectsLeagueAndIndices()
        {
            int l = 0, h = 0, a = 1;
            var problems = new List<string>();
            Assert.IsTrue(RosterSelection.TryResolve(Leagues(), null, "EEE", "ddd", ref l, ref h, ref a, problems), string.Join(";", problems));
            Assert.AreEqual(1, l);
            Assert.AreEqual(1, h);
            Assert.AreEqual(0, a);
        }

        [Test]
        public void TryResolve_ClubsFromDifferentLeagues_FailsAndKeepsIndices()
        {
            int l = 0, h = 0, a = 1;
            var problems = new List<string>();
            Assert.IsFalse(RosterSelection.TryResolve(Leagues(), null, "aaa", "ddd", ref l, ref h, ref a, problems));
            Assert.AreEqual(0, l);
            Assert.AreEqual(0, h);
            Assert.AreEqual(1, a);
            StringAssert.Contains("beta", problems[0]);
        }

        [Test]
        public void TryResolve_OnlyHomeCollidesWithDefaultAway_MovesAway()
        {
            int l = 0, h = 0, a = 1;
            Assert.IsTrue(RosterSelection.TryResolve(Leagues(), null, "bbb", null, ref l, ref h, ref a, null));
            Assert.AreEqual(1, h);
            Assert.AreEqual(2, a);
        }

        [Test]
        public void TryResolve_UnknownIdOrSameClub_Fails()
        {
            int l = 0, h = 0, a = 1;
            Assert.IsFalse(RosterSelection.TryResolve(Leagues(), null, "zzz", null, ref l, ref h, ref a, null));
            Assert.IsFalse(RosterSelection.TryResolve(Leagues(), null, "aaa", "aaa", ref l, ref h, ref a, null));
            Assert.IsFalse(RosterSelection.TryResolve(Leagues(), "gamma", null, null, ref l, ref h, ref a, null));
            Assert.IsTrue(RosterSelection.TryResolve(Leagues(), null, null, null, ref l, ref h, ref a, null));
            StringAssert.Contains("league beta (Beta, 2 clubs): ddd, eee", RosterSelection.Describe(Leagues()));
        }

        // ------------------------------------------------------------------ highlights

        private static CaptureHighlight H(CaptureHighlightKind k, int tick, float metric) =>
            new CaptureHighlight { Kind = k, Tick = tick, Metric = metric, PlayerId = 3, OtherPlayerId = -1, Half = 1 };

        [Test]
        public void Pick_OnePerCategoryInWindow_BestMetricNoOverlapTickOrder()
        {
            var all = new List<CaptureHighlight>
            {
                H(CaptureHighlightKind.Shot, 500, 0.3f),
                H(CaptureHighlightKind.LongPass, 200, 30f),
                H(CaptureHighlightKind.LongPass, 520, 50f),   // longest but overlaps the shot at 500? picked first, so the shot is dropped
                H(CaptureHighlightKind.Shot, 900, 0.1f),
                H(CaptureHighlightKind.Roll, 300, 3f),
                H(CaptureHighlightKind.Bounce, 1100, 4f),     // bounce beats roll
                H(CaptureHighlightKind.LongPass, 5000, 80f),  // outside the window
                H(CaptureHighlightKind.Bounce, 1190, 9f),     // its sequence would run past the window end
            };
            var picked = new List<CaptureHighlight>();
            CaptureHighlights.Pick(all, 100, 1200, 30, 30, 3, picked);
            Assert.AreEqual(3, picked.Count);
            Assert.AreEqual(520, picked[0].Tick); // long pass 50 m
            Assert.AreEqual(900, picked[1].Tick); // the 500 shot overlaps the pass
            Assert.AreEqual(1100, picked[2].Tick);
            Assert.AreEqual(CaptureHighlightKind.Bounce, picked[2].Kind);
        }

        [Test]
        public void Pick_MissingCategory_FillsWithOtherMoments()
        {
            var all = new List<CaptureHighlight>
            {
                H(CaptureHighlightKind.LongPass, 200, 30f),
                H(CaptureHighlightKind.LongPass, 400, 26f),
                H(CaptureHighlightKind.Goal, 600, 20f),
            };
            var picked = new List<CaptureHighlight>();
            CaptureHighlights.Pick(all, 0, 2000, 30, 30, 3, picked);
            Assert.AreEqual(3, picked.Count);
            Assert.AreEqual(200, picked[0].Tick);
            Assert.AreEqual(400, picked[1].Tick);
            Assert.AreEqual(CaptureHighlightKind.Goal, picked[2].Kind);
        }

        [Test]
        public void Describe_LongPass_NamesPasserAndReceiver()
        {
            var h = new CaptureHighlight { Kind = CaptureHighlightKind.LongPass, Tick = 3029, Half = 1, Clock = 50.5f, PlayerId = 12, OtherPlayerId = 20, Metric = 56.5f, Action = IntentAction.Lob };
            var names = new string[22];
            names[12] = "Estevao";
            names[20] = "Rogers";
            Assert.AreEqual("seqA | LongPass 56.5 m (Lob) | tick 3029 | 06:18.7 | Estevao (CHE) -> Rogers (CHE)",
                CaptureHighlights.Describe("seqA", h, 360f, names, "ARS", "CHE"));
        }

        [Test]
        public void Observe_AiMatch_FindsPassesAndBallContacts()
        {
            var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = 3, Config = new MatchSimConfig { HalfDurationSeconds = 360f } });
            s.HumanSlots[0].Input = null; // team AI on both sides
            var hl = new CaptureHighlights();
            int ticks = s.Sim.Config.SecondsToTicks(240f);
            for (int i = 0; i < ticks && !s.IsFinished; i++) hl.Observe(s.Sim, s.StepOnce(null));
            int passes = 0, contacts = 0;
            foreach (var h in hl.All)
            {
                if (h.Kind == CaptureHighlightKind.LongPass) passes++;
                if (h.Kind == CaptureHighlightKind.Bounce || h.Kind == CaptureHighlightKind.Roll) contacts++;
                Assert.Less(h.Tick, ticks + 1);
            }
            Assert.Greater(passes, 0);
            Assert.Greater(contacts, 0);
        }

        // ------------------------------------------------------------------ tactics

        [Test]
        public void InPressZone_OppositionOwnSixtyPercent_FlipsAtHalfTime()
        {
            var pitch = PitchSpec.Standard; // 105 m: the zone starts 42 m from the presser's own goal line (Z = -10.5 for Home, 1st half)
            Assert.IsTrue(MatchTacticsStats.InPressZone(SimTeam.Home, 1, new SimVec3(0f, 0f, -10f), pitch));
            Assert.IsFalse(MatchTacticsStats.InPressZone(SimTeam.Home, 1, new SimVec3(0f, 0f, -11f), pitch));
            Assert.IsTrue(MatchTacticsStats.InPressZone(SimTeam.Home, 2, new SimVec3(0f, 0f, 10f), pitch));
            Assert.IsTrue(MatchTacticsStats.InPressZone(SimTeam.Away, 1, new SimVec3(0f, 0f, 10f), pitch));
        }

        [Test]
        public void Observe_SyntheticEvents_CountsPassesShotsAndPpda()
        {
            var sim = new MatchSim(new MatchSimConfig(), MatchSetup.CreateDefault(1));
            var stats = new MatchTacticsStats();
            var ev = new List<MatchEvent>
            {
                // Away passes deep in their own half (1st half: Away defends +Z): inside Home's press zone.
                new MatchEvent { Type = MatchEventType.Pass, Team = SimTeam.Away, Half = 1, Position = new SimVec3(0f, 0f, 40f) },
                new MatchEvent { Type = MatchEventType.PassCompleted, Team = SimTeam.Away, Half = 1, Position = new SimVec3(0f, 0f, 35f) },
                new MatchEvent { Type = MatchEventType.Pass, Team = SimTeam.Away, Half = 1, Position = new SimVec3(0f, 0f, 30f) },
                new MatchEvent { Type = MatchEventType.Interception, Team = SimTeam.Home, Half = 1, Position = new SimVec3(0f, 0f, 25f) },
                new MatchEvent { Type = MatchEventType.Shot, Team = SimTeam.Home, Half = 1, Value = 0.25f, Position = new SimVec3(0f, 0f, 40f) },
                new MatchEvent { Type = MatchEventType.Save, Team = SimTeam.Away, Half = 1 },
                new MatchEvent { Type = MatchEventType.Save, Team = SimTeam.Away, Half = 1 }, // second handling of the same shot
            };
            stats.Observe(sim, ev);
            Assert.AreEqual(2, stats.Away.Passes);
            Assert.AreEqual(1, stats.Away.PassesCompleted);
            Assert.AreEqual(0.5f, stats.Away.PassCompletion, 1e-6f);
            Assert.AreEqual(1, stats.Home.Shots);
            Assert.AreEqual(1, stats.Home.ShotsOnTarget);
            Assert.AreEqual(0.25f, stats.Home.ExpectedGoals, 1e-6f);
            Assert.AreEqual(2, stats.Home.OppositionPassesInPressZone);
            Assert.AreEqual(1, stats.Home.DefensiveActionsInPressZone);
            Assert.AreEqual(2f, stats.Home.Ppda, 1e-6f);
            Assert.Less(stats.Away.Ppda, 0f); // no away defensive actions: undefined
            StringAssert.Contains("n/a", stats.Format("HOM", "AWY"));
        }

        [Test]
        public void Observe_AiMatch_ShapeAndPossessionArePlausible()
        {
            var s = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = 5, Config = new MatchSimConfig { HalfDurationSeconds = 360f } });
            s.HumanSlots[0].Input = null;
            var stats = new MatchTacticsStats(s.Sim.Config.TickRate / 2);
            int ticks = s.Sim.Config.SecondsToTicks(180f);
            for (int i = 0; i < ticks && !s.IsFinished; i++) stats.Observe(s.Sim, s.StepOnce(null));
            Assert.AreEqual(1f, stats.Possession(SimTeam.Home) + stats.Possession(SimTeam.Away), 1e-5f);
            foreach (var t in new[] { SimTeam.Home, SimTeam.Away })
            {
                var x = stats[t];
                Assert.Greater(x.Passes, 0);
                Assert.Greater(x.Samples, 0);
                Assert.That(x.Width, Is.InRange(10f, 68f));
                Assert.That(x.Depth, Is.InRange(5f, 105f));
                Assert.That(x.LineHeight, Is.InRange(1f, 70f));
                Assert.That(x.PassCompletion, Is.InRange(0f, 1f));
            }
        }
    }
}
