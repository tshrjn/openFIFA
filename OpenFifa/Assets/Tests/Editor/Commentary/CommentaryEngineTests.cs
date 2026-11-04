using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_COMMENTARY")]
    public class CommentaryEngineTests
    {
        private static CommentaryLineBank Shipped()
        {
            var bank = CommentaryLineBank.Load(File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            return bank;
        }

        private static CommentaryConfig NoAnalyst() => new CommentaryConfig { AnalystChance = 0 };

        private static CommentaryEvent Ev(CommentaryEventType t, double time, string name = null, float crowd = 0.3f)
        {
            return new CommentaryEvent { Type = t, Time = time, Minute = 30, PlayerName = name, CrowdIntensity = crowd };
        }

        private static CommentaryEvent Goal(double time, int team, int home, int away, int minute = 30, string name = "Okafor")
        {
            return new CommentaryEvent
            {
                Type = CommentaryEventType.Goal, Time = time, Minute = minute, Team = team, HomeScore = home,
                AwayScore = away, PlayerName = name, ShotQuality = 0.4f, DistanceMetres = 11, CrowdIntensity = 0.9f
            };
        }

        private static CommentaryUtterance Started(List<CommentaryCue> cues, CommentaryVoice voice)
        {
            foreach (var c in cues)
                if (c.Kind == CommentaryCueKind.Start && c.Utterance.Voice == voice) return c.Utterance;
            return null;
        }

        private static bool Stopped(List<CommentaryCue> cues, CommentaryVoice voice)
        {
            foreach (var c in cues)
                if (c.Kind == CommentaryCueKind.Stop && c.Utterance.Voice == voice) return true;
            return false;
        }

        [Test]
        public void Push_HigherPriorityResolution_InterruptsBuildUp()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 7);
            var first = Started(e.Push(Ev(CommentaryEventType.PassSequence, 0)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(first);
            Assert.AreEqual("pass_sequence", first.Category.Id);
            var cues = e.Push(Ev(CommentaryEventType.ShotSaved, 0.5));
            Assert.IsTrue(Stopped(cues, CommentaryVoice.PlayByPlay));
            Assert.AreEqual("shot.saved", Started(cues, CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void Push_DuringNonInterruptibleGoal_DoesNotCutIt()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 7);
            var goal = Started(e.Push(Goal(0, 0, 1, 0)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(goal);
            Assert.IsFalse(goal.Interruptible);
            var cues = e.Push(Ev(CommentaryEventType.RedCard, 0.3, "Vance", 0.9f));
            Assert.IsFalse(Stopped(cues, CommentaryVoice.PlayByPlay));
            Assert.AreSame(goal, e.CurrentPlayByPlay);
        }

        [Test]
        public void Push_LowerPriorityWhileTalking_QueuedThenPlayedOrDropped()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 7);
            var saved = Started(e.Push(Ev(CommentaryEventType.ShotSaved, 0, "Okafor", 0.5f)), CommentaryVoice.PlayByPlay);
            Assert.AreEqual(0, Started(e.Push(Ev(CommentaryEventType.Corner, 0.2)), CommentaryVoice.PlayByPlay) == null ? 0 : 1);
            var later = Started(e.Tick(saved.EndTime + 0.01), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(later, "queued corner should play once the lead voice is free");
            Assert.AreEqual("corner", later.Category.Id);

            // A queued line older than MaxQueueDelay is dropped.
            var e2 = new CommentaryEngine(Shipped(), NoAnalyst(), 7);
            var goal = Started(e2.Push(Goal(0, 0, 1, 0)), CommentaryVoice.PlayByPlay);
            e2.Push(Ev(CommentaryEventType.Tackle, 0.1));
            Assert.IsNull(Started(e2.Tick(goal.EndTime + e2.Config.MaxQueueDelay + 1), CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void Push_SameEventRepeatedly_NoLineRepeatsUntilBankExhausted()
        {
            var bank = Shipped();
            int count = bank.Find("corner").Lines.Count;
            var e = new CommentaryEngine(bank, NoAnalyst(), 3);
            var seen = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                var u = Started(e.Push(Ev(CommentaryEventType.Corner, i * 10.0)), CommentaryVoice.PlayByPlay);
                Assert.IsNotNull(u, "corner " + i);
                Assert.IsTrue(seen.Add(u.Line.Id), "repeated " + u.Line.Id + " after " + i);
            }
            // Bank exhausted: next pick is the least recently used line, never the one just played.
            var last = Started(e.Push(Ev(CommentaryEventType.Corner, count * 10.0)), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(last);
        }

        [Test]
        public void Push_WithinCooldown_CategorySkipped()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 3);
            Assert.IsNotNull(Started(e.Push(Ev(CommentaryEventType.PassSequence, 0, "Okafor")), CommentaryVoice.PlayByPlay));
            Assert.IsNull(Started(e.Push(Ev(CommentaryEventType.PassSequence, 5, "Okafor")), CommentaryVoice.PlayByPlay));
            Assert.IsNotNull(Started(e.Push(Ev(CommentaryEventType.PassSequence, 30, "Okafor")), CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void Push_LoudCrowd_GatesBuildUpButNotResolution()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 3);
            Assert.IsNull(Started(e.Push(Ev(CommentaryEventType.PassSequence, 0, "Okafor", 0.95f)), CommentaryVoice.PlayByPlay));
            Assert.IsNotNull(Started(e.Push(Ev(CommentaryEventType.ShotWide, 1, "Okafor", 0.95f)), CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void Analyst_AfterStoppage_SpeaksOnceLeadIsSilent()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1 }, 5);
            var lead = Started(e.Push(Ev(CommentaryEventType.Foul, 100, "Vance")), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(lead);
            Assert.IsTrue(e.IsStoppage);
            Assert.IsNull(Started(e.Tick(lead.EndTime - 0.1), CommentaryVoice.Analyst), "never over the lead voice");
            var analyst = Started(e.Tick(lead.EndTime + e.Config.AnalystLeadIn + 0.01), CommentaryVoice.Analyst);
            Assert.IsNotNull(analyst);
            Assert.AreEqual(CommentaryEventType.Foul, analyst.Category.Event);
        }

        [Test]
        public void Analyst_PlayResumes_CutAndQueueCleared()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1 }, 5);
            var lead = Started(e.Push(Ev(CommentaryEventType.Foul, 100, "Vance")), CommentaryVoice.PlayByPlay);
            var analyst = Started(e.Tick(lead.EndTime + 1), CommentaryVoice.Analyst);
            Assert.IsNotNull(analyst);
            var cues = e.Push(Ev(CommentaryEventType.PlayResumed, analyst.StartTime + 0.5));
            Assert.IsTrue(Stopped(cues, CommentaryVoice.Analyst));
            Assert.IsFalse(e.IsStoppage);
            Assert.IsNull(e.CurrentAnalyst);

            // Foul then immediate restart: the queued analyst line never plays.
            var e2 = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1 }, 5);
            var lead2 = Started(e2.Push(Ev(CommentaryEventType.Foul, 100, "Vance")), CommentaryVoice.PlayByPlay);
            e2.Push(Ev(CommentaryEventType.PlayResumed, lead2.EndTime - 0.1));
            for (double t = lead2.EndTime; t < lead2.EndTime + 20; t += 0.25)
                Assert.IsNull(Started(e2.Tick(t), CommentaryVoice.Analyst));
        }

        [Test]
        public void Analyst_DuringLivePlay_NeverSpeaks()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1 }, 9);
            e.Push(Ev(CommentaryEventType.KickOff, 0));
            var types = new[] { CommentaryEventType.PassSequence, CommentaryEventType.ThroughBall, CommentaryEventType.Tackle,
                CommentaryEventType.ShotSaved, CommentaryEventType.ShotPost };
            double t = 1;
            for (int i = 0; i < 40; i++, t += 1.5)
            {
                var cues = e.Push(Ev(types[i % types.Length], t, "Okafor"));
                cues.AddRange(e.Tick(t + 0.75));
                Assert.IsNull(Started(cues, CommentaryVoice.Analyst));
                Assert.IsFalse(e.IsStoppage);
            }
        }

        [Test]
        public void Analyst_NewLeadLine_StopsAnalyst()
        {
            var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 1 }, 5);
            var lead = Started(e.Push(Ev(CommentaryEventType.Foul, 100, "Vance")), CommentaryVoice.PlayByPlay);
            Assert.IsNotNull(Started(e.Tick(lead.EndTime + 1), CommentaryVoice.Analyst));
            var cues = e.Push(Ev(CommentaryEventType.YellowCard, lead.EndTime + 1.5, "Vance"));
            Assert.IsTrue(Stopped(cues, CommentaryVoice.Analyst));
            Assert.IsNotNull(Started(cues, CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void NameLines_StitchNameClipWithIntonation()
        {
            const string json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"g\",\"event\":\"Goal\",\"priority\":90,\"resolution\":true,\"tone\":\"excited\",\"nameTone\":\"excited\",\"lines\":[\"{name} scores!\"]}," +
                "{\"id\":\"t\",\"event\":\"ThroughBall\",\"nameTone\":\"question\",\"lines\":[\"Clever pass from {name}\"]}]}";
            var bank = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            var e = new CommentaryEngine(bank, NoAnalyst(), 1);
            var goal = Started(e.Push(Goal(0, 0, 1, 0, 30, "Dele Okonkwo-Hart")), CommentaryVoice.PlayByPlay);
            Assert.AreEqual(2, goal.Clips.Count);
            Assert.IsTrue(goal.Clips[0].IsName);
            Assert.AreEqual("name.dele_okonkwo_hart.excited", goal.Clips[0].ClipId);
            Assert.AreEqual("line.g.01", goal.Clips[1].ClipId);
            Assert.AreEqual("scores!", goal.Clips[1].Text);

            var tb = Started(e.Push(Ev(CommentaryEventType.ThroughBall, 20, "Vance")), CommentaryVoice.PlayByPlay);
            Assert.AreEqual("line.t.01", tb.Clips[0].ClipId);
            Assert.AreEqual("name.vance.question", tb.Clips[1].ClipId);

            // A {name} line cannot play without a name.
            Assert.IsNull(Started(e.Push(Ev(CommentaryEventType.ThroughBall, 40, null)), CommentaryVoice.PlayByPlay));
        }

        [Test]
        public void Goal_Contexts_PickMostSpecificCategory()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 11);
            Assert.AreEqual("goal.open_play", Started(e.Push(Goal(0, 1, 0, 1)), CommentaryVoice.PlayByPlay).Category.Id);
            Assert.AreEqual("goal.equaliser", Started(e.Push(Goal(60, 0, 1, 1)), CommentaryVoice.PlayByPlay).Category.Id);
            Assert.AreEqual("goal.late_winner", Started(e.Push(Goal(120, 0, 2, 1, 88)), CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void Goal_FromTwoDown_IsComeback()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 11);
            e.Push(Goal(0, 1, 0, 1));
            e.Push(Goal(60, 1, 0, 2));
            Assert.AreEqual("goal.open_play", Started(e.Push(Goal(120, 0, 1, 2)), CommentaryVoice.PlayByPlay).Category.Id);
            Assert.AreEqual("goal.comeback", Started(e.Push(Goal(180, 0, 2, 2, 70)), CommentaryVoice.PlayByPlay).Category.Id);
            var ft = Started(e.Push(new CommentaryEvent { Type = CommentaryEventType.FullTime, Time = 400, Minute = 90, HomeScore = 3, AwayScore = 2 }), CommentaryVoice.PlayByPlay);
            Assert.AreEqual("full_time.comeback", ft.Category.Id);
        }

        [Test]
        public void Goal_Situation_PicksSituationCategory()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 11);
            var ev = Goal(0, 0, 1, 0);
            ev.Situation = GoalSituation.Header;
            Assert.AreEqual("goal.header", Started(e.Push(ev), CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void KickOff_SecondHalf_UsesSecondHalfLines()
        {
            var e = new CommentaryEngine(Shipped(), NoAnalyst(), 11);
            Assert.AreEqual("kickoff", Started(e.Push(Ev(CommentaryEventType.KickOff, 0)), CommentaryVoice.PlayByPlay).Category.Id);
            e.Push(new CommentaryEvent { Type = CommentaryEventType.HalfTime, Time = 100, Minute = 45 });
            Assert.AreEqual("kickoff.second_half", Started(e.Push(Ev(CommentaryEventType.KickOff, 200)), CommentaryVoice.PlayByPlay).Category.Id);
        }

        [Test]
        public void Engine_SameSeedSameEvents_SameTranscript()
        {
            string Run()
            {
                var e = new CommentaryEngine(Shipped(), new CommentaryConfig { AnalystChance = 0.5 }, 42);
                var sb = new System.Text.StringBuilder();
                var types = new[] { CommentaryEventType.KickOff, CommentaryEventType.PassSequence, CommentaryEventType.Foul,
                    CommentaryEventType.PlayResumed, CommentaryEventType.ShotWide, CommentaryEventType.Corner, CommentaryEventType.Tackle };
                double t = 0;
                for (int i = 0; i < 60; i++, t += 2.7)
                {
                    foreach (var c in e.Push(Ev(types[i % types.Length], t, "Okafor")))
                        sb.Append(c.Kind).Append(c.Utterance.Line.Id).Append(';');
                    foreach (var c in e.Tick(t + 1.9))
                        sb.Append(c.Kind).Append(c.Utterance.Line.Id).Append(';');
                }
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
