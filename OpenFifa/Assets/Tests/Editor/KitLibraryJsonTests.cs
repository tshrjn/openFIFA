using NUnit.Framework;
using UnityEngine;
using OpenFifa.Core;
using OpenFifa.Gameplay;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// JSON round-trip and the shipped placeholder library (needs UnityEngine.JsonUtility / Resources).
    /// </summary>
    [TestFixture]
    [Category("Kits")]
    public class KitLibraryJsonTests
    {
        private const string Json = @"{
            ""SchemaVersion"": 1,
            ""DefaultHomeTeamId"": ""a"",
            ""DefaultAwayTeamId"": ""b"",
            ""Teams"": [
                { ""Id"": ""a"", ""Name"": ""Team A"", ""Initials"": ""TA"", ""Roster"": [ { ""Name"": ""One"", ""Number"": 7 } ] },
                { ""Id"": ""b"", ""Name"": ""Team B"", ""Initials"": ""TB"", ""Roster"": [] }
            ],
            ""Kits"": [
                { ""Id"": ""a-home"", ""TeamId"": ""a"", ""Variant"": ""Home"", ""PrimaryColor"": ""#FF0000"", ""SecondaryColor"": ""#FFFFFF"",
                  ""TertiaryColor"": ""#000000"", ""ShortsColor"": ""#FFFFFF"", ""SocksColor"": ""#FF0000"", ""Pattern"": ""Sash"", ""PatternScale"": 1.0,
                  ""Collar"": ""Round"", ""FontStyle"": ""Block"", ""NameLayout"": ""Arched"", ""NumberColor"": ""#FFFFFF"",
                  ""NumberOutlineColor"": ""#000000"", ""NumberOutlineWidth"": 0.0, ""ShowFrontNumber"": true, ""FabricSheen"": 0.5,
                  ""Licensed"": { ""AlbedoPath"": """" } },
                { ""Id"": ""b-home"", ""TeamId"": ""b"", ""Variant"": ""Home"", ""PrimaryColor"": ""#FF1010"", ""NumberColor"": ""#FFFFFF"",
                  ""SecondaryColor"": ""#000000"", ""TertiaryColor"": ""#000000"", ""ShortsColor"": ""#FFFFFF"", ""SocksColor"": ""#FFFFFF"",
                  ""Pattern"": ""Solid"", ""PatternScale"": 1.0, ""Collar"": ""Round"", ""FontStyle"": ""Block"", ""NameLayout"": ""Straight"",
                  ""NumberOutlineColor"": ""#000000"", ""NumberOutlineWidth"": 0.0, ""ShowFrontNumber"": true, ""FabricSheen"": 0.5, ""Licensed"": { ""AlbedoPath"": """" } },
                { ""Id"": ""b-away"", ""TeamId"": ""b"", ""Variant"": ""Away"", ""PrimaryColor"": ""#FFFFFF"", ""NumberColor"": ""#000000"",
                  ""SecondaryColor"": ""#000000"", ""TertiaryColor"": ""#000000"", ""ShortsColor"": ""#FFFFFF"", ""SocksColor"": ""#FFFFFF"",
                  ""Pattern"": ""Solid"", ""PatternScale"": 1.0, ""Collar"": ""Round"", ""FontStyle"": ""Block"", ""NameLayout"": ""Straight"",
                  ""NumberOutlineColor"": ""#000000"", ""NumberOutlineWidth"": 0.0, ""ShowFrontNumber"": true, ""FabricSheen"": 0.5, ""Licensed"": { ""AlbedoPath"": """" } }
            ]
        }";

        [Test]
        public void ParseJson_Library_ResolvesClashFromData()
        {
            var data = KitLibrary.ParseJson(Json);
            var logic = new KitLibraryLogic(data);

            Assert.AreEqual(3, logic.KitCount, string.Join(" | ", logic.Errors));
            Assert.AreEqual(7, logic.GetRosterEntry("a", 0).Number);
            Assert.AreEqual(JerseyPattern.Sash, KitDefinitionConverter.GetPattern(logic.GetKitById("a-home")));

            var selection = logic.ResolveMatchKits("a", "b");
            Assert.AreEqual(KitVariant.Away, selection.AwayVariant, "Red vs red must switch the away side.");
        }

        [Test]
        public void KitDefinition_JsonRoundTrip_PreservesData()
        {
            var data = KitLibrary.ParseJson(Json).Kits[0];
            var definition = KitDefinition.CreateRuntime(data);
            var copy = KitDefinition.FromJson(definition.ToJson());

            Assert.AreEqual("a-home", copy.Id);
            Assert.AreEqual("Sash", copy.Data.Pattern);
            Assert.AreEqual(KitVariant.Home, copy.Variant);
            Assert.IsEmpty(copy.Validate());

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(copy);
        }

        [Test]
        public void DefaultKits_ShippedJson_IsValidAndCompleteForDefaults()
        {
            var asset = Resources.Load<TextAsset>(KitLibrary.DefaultJsonResourcePath);
            Assert.IsNotNull(asset, "Resources/Kits/DefaultKits.json must exist.");

            var logic = new KitLibraryLogic(KitLibrary.ParseJson(asset.text));
            var problems = logic.ValidateLibrary();
            Assert.IsEmpty(problems, string.Join(" | ", problems));

            var selection = logic.ResolveMatchKits(logic.DefaultHomeTeamId, logic.DefaultAwayTeamId);
            Assert.IsNotNull(selection.HomeKit);
            Assert.IsNotNull(selection.AwayKit);
            Assert.IsFalse(selection.ClashUnresolved);
        }
    }
}
