using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class ClubKitFactoryTests
    {
        private static KitLibraryData Build(string id = "kingsmere") =>
            ClubKitFactory.Build(id, "Kingsmere United", "KMU", "#B3122E", "#FFFFFF", "#1C1C24");

        [Test]
        public void Build_ClubColours_FourValidKitsAndOneTeam()
        {
            var data = Build();
            Assert.AreEqual(1, data.Teams.Count);
            Assert.AreEqual("KMU", data.Teams[0].Initials);
            Assert.AreEqual(4, data.Kits.Count);
            foreach (var kit in data.Kits)
                CollectionAssert.IsEmpty(KitDefinitionValidator.Validate(kit), kit.Id);

            var logic = new KitLibraryLogic(data);
            CollectionAssert.IsEmpty(logic.ValidateLibrary());
            Assert.AreEqual(Soft("#B3122E"), logic.GetKit("kingsmere", KitVariant.Home).PrimaryColor);
            Assert.AreEqual(Soft("#1C1C24"), logic.GetKit("kingsmere", KitVariant.Away).PrimaryColor);
            var keeper = logic.GetKit("kingsmere", KitVariant.Goalkeeper);
            Assert.IsNotNull(keeper);
            Assert.AreEqual("Long", keeper.Sleeves);
            Assert.IsNotEmpty(keeper.GloveColor);
        }

        private static string Soft(string hex) =>
            KitColorUtility.ToHex(ClubKitFactory.Soften(KitColorUtility.ParseOrDefault(hex, SimpleColor.Black)));

        [Test]
        public void Soften_SaturatedRed_CapsSaturationKeepsValue()
        {
            var c = ClubKitFactory.Soften(new SimpleColor(1f, 0f, 0f));
            Assert.AreEqual(1f, c.R, 1e-4f);
            Assert.AreEqual(1f - ClubKitFactory.MaxSaturation, c.G, 1e-3f);
            var grey = ClubKitFactory.Soften(new SimpleColor(0.5f, 0.5f, 0.5f));
            Assert.AreEqual(0.5f, grey.G, 1e-4f);
        }

        [Test]
        public void ResolveMatchKitsContrasting_TwoDarkHomeKits_AwayPicksLightKit()
        {
            var logic = new KitLibraryLogic(ClubKitFactory.Build("a", "A", "AAA", "#1B3F8F", "#F2C230", "#FFFFFF"));
            logic.Merge(ClubKitFactory.Build("b", "B", "BBB", "#7A2639", "#86C5DA", "#F4F1E8"));
            var sel = logic.ResolveMatchKitsContrasting("a", "b");
            float home = JerseyComparer.GetLuminance(KitColorUtility.ParseOrDefault(sel.HomeKit.PrimaryColor, SimpleColor.Black));
            float away = JerseyComparer.GetLuminance(KitColorUtility.ParseOrDefault(sel.AwayKit.PrimaryColor, SimpleColor.Black));
            Assert.AreEqual(KitVariant.Away, sel.AwayVariant);
            Assert.Greater(System.Math.Abs(home - away), 0.3f);
        }

        [Test]
        public void Build_SameClub_IsDeterministic()
        {
            var a = Build();
            var b = Build();
            for (int i = 0; i < a.Kits.Count; i++)
            {
                Assert.AreEqual(a.Kits[i].Pattern, b.Kits[i].Pattern);
                Assert.AreEqual(a.Kits[i].PrimaryColor, b.Kits[i].PrimaryColor);
                Assert.AreEqual(a.Kits[i].Collar, b.Kits[i].Collar);
            }
        }

        [Test]
        public void ResolveMatchKits_GeneratedKits_NeverPicksGoalkeeperForOutfield()
        {
            var logic = new KitLibraryLogic(Build("a"));
            logic.Merge(ClubKitFactory.Build("b", "B", "BBB", "#B3122E", "#FFFFFF", "#1C1C24"));
            var sel = logic.ResolveMatchKits("a", "b");
            Assert.AreNotEqual(KitVariant.Goalkeeper, sel.AwayVariant);
            Assert.AreEqual(KitVariant.Away, sel.AwayVariant, "identical home colours -> away kit");
        }

        [Test]
        public void ResolveGoalkeeperKits_KeeperColourClashes_PicksDistinctAlternatives()
        {
            // Both clubs' outfield kits are green, the colour of the first keeper palette entry.
            var logic = new KitLibraryLogic(ClubKitFactory.Build("a", "A", "AAA", "#2BB673", "#FFFFFF", "#2BB673"));
            logic.Merge(ClubKitFactory.Build("b", "B", "BBB", "#F2A900", "#000000", "#2BB673"));
            var sel = logic.ResolveMatchKits("a", "b");
            logic.ResolveGoalkeeperKits(sel, "a", "b");

            var colours = new[]
            {
                KitColorUtility.ParseOrDefault(sel.HomeKit.PrimaryColor, SimpleColor.Black),
                KitColorUtility.ParseOrDefault(sel.AwayKit.PrimaryColor, SimpleColor.Black),
                KitColorUtility.ParseOrDefault(sel.HomeKeeperKit.PrimaryColor, SimpleColor.Black),
                KitColorUtility.ParseOrDefault(sel.AwayKeeperKit.PrimaryColor, SimpleColor.Black),
            };
            for (int i = 2; i < 4; i++)
                for (int j = 0; j < i; j++)
                    Assert.Greater(JerseyComparer.ColorDistance(colours[i], colours[j]), KitLibraryLogic.KeeperClashThreshold,
                        $"kit {i} vs {j}");
            Assert.AreEqual(KitVariant.Goalkeeper, KitDefinitionConverter.GetVariant(sel.HomeKeeperKit));
            Assert.AreEqual("Long", sel.AwayKeeperKit.Sleeves);
        }

        [Test]
        public void SkinTones_SamePlayer_SameToneAndFromPalette()
        {
            var a = KitSkinTones.ForPlayer("kingsmere-07");
            var b = KitSkinTones.ForPlayer("kingsmere-07");
            Assert.AreEqual(a.R, b.R);
            Assert.AreEqual(a.G, b.G);
            Assert.AreEqual(a.B, b.B);
        }
    }
}
