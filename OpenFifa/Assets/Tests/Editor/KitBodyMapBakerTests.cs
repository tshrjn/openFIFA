using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class KitBodyMapBakerTests
    {
        // T-pose-ish skeleton, facing +Z, metres.
        private static KitBodySkeleton Skeleton() => new KitBodySkeleton
        {
            Hips = new SimVec3(0f, 1.0f, 0f),
            Neck = new SimVec3(0f, 1.5f, 0f),
            ArmLeft = new[] { new SimVec3(-0.2f, 1.45f, 0f), new SimVec3(-0.48f, 1.45f, 0f), new SimVec3(-0.74f, 1.45f, 0f), new SimVec3(-0.9f, 1.45f, 0f) },
            ArmRight = new[] { new SimVec3(0.2f, 1.45f, 0f), new SimVec3(0.48f, 1.45f, 0f), new SimVec3(0.74f, 1.45f, 0f), new SimVec3(0.9f, 1.45f, 0f) },
            LegLeft = new[] { new SimVec3(-0.1f, 0.95f, 0f), new SimVec3(-0.1f, 0.5f, 0f), new SimVec3(-0.1f, 0.08f, 0f), new SimVec3(-0.1f, 0.02f, 0.2f) },
            LegRight = new[] { new SimVec3(0.1f, 0.95f, 0f), new SimVec3(0.1f, 0.5f, 0f), new SimVec3(0.1f, 0.08f, 0f), new SimVec3(0.1f, 0.02f, 0.2f) },
        };

        [Test]
        public void ComputeFrame_UprightRig_RightUpFrontMatchUnityAxes()
        {
            var f = KitBodyMapBaker.ComputeFrame(Skeleton());
            Assert.AreEqual(1f, f.Right.X, 1e-4f);
            Assert.AreEqual(1f, f.Up.Y, 1e-4f);
            Assert.AreEqual(1f, f.Forward.Z, 1e-4f);
        }

        [Test]
        public void ComputeFrame_MirroredLabels_StillFacesToes()
        {
            var s = Skeleton();
            var tmp = s.ArmLeft; s.ArmLeft = s.ArmRight; s.ArmRight = tmp;
            var f = KitBodyMapBaker.ComputeFrame(s);
            Assert.Greater(f.Forward.Z, 0.99f);
        }

        [Test]
        public void ChainParameter_JointsAndMidpoints_MapToSegmentParams()
        {
            var chain = Skeleton().ArmRight;
            Assert.AreEqual(0f, KitBodyMapBaker.ChainParameter(new SimVec3(0.1f, 1.45f, 0f), chain), 1e-4f);
            Assert.AreEqual(0.5f, KitBodyMapBaker.ChainParameter(new SimVec3(0.34f, 1.5f, 0.03f), chain), 1e-3f);
            Assert.AreEqual(2f, KitBodyMapBaker.ChainParameter(new SimVec3(0.74f, 1.4f, 0f), chain), 1e-3f);
        }

        [Test]
        public void Bake_LimbWeightsAndKitPositions_AreWrittenPerVertex()
        {
            var s = Skeleton();
            var positions = new[]
            {
                new SimVec3(0.34f, 1.48f, 0f),   // right upper arm
                new SimVec3(-0.1f, 0.3f, 0.05f), // left shin
                new SimVec3(0f, 1.25f, 0.12f),   // chest front
                new SimVec3(0f, 1.25f, -0.1f),   // back
            };
            var limbs = new[] { KitBodyLimb.Torso, KitBodyLimb.ArmRight, KitBodyLimb.LegLeft };
            var idx = new[] { 1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            var w = new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f };
            var b0 = new float[16];
            var b1 = new float[16];
            KitBodyMapBaker.Bake(s, positions, idx, w, 4, limbs, b0, b1);

            Assert.AreEqual(1f, b0[2], 1e-4f, "arm weight");
            Assert.AreEqual(0.5f, b0[0], 0.05f, "mid upper arm");
            Assert.AreEqual(1f, b0[4 + 3], 1e-4f, "leg weight");
            Assert.Greater(b0[4 + 1], 1.3f, "shin is between knee and ankle");
            Assert.Less(b0[4 + 1], 2f);
            Assert.Greater(b1[8 + 2], 0f, "chest is in front of the torso axis");
            Assert.Less(b1[12 + 2], 0f, "back is behind it");
            Assert.AreEqual(-0.25f, b1[8 + 1], 1e-4f, "kit Y is metres from the neck joint");
            Assert.GreaterOrEqual(b1[3], 1f, "baked marker");
            Assert.AreEqual(1f + (1.25f - 0.3f), b1[8 + 3], 1e-4f, "w = 1 + height above the lowest vertex");
        }
    }
}
