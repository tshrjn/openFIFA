using System.IO;
using NUnit.Framework;

namespace OpenFifa.Tests.Editor
{
    /// <summary>Text-level checks on the input actions asset (Unity runs EditMode tests from the project root).</summary>
    [TestFixture]
    [Category("FID-CONTROLS")]
    public class InputActionsAssetTests
    {
        private const string AssetPath = "Assets/Settings/Input/OpenFifaControls.inputactions";

        private static string Load()
        {
            Assert.IsTrue(File.Exists(AssetPath));
            return File.ReadAllText(AssetPath);
        }

        [Test]
        public void Asset_HasGamepadAndTouchControlSchemes()
        {
            string json = Load();
            Assert.IsTrue(json.Contains("\"bindingGroup\": \"Gamepad\""));
            Assert.IsTrue(json.Contains("\"bindingGroup\": \"Touch\""));
            Assert.IsTrue(json.Contains("\"devicePath\": \"<Touchscreen>\""));
        }

        [Test]
        public void Asset_BindsClassicFaceButtons()
        {
            string json = Load();
            Assert.IsTrue(json.Contains("\"path\": \"<Gamepad>/buttonSouth\""));
            Assert.IsTrue(json.Contains("\"path\": \"<Gamepad>/buttonEast\""));
            Assert.IsTrue(json.Contains("\"path\": \"<Gamepad>/buttonWest\""));
            Assert.IsTrue(json.Contains("\"path\": \"<Gamepad>/buttonNorth\""));
            Assert.IsTrue(json.Contains("\"path\": \"<Gamepad>/rightTrigger\""));
            Assert.IsTrue(json.Contains("\"path\": \"<Touchscreen>/touch0/position\""));
        }
    }
}
