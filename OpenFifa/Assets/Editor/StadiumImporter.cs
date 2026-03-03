using UnityEngine;
using UnityEditor;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Editor window for stadium config inspection and validation.
    /// Displays all stadium configuration data and highlights any issues.
    /// </summary>
    public class StadiumImporter : EditorWindow
    {
        private StadiumConfig _stadiumConfig;
        private StadiumLightingConfig _lightingConfig;
        private CrowdPlacementConfig _crowdConfig;
        private Vector2 _scrollPos;

        [MenuItem("OpenFifa/Stadium Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<StadiumImporter>("Stadium Inspector");
            window.minSize = new Vector2(400, 600);
        }

        private void OnEnable()
        {
            _stadiumConfig = new StadiumConfig();
            _lightingConfig = StadiumLightingConfig.CreateNightStadium();
            _crowdConfig = new CrowdPlacementConfig();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Stadium Configuration Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawStadiumOverview();
            EditorGUILayout.Space();

            DrawStandSections();
            EditorGUILayout.Space();

            DrawFloodlightTowers();
            EditorGUILayout.Space();

            DrawAdvertisingBoards();
            EditorGUILayout.Space();

            DrawCornerFlags();
            EditorGUILayout.Space();

            DrawDugouts();
            EditorGUILayout.Space();

            DrawScoreboard();
            EditorGUILayout.Space();

            DrawTunnel();
            EditorGUILayout.Space();

            DrawLightingConfig();
            EditorGUILayout.Space();

            DrawCrowdConfig();
            EditorGUILayout.Space();

            DrawValidation();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStadiumOverview()
        {
            EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Skybox HDRI", _stadiumConfig.SkyboxHDRIName);
            EditorGUILayout.LabelField("Pitch Dimensions", $"{_stadiumConfig.PitchLength}m x {_stadiumConfig.PitchWidth}m");
            EditorGUILayout.LabelField("Goal Dimensions", $"{_stadiumConfig.GoalPostWidth}m wide x {_stadiumConfig.GoalPostHeight}m high");
            EditorGUILayout.LabelField("Stand Sections", _stadiumConfig.StandsSections.ToString());
            EditorGUILayout.LabelField("Total Capacity", _stadiumConfig.TotalCapacity.ToString());
            EditorGUILayout.LabelField("Floodlight Towers", _stadiumConfig.FloodlightCount.ToString());

            EditorGUI.indentLevel--;
        }

        private void DrawStandSections()
        {
            EditorGUILayout.LabelField("Stand Sections", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            string[] sectionNames = { "North", "South", "East", "West", "NE Corner", "NW Corner", "SE Corner", "SW Corner" };

            for (int i = 0; i < _stadiumConfig.Sections.Count; i++)
            {
                var section = _stadiumConfig.Sections[i];
                string name = i < sectionNames.Length ? sectionNames[i] : $"Section {i}";
                EditorGUILayout.LabelField($"{name}: {section.Width}m x {section.Depth}m, {section.TierCount} tiers, cap={section.Capacity}");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFloodlightTowers()
        {
            EditorGUILayout.LabelField("Floodlight Towers", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (int i = 0; i < _stadiumConfig.FloodlightTowers.Count; i++)
            {
                var tower = _stadiumConfig.FloodlightTowers[i];
                EditorGUILayout.LabelField($"Tower {i}: ({tower.X:F1}, {tower.Y:F1}, {tower.Z:F1}) intensity={tower.Intensity} cone={tower.ConeAngle}");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAdvertisingBoards()
        {
            EditorGUILayout.LabelField($"Advertising Boards ({_stadiumConfig.AdvertisingBoards.Count})", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (int i = 0; i < _stadiumConfig.AdvertisingBoards.Count; i++)
            {
                var board = _stadiumConfig.AdvertisingBoards[i];
                EditorGUILayout.LabelField($"Board {i}: ({board.X:F1}, {board.Y:F1}, {board.Z:F1}) {board.Width}x{board.Height}m rot={board.RotationY}");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawCornerFlags()
        {
            EditorGUILayout.LabelField("Corner Flags", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (int i = 0; i < _stadiumConfig.CornerFlags.Count; i++)
            {
                var flag = _stadiumConfig.CornerFlags[i];
                EditorGUILayout.LabelField($"Flag {i}: ({flag.X:F1}, {flag.Y:F1}, {flag.Z:F1}) h={flag.PoleHeight}m");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawDugouts()
        {
            EditorGUILayout.LabelField("Dugouts", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            for (int i = 0; i < _stadiumConfig.Dugouts.Count; i++)
            {
                var dugout = _stadiumConfig.Dugouts[i];
                EditorGUILayout.LabelField($"{dugout.TeamLabel}: ({dugout.X:F1}, {dugout.Y:F1}, {dugout.Z:F1}) {dugout.Width}x{dugout.Depth}x{dugout.Height}m");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawScoreboard()
        {
            EditorGUILayout.LabelField("Scoreboard", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var sb = _stadiumConfig.Scoreboard;
            EditorGUILayout.LabelField($"Position: ({sb.X:F1}, {sb.Y:F1}, {sb.Z:F1})");
            EditorGUILayout.LabelField($"Size: {sb.Width}m x {sb.Height}m");

            EditorGUI.indentLevel--;
        }

        private void DrawTunnel()
        {
            EditorGUILayout.LabelField("Tunnel", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var t = _stadiumConfig.Tunnel;
            EditorGUILayout.LabelField($"Position: ({t.X:F1}, {t.Y:F1}, {t.Z:F1})");
            EditorGUILayout.LabelField($"Size: {t.Width}m x {t.Height}m x {t.Depth}m");

            EditorGUI.indentLevel--;
        }

        private void DrawLightingConfig()
        {
            EditorGUILayout.LabelField("Lighting Config", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Floodlight Intensity", _lightingConfig.FloodlightIntensity.ToString("F1"));
            EditorGUILayout.LabelField("Floodlight Range", _lightingConfig.FloodlightRange.ToString("F1"));
            EditorGUILayout.LabelField("Floodlight Spot Angle", _lightingConfig.FloodlightSpotAngle.ToString("F1"));
            EditorGUILayout.LabelField("Fill Intensity", _lightingConfig.FillIntensity.ToString("F1"));
            EditorGUILayout.LabelField("Tower Positions", _lightingConfig.FloodlightTowerCount.ToString());
            EditorGUILayout.LabelField("Intensity Curve Keyframes", _lightingConfig.IntensityCurve.Count.ToString());
            EditorGUILayout.LabelField("AO Intensity", _lightingConfig.AmbientOcclusion.Intensity.ToString("F2"));
            EditorGUILayout.LabelField("Shadow Strength", _lightingConfig.Shadows.Strength.ToString("F2"));
            EditorGUILayout.LabelField("Shadow Resolution", _lightingConfig.Shadows.Resolution.ToString());

            EditorGUI.indentLevel--;
        }

        private void DrawCrowdConfig()
        {
            EditorGUILayout.LabelField("Crowd Config", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Rows Per Section", _crowdConfig.RowsPerSection.ToString());
            EditorGUILayout.LabelField("Seats Per Row", _crowdConfig.SeatsPerRow.ToString());
            EditorGUILayout.LabelField("Seat Spacing", _crowdConfig.SeatSpacing.ToString("F2") + "m");
            EditorGUILayout.LabelField("Max Per Section", _crowdConfig.MaxCrowdPerSection.ToString());
            EditorGUILayout.LabelField("Total Max Crowd", _crowdConfig.TotalMaxCrowd.ToString());
            EditorGUILayout.LabelField("Total Actual Crowd", _crowdConfig.TotalActualCrowd.ToString());

            EditorGUI.indentLevel--;
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            bool allValid = true;

            // Check section count
            if (_stadiumConfig.Sections.Count != 8)
            {
                EditorGUILayout.HelpBox($"Expected 8 stand sections, found {_stadiumConfig.Sections.Count}", MessageType.Warning);
                allValid = false;
            }

            // Check floodlight count
            if (_stadiumConfig.FloodlightTowers.Count != 4)
            {
                EditorGUILayout.HelpBox($"Expected 4 floodlight towers, found {_stadiumConfig.FloodlightTowers.Count}", MessageType.Warning);
                allValid = false;
            }

            // Check corner flags
            if (_stadiumConfig.CornerFlags.Count != 4)
            {
                EditorGUILayout.HelpBox($"Expected 4 corner flags, found {_stadiumConfig.CornerFlags.Count}", MessageType.Warning);
                allValid = false;
            }

            // Check dugouts
            if (_stadiumConfig.Dugouts.Count != 2)
            {
                EditorGUILayout.HelpBox($"Expected 2 dugouts, found {_stadiumConfig.Dugouts.Count}", MessageType.Warning);
                allValid = false;
            }

            // Check shadow resolution is power of 2
            if (!_lightingConfig.Shadows.IsPowerOfTwo)
            {
                EditorGUILayout.HelpBox($"Shadow resolution {_lightingConfig.Shadows.Resolution} is not a power of 2", MessageType.Warning);
                allValid = false;
            }

            // Check crowd density zones match sections
            if (_stadiumConfig.CrowdDensityZones.Count != _stadiumConfig.Sections.Count)
            {
                EditorGUILayout.HelpBox($"Crowd density zones ({_stadiumConfig.CrowdDensityZones.Count}) don't match sections ({_stadiumConfig.Sections.Count})", MessageType.Warning);
                allValid = false;
            }

            if (allValid)
            {
                EditorGUILayout.HelpBox("All validation checks passed.", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }
    }
}
