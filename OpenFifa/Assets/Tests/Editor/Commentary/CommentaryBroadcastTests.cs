using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Broadcast-feel behaviour: name calls, the strike before the outcome, braces and hat-tricks, "{team}" lines,
    /// quiet-spell chatter, live analyst reactions and the rendered-clip catalog.
    /// </summary>
    [TestFixture]
    [Category("FID_COMMENTARY")]
    public class CommentaryBroadcastTests
    {
        private static CommentaryLineBank Shipped()
        {
            var bank = CommentaryLineBank.Load(File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            return bank;
        }

        private static CommentaryEvent Ev(CommentaryEventType t, double time, string name = "Okafor", int minute = 30,
            float crowd = 0.3f)
        {
            return new CommentaryEvent
            {
                Type = t, Time = time, Minute = minute, PlayerName = name, PlayerId = name, TeamName = "Kingsmere United",
                CrowdIntensity = crowd
            };
        }

        private static CommentaryEvent Goal(double time, string scorer, int home, int away, int minute = 30)
        {
            var e = Ev(CommentaryEventType.Goal, time, scorer, minute, 0.9f);
            e.HomeScore = home;
            e.AwayScore = away;
            e.ShotQuality = 0.4f;
            e.DistanceMetres = 11;
            return e;
        }

        private static CommentaryUtterance Started(IReadOnlyList<CommentaryCue> cues, CommentaryVoice voice)
        {
            foreach (var c in cues)
                if (c.Kind == CommentaryCueKind.Start && c.Utterance.Voice == voice) return c.Utterance;
            return null;
        }

        private static CommentaryConfig Quiet() => new CommentaryConfig { AnalystChance = 0, LullSeconds = 0 };

        [Test]
        public void OnTheBall_BareNameCall_PlaysOnlyTheNameClip()
        {
            const string json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"otb\",\"event\":\"OnTheBall\",\"priority\":12,\"lines\":[\"{name}...\"]}]}";
            var bank = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            Assert.IsTrue(bank.Find("otb").Lines[0].NameOnly);
            var e = new CommentaryEngine(bank, Quiet(), 1);
            var u = Started(e.Push(Ev(CommentaryEventType.OnTheBall, 1)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(u);
            Assert.AreEqual(1, u.Clips.Count);
            Assert.AreEqual("name.okafor.neutral", u.Clips[0].ClipId);
            Assert.AreEqual("Okafor", u.Transcript);
        }

        [Test]
        public void SplitName_TeamToken_AndBareCallRules()
        {
            Assert.IsTrue(CommentaryLineBank.SplitName("{team} are level!", out var t, out var p, out var k, out _));
            Assert.AreEqual("are level!", t);
            Assert.AreEqual(CommentaryNamePlacement.Before, p);
            Assert.AreEqual(CommentaryNameKind.Team, k);
            Assert.IsFalse(CommentaryLineBank.SplitName("{team}!", out _, out _, out _, out _), "a bare call is a player");
            Assert.IsFalse(CommentaryLineBank.SplitName("{name} and {team}", out _, out _, out _, out _));
            Assert.IsTrue(CommentaryLineBank.SplitName("{name}...", out var bare, out _, out _, out _));
            Assert.AreEqual("", bare);
        }

        [Test]
        public void TeamLines_StitchTheClubClip_AndAnalystMayUseThem()
        {
            const string json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"c\",\"event\":\"Corner\",\"priority\":40,\"lines\":[\"Corner to {team}.\"]}," +
                "{\"id\":\"a\",\"event\":\"Corner\",\"voice\":\"analyst\",\"lines\":[\"{team} look dangerous from these.\"]}]}";
            var bank = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            var e = new CommentaryEngine(bank, new CommentaryConfig { AnalystChance = 1, LullSeconds = 0 }, 1);
            var lead = Started(e.Push(Ev(CommentaryEventType.Corner, 10)), CommentaryVoice.PlayByPlay);
            Assert.AreEqual("team.kingsmere_united.neutral", lead.Clips[1].ClipId);
            var analyst = Started(e.Tick(lead.EndTime + 1), CommentaryVoice.Analyst);
            Assert.IsNotNull(analyst);
            Assert.AreEqual("team.kingsmere_united.neutral", analyst.Clips[0].ClipId);
            Assert.AreEqual(CommentaryVoice.Analyst, analyst.Clips[0].Voice);
        }

        [Test]
        public void Goals_SecondAndThirdForTheSamePlayer_AreBraceThenHatTrick()
        {
            var e = new CommentaryEngine(Shipped(), Quiet(), 4);
            Assert.AreEqual("goal.open_play", Started(e.Push(Goal(0, "Okafor", 1, 0)), CommentaryVoice.PlayByPlay).Category.Id);
            Assert.AreEqual("goal.open_play", Started(e.Push(Goal(100, "Vance", 2, 0)), CommentaryVoice.PlayByPlay).Category.Id);
            var brace = Goal(200, "Okafor", 3, 0);
            Assert.AreEqual("goal.brace", Started(e.Push(brace), CommentaryVoice.PlayByPlay).Category.Id);
            Assert.AreEqual(2, brace.PlayerGoals);
            Assert.AreEqual("goal.hat_trick", Started(e.Push(Goal(300, "Okafor", 4, 0)), CommentaryVoice.PlayByPlay).Category.Id);
            // Own goals never count towards a tally.
            var og = Goal(400, "Okafor", 5, 0);
            og.Situation = GoalSituation.OwnGoal;
            e.Push(og);
            Assert.AreEqual(0, og.PlayerGoals);
        }

        [Test]
        public void Goal_InTheOpeningMinutes_IsEarlyGoal()
        {
            var e = new CommentaryEngine(Shipped(), Quiet(), 4);
            Assert.AreEqual("goal.early_goal", Started(e.Push(Goal(0, "Okafor", 1, 0, 2)), CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void ShotTaken_ThenSave_StrikeFinishesWithinGrace_ElseIsCut()
        {
            var e = new CommentaryEngine(Shipped(), Quiet(), 2);
            var shot = Started(e.Push(Ev(CommentaryEventType.ShotTaken, 10)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(shot);
            Assert.AreEqual("shot.taken", shot.Category.Id);

            // The save lands just before the strike call ends: it waits, then plays.
            var saveAt = shot.EndTime - e.Config.InterruptGrace * 0.5;
            var cues = e.Push(Ev(CommentaryEventType.ShotSaved, saveAt));
            Assert.IsNull(Started(cues, CommentaryVoice.PlayByPlay), "within grace: queued, not cut");
            var save = Started(e.Tick(shot.EndTime + 0.01), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(save);
            Assert.AreEqual(CommentaryEventType.ShotSaved, save.Category.Event);

            // Long strike call, early outcome: cut.
            var e2 = new CommentaryEngine(Shipped(), Quiet(), 2);
            var shot2 = Started(e2.Push(Ev(CommentaryEventType.ShotTaken, 10)), CommentaryVoice.PlayByPlay);
            var cues2 = e2.Push(Ev(CommentaryEventType.ShotSaved, 10.05));
            if (shot2.EndTime - 10.05 > e2.Config.InterruptGrace)
                Assert.IsNotNull(Started(cues2, CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void LateChance_UsesLateChanceLines()
        {
            var e = new CommentaryEngine(Shipped(), Quiet(), 2);
            var ev = Ev(CommentaryEventType.ShotWide, 10, "Okafor", 88);
            ev.HomeScore = 0;
            ev.AwayScore = 1;
            Assert.AreEqual("shot.wide.late_chance", Started(e.Push(ev), CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void Lull_AfterQuietSpellInLivePlay_SomeoneSpeaks_NotDuringStoppages()
        {
            var cfg = new CommentaryConfig { AnalystChance = 0, LullSeconds = 10, AnalystLullShare = 0 };
            var e = new CommentaryEngine(Shipped(), cfg, 6);
            var ko = Started(e.Push(Ev(CommentaryEventType.KickOff, 0)), CommentaryVoice.PlayByPlay);
            Assert.IsNull(Started(e.Tick(ko.EndTime + 9.5), CommentaryVoice.PlayByPlay));
            var lull = Started(e.Tick(ko.EndTime + 10.1), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(lull);
            Assert.AreEqual("lull.goalless", lull.Category.Id);

            var e2 = new CommentaryEngine(Shipped(), cfg, 6);
            var foul = Started(e2.Push(Ev(CommentaryEventType.Foul, 0)), CommentaryVoice.PlayByPlay);
            for (double t = foul.EndTime; t < foul.EndTime + 60; t += 0.5)
                Assert.IsNull(Started(e2.Tick(t), CommentaryVoice.PlayByPlay), "no filler at a stoppage");
        }

        [Test]
        public void Lull_CanGoToTheAnalyst_WithMatchSituationLines()
        {
            var cfg = new CommentaryConfig { AnalystChance = 0, LullSeconds = 10, AnalystLullShare = 1, AnalystMinGap = 0 };
            var e = new CommentaryEngine(Shipped(), cfg, 6);
            var ev = Ev(CommentaryEventType.KickOff, 0, null, 80);
            ev.HomeScore = 1;
            var ko = Started(e.Push(ev), CommentaryVoice.PlayByPlay);
            var u = Started(e.Tick(ko.EndTime + 10.1), CommentaryVoice.Analyst);
            Assert.IsNotNull(u);
            Assert.AreEqual("analyst.lull.late_on", u.Category.Id);
        }

        [Test]
        public void Analyst_ReactsToASaveInLivePlay_WhenTheLeadGoesQuiet()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1, LullSeconds = 0 }, 3);
            e.Push(Ev(CommentaryEventType.KickOff, 0));
            var save = Started(e.Push(Ev(CommentaryEventType.ShotSaved, 20)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(save);
            Assert.IsFalse(e.IsStoppage);
            Assert.IsNull(Started(e.Tick(save.EndTime + e.Config.AnalystLiveQuiet - 0.1), CommentaryVoice.Analyst));
            var a = Started(e.Tick(save.EndTime + e.Config.AnalystLiveQuiet + 0.05), CommentaryVoice.Analyst);
            Assert.IsNotNull(a);
            Assert.AreEqual("analyst.save", a.Category.Id);
        }

        [Test]
        public void NameCalls_NeverCutTheAnalyst()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1, LullSeconds = 0 }, 5);
            var lead = Started(e.Push(Ev(CommentaryEventType.Foul, 100)), CommentaryVoice.PlayByPlay);
            var analyst = Started(e.Tick(lead.EndTime + 1), CommentaryVoice.Analyst);
            Assert.IsNotNull(analyst);
            var cues = e.Push(Ev(CommentaryEventType.OnTheBall, analyst.StartTime + 0.3, "Vance"));
            Assert.IsNull(Started(cues, CommentaryVoice.PlayByPlay));
            Assert.AreSame(analyst, e.CurrentAnalyst);
        }

        private sealed class FakeCatalog : ICommentaryClipCatalog
        {
            public readonly HashSet<string> Missing = new HashSet<string>();
            public bool Has(CommentaryVoice voice, string clipId) => !Missing.Contains(clipId);
            public double Duration(CommentaryVoice voice, string clipId) => clipId.StartsWith("name.") ? 0.7 : 2.0;
        }

        [Test]
        public void Catalog_MissingNameClip_FallsBackToNamelessLines_AndTimesFromClipLengths()
        {
            const string json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"g\",\"event\":\"Goal\",\"priority\":90,\"resolution\":true,\"lines\":[\"{name} scores!\",\"Goal!\"]}]}";
            var bank = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            var cat = new FakeCatalog();
            cat.Missing.Add("name.newboy.neutral");
            for (int seed = 1; seed < 20; seed++)
            {
                var e = new CommentaryEngine(bank, Quiet(), (uint)seed) { Catalog = cat };
                var u = Started(e.Push(Goal(0, "Newboy", 1, 0)), CommentaryVoice.PlayByPlay);
                Assert.AreEqual("line.g.02", u.Line.ClipId, "the name was never rendered");
                Assert.AreEqual(2.0, u.EndTime - u.StartTime, 1e-9);
            }
            var e2 = new CommentaryEngine(bank, Quiet(), 1) { Catalog = new FakeCatalog() };
            var named = Started(e2.Push(Goal(0, "Okafor", 1, 0)), CommentaryVoice.PlayByPlay);
            if (named.Line.Name != CommentaryNamePlacement.None)
                Assert.AreEqual(0.7 + e2.Config.ClipGap + 2.0, named.EndTime - named.StartTime, 1e-9);
        }

        [Test]
        public void ShippedBank_EveryContextHasLines_AndNewEventsAreCovered()
        {
            var bank = Shipped();
            foreach (var ctx in CommentaryEngine.Contexts)
            {
                bool found = false;
                foreach (var c in bank.Categories) found |= c.Context == ctx;
                Assert.IsTrue(found, ctx);
            }
            Assert.Greater(bank.For(CommentaryEventType.OnTheBall, CommentaryVoice.PlayByPlay).Count, 0);
            Assert.Greater(bank.For(CommentaryEventType.ShotTaken, CommentaryVoice.PlayByPlay).Count, 0);
            Assert.Greater(bank.For(CommentaryEventType.Lull, CommentaryVoice.Analyst).Count, 0);
            foreach (var c in bank.For(CommentaryEventType.Lull, CommentaryVoice.Analyst)) Assert.IsTrue(c.Live, c.Id);
        }

        [Test]
        public void AudioIndex_Parse_ReadsBothVoicesAndLengths()
        {
            const string json = "{\"format\": \"openfootball-commentary-audio\", \"schemaVersion\": 1, \"sampleRate\": 24000, \"voices\": {" +
                "\"lead\": {\"line.goal.open_play.01\": [1.25, \"abc\"], \"name.okafor.excited\": [0.6, \"def\"]}," +
                "\"analyst\": {\"line.analyst.save.01\": [2.5, \"123\"]}}}";
            var idx = CommentaryAudioIndex.Parse(json, out var error);
            Assert.IsNotNull(idx, error);
            Assert.AreEqual(3, idx.Count);
            Assert.IsTrue(idx.Has(CommentaryVoice.PlayByPlay, "name.okafor.excited"));
            Assert.IsFalse(idx.Has(CommentaryVoice.Analyst, "name.okafor.excited"));
            Assert.AreEqual(2.5, idx.Duration(CommentaryVoice.Analyst, "line.analyst.save.01"), 1e-9);
            Assert.AreEqual(0, idx.Duration(CommentaryVoice.PlayByPlay, "missing"));
            Assert.AreEqual("analyst/line.analyst.save.01.ogg", CommentaryAudioIndex.RelativePath(CommentaryVoice.Analyst, "line.analyst.save.01"));
            Assert.IsNull(CommentaryAudioIndex.Parse("{\"format\": \"other\"}", out _));
        }
    }
}
