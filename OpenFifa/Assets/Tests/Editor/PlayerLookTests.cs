using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class PlayerLookTests
    {
        [Test]
        public void ForPlayer_SameId_SameLook()
        {
            var a = PlayerLook.ForPlayer("ashbury-09");
            var b = PlayerLook.ForPlayer("ashbury-09");
            Assert.AreEqual(a.Hair, b.Hair);
            Assert.AreEqual(a.Facial, b.Facial);
            Assert.AreEqual(a.SkinIndex, b.SkinIndex);
            Assert.AreEqual(a.HairColour.R, b.HairColour.R);
            Assert.AreEqual(a.BootAccent.G, b.BootAccent.G);
        }

        [Test]
        public void ForPlayer_FullSquads_VariedSkinHairAndBoots()
        {
            var skins = new HashSet<int>();
            var hair = new HashSet<HairStyle>();
            var facial = new HashSet<FacialHair>();
            for (int i = 1; i <= 44; i++)
            {
                var look = PlayerLook.ForPlayer((i <= 22 ? "kingsmere-" : "ashbury-") + i.ToString("00"));
                skins.Add(look.SkinIndex);
                hair.Add(look.Hair);
                facial.Add(look.Facial);
                Assert.Less(JerseyComparer.GetLuminance(look.BootBase), 0.95f);
            }
            Assert.GreaterOrEqual(skins.Count, 4, "skin tones vary across two squads");
            Assert.GreaterOrEqual(hair.Count, 3, "hair styles vary");
            Assert.GreaterOrEqual(facial.Count, 2, "facial hair varies");
        }

        private static RosterAppearance Full() => RosterAppearance.FromFields(0.875f, "afro", "#C9A66B", "moustache",
            188, "stocky", "#FFFFFF");

        private static RosterAppearance Empty() => RosterAppearance.FromFields(null, null, null, null, null, null, null);

        [Test]
        public void ForPlayer_RosterSkinTone_UsesToneNotHash()
        {
            var dark = PlayerLook.ForPlayer("x-1", RosterAppearance.FromFields(1f, null, null, null, null, null, null));
            var light = PlayerLook.ForPlayer("x-1", RosterAppearance.FromFields(0f, null, null, null, null, null, null));
            Assert.AreEqual(KitSkinTones.Palette.Length - 1, dark.SkinIndex);
            Assert.AreEqual(0, light.SkinIndex);
            Assert.Greater(JerseyComparer.GetLuminance(light.Skin), JerseyComparer.GetLuminance(dark.Skin));
            var mid = PlayerLook.ForPlayer("x-1", RosterAppearance.FromFields(0.5f, null, null, null, null, null, null));
            Assert.AreEqual(3, mid.SkinIndex);
        }

        [Test]
        public void ForPlayer_RosterHairStyleTokens_MapToStyles()
        {
            var expected = new System.Collections.Generic.Dictionary<string, HairStyle>
            {
                { "buzz", HairStyle.Buzz }, { "short", HairStyle.Short }, { "curly", HairStyle.Curly }, { "afro", HairStyle.Curly },
                { "long_tied", HairStyle.Tied }, { "bald", HairStyle.Bald }, { "braids", HairStyle.Buzz },
            };
            foreach (var token in RosterAppearance.HairStyles)
            {
                var look = PlayerLook.ForPlayer("x-2", RosterAppearance.FromFields(null, token, null, null, null, null, null));
                Assert.AreEqual(expected[token], look.Hair, token);
            }
        }

        [Test]
        public void ForPlayer_RosterHairColourFacialHairAndBoots_AreUsed()
        {
            var look = PlayerLook.ForPlayer("x-3", Full());
            Assert.AreEqual(KitColorUtility.ParseOrDefault("#C9A66B", SimpleColor.Black).R, look.HairColour.R, 1e-4f);
            Assert.AreEqual(FacialHair.Goatee, look.Facial);
            Assert.AreEqual(1f, look.BootBase.R, 1e-4f);
            Assert.Less(JerseyComparer.GetLuminance(look.BootAccent), 0.2f, "white boots get a dark accent");
            foreach (var token in RosterAppearance.FacialHairStyles)
                Assert.IsTrue(PlayerLook.TryFacialHair(token, out _), token);
        }

        [Test]
        public void ForPlayer_MissingRosterFields_FallBackToIdHash()
        {
            var seeded = PlayerLook.ForPlayer("arsenal-07");
            var fromEmpty = PlayerLook.ForPlayer("arsenal-07", Empty());
            var fromNull = PlayerLook.ForPlayer("arsenal-07", null);
            foreach (var look in new[] { fromEmpty, fromNull })
            {
                Assert.AreEqual(seeded.SkinIndex, look.SkinIndex);
                Assert.AreEqual(seeded.Hair, look.Hair);
                Assert.AreEqual(seeded.Facial, look.Facial);
                Assert.AreEqual(seeded.HairColour.G, look.HairColour.G, 1e-5f);
                Assert.AreEqual(seeded.BootBase.B, look.BootBase.B, 1e-5f);
            }
        }

        [Test]
        public void ForPlayer_PartialRoster_OnlyGivenFieldsOverride()
        {
            var seeded = PlayerLook.ForPlayer("chelsea-10");
            var look = PlayerLook.ForPlayer("chelsea-10", RosterAppearance.FromFields(null, "bald", null, null, null, null, null));
            Assert.AreEqual(HairStyle.Bald, look.Hair);
            Assert.AreEqual(seeded.SkinIndex, look.SkinIndex);
            Assert.AreEqual(seeded.Facial, look.Facial);
        }

        [Test]
        public void FromFields_RecordsWhichFieldsWereGiven()
        {
            var e = Empty();
            Assert.IsFalse(e.HasSkinTone); Assert.IsFalse(e.HasHairColour); Assert.IsFalse(e.HasBootColour);
            Assert.IsFalse(e.HasHeight); Assert.IsFalse(e.HasBuild);
            Assert.IsNull(e.HairStyle); Assert.IsNull(e.FacialHair);
            var f = Full();
            Assert.IsTrue(f.HasSkinTone && f.HasHairColour && f.HasBootColour && f.HasHeight && f.HasBuild);
            Assert.AreEqual(188, f.HeightCm);
            Assert.AreEqual("stocky", f.Build);
        }

        [Test]
        public void ShirtName_RosterNameOrFoldedKnownAs()
        {
            Assert.AreEqual("PETROVIC", KitShirtName.For("PETROVIĆ", "Petrović"));
            Assert.AreEqual("DI GREGORIO", KitShirtName.For("DI GREGORIO", "Di Gregorio"));
            Assert.AreEqual("ODEGAARD", KitShirtName.For(null, "Ødegaard"));
            Assert.AreEqual("DORDE", KitShirtName.For("", "Đorđe"));
            Assert.AreEqual("MULLER", KitShirtName.Fold("Müller"));
            Assert.AreEqual("STRASSE", KitShirtName.Fold("straße"));
            Assert.AreEqual("ESTEVAO", KitShirtName.Fold("Estêvão"));
            Assert.AreEqual("", KitShirtName.For(null, null));
        }
    }
}
