using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Feedback")]
    public class FeedbackUrlTests
    {
        private static FeedbackContext Full() => new FeedbackContext
        {
            BuildVersion = "0.3.1",
            GitSha = "052066b",
            Platform = "iPadOS",
            DeviceModel = "iPad14,1",
            OsVersion = "iPadOS 18.1",
            MeanFps = 59.62f,
            InputScheme = "Touch",
            League = "opd",
            Difficulty = "Normal",
            MatchMinutes = 6,
        };

        [Test]
        public void Build_TargetsGameplayFeedbackForm()
        {
            string url = FeedbackUrl.Build(Full());
            StringAssert.StartsWith("https://github.com/tshrjn/openFIFA/issues/new?template=gameplay_feedback.yml&title=", url);
            StringAssert.Contains("&build-info=", url);
        }

        [Test]
        public void Build_EscapesValues()
        {
            string url = FeedbackUrl.Build(Full());
            Assert.IsFalse(url.Contains(" "), url);
            Assert.IsFalse(url.Contains("\n"), url);
            StringAssert.Contains("Feedback%3A%20iPadOS%2C%20build%200.3.1", url);
            StringAssert.Contains("mean%20fps%3A%2059.6", url);
        }

        [Test]
        public void BuildInfo_ListsKnownFactsOnePerLine()
        {
            Assert.AreEqual(
                "build: 0.3.1 (052066b)\nplatform: iPadOS\ndevice: iPad14,1\nos: iPadOS 18.1\nmean fps: 59.6\n" +
                "input: Touch\nleague: opd\ndifficulty: Normal\nmatch length: 6 min",
                FeedbackUrl.BuildInfo(Full()));
        }

        [Test]
        public void BuildInfo_SkipsUnknowns()
        {
            var ctx = new FeedbackContext { BuildVersion = "0.3.1", Platform = "  ", MeanFps = 0f, MatchMinutes = 0 };
            Assert.AreEqual("build: 0.3.1", FeedbackUrl.BuildInfo(ctx));
            Assert.AreEqual("Feedback: unknown platform, build 0.3.1", FeedbackUrl.Title(ctx));
        }

        [Test]
        public void BuildInfo_FlattensNewlinesInValues()
        {
            var ctx = new FeedbackContext { DeviceModel = "Mac\nmini\r" };
            Assert.AreEqual("device: Mac mini", FeedbackUrl.BuildInfo(ctx));
        }

        [Test]
        public void BuildInfo_IsCapped()
        {
            var ctx = Full();
            ctx.DeviceModel = new string('x', 5000);
            Assert.LessOrEqual(FeedbackUrl.BuildInfo(ctx).Length, FeedbackUrl.MaxFieldLength);
        }

        [Test]
        public void Build_UsesDecimalPointInAnyCulture()
        {
            var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                StringAssert.Contains("mean fps: 59.6", FeedbackUrl.BuildInfo(Full()));
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = prev; }
        }

        [Test]
        public void Build_RepoCanBeOverriddenForTheRename()
        {
            StringAssert.StartsWith("https://github.com/tshrjn/openfootball/issues/new?",
                FeedbackUrl.Build(Full(), "tshrjn/openfootball"));
            Assert.Throws<ArgumentException>(() => FeedbackUrl.Build(Full(), "nope"));
        }
    }
}
