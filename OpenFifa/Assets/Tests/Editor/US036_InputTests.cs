using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US036")]
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
        public void InputMapping_KeyboardScheme_WASD_Movement()
        {
            var mapping = new InputMappingLogic();
            string source = mapping.GetMovementSource(ControlScheme.KeyboardMouse);
            Assert.AreEqual("wasd", source);
        }

        [Test]
        public void InputMapping_GamepadScheme_LeftStick_Movement()
        {
            var mapping = new InputMappingLogic();
            string source = mapping.GetMovementSource(ControlScheme.Gamepad);
            Assert.AreEqual("leftstick", source);
        }

        [Test]
        public void InputMapping_KeyboardBindings_AllFIFAActionsPresent()
        {
            var mapping = new InputMappingLogic();
            // FIFA-style: Space=Pass, W=ThroughBall, D=Shoot, S=Tackle, LeftShift=Sprint, Q=Switch, E=LobPass, Mouse0=Shoot
            Assert.AreEqual(ActionType.Pass, mapping.GetKeyboardAction("space"));
            Assert.AreEqual(ActionType.ThroughBall, mapping.GetKeyboardAction("w"));
            Assert.AreEqual(ActionType.Shoot, mapping.GetKeyboardAction("d"));
            Assert.AreEqual(ActionType.Tackle, mapping.GetKeyboardAction("s"));
            Assert.AreEqual(ActionType.Sprint, mapping.GetKeyboardAction("leftshift"));
            Assert.AreEqual(ActionType.Switch, mapping.GetKeyboardAction("q"));
            Assert.AreEqual(ActionType.LobPass, mapping.GetKeyboardAction("e"));
            Assert.AreEqual(ActionType.Shoot, mapping.GetKeyboardAction("mouse0"));
        }

        [Test]
        public void InputMapping_GamepadBindings_AllFIFAActionsPresent()
        {
            var mapping = new InputMappingLogic();
            // FIFA-style: A=Pass, Y=ThroughBall, B=Shoot, X=Tackle, RT=Sprint, LB=Switch, RB=LobPass
            Assert.AreEqual(ActionType.Pass, mapping.GetGamepadAction("buttonsouth"));
            Assert.AreEqual(ActionType.ThroughBall, mapping.GetGamepadAction("buttonnorth"));
            Assert.AreEqual(ActionType.Shoot, mapping.GetGamepadAction("buttoneast"));
            Assert.AreEqual(ActionType.Tackle, mapping.GetGamepadAction("buttonwest"));
            Assert.AreEqual(ActionType.Sprint, mapping.GetGamepadAction("righttrigger"));
            Assert.AreEqual(ActionType.Switch, mapping.GetGamepadAction("leftshoulder"));
            Assert.AreEqual(ActionType.LobPass, mapping.GetGamepadAction("rightshoulder"));
        }

        [Test]
        public void InputMapping_UnknownKey_ReturnsNone()
        {
            var mapping = new InputMappingLogic();
            Assert.AreEqual(ActionType.None, mapping.GetKeyboardAction("f12"));
            Assert.AreEqual(ActionType.None, mapping.GetGamepadAction("unknown"));
        }

        [Test]
        public void InputMapping_CaseInsensitive_ReturnsAction()
        {
            var mapping = new InputMappingLogic();
            Assert.AreEqual(ActionType.Pass, mapping.GetKeyboardAction("Space"));
            Assert.AreEqual(ActionType.Pass, mapping.GetKeyboardAction("SPACE"));
            Assert.AreEqual(ActionType.Pass, mapping.GetGamepadAction("ButtonSouth"));
        }
    }
}
