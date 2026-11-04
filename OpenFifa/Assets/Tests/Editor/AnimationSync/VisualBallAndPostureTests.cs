using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.AnimationSync
{
    /// <summary>
    /// Foot IK aims at the ball as drawn (scaled up for readability, resting on the grass), and side-on moves show the
    /// defensive jockey only for the side out of possession.
    /// </summary>
    [TestFixture]
    [Category("FID_ANIM")]
    public class VisualBallAndPostureTests
    {
        private const float SimR = 0.11f;

        [Test]
        public void StrikeTarget_ScaledBall_TargetSitsOnTheDrawnSurface()
        {
            var ball = new SimVec3(2f, SimR, 3f);
            // Planned contact: on the sim ball's surface, behind it (-Z) and a little low.
            var planned = ball + new SimVec3(0f, -0.03f, -SimR);
            float vr = 1.8f * SimR;
            var t = VisualBallContact.StrikeTarget(planned, ball, SimR, vr);
            var c = VisualBallContact.VisualCentre(ball, SimR, vr);
            Assert.AreEqual(vr, c.Y, 1e-5f, "a grounded drawn ball rests on the grass");
            float gapToSurface = (t - c).Magnitude - vr;
            Assert.That(gapToSurface, Is.InRange(-0.005f, 0.04f), "the boot meets the drawn ball, not its inside");
            Assert.Less(t.Z, planned.Z, "pushed out behind the bigger ball");
        }

        [Test]
        public void StrikeTarget_TrueSizeBall_Unchanged()
        {
            var ball = new SimVec3(0f, SimR, 0f);
            var planned = new SimVec3(0.1f, 0.05f, -0.1f);
            Assert.AreEqual(planned, VisualBallContact.StrikeTarget(planned, ball, SimR, SimR));
            Assert.AreEqual(planned, VisualBallContact.PlantTarget(planned, ball, SimR, SimR));
        }

        [Test]
        public void PlantTarget_ScaledBall_MovesAwayOnTheGround()
        {
            var ball = new SimVec3(0f, SimR, 0f);
            var planned = new SimVec3(-0.25f, 0.02f, -0.05f); // plant foot beside and a bit behind
            float vr = 1.8f * SimR;
            var t = VisualBallContact.PlantTarget(planned, ball, SimR, vr);
            Assert.AreEqual(planned.Y, t.Y, 1e-6f, "plant foot stays on the grass");
            Assert.AreEqual(SimMath.FlatDistance(planned, ball) + (vr - SimR), SimMath.FlatDistance(t, ball), 1e-4f);
        }

        [Test]
        public void VisualCentre_AirborneBall_KeepsItsHeight()
        {
            var c = VisualBallContact.VisualCentre(new SimVec3(0f, 1.5f, 0f), SimR, 0.2f);
            Assert.AreEqual(1.5f, c.Y, 1e-6f);
        }

        [Test]
        public void Defending_OnlyTheSideOutOfPossession()
        {
            Assert.IsFalse(StrafePosture.Defending(0, 0, 0), "our ball: attackers side-step upright");
            Assert.IsTrue(StrafePosture.Defending(0, 1, 1), "their ball: jockey");
            Assert.IsFalse(StrafePosture.Defending(0, -1, 0), "loose, we touched it last");
            Assert.IsTrue(StrafePosture.Defending(0, -1, 1), "loose, they touched it last");
            Assert.IsFalse(StrafePosture.Defending(1, -1, -1), "kick-off ball nobody touched: neutral");
        }

        [Test]
        public void Step_Turnover_BlendsWithoutSnapping()
        {
            float g = 0f;
            g = StrafePosture.Step(g, true, 1f / 60f);
            Assert.Less(g, 0.1f, "no snap on a turnover");
            for (int i = 0; i < 120; i++) g = StrafePosture.Step(g, true, 1f / 60f);
            Assert.Greater(g, 0.95f, "jockey within two seconds");
            for (int i = 0; i < 120; i++) g = StrafePosture.Step(g, false, 1f / 60f);
            Assert.Less(g, 0.05f);
        }
    }
}
