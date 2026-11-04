using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-DATA")]
    public class RosterLoaderTests
    {
        [Test]
        public void BundledRoster_Load_TwentyClubsOfAtLeast23Players()
        {
            var r = new BundledRosterSource(RosterTestData.StreamingAssets).Load();
            Assert.IsTrue(r.Success, r.Describe());
            Assert.AreEqual(0, r.ErrorCount);
            Assert.AreEqual(0, r.WarningCount, r.Describe());
            Assert.AreEqual(20, r.League.Clubs.Count);
            foreach (var c in r.League.Clubs)
                Assert.GreaterOrEqual(c.Players.Count, 23, c.Name);
        }

        [Test]
        public void BundledRoster_Load_IsMarkedFictionalWithUniqueIds()
        {
            var league = new BundledRosterSource(RosterTestData.StreamingAssets).Load().League;
            Assert.IsTrue(league.IsFictional);
            Assert.AreEqual("fictional-generated", league.ProvenanceSource);
            var ids = new HashSet<string>();
            foreach (var c in league.Clubs)
                foreach (var p in c.Players)
                    Assert.IsTrue(ids.Add(p.Id), "duplicate " + p.Id);
            Assert.AreEqual(league.PlayerCount, ids.Count);
        }

        [Test]
        public void BundledRoster_Strength_TopReputationBeatsBottom()
        {
            var league = new BundledRosterSource(RosterTestData.StreamingAssets).Load().League;
            int top = 0, topN = 0, bottom = 0, bottomN = 0;
            foreach (var c in league.Clubs)
            {
                if (c.Reputation == 5) { top += c.StartingElevenTenths(); topN++; }
                if (c.Reputation == 1) { bottom += c.StartingElevenTenths(); bottomN++; }
            }
            Assert.Greater(topN, 0);
            Assert.Greater(bottomN, 0);
            Assert.Greater(top / topN, bottom / bottomN + 15, "top clubs should be clearly stronger");
        }

        [Test]
        public void BundledRoster_Players_SpecialistsRateHighestInOwnRole()
        {
            var league = new BundledRosterSource(RosterTestData.StreamingAssets).Load().League;
            int keepersBetterInGoal = 0, keepers = 0;
            foreach (var c in league.Clubs)
                foreach (var p in c.Players)
                    if (p.PrimaryPosition == RosterPosition.GK)
                    {
                        keepers++;
                        if (p.OverallTenths > p.OverallTenthsAt(RosterPosition.ST)) keepersBetterInGoal++;
                    }
            Assert.AreEqual(keepers, keepersBetterInGoal);
        }

        [Test]
        public void Load_SameText_IdenticalResult()
        {
            string text = RosterTestData.BundledText();
            var a = RosterLoader.Load(text, "a").League;
            var b = RosterLoader.Load(text, "b").League;
            Assert.AreEqual(a.PlayerCount, b.PlayerCount);
            for (int c = 0; c < a.Clubs.Count; c++)
                for (int p = 0; p < a.Clubs[c].Players.Count; p++)
                    Assert.AreEqual(a.Clubs[c].Players[p].OverallTenths, b.Clubs[c].Players[p].OverallTenths);
        }

        [Test]
        public void CsvImportExample_Load_MatchesBundledClubs()
        {
            var imported = new LocalFileRosterSource(RosterTestData.ExampleImportPath).Load();
            Assert.IsTrue(imported.Success, imported.Describe());
            Assert.AreEqual("user-import", imported.League.ProvenanceSource);
            Assert.IsFalse(imported.League.IsFictional);
            var bundled = new BundledRosterSource(RosterTestData.StreamingAssets).Load().League;
            foreach (var club in imported.League.Clubs)
            {
                var original = bundled.FindClub(club.Id);
                Assert.IsNotNull(original, club.Id);
                Assert.AreEqual(original.Players.Count, club.Players.Count);
                for (int i = 0; i < club.Players.Count; i++)
                {
                    var a = original.Players[i];
                    var b = club.Players[i];
                    Assert.AreEqual(a.Id, b.Id);
                    Assert.AreEqual(a.KnownAs, b.KnownAs);
                    Assert.AreEqual(a.OverallTenths, b.OverallTenths);
                    for (int k = 0; k < RosterSchema.AttributeCount; k++)
                        Assert.AreEqual(a.Get((RosterAttribute)k), b.Get((RosterAttribute)k));
                }
            }
        }

        [Test]
        public void LocalFileSource_Discover_ListsOnlyRosterFilesSorted()
        {
            string dir = Path.Combine(Path.GetTempPath(), "openfifa-roster-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "b.roster.json"), RosterTestData.BundledText());
                File.WriteAllText(Path.Combine(dir, "a.roster.json"), "{ not json");
                File.WriteAllText(Path.Combine(dir, "notes.json"), "{}");
                var found = LocalFileRosterSource.Discover(dir);
                Assert.AreEqual(2, found.Count);
                Assert.AreEqual("local:a.roster.json", found[0].SourceId);
                Assert.AreEqual("b", found[1].DisplayName);
                Assert.AreEqual(RosterSourceKind.LocalFile, found[0].Kind);
                var bad = found[0].Load();
                Assert.IsFalse(bad.Success);
                StringAssert.Contains("line 1", bad.Describe());
                Assert.IsTrue(found[1].Load().Success);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LocalFileSource_MissingFile_FailsWithMessage()
        {
            var r = new LocalFileRosterSource(Path.Combine(Path.GetTempPath(), "nope-" + System.Guid.NewGuid() + ".roster.json")).Load();
            Assert.IsFalse(r.Success);
            StringAssert.Contains("file not found", r.Describe());
        }

        [Test]
        public void BundledSource_InjectedReader_IsUsed()
        {
            IRosterSource source = new BundledRosterSource(() => RosterTestData.BundledText(), "x.json", "X");
            Assert.AreEqual(RosterSourceKind.Bundled, source.Kind);
            Assert.AreEqual("bundled:x", source.SourceId);
            Assert.IsTrue(source.Load().Success);
        }
    }
}
