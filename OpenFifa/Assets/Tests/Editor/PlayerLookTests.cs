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
    }
}
