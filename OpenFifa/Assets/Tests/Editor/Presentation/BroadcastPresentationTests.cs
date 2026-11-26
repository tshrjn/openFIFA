using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Presentation
{
    /// <summary>Engine-free checks of the broadcast framing maths, the scorebug crest and the HUD shirt numbers.</summary>
    [TestFixture]
    [Category("FID_PRESENTATION")]
    public class BroadcastPresentationTests
    {
        // SinglePlayerCamera broadcast defaults.
        private const float Fov = 27f, Height = 19f, Setback = 28f, HalfWidth = 34f, DollyIn = 0.15f;

        [Test]
        public void PitchUpDegrees_AimedPoint_LandsAtRequestedScreenFraction()
        {
            foreach (float f in new[] { 0.5f, 0.55f, 0.57f, 0.6f })
            {
                float up = BroadcastFraming.PitchUpDegrees(Fov, f);
                Assert.AreEqual(f, BroadcastFraming.FromTopForAngle(Fov, up), 1e-4f);
            }
            Assert.AreEqual(0f, BroadcastFraming.PitchUpDegrees(Fov, 0.5f), 1e-5f);
            Assert.That(BroadcastFraming.PitchUpDegrees(Fov, BroadcastFraming.BallScreenFromTop), Is.InRange(1.5f, 2.5f));
        }

        [Test]
        public void BallScreenFromTop_Default_IsBetween55And60Percent()
        {
            Assert.That(BroadcastFraming.BallScreenFromTop, Is.InRange(0.55f, 0.6f));
        }

        [Test]
        public void ClampFollow_FastBall_NeverTrailsBeyondLimit()
        {
            // A smoothed value lagging a ball that runs at 25 m/s into the goalmouth stays within the dead-zone edge.
            float framed = 0f, vel = 0f;
            const float dt = 1f / 60f, limit = 9f;
            for (int i = 0; i < 120; i++)
            {
                float ball = Math.Min(52f, i * dt * 25f);
                // critically damped follow, 0.3 s
                float omega = 2f / 0.3f, x = omega * dt, exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
                float change = framed - ball, temp = (vel + omega * change) * dt;
                vel = (vel - omega * temp) * exp;
                framed = ball + (change + temp) * exp;
                framed = BroadcastFraming.ClampFollow(framed, ball, limit);
                Assert.That(Math.Abs(framed - ball), Is.LessThanOrEqualTo(limit + 1e-4f));
            }
        }

        [Test]
        public void BroadcastGeometry_MidPitchPlay_Players35To50PixelsAt720p()
        {
            foreach (float x in new[] { -10f, 0f, 8f })
            {
                float camX = HalfWidth + Setback - Math.Max(0f, x) * DollyIn;
                float d = (float)Math.Sqrt((camX - x) * (camX - x) + Height * Height);
                float px = BroadcastFraming.Pixels(1.8f, d, Fov, 720f);
                Assert.That(px, Is.InRange(35f, 50f), "play at x = " + x);
            }
        }

        [Test]
        public void BallScale_SmallBall_ScaledToMinimumWithinCap()
        {
            Assert.AreEqual(1f, BroadcastFraming.BallScale(9f, 7.5f, 3f), 1e-5f);
            Assert.AreEqual(1.5f, BroadcastFraming.BallScale(5f, 7.5f, 3f), 1e-5f);
            Assert.AreEqual(3f, BroadcastFraming.BallScale(1f, 7.5f, 3f), 1e-5f);
            // The ball (0.22 m) at the far touchline from the broadcast camera reaches the 7.5 px target.
            float d = (float)Math.Sqrt(96f * 96f + Height * Height);
            float px = BroadcastFraming.Pixels(0.22f, d, Fov, 720f);
            Assert.That(px * BroadcastFraming.BallScale(px, 7.5f, 3f), Is.GreaterThanOrEqualTo(7.5f - 1e-3f));
        }

        [Test]
        public void Initials_ClubNames_TwoLettersSkippingFc()
        {
            Assert.AreEqual("KU", ClubCrest.Initials("Kingsmere United"));
            Assert.AreEqual("AA", ClubCrest.Initials("Ashbury Athletic"));
            Assert.AreEqual("RE", ClubCrest.Initials("FC Redhaven"));
            Assert.AreEqual("TW", ClubCrest.Initials("thornwick wanderers"));
            Assert.AreEqual("?", ClubCrest.Initials("  "));
        }

        [Test]
        public void Shield_Shape_FlatTopStraightSidesPointedBottom()
        {
            Assert.IsTrue(ClubCrest.InShield(0.5f, 0.5f));
            Assert.IsTrue(ClubCrest.InShield(0.1f, 0.8f));
            Assert.IsFalse(ClubCrest.InShield(0.5f, 0.97f));
            Assert.IsFalse(ClubCrest.InShield(0.1f, 0.1f));
            Assert.IsTrue(ClubCrest.InShield(0.5f, 0.03f));
            Assert.Less(ClubCrest.ShieldHalfWidth(0.1f), ClubCrest.ShieldHalfWidth(0.3f));
            Assert.IsTrue(ClubCrest.OnBorder(0.07f, 0.7f, 0.06f));
            Assert.IsFalse(ClubCrest.OnBorder(0.5f, 0.5f, 0.06f));
        }

        [Test]
        public void PatternFor_SameShortName_SamePattern()
        {
            Assert.AreEqual(ClubCrest.PatternFor("KMU"), ClubCrest.PatternFor("KMU"));
            var seen = new HashSet<ClubCrest.Pattern>();
            foreach (var n in new[] { "KMU", "ASH", "RHC", "THW", "ABC", "XYZ", "NOR", "SOU" }) seen.Add(ClubCrest.PatternFor(n));
            Assert.GreaterOrEqual(seen.Count, 3);
        }

        [Test]
        public void MatchSheetNumbers_BundledClubs_EachPanelUsesItsOwnPlayersNumber()
        {
            var problems = new List<string>();
            var leagues = RosterMatchBuilder.LoadLeagues(RosterTestData.StreamingAssets, null, problems);
            Assert.AreEqual(1, leagues.Count, string.Join("\n", problems));
            var clubs = leagues[0].Clubs;
            RosterMatchBuilder.BuildSetup(clubs[0], clubs[1], 2, out var home, out var away, out var names, out _);
            var numbers = MatchSheetNumbers.Build(home, away);
            Assert.AreEqual(MatchSimState.PlayerCount, numbers.Length);
            for (int i = 0; i < MatchSetup.PlayersPerTeam; i++)
            {
                Assert.AreEqual(home.Starters[i].ShirtNumber, numbers[i]);
                Assert.AreEqual(home.Starters[i].KnownAs, names[i]);
                Assert.AreEqual(away.Starters[i].ShirtNumber, numbers[i + MatchSetup.PlayersPerTeam]);
                Assert.AreEqual(away.Starters[i].KnownAs, names[i + MatchSetup.PlayersPerTeam]);
            }
            // Numbers are unique within each side.
            for (int t = 0; t < 2; t++)
            {
                var set = new HashSet<int>();
                for (int i = 0; i < MatchSetup.PlayersPerTeam; i++) Assert.IsTrue(set.Add(numbers[t * MatchSetup.PlayersPerTeam + i]));
            }
            // The capture's "TEVIOLA 11 v CALLOW 11": both clubs' rosters really give their left winger No. 11.
            int tev = Array.IndexOf(names, "Teviola"), cal = Array.IndexOf(names, "Callow");
            if (tev >= 0) Assert.AreEqual(11, numbers[tev]);
            if (cal >= 0) Assert.AreEqual(11, numbers[cal]);
        }

        [Test]
        public void MatchSheetNumbers_Default_OneToElevenPerSide()
        {
            var n = MatchSheetNumbers.Default();
            Assert.AreEqual(1, n[0]);
            Assert.AreEqual(11, n[10]);
            Assert.AreEqual(1, n[11]);
            Assert.AreEqual(11, n[21]);
        }

        [Test]
        public void PixelFont_LedWordmarks_AllCharactersSupported()
        {
            foreach (var w in new[] { "OPENFOOTBALL", "FAIR PLAY", "MATCHDAY", "RESPECT", "PITCHSIDE", "KICK OFF", "TEAM SPIRIT", "GOAL RUSH", "GOAL!" })
                foreach (char c in w) Assert.IsTrue(PixelFont.Supports(c), "'" + c + "' in " + w);
            Assert.IsTrue(PixelFont.Pixel('I', 2, 3));
            Assert.IsFalse(PixelFont.Pixel('I', 0, 3));
            Assert.IsFalse(PixelFont.Pixel(' ', 2, 3));
            Assert.AreEqual(PixelFont.Advance * 4 - 1, PixelFont.Width("GOAL"));
            Assert.IsTrue(PixelFont.TextPixel("AT", PixelFont.Advance + 2, 0)); // T's top bar
        }
    }
}
