using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US050")]
    [Category("Input")]
    public class US050_LocalMultiplayerTests
    {
        [Test]
        public void ControlSchemeAssigner_Player1_DefaultKeyboardMouse()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(ControlScheme.KeyboardMouse, assigner.GetScheme(0));
        }

        [Test]
        public void ControlSchemeAssigner_Player2_DefaultGamepad()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.AreEqual(ControlScheme.Gamepad, assigner.GetScheme(1));
        }

        [Test]
        public void ControlSchemeAssigner_DefaultSchemes_AreSeparate()
        {
            var assigner = new ControlSchemeAssigner();
            Assert.IsTrue(assigner.AreSchemesSeparate());
        }

        [Test]
        public void ControlSchemeAssigner_BothGamepads_NotSeparate()
        {
            var assigner = new ControlSchemeAssigner();
            assigner.SetScheme(0, ControlScheme.Gamepad);
            assigner.SetScheme(1, ControlScheme.Gamepad);
            Assert.IsFalse(assigner.AreSchemesSeparate(),
                "Both players on gamepads should not be flagged as separate schemes");
        }

        [Test]
        public void ControlSchemeAssigner_SetScheme_OverridesDefault()
        {
            var assigner = new ControlSchemeAssigner();
            assigner.SetScheme(0, ControlScheme.Gamepad);
            Assert.AreEqual(ControlScheme.Gamepad, assigner.GetScheme(0));
        }

        [Test]
        public void LocalMultiplayerConfig_DefaultConfig_TwoPlayers()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(2, config.HumanPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_DefaultConfig_AIFillsRemaining()
        {
            var config = new LocalMultiplayerConfig();
            // 5v5 = 10 players total, 2 human, 8 AI
            Assert.AreEqual(8, config.AIPlayerCount);
            Assert.AreEqual(10, config.TotalPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_Player1_ControlsTeamA()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(0, config.Player1TeamIndex);
        }

        [Test]
        public void LocalMultiplayerConfig_Player2_ControlsTeamB()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(1, config.Player2TeamIndex);
        }

        [Test]
        public void DeviceInputRouter_AssignDevice_RoutesToPlayer()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            Assert.AreEqual(0, router.GetOwningPlayer(100));
        }

        [Test]
        public void DeviceInputRouter_AssignMultipleDevices_RoutesIndependently()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0); // Keyboard -> Player 0
            router.AssignDevice(200, 1); // Gamepad -> Player 1
            Assert.AreEqual(0, router.GetOwningPlayer(100));
            Assert.AreEqual(1, router.GetOwningPlayer(200));
        }

        [Test]
        public void DeviceInputRouter_UnassignedDevice_ReturnsNegOne()
        {
            var router = new DeviceInputRouter();
            Assert.AreEqual(-1, router.GetOwningPlayer(999));
        }

        [Test]
        public void DeviceInputRouter_UnassignDevice_RemovesMapping()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            router.UnassignDevice(100);
            Assert.AreEqual(-1, router.GetOwningPlayer(100));
        }

        [Test]
        public void DeviceInputRouter_ClearAll_RemovesAllMappings()
        {
            var router = new DeviceInputRouter();
            router.AssignDevice(100, 0);
            router.AssignDevice(200, 1);
            router.ClearAll();
            Assert.AreEqual(-1, router.GetOwningPlayer(100));
            Assert.AreEqual(-1, router.GetOwningPlayer(200));
            Assert.AreEqual(0, router.AssignedDeviceCount);
        }
    }
}
