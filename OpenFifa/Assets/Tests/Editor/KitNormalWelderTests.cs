using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class KitNormalWelderTests
    {
        [Test]
        public void Weld_SeamVerticesWithinRadius_ShareAveragedNormal()
        {
            var p = new[] { new SimVec3(0f, 0f, 0f), new SimVec3(0.005f, 0f, 0f), new SimVec3(1f, 0f, 0f) };
            var n = new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
            n[3] = 0.5f; n[4] = 0.8660254f;   // 60 degrees apart: a seam, not opposite faces
            var w = KitNormalWelder.Weld(p, n, 0.012f);
            Assert.AreEqual(w[0], w[3], 1e-5f);
            Assert.AreEqual(w[1], w[4], 1e-5f);
            Assert.AreEqual(0.8660254f, w[0], 1e-3f);   // average direction is 30 degrees from each
            Assert.AreEqual(1f, w[8], 1e-5f, "far vertex unchanged");
        }

        [Test]
        public void Weld_OppositeFacingNeighbours_AreNotMixed()
        {
            var p = new[] { new SimVec3(0f, 0f, 0f), new SimVec3(0.004f, 0f, 0f) };
            var n = new[] { 0f, 0f, 1f, 0f, 0f, -1f };
            var w = KitNormalWelder.Weld(p, n, 0.012f);
            Assert.AreEqual(1f, w[2], 1e-5f);
            Assert.AreEqual(-1f, w[5], 1e-5f);
        }
    }
}
