using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class KitLibraryClashTests
    {
        private static KitDefinitionData Kit(string team, string variant, string primary, string number = "#FFFFFF")
        {
            return new KitDefinitionData
            {
                Id = team + "-" + variant.ToLowerInvariant(),
                TeamId = team,
                Variant = variant,
                PrimaryColor = primary,
                NumberColor = number
            };
        }

        private static KitLibraryData Library()
        {
            return new KitLibraryData
            {
                DefaultHomeTeamId = "blues",
                DefaultAwayTeamId = "reds",
                Teams = new List<KitTeamData>
                {
                    new KitTeamData { Id = "blues", Name = "Blues", Initials = "B",
                        Roster = new List<KitRosterEntry> { new KitRosterEntry { Name = "Keeper", Number = 1 } } },
                    new KitTeamData { Id = "reds", Name = "Reds", Initials = "R" },
                    new KitTeamData { Id = "navy", Name = "Navy", Initials = "N" },
                    new KitTeamData { Id = "allblue", Name = "All Blue", Initials = "AB" }
                },
                Kits = new List<KitDefinitionData>
                {
                    Kit("blues", "Home", "#1F4FC9"),
                    Kit("blues", "Away", "#FFFFFF", "#000000"),
                    Kit("reds", "Home", "#C8182B"),
                    Kit("reds", "Away", "#14213D"),
                    Kit("navy", "Home", "#2050CC"),
                    Kit("navy", "Away", "#2252C8"),
                    Kit("navy", "Third", "#F2C230", "#000000"),
                    Kit("allblue", "Home", "#1F4FC9"),
                    Kit("allblue", "Away", "#1E50CA")
                }
            };
        }

        [Test]
        public void Merge_ValidLibrary_IndexesAllKitsWithoutErrors()
        {
            var logic = new KitLibraryLogic(Library());
            Assert.AreEqual(9, logic.KitCount);
            Assert.AreEqual(4, logic.TeamCount);
            Assert.IsEmpty(logic.ValidateLibrary(), string.Join(" | ", logic.ValidateLibrary()));
        }

        [Test]
        public void ResolveMatchKits_DifferentColours_AwayKeepsHomeKit()
        {
            var selection = new KitLibraryLogic(Library()).ResolveMatchKits("blues", "reds");
            Assert.AreEqual("blues-home", selection.HomeKit.Id);
            Assert.AreEqual(KitVariant.Home, selection.AwayVariant);
            Assert.AreEqual("reds-home", selection.AwayKit.Id);
            Assert.IsFalse(selection.ClashUnresolved);
        }

        [Test]
        public void ResolveMatchKits_HomeAndAwayClash_AwaySkipsToThird()
        {
            // Navy home and away are both blue: away side must pick the yellow third kit.
            var selection = new KitLibraryLogic(Library()).ResolveMatchKits("blues", "navy");
            Assert.AreEqual(KitVariant.Third, selection.AwayVariant);
            Assert.AreEqual("navy-third", selection.AwayKit.Id);
            Assert.IsFalse(selection.ClashUnresolved);
        }

        [Test]
        public void ResolveMatchKits_PrimaryClash_AwaySwitchesToAway()
        {
            var selection = new KitLibraryLogic(Library()).ResolveMatchKits("navy", "blues");
            Assert.AreEqual(KitVariant.Away, selection.AwayVariant);
            Assert.AreEqual("blues-away", selection.AwayKit.Id);
        }

        [Test]
        public void ResolveMatchKits_EveryVariantClashes_FallsBackToHomeAndFlags()
        {
            var selection = new KitLibraryLogic(Library()).ResolveMatchKits("blues", "allblue");
            Assert.AreEqual(KitVariant.Home, selection.AwayVariant);
            Assert.IsTrue(selection.ClashUnresolved);
        }

        [Test]
        public void BuildVariantManager_MatchesExistingClashLogic()
        {
            var logic = new KitLibraryLogic(Library());
            var manager = logic.BuildVariantManager("navy");
            Assert.AreEqual(3, manager.KitCount);

            var homePrimary = KitDefinitionConverter.ToJerseyDesign(logic.GetKit("blues", KitVariant.Home)).PrimaryColor;
            Assert.AreEqual(KitVariant.Third, manager.SelectNonClashingVariant(homePrimary));
        }

        [Test]
        public void Merge_InvalidKit_IsExcludedAndReported()
        {
            var data = Library();
            data.Kits.Add(new KitDefinitionData { Id = "broken", TeamId = "reds", Variant = "Third", PrimaryColor = "nope" });
            var logic = new KitLibraryLogic(data);
            Assert.IsNull(logic.GetKitById("broken"));
            Assert.IsNotEmpty(logic.Errors);
        }

        [Test]
        public void Merge_LicensedPackSameSlot_ReplacesPlaceholderKit()
        {
            var logic = new KitLibraryLogic(Library());
            var licensed = new KitLibraryData
            {
                Kits = new List<KitDefinitionData> { Kit("reds", "Home", "#B01020") }
            };
            licensed.Kits[0].Id = "reds-home-licensed";
            licensed.Kits[0].Licensed.AlbedoPath = "Kits/Licensed/reds_home_albedo";

            logic.Merge(licensed);

            var home = logic.GetKit("reds", KitVariant.Home);
            Assert.AreEqual("reds-home-licensed", home.Id);
            Assert.IsNull(logic.GetKitById("reds-home"), "Old kit in the same slot should be replaced.");
            Assert.AreEqual(9, logic.KitCount);
        }

        [Test]
        public void ValidateLibrary_KitForUnknownTeam_Reported()
        {
            var data = Library();
            data.Kits.Add(Kit("ghosts", "Home", "#777777", "#000000"));
            var problems = new KitLibraryLogic(data).ValidateLibrary();
            Assert.That(problems.Exists(p => p.Contains("ghosts")), string.Join(" | ", problems));
        }

        [Test]
        public void ValidateLibrary_TeamWithoutHomeKit_Reported()
        {
            var data = Library();
            data.Teams.Add(new KitTeamData { Id = "nokit", Name = "No Kit" });
            var problems = new KitLibraryLogic(data).ValidateLibrary();
            Assert.That(problems.Exists(p => p.Contains("nokit")), string.Join(" | ", problems));
        }

        [Test]
        public void GetRosterEntry_KnownAndUnknownSlots_ReturnsRosterOrFallback()
        {
            var logic = new KitLibraryLogic(Library());
            Assert.AreEqual("Keeper", logic.GetRosterEntry("blues", 0).Name);
            var fallback = logic.GetRosterEntry("blues", 3);
            Assert.AreEqual("", fallback.Name);
            Assert.AreEqual(4, fallback.Number);
        }

        [Test]
        public void DefaultTeamIds_FromData_AreExposed()
        {
            var logic = new KitLibraryLogic(Library());
            Assert.AreEqual("blues", logic.DefaultHomeTeamId);
            Assert.AreEqual("reds", logic.DefaultAwayTeamId);
        }
    }
}
