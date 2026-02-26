using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-028")]
    public class US028_MainMenuTests
    {
        [Test]
        public void MenuNavigationLogic_PlayButton_TargetsTeamSelect()
        {
            var logic = new MenuNavigationLogic();
            string target = logic.GetTargetScene(MenuButton.Play);
            Assert.AreEqual("TeamSelect", target);
        }

        [Test]
        public void MenuNavigationLogic_SettingsButton_TargetsSettings()
        {
            var logic = new MenuNavigationLogic();
            string target = logic.GetTargetScene(MenuButton.Settings);
            Assert.AreEqual("Settings", target);
        }

        [Test]
        public void MenuNavigationLogic_CreditsButton_TargetsCredits()
        {
            var logic = new MenuNavigationLogic();
            string target = logic.GetTargetScene(MenuButton.Credits);
            Assert.AreEqual("Credits", target);
        }

        [Test]
        public void MenuNavigationLogic_AllButtonsHaveTargets()
        {
            var logic = new MenuNavigationLogic();
            foreach (MenuButton button in System.Enum.GetValues(typeof(MenuButton)))
            {
                string target = logic.GetTargetScene(button);
                Assert.IsFalse(string.IsNullOrEmpty(target),
                    $"Button {button} should have a non-empty target scene");
            }
        }

        [Test]
        public void MenuNavigationLogic_ButtonCount_IsThree()
        {
            var values = System.Enum.GetValues(typeof(MenuButton));
            Assert.AreEqual(3, values.Length, "Should have exactly 3 menu buttons");
        }

        [Test]
        public void MenuNavigationLogic_GameTitle_IsOpenFifa()
        {
            var logic = new MenuNavigationLogic();
            Assert.AreEqual("OpenFifa", logic.GameTitle);
        }

        [Test]
        public void CanvasScalerConfig_ReferenceResolution_Is1920x1080()
        {
            var config = new UIScalerConfig();
            Assert.AreEqual(1920, config.ReferenceWidth);
            Assert.AreEqual(1080, config.ReferenceHeight);
        }

        [Test]
        public void CanvasScalerConfig_MatchWidthOrHeight_IsHalf()
        {
            var config = new UIScalerConfig();
            Assert.AreEqual(0.5f, config.MatchWidthOrHeight, 0.001f);
        }
    }
}
