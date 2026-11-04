using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// The shipped Premier League pack (StreamingAssets/Rosters/Packs/premier_league.roster.json, built by
    /// tools/rosters/premier_league/build_premier_league.py) and the appearance/kit fields it carries.
    /// </summary>
    [TestFixture]
    [Category("FID_DATA")]
    public class PremierLeaguePackTests
    {
        private static RosterLeague _league;

        private static RosterLeague League
        {
            get
            {
                if (_league != null) return _league;
                var r = RosterLoader.Load(File.ReadAllText(RosterTestData.PremierLeaguePath), "premier_league.roster.json");
                Assert.IsTrue(r.Success, r.Describe());
                Assert.AreEqual(0, r.WarningCount, r.Describe());
                _league = r.League;
                return _league;
            }
        }

        [Test]
        public void Pack_Loads_TwentyClubsWithFullSquads()
        {
            Assert.AreEqual("premier-league", League.Id);
            Assert.AreEqual("2026/27", League.Season);
            Assert.IsFalse(League.IsFictional);
            Assert.AreEqual(20, League.Clubs.Count);
            foreach (var c in League.Clubs)
            {
                Assert.GreaterOrEqual(c.Players.Count, 20, c.Name);
                int keepers = 0;
                foreach (var p in c.Players) if (p.PrimaryPosition == RosterPosition.GK) keepers++;
                Assert.GreaterOrEqual(keepers, 2, c.Name);
            }
        }

        [Test]
        public void Pack_Clubs_AreThe2026_27Season()
        {
            var expected = new[] { "ARS", "AVL", "BOU", "BRE", "BHA", "CHE", "COV", "CRY", "EVE", "FUL",
                "HUL", "IPS", "LEE", "LIV", "MCI", "MUN", "NEW", "NFO", "SUN", "TOT" };
            var seen = new HashSet<string>();
            foreach (var c in League.Clubs) seen.Add(c.ShortName);
            foreach (var s in expected) Assert.IsTrue(seen.Contains(s), s);
        }

        [Test]
        public void Pack_EveryPlayer_HasShirtNameSquadNumberAndAppearance()
        {
            int unknownHair = 0, players = 0;
            foreach (var c in League.Clubs)
                foreach (var p in c.Players)
                {
                    Assert.AreEqual(p.ShirtNumber, p.SquadNumber, p.Id);
                    Assert.IsFalse(string.IsNullOrEmpty(p.ShirtName), p.Id);
                    foreach (char ch in p.ShirtName) Assert.Less((int)ch, 128, p.Id + " shirt name is plain ASCII");
                    var a = p.Appearance;
                    Assert.IsNotNull(a, p.Id);
                    Assert.That(a.SkinTone, Is.InRange(0f, 1f), p.Id);
                    // Unknown hair / facial hair stays null so the appearance pipeline picks a seeded look.
                    if (a.HairStyle != null) Assert.IsTrue(RosterAppearance.IsToken(RosterAppearance.HairStyles, a.HairStyle), p.Id);
                    if (a.FacialHair != null) Assert.IsTrue(RosterAppearance.IsToken(RosterAppearance.FacialHairStyles, a.FacialHair), p.Id);
                    if (a.HairStyle == null) unknownHair++;
                    Assert.IsTrue(RosterAppearance.IsToken(RosterAppearance.Builds, a.Build), p.Id);
                    Assert.AreEqual(p.HeightCm, a.HeightCm, p.Id);
                    players++;
                }
            // No blanket default: a known style is written, everything else is left for the pipeline.
            Assert.Greater(unknownHair, 0);
            Assert.Less(unknownHair, players);
        }

        [Test]
        public void Pack_KnownAs_KeepsOriginalSpelling()
        {
            var odegaard = League.FindPlayer("arsenal-martin-odegaard");
            Assert.AreEqual("Ødegaard", odegaard.KnownAs);
            Assert.AreEqual("ODEGAARD", odegaard.ShirtName);
            Assert.AreEqual("Gvardiol", League.FindPlayer("man-city-josko-gvardiol").KnownAs);
            Assert.AreEqual("Šeško", League.FindPlayer("man-united-benjamin-sesko").KnownAs);
        }

        [Test]
        public void Pack_EveryClub_HasFourKitsInSlotOrder()
        {
            foreach (var c in League.Clubs)
            {
                Assert.AreEqual(4, c.Kits.Count, c.Name);
                for (int i = 0; i < RosterKit.Slots.Length; i++) Assert.AreEqual(RosterKit.Slots[i], c.Kits[i].Slot, c.Name);
                Assert.AreEqual(c.PrimaryColour, c.Kit("home").Primary, c.Name);
                Assert.AreNotEqual(c.Kit("home").Primary, c.Kit("gk").Primary, c.Name + " keeper must not wear the home shirt");
            }
            var newcastle = League.FindClub("newcastle");
            Assert.AreEqual("stripes", newcastle.Kit("home").Pattern);
            Assert.AreEqual("Stripes", newcastle.Kit("home").PatternName);
        }

        [Test]
        public void Pack_Strength_TopClubsRateAbovePromotedClubs()
        {
            int top = League.FindClub("man-city").StartingElevenTenths();
            int arsenal = League.FindClub("arsenal").StartingElevenTenths();
            int hull = League.FindClub("hull").StartingElevenTenths();
            int coventry = League.FindClub("coventry").StartingElevenTenths();
            Assert.Greater(top, hull + 15);
            Assert.Greater(arsenal, coventry + 15);
        }

        [Test]
        public void Pack_KnownPlayers_LandInTheRightPlaces()
        {
            var haaland = League.FindPlayer("man-city-erling-haaland");
            Assert.IsNotNull(haaland);
            Assert.AreEqual(9, haaland.ShirtNumber);
            Assert.AreEqual(RosterPosition.ST, haaland.PrimaryPosition);
            Assert.AreEqual(PreferredFoot.Left, haaland.PreferredFoot);
            Assert.AreEqual("HAALAND", haaland.ShirtName);
            var bruno = League.FindPlayer("man-united-bruno-fernandes");
            Assert.AreEqual("B.FERNANDES", bruno.ShirtName);
            Assert.AreEqual("ODEGAARD", League.FindPlayer("arsenal-martin-odegaard").ShirtName);
            // Mononyms keep a single name.
            var evanilson = League.FindPlayer("bournemouth-evanilson");
            Assert.AreEqual("", evanilson.FirstName);
            Assert.AreEqual("Evanilson", evanilson.KnownAs);
        }

        [Test]
        public void LoadLeagues_PackComesFirst_FictionalLeagueStillLoads()
        {
            var problems = new List<string>();
            var leagues = RosterMatchBuilder.LoadLeagues(RosterTestData.StreamingAssets, null, problems);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
            Assert.AreEqual(2, leagues.Count);
            Assert.AreEqual("premier-league", leagues[0].Id);
            Assert.IsTrue(leagues[1].IsFictional);
        }

        [Test]
        public void Pack_BuildsAPlayableMatchSetup()
        {
            var setup = RosterMatchBuilder.BuildSetup(League.FindClub("liverpool"), League.FindClub("arsenal"), 7,
                out var home, out var away, out var names, out var ids);
            Assert.IsTrue(setup.Home[0].IsGoalkeeper);
            Assert.AreEqual(RosterPosition.GK, home.Starters[0].PrimaryPosition);
            Assert.AreEqual(RosterPosition.GK, away.Starters[0].PrimaryPosition);
            Assert.AreEqual(22, new HashSet<string>(ids).Count);
        }

        [Test]
        public void DiscoverPacks_MissingFolder_ReturnsEmpty()
        {
            Assert.AreEqual(0, BundledRosterSource.DiscoverPacks(Path.Combine(Path.GetTempPath(), "no-such-root")).Count);
            var packs = BundledRosterSource.DiscoverPacks(RosterTestData.StreamingAssets);
            Assert.AreEqual(1, packs.Count);
            Assert.AreEqual("bundled:premier_league", packs[0].SourceId);
        }

        // ---- schema checks for the new optional fields, on the small fictional fixture ----

        [Test]
        public void Validate_BadAppearanceToken_ErrorNamesAllowedValues()
        {
            var root = RosterTestData.SmallLeague();
            var a = JsonNode.NewObject();
            a.Set("hairStyle", JsonNode.FromString("mohawk"));
            a.Set("skinTone", JsonNode.FromNumber(1.4));
            RosterTestData.FirstPlayer(root).Set("appearance", a);
            var r = RosterTestData.Load(root);
            Assert.IsFalse(r.Success);
            Assert.IsTrue(RosterTestData.HasError(r, "appearance.hairStyle", "buzz"), r.Describe());
            Assert.IsTrue(RosterTestData.HasError(r, "appearance.skinTone", "0 (light) to 1 (dark)"), r.Describe());
        }

        [Test]
        public void Validate_SquadNumberDisagreesWithShirtNumber_Error()
        {
            var root = RosterTestData.SmallLeague();
            var p = RosterTestData.FirstPlayer(root);
            p.Set("squadNumber", JsonNode.FromNumber(p.GetInt("shirtNumber", 1) + 1));
            var r = RosterTestData.Load(root);
            Assert.IsTrue(RosterTestData.HasError(r, "squadNumber", "must match shirtNumber"), r.Describe());
        }

        [Test]
        public void Load_SquadNumberOnly_IsAcceptedAsShirtNumber()
        {
            var root = RosterTestData.SmallLeague();
            var p = RosterTestData.FirstPlayer(root);
            int n = p.GetInt("shirtNumber", 1);
            p.Remove("shirtNumber");
            p.Set("squadNumber", JsonNode.FromNumber(n));
            var r = RosterTestData.Load(root);
            Assert.IsTrue(r.Success, r.Describe());
            Assert.AreEqual(n, r.League.Clubs[0].Players[0].ShirtNumber);
        }

        [Test]
        public void Load_NoKitsBlock_FallsBackToColours()
        {
            var r = RosterTestData.Load(RosterTestData.SmallLeague());
            var club = r.League.Clubs[0];
            Assert.AreEqual(2, club.Kits.Count);
            Assert.AreEqual(club.PrimaryColour, club.Kit("home").Primary);
            Assert.AreEqual(club.AwayColour, club.Kit("away").Primary);
            Assert.IsNull(club.Kit("gk"));
            Assert.IsNull(club.Players[0].Appearance);
            Assert.AreEqual(club.Players[0].KnownAs.ToUpperInvariant(), club.Players[0].ShirtName);
        }

        [Test]
        public void Validate_KitWithBadPattern_Error()
        {
            var root = RosterTestData.SmallLeague();
            var kits = JsonNode.NewObject();
            var home = JsonNode.NewObject();
            foreach (var key in new[] { "primary", "secondary", "tertiary", "shortsColor", "socksColor", "numberColor", "numberOutline" })
                home.Set(key, JsonNode.FromString("#112233"));
            home.Set("pattern", JsonNode.FromString("tartan"));
            kits.Set("home", home);
            root.Get("clubs")[0].Set("kits", kits);
            var r = RosterTestData.Load(root);
            Assert.IsTrue(RosterTestData.HasError(r, "kits.home.pattern", "stripes"), r.Describe());
        }
    }
}
