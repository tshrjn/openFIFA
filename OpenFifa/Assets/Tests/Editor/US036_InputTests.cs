using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-036")]
    public class US036_InputTests
    {
        [Test]
        public void InputLogic_DeadZone_DefaultIs10Percent()
        {
            var logic = new InputFilterLogic();
            Assert.AreEqual(0.1f, logic.DeadZone, 0.001f);
        }

        [Test]
        public void InputLogic_ApplyDeadZone_BelowThreshold_ReturnsZero()
        {
            var logic = new InputFilterLogic();
            float x, y;
            logic.ApplyDeadZone(0.05f, 0.05f, out x, out y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void InputLogic_ApplyDeadZone_AboveThreshold_PassesThrough()
        {
            var logic = new InputFilterLogic();
            float x, y;
            logic.ApplyDeadZone(0.5f, 0.5f, out x, out y);
            Assert.Greater(x, 0f);
            Assert.Greater(y, 0f);
        }

        [Test]
        public void InputLogic_Normalize_ClampsToOne()
        {
            var logic = new InputFilterLogic();
            float x, y;
            logic.Normalize(2f, 0f, out x, out y);
            Assert.AreEqual(1f, x, 0.001f);
            Assert.AreEqual(0f, y, 0.001f);
        }

        [Test]
        public void InputLogic_Normalize_PreservesDirection()
        {
            var logic = new InputFilterLogic();
            float x, y;
            logic.Normalize(0.6f, 0.8f, out x, out y);
            Assert.AreEqual(0.6f, x, 0.01f);
            Assert.AreEqual(0.8f, y, 0.01f);
        }

        [Test]
        public void InputLogic_Normalize_DiagonalClamped()
        {
            var logic = new InputFilterLogic();
            float x, y;
            logic.Normalize(1f, 1f, out x, out y);
            float magnitude = x * x + y * y;
            Assert.LessOrEqual(magnitude, 1.01f, "Diagonal should be clamped to unit circle");
        }

        [Test]
        public void VirtualJoystickLogic_InitialOutput_IsZero()
        {
            var logic = new VirtualJoystickLogic(100f, 0.1f);
            Assert.AreEqual(0f, logic.OutputX);
            Assert.AreEqual(0f, logic.OutputY);
        }

        [Test]
        public void VirtualJoystickLogic_Drag_ProducesOutput()
        {
            var logic = new VirtualJoystickLogic(100f, 0.1f);
            logic.OnPointerDown(200f, 200f);
            logic.OnDrag(250f, 250f);

            float magnitude = logic.OutputX * logic.OutputX + logic.OutputY * logic.OutputY;
            Assert.Greater(magnitude, 0f, "Dragging should produce non-zero output");
        }

        [Test]
        public void VirtualJoystickLogic_Release_ReturnsToZero()
        {
            var logic = new VirtualJoystickLogic(100f, 0.1f);
            logic.OnPointerDown(200f, 200f);
            logic.OnDrag(250f, 250f);
            logic.OnPointerUp();

            Assert.AreEqual(0f, logic.OutputX);
            Assert.AreEqual(0f, logic.OutputY);
        }

        [Test]
        public void VirtualJoystickLogic_Output_ClampedToOne()
        {
            var logic = new VirtualJoystickLogic(100f, 0.1f);
            logic.OnPointerDown(200f, 200f);
            logic.OnDrag(500f, 500f); // Far drag

            float magnitude = logic.OutputX * logic.OutputX + logic.OutputY * logic.OutputY;
            Assert.LessOrEqual(magnitude, 1.01f, "Output should be clamped to unit circle");
        }
    }
}
