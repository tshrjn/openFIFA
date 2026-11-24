using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Offside")]
    public class OffsideLineWidthTests
    {
        [Test]
        public void AtDistance_Close_KeepsBaseWidth()
        {
            Assert.AreEqual(0.22f, OffsideLineWidth.AtDistance(0.22f, 5f, 50f), 1e-5f);
        }

        [TestCase(30f)]
        [TestCase(60f)]
        [TestCase(90f)]
        public void AtDistance_BroadcastDistance_AtLeastMinimumScreenFraction(float distance)
        {
            const float fov = 50f;
            float w = OffsideLineWidth.AtDistance(0.22f, distance, fov);
            double frustum = 2.0 * distance * System.Math.Tan(fov * 0.5 * System.Math.PI / 180.0);
            Assert.That(w / frustum, Is.GreaterThanOrEqualTo(OffsideLineWidth.DefaultMinScreenFraction - 1e-6),
                $"{w:F3} m at {distance} m is {w / frustum * 1080:F1} px at 1080p");
            Assert.That(w, Is.GreaterThanOrEqualTo(0.22f));
        }

        [Test]
        public void AtDistance_WiderWithDistance()
        {
            float near = OffsideLineWidth.AtDistance(0.22f, 40f, 50f);
            float far = OffsideLineWidth.AtDistance(0.22f, 90f, 50f);
            Assert.Greater(far, near);
        }

        [Test]
        public void AtDistance_DegenerateInputs_ReturnBaseWidth()
        {
            Assert.AreEqual(0.3f, OffsideLineWidth.AtDistance(0.3f, 0f, 50f));
            Assert.AreEqual(0.3f, OffsideLineWidth.AtDistance(0.3f, 50f, 0f));
            Assert.AreEqual(0.3f, OffsideLineWidth.AtDistance(0.3f, 50f, 50f, 0f));
        }

        [Test]
        public void Default_IsBroadcastWidth()
        {
            Assert.That(OffsideLineWidth.DefaultBaseWidth, Is.InRange(0.15f, 0.35f));
        }
    }
}
