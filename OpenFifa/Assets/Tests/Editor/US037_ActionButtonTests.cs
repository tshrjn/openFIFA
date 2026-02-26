using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-037")]
    public class US037_ActionButtonTests
    {
        [Test]
        public void ActionButtonLogic_InitialState_NoButtonsPressed()
        {
            var logic = new ActionButtonLogic();
            Assert.IsFalse(logic.IsPassPressed);
            Assert.IsFalse(logic.IsShootPressed);
            Assert.IsFalse(logic.IsTacklePressed);
            Assert.IsFalse(logic.IsSprintPressed);
            Assert.IsFalse(logic.IsSwitchPressed);
            Assert.IsFalse(logic.IsThroughBallPressed);
            Assert.IsFalse(logic.IsLobPassPressed);
        }

        [Test]
        public void ActionButtonLogic_PressPass_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressPass();
            Assert.IsTrue(logic.IsPassPressed);
        }

        [Test]
        public void ActionButtonLogic_PressShoot_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressShoot();
            Assert.IsTrue(logic.IsShootPressed);
        }

        [Test]
        public void ActionButtonLogic_PressTackle_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressTackle();
            Assert.IsTrue(logic.IsTacklePressed);
        }

        [Test]
        public void ActionButtonLogic_PressThroughBall_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressThroughBall();
            Assert.IsTrue(logic.IsThroughBallPressed);
        }

        [Test]
        public void ActionButtonLogic_PressLobPass_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressLobPass();
            Assert.IsTrue(logic.IsLobPassPressed);
        }

        [Test]
        public void ActionButtonLogic_PressSwitch_SetsFlag()
        {
            var logic = new ActionButtonLogic();
            logic.PressSwitch();
            Assert.IsTrue(logic.IsSwitchPressed);
        }

        [Test]
        public void ActionButtonLogic_Sprint_HoldBehavior()
        {
            var logic = new ActionButtonLogic();
            logic.SetSprint(true);
            Assert.IsTrue(logic.IsSprintPressed);

            logic.SetSprint(false);
            Assert.IsFalse(logic.IsSprintPressed);
        }

        [Test]
        public void ActionButtonLogic_ConsumeActions_ClearsSinglePress()
        {
            var logic = new ActionButtonLogic();
            logic.PressPass();
            logic.ConsumeActions();
            Assert.IsFalse(logic.IsPassPressed, "Single-press actions should clear after consume");
        }

        [Test]
        public void ActionButtonLogic_ConsumeActions_PreservesSprint()
        {
            var logic = new ActionButtonLogic();
            logic.SetSprint(true);
            logic.ConsumeActions();
            Assert.IsTrue(logic.IsSprintPressed, "Sprint (hold) should not be cleared by consume");
        }

        [Test]
        public void ActionButtonLogic_ConsumeActions_ClearsAllSinglePress()
        {
            var logic = new ActionButtonLogic();
            logic.PressPass();
            logic.PressShoot();
            logic.PressTackle();
            logic.PressThroughBall();
            logic.PressLobPass();
            logic.PressSwitch();
            logic.ConsumeActions();
            Assert.IsFalse(logic.IsPassPressed);
            Assert.IsFalse(logic.IsShootPressed);
            Assert.IsFalse(logic.IsTacklePressed);
            Assert.IsFalse(logic.IsThroughBallPressed);
            Assert.IsFalse(logic.IsLobPassPressed);
            Assert.IsFalse(logic.IsSwitchPressed);
        }

        [Test]
        public void KeyboardMapping_SpaceKey_MapsToPass()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Pass, mapping.GetAction("space"));
        }

        [Test]
        public void KeyboardMapping_DKey_MapsToShoot()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Shoot, mapping.GetAction("d"));
        }

        [Test]
        public void KeyboardMapping_SKey_MapsToTackle()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Tackle, mapping.GetAction("s"));
        }

        [Test]
        public void KeyboardMapping_WKey_MapsToThroughBall()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.ThroughBall, mapping.GetAction("w"));
        }

        [Test]
        public void KeyboardMapping_EKey_MapsToLobPass()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.LobPass, mapping.GetAction("e"));
        }

        [Test]
        public void KeyboardMapping_QKey_MapsToSwitch()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Switch, mapping.GetAction("q"));
        }

        [Test]
        public void KeyboardMapping_LeftShift_MapsToSprint()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Sprint, mapping.GetAction("leftshift"));
        }

        [Test]
        public void KeyboardMapping_Mouse0_MapsToShoot()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Shoot, mapping.GetAction("mouse0"));
        }

        [Test]
        public void GamepadMapping_AButton_MapsToPass()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.Pass, mapping.GetAction("buttonsouth"));
        }

        [Test]
        public void GamepadMapping_BButton_MapsToShoot()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.Shoot, mapping.GetAction("buttoneast"));
        }

        [Test]
        public void GamepadMapping_XButton_MapsToTackle()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.Tackle, mapping.GetAction("buttonwest"));
        }

        [Test]
        public void GamepadMapping_YButton_MapsToThroughBall()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.ThroughBall, mapping.GetAction("buttonnorth"));
        }

        [Test]
        public void GamepadMapping_RB_MapsToLobPass()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.LobPass, mapping.GetAction("rightshoulder"));
        }

        [Test]
        public void GamepadMapping_LB_MapsToSwitch()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.Switch, mapping.GetAction("leftshoulder"));
        }

        [Test]
        public void GamepadMapping_RT_MapsToSprint()
        {
            var mapping = new GamepadActionMapping();
            Assert.AreEqual(ActionType.Sprint, mapping.GetAction("righttrigger"));
        }
    }
}
