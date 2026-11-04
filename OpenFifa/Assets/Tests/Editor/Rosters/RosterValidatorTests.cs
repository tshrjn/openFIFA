using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-DATA")]
    public class RosterValidatorTests
    {
        private sealed class RenameSquadMigration : IRosterMigration
        {
            public int FromVersion => 0;

            public string Apply(JsonNode root)
            {
                foreach (var club in root.Get("clubs").Items)
                {
                    var squad = club.Get("squad");
                    if (squad == null) return "club without squad";
                    club.Remove("squad");
                    club.Set("players", squad);
                }
                return null;
            }
        }

        [Test]
        public void Validate_SmallLeague_NoIssues()
        {
            var r = RosterTestData.Load(RosterTestData.SmallLeague());
            Assert.IsTrue(r.Success, r.Describe());
            Assert.AreEqual(0, r.Issues.Count, r.Describe());
        }

        [Test]
        public void Validate_AttributeOutOfRange_ErrorNamesPathAndValue()
        {
            var root = RosterTestData.SmallLeague();
            RosterTestData.FirstPlayer(root).Get("attributes").Set("finishing", JsonNode.FromNumber(25));
            var r = RosterTestData.Load(root);
            Assert.IsFalse(r.Success);
            Assert.IsTrue(RosterTestData.HasError(r, "clubs[0].players[0].attributes.finishing", "from 1 to 20 (got 25)"), r.Describe());
        }

        [Test]
        public void Validate_FractionalAttribute_Error()
        {
            var root = RosterTestData.SmallLeague();
            RosterTestData.FirstPlayer(root).Get("attributes").Set("vision", JsonNode.FromNumber(12.5));
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "attributes.vision", "whole number"));
        }

        [Test]
        public void Validate_MissingAttribute_ListsItsName()
        {
            var root = RosterTestData.SmallLeague();
            RosterTestData.FirstPlayer(root).Get("attributes").Remove("gkReflexes");
            var r = RosterTestData.Load(root);
            Assert.IsTrue(RosterTestData.HasError(r, "players[0].attributes", "gkReflexes"), r.Describe());
        }

        [Test]
        public void Validate_UnknownAttribute_WarningOnly()
        {
            var root = RosterTestData.SmallLeague();
            RosterTestData.FirstPlayer(root).Get("attributes").Set("sprintSpeed", JsonNode.FromNumber(10));
            var r = RosterTestData.Load(root);
            Assert.IsTrue(r.Success, r.Describe());
            Assert.AreEqual(1, r.WarningCount);
            StringAssert.Contains("sprintSpeed", r.Describe());
        }

        [Test]
        public void Validate_UnknownPosition_ErrorListsValidCodes()
        {
            var root = RosterTestData.SmallLeague();
            var pos = JsonNode.NewArray();
            pos.Add(JsonNode.FromString("SW"));
            RosterTestData.FirstPlayer(root).Set("positions", pos);
            var r = RosterTestData.Load(root);
            Assert.IsTrue(RosterTestData.HasError(r, "players[0].positions[0]", "unknown position"), r.Describe());
            StringAssert.Contains("CDM", r.Describe());
        }

        [Test]
        public void Validate_DuplicatePlayerIdAcrossClubs_Error()
        {
            var root = RosterTestData.SmallLeague();
            string id = RosterTestData.FirstPlayer(root).GetString("id");
            root.Get("clubs")[1].Get("players")[3].Set("id", JsonNode.FromString(id));
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "clubs[1].players[3].id", "duplicate player id"));
        }

        [Test]
        public void Validate_DuplicateShirtNumber_Error()
        {
            var root = RosterTestData.SmallLeague();
            var players = root.Get("clubs")[0].Get("players");
            players[5].Set("shirtNumber", JsonNode.FromNumber(players[4].GetInt("shirtNumber", 0)));
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "clubs[0].players[5].shirtNumber", "already used"));
        }

        [Test]
        public void Validate_ClubWithoutGoalkeeper_Error()
        {
            var root = RosterTestData.SmallLeague();
            foreach (var p in root.Get("clubs")[0].Get("players").Items)
                if (p.Get("positions")[0].StringValue == "GK")
                {
                    var pos = JsonNode.NewArray();
                    pos.Add(JsonNode.FromString("CB"));
                    p.Set("positions", pos);
                }
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "clubs[0].players", "GK"));
        }

        [Test]
        public void Validate_BadColourAndFormation_BothReported()
        {
            var root = RosterTestData.SmallLeague();
            var club = root.Get("clubs")[0];
            club.Get("colours").Set("primary", JsonNode.FromString("red"));
            club.Set("formation", JsonNode.FromString("4-4-3"));
            var r = RosterTestData.Load(root);
            Assert.IsTrue(RosterTestData.HasError(r, "colours.primary", "#1A2B3C"), r.Describe());
            Assert.IsTrue(RosterTestData.HasError(r, "clubs[0].formation", "add up to 10"), r.Describe());
            Assert.GreaterOrEqual(r.ErrorCount, 2);
        }

        [Test]
        public void Validate_TooFewPlayers_Error()
        {
            var root = RosterTestData.SmallLeague();
            var few = JsonNode.NewArray();
            var players = root.Get("clubs")[0].Get("players");
            for (int i = 0; i < 5; i++) few.Add(players[i]);
            root.Get("clubs")[0].Set("players", few);
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "clubs[0].players", "from 11 to 40"));
        }

        [Test]
        public void Validate_WrongFormatTag_Error()
        {
            var root = RosterTestData.SmallLeague();
            root.Set("format", JsonNode.FromString("something-else"));
            Assert.IsTrue(RosterTestData.HasError(RosterTestData.Load(root), "format", "openfootball-roster"));
        }

        [Test]
        public void Load_SyntaxError_ReportsLine()
        {
            var r = RosterLoader.Load("{\n  \"format\": \"openfootball-roster\",\n  \"schemaVersion\": 1,,\n}", "broken");
            Assert.IsFalse(r.Success);
            Assert.AreEqual(3, r.Issues[0].Line);
            StringAssert.Contains("line 3", r.Describe());
        }

        [Test]
        public void Load_NewerSchemaVersion_FailsWithUpdateHint()
        {
            var root = RosterTestData.SmallLeague();
            root.Set("schemaVersion", JsonNode.FromNumber(RosterSchema.CurrentVersion + 1));
            var r = RosterTestData.Load(root);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("newer than this build supports", r.Describe());
        }

        [Test]
        public void Load_OldVersionWithoutMigration_Fails()
        {
            var root = RosterTestData.SmallLeague();
            root.Set("schemaVersion", JsonNode.FromNumber(0));
            var r = RosterTestData.Load(root);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("no migration from schemaVersion 0", r.Describe());
        }

        [Test]
        public void Load_OldVersionWithRegisteredMigration_Upgrades()
        {
            var root = RosterTestData.SmallLeague();
            root.Set("schemaVersion", JsonNode.FromNumber(0));
            foreach (var club in root.Get("clubs").Items)
            {
                var players = club.Get("players");
                club.Remove("players");
                club.Set("squad", players);
            }
            var migrator = new RosterMigrator().Register(new RenameSquadMigration());
            var r = RosterLoader.Load(JsonWriter.Serialize(root), "old", migrator);
            Assert.IsTrue(r.Success, r.Describe());
            Assert.AreEqual(0, r.OriginalSchemaVersion);
            Assert.AreEqual(RosterSchema.CurrentVersion, r.League.SchemaVersion);
            Assert.AreEqual(25, r.League.Clubs[0].Players.Count);
        }
    }
}
