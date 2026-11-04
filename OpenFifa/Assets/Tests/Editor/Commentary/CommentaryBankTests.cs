using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_COMMENTARY")]
    public class CommentaryBankTests
    {
        private static CommentaryLineBank LoadShipped()
        {
            var bank = CommentaryLineBank.Load(File.ReadAllText(RosterTestData.LinesPath), out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            return bank;
        }

        [Test]
        public void ShippedBank_Load_NoErrors()
        {
            var bank = LoadShipped();
            Assert.Greater(bank.LineCount, 300);
            Assert.AreEqual("en", bank.Language);
        }

        [Test]
        public void ShippedBank_EveryCategory_Has10To20Lines()
        {
            foreach (var c in LoadShipped().Categories)
            {
                Assert.GreaterOrEqual(c.Lines.Count, 10, c.Id);
                Assert.LessOrEqual(c.Lines.Count, 20, c.Id);
            }
        }

        [Test]
        public void ShippedBank_CoversEveryEventExceptPlayResumed()
        {
            var bank = LoadShipped();
            foreach (CommentaryEventType t in Enum.GetValues(typeof(CommentaryEventType)))
            {
                if (t == CommentaryEventType.PlayResumed) continue;
                int n = bank.For(t, CommentaryVoice.PlayByPlay).Count + bank.For(t, CommentaryVoice.Analyst).Count;
                Assert.Greater(n, 0, t.ToString());
            }
        }

        [Test]
        public void ShippedBank_GoalSituationsAndContexts_AllPresent()
        {
            var bank = LoadShipped();
            foreach (GoalSituation s in Enum.GetValues(typeof(GoalSituation)))
            {
                if (s == GoalSituation.OpenPlay) continue;
                string snake = CommentaryLineBank.SnakeCase(s.ToString());
                bool found = false;
                foreach (var c in bank.For(CommentaryEventType.Goal, CommentaryVoice.PlayByPlay)) found |= c.Situation == snake;
                Assert.IsTrue(found, snake);
            }
            Assert.IsNotNull(bank.Find("goal.comeback"));
            Assert.IsNotNull(bank.Find("goal.late_winner"));
            Assert.IsNotNull(bank.Find("goal.equaliser"));
        }

        [Test]
        public void ShippedBank_ClipIdsUniqueAndTextsUnique()
        {
            var ids = new HashSet<string>();
            var texts = new HashSet<string>();
            foreach (var c in LoadShipped().Categories)
                foreach (var l in c.Lines)
                {
                    Assert.IsTrue(ids.Add(l.ClipId), l.ClipId);
                    if (l.NameOnly) continue; // bare name calls differ only by intonation
                    Assert.IsTrue(texts.Add(c.Voice + l.Text), "duplicate text: " + l.Text);
                }
        }

        [Test]
        public void SplitName_StartAndEnd_Placement()
        {
            Assert.IsTrue(CommentaryLineBank.SplitName("{name}! What a hit!", out var t1, out var p1, out _));
            Assert.AreEqual("What a hit!", t1);
            Assert.AreEqual(CommentaryNamePlacement.Before, p1);
            Assert.IsTrue(CommentaryLineBank.SplitName("A lovely ball from {name}!", out var t2, out var p2, out _));
            Assert.AreEqual("A lovely ball from", t2);
            Assert.AreEqual(CommentaryNamePlacement.After, p2);
        }

        [Test]
        public void SplitName_Middle_Rejected()
        {
            Assert.IsFalse(CommentaryLineBank.SplitName("Here is {name} again", out _, out _, out var err));
            StringAssert.Contains("start or the end", err);
        }

        private const string MinimalCategories =
            ",\"schemaVersion\":1,\"categories\":[{\"id\":\"foul\",\"event\":\"Foul\",\"lines\":[\"A foul.\"]}]}";

        [Test]
        public void ShippedBank_FormatTag_IsOpenFootball()
        {
            Assert.AreEqual("openfootball-commentary-lines", CommentaryLineBank.FormatTag);
            var root = JsonParser.Parse(File.ReadAllText(RosterTestData.LinesPath));
            Assert.AreEqual(CommentaryLineBank.FormatTag, root.GetString("format"));
        }

        [Test]
        public void Load_LegacyOpenFifaFormatTag_Loads()
        {
            Assert.AreEqual("openfifa-commentary-lines", CommentaryLineBank.LegacyFormatTag);
            var bank = CommentaryLineBank.Load("{\"format\":\"openfifa-commentary-lines\"" + MinimalCategories, out var errors);
            Assert.IsNotNull(bank, string.Join("\n", errors.ToArray()));
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Load_WrongFormatTag_ErrorNamesCurrentTag()
        {
            var bank = CommentaryLineBank.Load("{\"format\":\"something-else\"" + MinimalCategories, out var errors);
            Assert.IsNull(bank);
            StringAssert.Contains("openfootball-commentary-lines", string.Join("\n", errors.ToArray()));
        }

        [Test]
        public void Load_BadBank_CollectsErrors()
        {
            const string json = "{\"format\":\"openfootball-commentary-lines\",\"schemaVersion\":1,\"categories\":[" +
                "{\"id\":\"a\",\"event\":\"Nope\",\"lines\":[\"x\"]}," +
                "{\"id\":\"b\",\"event\":\"Foul\",\"voice\":\"analyst\",\"lines\":[\"{name} fouls\"]}," +
                "{\"id\":\"c\",\"event\":\"Foul\",\"lines\":[]}]}";
            var bank = CommentaryLineBank.Load(json, out var errors);
            Assert.IsNull(bank);
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors.ToArray()));
        }
    }
}
