using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// The offside presentation's axis mapping: lines span the pitch width at the attacker's and the second-last
    /// defender's depth along the pitch length, for the 4v4 scene (length along X) and the 11v11 sim (length along Z),
    /// for both teams / attack directions.
    /// </summary>
    [TestFixture]
    [Category("Offside")]
    public class OffsidePitchFrameTests
    {
        private const float Eps = 1e-4f;
        private const float SimHalfWidth = 34f;

        [Test]
        public void LengthAlongX_LineEndpoints_SpanWidthAlongZAtDepthX()
        {
            var frame = OffsidePitchFrame.LengthAlongX(15f);
            frame.LineEndpoints(12.5f, 1f, out float sx, out float sz, out float ex, out float ez);
            Assert.AreEqual(12.5f, sx, Eps);
            Assert.AreEqual(12.5f, ex, Eps);
            // Original 4v4 behaviour: sweep from the -Z (camera) touchline to +Z.
            Assert.AreEqual(-15f, sz, Eps);
            Assert.AreEqual(15f, ez, Eps);
        }

        [Test]
        public void LengthAlongX_PartialReveal_MatchesOriginalLerp()
        {
            var frame = OffsidePitchFrame.LengthAlongX(15f);
            frame.LineEndpoints(-3f, 0.25f, out _, out float sz, out _, out float ez);
            Assert.AreEqual(-15f, sz, Eps);
            Assert.AreEqual(-15f + 30f * 0.25f, ez, Eps);
        }

        [TestCase(30.4f, 29.1f)]    // home attacking +Z
        [TestCase(-37.55f, -36.24f)] // away attacking -Z (sim seed 1's first offside)
        public void SimPitch_LinesAtAttackerAndDefenderZ_SpanFullWidthAlongX(float attackerZ, float defenderZ)
        {
            var frame = OffsidePitchFrame.ForSimPitch(PitchSpec.Standard);
            Assert.AreEqual(PitchLengthAxis.Z, frame.LengthAxis);
            Assert.AreEqual(SimHalfWidth, frame.HalfWidth, Eps);

            foreach (float depth in new[] { attackerZ, defenderZ })
            {
                frame.LineEndpoints(depth, 1f, out float sx, out float sz, out float ex, out float ez);
                Assert.AreEqual(depth, sz, Eps, "line start must sit at the line's Z");
                Assert.AreEqual(depth, ez, Eps, "line end must sit at the line's Z");
                Assert.AreEqual(SimHalfWidth, sx, Eps, "starts on the +X (camera) touchline");
                Assert.AreEqual(-SimHalfWidth, ex, Eps, "ends on the far touchline");
            }
        }

        [Test]
        public void SimPitch_DecisionFromSimEvent_LinesUseWorldZ()
        {
            var e = new MatchEvent
            {
                Type = MatchEventType.Offside, PlayerId = 20, OtherPlayerId = 17, Team = SimTeam.Away,
                Position = new SimVec3(-6.8f, 0f, -37.55f), DefenderLineZ = -36.24f, BallLineZ = -34.17f
            };
            var d = SimOffsideDecisionSource.ToDecision(e);
            var frame = OffsidePitchFrame.ForSimPitch(PitchSpec.Standard);
            frame.LineEndpoints(d.AttackerLineX, 1f, out _, out float az, out _, out _);
            frame.LineEndpoints(d.DefenderLineX, 1f, out _, out float dz, out _, out _);
            Assert.AreEqual(-37.55f, az, Eps);
            Assert.AreEqual(-36.24f, dz, Eps);
        }

        [Test]
        public void LengthAlongX_CameraPose_MatchesOriginalCut()
        {
            var frame = OffsidePitchFrame.LengthAlongX(15f);
            frame.CameraPose(8f, 14f, 6f, out float px, out float py, out float pz, out float lx, out float ly, out float lz);
            Assert.AreEqual(8f, px, Eps);
            Assert.AreEqual(14f, py, Eps);
            Assert.AreEqual(-21f, pz, Eps);
            Assert.AreEqual(8f, lx, Eps);
            Assert.AreEqual(0f, ly, Eps);
            Assert.AreEqual(15f * 0.25f, lz, Eps);
        }

        [TestCase(30f)]
        [TestCase(-30f)]
        public void SimPitch_CameraPose_SideOnFromPlusXLookingAcrossAlongTheLines(float depth)
        {
            var frame = OffsidePitchFrame.ForSimPitch(PitchSpec.Standard);
            frame.CameraPose(depth, 14f, 6f, out float px, out float py, out float pz, out float lx, out _, out float lz);
            Assert.AreEqual(SimHalfWidth + 6f, px, Eps, "beyond the +X touchline, where the broadcast camera works");
            Assert.AreEqual(14f, py, Eps);
            Assert.AreEqual(depth, pz, Eps, "level with the lines");
            Assert.AreEqual(depth, lz, Eps, "aimed along the lines, so they read as parallel");
            Assert.Less(lx, 0f, "looks across the pitch towards the far touchline");
        }

        [Test]
        public void LengthAlongX_Assistant_NearTouchlineFacingPlusZ()
        {
            var frame = OffsidePitchFrame.LengthAlongX(15f);
            frame.AssistantPosition(4f, 1.2f, false, out float x, out float z);
            Assert.AreEqual(4f, x, Eps);
            Assert.AreEqual(-16.2f, z, Eps);
            Assert.AreEqual(0f, frame.AssistantFacingYaw(false), Eps);
        }

        [Test]
        public void SimPitch_AssistantOnFarSide_StandsAtMinusXFacingPlusX()
        {
            var frame = OffsidePitchFrame.ForSimPitch(PitchSpec.Standard);
            frame.AssistantPosition(-20f, 1.2f, true, out float x, out float z);
            Assert.AreEqual(-(SimHalfWidth + 1.2f), x, Eps);
            Assert.AreEqual(-20f, z, Eps);
            Assert.AreEqual(90f, frame.AssistantFacingYaw(true), Eps);
            Assert.AreEqual(-90f, frame.AssistantFacingYaw(false), Eps);
        }

        [Test]
        public void DepthOf_ReadsTheLengthAxis()
        {
            Assert.AreEqual(3f, OffsidePitchFrame.LengthAlongX(15f).DepthOf(3f, 7f), Eps);
            Assert.AreEqual(7f, OffsidePitchFrame.LengthAlongZ(34f).DepthOf(3f, 7f), Eps);
        }
    }
}
