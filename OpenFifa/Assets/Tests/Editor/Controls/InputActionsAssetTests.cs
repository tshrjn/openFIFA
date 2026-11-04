using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>
    /// Parses the input actions asset with the engine-free <see cref="JsonParser"/> and checks the concrete
    /// action -> binding pairs for both control schemes. Unity runs EditMode tests from the project root.
    /// </summary>
    [TestFixture]
    [Category("FID-CONTROLS")]
    public class InputActionsAssetTests
    {
        private const string AssetPath = "Assets/Settings/Input/OpenFifaControls.inputactions";

        private struct Binding
        {
            public string Action;
            public string Path;
            public string Groups;
        }

        /// <summary>Classic preset, Gamepad scheme: action, control path, action type, expected control type.</summary>
        private static readonly string[,] GamepadPairs =
        {
            { "Move", "<Gamepad>/leftStick", "Value", "Vector2" },
            { "RightStick", "<Gamepad>/rightStick", "Value", "Vector2" },
            { "Sprint", "<Gamepad>/rightTrigger", "Value", "Axis" },
            { "JockeyShield", "<Gamepad>/leftTrigger", "Value", "Axis" },
            { "ModifierLeft", "<Gamepad>/leftShoulder", "Button", "Button" },
            { "ModifierRight", "<Gamepad>/rightShoulder", "Button", "Button" },
            { "Pass", "<Gamepad>/buttonSouth", "Button", "Button" },
            { "LobCross", "<Gamepad>/buttonWest", "Button", "Button" },
            { "Through", "<Gamepad>/buttonNorth", "Button", "Button" },
            { "Shoot", "<Gamepad>/buttonEast", "Button", "Button" },
            { "TacticsUp", "<Gamepad>/dpad/up", "Button", "Button" },
            { "TacticsDown", "<Gamepad>/dpad/down", "Button", "Button" },
            { "TacticsLeft", "<Gamepad>/dpad/left", "Button", "Button" },
            { "TacticsRight", "<Gamepad>/dpad/right", "Button", "Button" },
            { "LeftStickPress", "<Gamepad>/leftStickPress", "Button", "Button" },
            { "RightStickPress", "<Gamepad>/rightStickPress", "Button", "Button" },
            { "Pause", "<Gamepad>/start", "Button", "Button" },
        };

        private static string[,] TouchPairs()
        {
            var pairs = new string[10, 4];
            for (int i = 0; i < 5; i++)
            {
                pairs[i * 2, 0] = "Touch" + i + "Position";
                pairs[i * 2, 1] = "<Touchscreen>/touch" + i + "/position";
                pairs[i * 2, 2] = "PassThrough";
                pairs[i * 2, 3] = "Vector2";
                pairs[i * 2 + 1, 0] = "Touch" + i + "Press";
                pairs[i * 2 + 1, 1] = "<Touchscreen>/touch" + i + "/press";
                pairs[i * 2 + 1, 2] = "PassThrough";
                pairs[i * 2 + 1, 3] = "Button";
            }
            return pairs;
        }

        private static JsonNode Load()
        {
            Assert.IsTrue(File.Exists(AssetPath), "missing " + AssetPath);
            JsonNode root = JsonParser.Parse(File.ReadAllText(AssetPath));
            Assert.IsTrue(root.IsObject);
            return root;
        }

        private static JsonNode Map(JsonNode root, string name)
        {
            JsonNode maps = root.Get("maps");
            Assert.IsNotNull(maps, "no maps");
            Assert.IsTrue(maps.IsArray);
            foreach (JsonNode m in maps.Items)
                if (m.GetString("name") == name) return m;
            Assert.Fail("action map not found: " + name);
            return null;
        }

        private static List<Binding> Bindings(JsonNode map)
        {
            var list = new List<Binding>();
            foreach (JsonNode b in map.Get("bindings").Items)
            {
                list.Add(new Binding
                {
                    Action = b.GetString("action"),
                    Path = b.GetString("path"),
                    Groups = b.GetString("groups", string.Empty),
                });
            }
            return list;
        }

        /// <summary>
        /// The map holds exactly the expected actions, each with exactly one binding on the expected path,
        /// tagged with the expected control scheme, and with the expected action / control types.
        /// </summary>
        private static void AssertPairs(JsonNode map, string[,] pairs, string scheme)
        {
            string mapName = map.GetString("name");
            var actions = new Dictionary<string, JsonNode>();
            foreach (JsonNode a in map.Get("actions").Items) actions[a.GetString("name")] = a;
            List<Binding> bindings = Bindings(map);
            int n = pairs.GetLength(0);
            Assert.AreEqual(n, actions.Count, "action count in map " + mapName);
            Assert.AreEqual(n, bindings.Count, "binding count in map " + mapName);
            for (int i = 0; i < n; i++)
            {
                string action = pairs[i, 0];
                Assert.IsTrue(actions.ContainsKey(action), "missing action " + mapName + "/" + action);
                Assert.AreEqual(pairs[i, 2], actions[action].GetString("type"), "type of " + action);
                Assert.AreEqual(pairs[i, 3], actions[action].GetString("expectedControlType"), "control type of " + action);
                int found = 0;
                foreach (Binding b in bindings)
                {
                    if (b.Action != action) continue;
                    found++;
                    Assert.AreEqual(pairs[i, 1], b.Path, "binding path for " + action);
                    Assert.AreEqual(scheme, b.Groups, "binding group for " + action);
                }
                Assert.AreEqual(1, found, "bindings for " + action);
            }
        }

        [Test]
        public void ControlSchemes_GamepadAndTouch_HaveRequiredDevices()
        {
            JsonNode schemes = Load().Get("controlSchemes");
            Assert.IsNotNull(schemes, "no controlSchemes");
            var devices = new Dictionary<string, string>();
            foreach (JsonNode s in schemes.Items)
            {
                Assert.AreEqual(s.GetString("name"), s.GetString("bindingGroup"));
                JsonNode devs = s.Get("devices");
                Assert.AreEqual(1, devs.Count, "devices in scheme " + s.GetString("name"));
                Assert.IsFalse(devs[0].GetBool("isOptional", true));
                devices[s.GetString("bindingGroup")] = devs[0].GetString("devicePath");
            }
            Assert.AreEqual(2, devices.Count);
            Assert.AreEqual("<Gamepad>", devices["Gamepad"]);
            Assert.AreEqual("<Touchscreen>", devices["Touch"]);
        }

        [Test]
        public void GameplayMap_GamepadScheme_BindsClassicPreset()
        {
            AssertPairs(Map(Load(), "Gameplay"), GamepadPairs, "Gamepad");
        }

        [Test]
        public void TouchMap_TouchScheme_BindsFiveFingerSlots()
        {
            AssertPairs(Map(Load(), "Touch"), TouchPairs(), "Touch");
        }

        [Test]
        public void Asset_HasOnlyGameplayAndTouchMaps()
        {
            JsonNode maps = Load().Get("maps");
            Assert.AreEqual(2, maps.Count);
            Map(Load(), "Gameplay");
            Map(Load(), "Touch");
        }
    }
}
