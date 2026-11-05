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

        [Test]
        public void OwnGoal_BeatsContextCategories_AndNeverNamesTheDefenderAsScorer()
        {
            var bank = Shipped();
            for (uint seed = 1; seed < 40; seed++)
            {
                var e = new CommentaryEngine(bank, Quiet(), seed);
                e.Push(Goal(0, "Vance", 0, 1));
                // Equalising own goal in the 88th minute: equaliser and late contexts must not claim it.
                var og = Goal(100, "Hollis", 1, 1, 88);
                og.Situation = GoalSituation.OwnGoal;
                var u = Started(e.Push(og), CommentaryVoice.PlayByPlay);
                Assert.IsNotNull(u);
                Assert.AreEqual("goal.own_goal", u.Category.Id, "seed " + seed);
            }
            // Without an own-goal category, scorer lines that would name him are skipped.
            var json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"goal.equaliser\",\"event\":\"Goal\",\"context\":\"equaliser\",\"priority\":95,\"resolution\":true,\"lines\":[\"{name} levels it!\",\"All square!\"]}]}";
            var small = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNotNull(small, string.Join("\n", errors.ToArray()));
            for (uint seed = 1; seed < 20; seed++)
            {
                var e = new CommentaryEngine(small, Quiet(), seed);
                var og = Goal(0, "Hollis", 1, 1);
                og.Situation = GoalSituation.OwnGoal;
                Assert.AreEqual("All square!", Started(e.Push(og), CommentaryVoice.PlayByPlay).Transcript);
            }
        }

        [Test]
        public void Slug_NonAsciiNames_MatchTheManifestTool()
        {
            // Expected values are what tools/commentary/build_tts_manifest.py slug() returns for the same names.
            var cases = new Dictionary<string, string>
            {
                { "Ødegaard", "odegaard" }, { "Kadıoğlu", "kadioglu" }, { "Gyökeres", "gyokeres" },
                { "Martínez", "martinez" }, { "Milosavljević", "milosavljevic" }, { "Guimarães", "guimaraes" },
                { "Dele Okonkwo-Hart", "dele_okonkwo_hart" }, { "Brighton & Hove Albion", "brighton_hove_albion" },
                { "Łukasz Fabiański", "lukasz_fabianski" }, { "Müller", "muller" }
            };
            foreach (var kv in cases) Assert.AreEqual(kv.Value, RosterSchema.Slug(kv.Key), kv.Key);
            Assert.AreEqual("name.odegaard.excited", CommentaryClip.NameClipId("Ødegaard", CommentaryIntonation.Excited));
            Assert.AreEqual("team.brighton_hove_albion.neutral", CommentaryClip.TeamClipId("Brighton & Hove Albion", CommentaryIntonation.Neutral));
        }

        [Test]
        public void ShippedAudioIndex_WhenPresent_CoversEveryBundledClubAndPlayer()
        {
            string path = Path.Combine(RosterTestData.StreamingAssets, "Commentary", "audio", "en", "index.json");
            if (!File.Exists(path)) Assert.Ignore("no rendered audio in this checkout");
            var idx = CommentaryAudioIndex.Parse(File.ReadAllText(path), out var error);
            Assert.IsNotNull(idx, error);
            var problems = new List<string>();
            var missing = new List<string>();
            foreach (var league in RosterMatchBuilder.LoadLeagues(RosterTestData.StreamingAssets, null, problems))
                foreach (var club in league.Clubs)
                {
                    foreach (var voice in new[] { CommentaryVoice.PlayByPlay, CommentaryVoice.Analyst })
                        if (!idx.Has(voice, CommentaryClip.TeamClipId(club.Name, CommentaryIntonation.Neutral))) missing.Add(club.Name);
                    foreach (var p in club.Players)
                        if (!idx.Has(CommentaryVoice.PlayByPlay, CommentaryClip.NameClipId(p.KnownAs, CommentaryIntonation.Neutral)))
                            missing.Add(p.KnownAs);
                }
            Assert.IsEmpty(missing, "names without clips (re-run render_tts.py): " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void Session_SameSeed_SameCommentary_WithCatalog()
        {
            var bank = Shipped();
            var catalog = new FakeCatalog();
            string Run()
            {
                var session = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = 11, Config = new MatchSimConfig { HalfDurationSeconds = 40f } });
                session.AttachCommentaryEngine(new CommentaryEngine(bank, null, 5) { Catalog = catalog });
                var sb = new System.Text.StringBuilder();
                double t = 0;
                while (!session.IsFinished && t < 400)
                {
                    session.Advance(1.0 / 30.0, null, t);
                    foreach (var cue in session.FrameCues) sb.Append(cue.Kind).Append(' ').Append(cue.Utterance.Transcript).Append('\n');
                    t += 1.0 / 30.0;
                }
                return sb.ToString();
            }
            string a = Run(), b = Run();
            Assert.Greater(a.Length, 100);
            Assert.AreEqual(a, b);
        }

        /// <summary>"Gómez" -> "Gomez": the name with accents stripped (same sound, so sharing a clip is fine).</summary>
        private static string Unaccented(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Normalize(System.Text.NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            return sb.ToString().ToLowerInvariant();
        }

        [Test]
        public void BundledLeagues_NoTwoDifferentNamesInAFixtureShareANameClip()
        {
            // Two players can share a name clip only when they are spoken the same (spelling differs by accents alone,
            // e.g. Gomez and Gómez). Anything else would make the commentator say the wrong name in some fixture.
            var problems = new List<string>();
            var clashes = new List<string>();
            foreach (var league in RosterMatchBuilder.LoadLeagues(RosterTestData.StreamingAssets, null, problems))
            {
                var bySlug = new Dictionary<string, string>();
                foreach (var club in league.Clubs) // every fixture pairs two clubs of one league, so check league-wide
                    foreach (var p in club.Players)
                    {
                        string slug = RosterSchema.Slug(p.KnownAs);
                        if (bySlug.TryGetValue(slug, out var other) && Unaccented(other) != Unaccented(p.KnownAs))
                            clashes.Add(other + " / " + p.KnownAs + " -> " + slug);
                        else if (!bySlug.ContainsKey(slug)) bySlug[slug] = p.KnownAs;
                    }
            }
            Assert.IsEmpty(clashes, string.Join("; ", clashes.ToArray()));
        }

        [Test]
        public void AnalystOnAGoal_HoldsTheFloor_RestartCallWaits()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1, LullSeconds = 0 }, 8);
            e.Push(Ev(CommentaryEventType.KickOff, 0));
            var goal = Started(e.Push(Goal(30, "Okafor", 1, 0)), CommentaryVoice.PlayByPlay);
            var analyst = Started(e.Tick(goal.EndTime + e.Config.AnalystLeadIn + 0.01), CommentaryVoice.Analyst);
            Assert.IsNotNull(analyst);
            Assert.IsFalse(analyst.Interruptible);
            // Kick-off while he is talking: he is not cut, and the kick-off call is not started over him.
            var cues = e.Push(Ev(CommentaryEventType.KickOff, analyst.StartTime + 0.3));
            Assert.IsNull(Started(cues, CommentaryVoice.PlayByPlay));
            Assert.AreSame(analyst, e.CurrentAnalyst);
            // A shot (resolution) still breaks in.
            var shot = e.Push(Ev(CommentaryEventType.ShotTaken, analyst.StartTime + 0.6));
            Assert.IsNotNull(Started(shot, CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void PresentationClock_UsesCaptureFrameTime_WhenCapturing()
        {
            // A capture renders slowly (2 s of wall time per frame) but plays back at 30 fps: commentary must follow the video.
            Assert.AreEqual(1.0 / 30, PresentationClock.FrameSeconds(1f / 30, 2f), 1e-6);
            Assert.AreEqual(0.016, PresentationClock.FrameSeconds(0f, 0.016f), 1e-6);
            Assert.AreEqual(0.0, PresentationClock.FrameSeconds(0f, -1f));
        }

        [Test]
        public void Session_SixtySecondsOfPlay_BroadcastPacing_OneVoiceAtATime()
        {
            // 60 s of AI v AI on the presentation clock, timed from the shipped clip index when present.
            var bank = Shipped();
            string path = Path.Combine(RosterTestData.StreamingAssets, "Commentary", "audio", "en", "index.json");
            ICommentaryClipCatalog catalog = File.Exists(path) ? CommentaryAudioIndex.Parse(File.ReadAllText(path), out _) : null;
            foreach (ulong seed in new ulong[] { 1, 7, 21 })
            {
                var session = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = seed });
                var engine = new CommentaryEngine(bank, null, (uint)seed | 1u) { Catalog = catalog };
                session.AttachCommentaryEngine(engine);
                var spoken = new List<double[]>(); // start, end (cut lines end at their Stop)
                var open = new Dictionary<CommentaryUtterance, int>();
                const double dt = 1.0 / 30.0;
                double t = 0;
                int starts = 0;
                for (int frame = 0; frame < 60 * 30; frame++, t += dt)
                {
                    session.Advance(dt, null, t);
                    foreach (var cue in session.FrameCues)
                    {
                        var u = cue.Utterance;
                        if (cue.Kind == CommentaryCueKind.Start)
                        {
                            starts++;
                            open[u] = spoken.Count;
                            spoken.Add(new[] { u.StartTime, u.EndTime });
                        }
                        else if (open.TryGetValue(u, out int k)) spoken[k][1] = System.Math.Min(spoken[k][1], t);
                    }
                }
                Assert.LessOrEqual(starts, 25, "seed " + seed + ": too many lines for a minute of broadcast");
                Assert.GreaterOrEqual(starts, 6, "seed " + seed + ": the commentators should not go silent");
                spoken.Sort((a, b) => a[0].CompareTo(b[0]));
                for (int i = 1; i < spoken.Count; i++)
                    Assert.GreaterOrEqual(spoken[i][0] + 1e-6, spoken[i - 1][1],
                        "seed " + seed + ": two voices at once at t=" + spoken[i][0].ToString("F2"));
            }
        }
    }
}
