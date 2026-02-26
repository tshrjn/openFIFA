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
        public void ActionButtonLogic_MultipleActions_OnlyOneActive()
        {
            var logic = new ActionButtonLogic();
            logic.PressPass();
            logic.PressShoot();
            // Last action should take priority
            Assert.IsTrue(logic.IsShootPressed);
        }

        [Test]
        public void KeyboardMapping_ZKey_MapsToPass()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Pass, mapping.GetAction("z"));
        }

        [Test]
        public void KeyboardMapping_XKey_MapsToShoot()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Shoot, mapping.GetAction("x"));
        }

        [Test]
        public void KeyboardMapping_CKey_MapsToTackle()
        {
            var mapping = new KeyboardActionMapping();
            Assert.AreEqual(ActionType.Tackle, mapping.GetAction("c"));
        }
    }
}
