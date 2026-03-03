using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US046")]
    [Category("Art")]
    public class US046_StadiumTests
    {
        // ================================================================
        // Existing tests (unchanged)
        // ================================================================

        [Test]
        public void StadiumConfig_SkyboxSettings_HasSkyboxHDRI()
        {
            var config = new StadiumConfig();
            Assert.IsNotNull(config.SkyboxHDRIName);
            Assert.AreEqual("kloppenheim_stadium", config.SkyboxHDRIName);
        }

        [Test]
        public void StadiumConfig_SkyboxShader_IsPanoramic()
        {
            var config = new StadiumConfig();
            Assert.AreEqual("Skybox/Panoramic", config.SkyboxShaderName);
        }

        [Test]
        public void StadiumConfig_PitchTexture_HasGrassBands()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.UsePitchGrassBands);
            Assert.Greater(config.GrassBandCount, 0);
        }

        [Test]
        public void GoalPostConfig_Dimensions_MatchPitch()
        {
            var config = new StadiumConfig();
            // Goal width from PitchConfig is typically ~3.66m (12ft)
            Assert.Greater(config.GoalPostWidth, 3f);
            Assert.Less(config.GoalPostWidth, 5f);
            Assert.Greater(config.GoalPostHeight, 2f);
            Assert.Less(config.GoalPostHeight, 3f);
        }

        [Test]
        public void GoalPostConfig_PostRadius_Reasonable()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.PostRadius, 0.03f);
            Assert.Less(config.PostRadius, 0.15f);
        }

        [Test]
        public void GoalNetConfig_NetAlpha_HasSemiTransparentMaterial()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.NetAlpha, 0f);
            Assert.Less(config.NetAlpha, 1f);
        }

        [Test]
        public void GoalPostConfig_ColliderSettings_HasMeshCollider()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.PostsHaveMeshCollider);
            Assert.IsTrue(config.PostColliderConvex);
        }

        [Test]
        public void StadiumConfig_Geometry_HasStandsGeometry()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.HasStandsGeometry);
            Assert.Greater(config.StandsSections, 0);
        }

        [Test]
        public void StadiumConfig_ScreenshotResolution_1920x1080()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(1920, config.BaselineScreenshotWidth);
            Assert.AreEqual(1080, config.BaselineScreenshotHeight);
        }

        // ================================================================
        // New tests: StandsSection data class
        // ================================================================

        [Test]
        public void StandsSection_DefaultValues_AreReasonable()
        {
            var section = new StandsSection();
            Assert.Greater(section.Width, 0f, "Width must be positive");
            Assert.Greater(section.Depth, 0f, "Depth must be positive");
            Assert.Greater(section.Height, 0f, "Height must be positive");
            Assert.Greater(section.TierCount, 0, "TierCount must be positive");
            Assert.That(section.DistanceFromPitch, Is.GreaterThanOrEqualTo(0f));
            Assert.That(section.Capacity, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void StandsSection_TierHeight_IsHeightDividedByTierCount()
        {
            var section = new StandsSection(height: 12f, tierCount: 4);
            Assert.AreEqual(3f, section.TierHeight, 0.001f,
                $"Expected 12/4=3, got {section.TierHeight}");
        }

        [Test]
        public void StandsSection_InvalidWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StandsSection(width: -1f));
        }

        [Test]
        public void StandsSection_InvalidTierCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StandsSection(tierCount: 0));
        }

        // ================================================================
        // New tests: FloodlightTowerData
        // ================================================================

        [Test]
        public void FloodlightTowerData_DefaultValues_AreReasonable()
        {
            var tower = new FloodlightTowerData();
            Assert.AreEqual(25f, tower.Y, 0.001f, "Default tower height should be 25m");
            Assert.Greater(tower.Intensity, 0f, "Intensity must be positive");
            Assert.That(tower.ConeAngle, Is.InRange(1f, 180f), "Cone angle must be in valid range");
        }

        [Test]
        public void FloodlightTowerData_CustomPosition_IsStored()
        {
            var tower = new FloodlightTowerData(x: 40f, y: 30f, z: -20f, intensity: 200f, coneAngle: 90f);
            Assert.AreEqual(40f, tower.X, 0.001f);
            Assert.AreEqual(30f, tower.Y, 0.001f);
            Assert.AreEqual(-20f, tower.Z, 0.001f);
            Assert.AreEqual(200f, tower.Intensity, 0.001f);
            Assert.AreEqual(90f, tower.ConeAngle, 0.001f);
        }

        [Test]
        public void FloodlightTowerData_InvalidConeAngle_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FloodlightTowerData(coneAngle: 0f));
            Assert.Throws<ArgumentException>(() => new FloodlightTowerData(coneAngle: 181f));
        }

        // ================================================================
        // New tests: AdvertisingBoardData
        // ================================================================

        [Test]
        public void AdvertisingBoardData_DefaultValues_AreReasonable()
        {
            var board = new AdvertisingBoardData();
            Assert.Greater(board.Width, 0f);
            Assert.Greater(board.Height, 0f);
        }

        [Test]
        public void AdvertisingBoardData_InvalidDimensions_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AdvertisingBoardData(width: 0f));
            Assert.Throws<ArgumentException>(() => new AdvertisingBoardData(height: -1f));
        }

        // ================================================================
        // New tests: CornerFlagData
        // ================================================================

        [Test]
        public void CornerFlagData_DefaultValues_AreReasonable()
        {
            var flag = new CornerFlagData();
            Assert.AreEqual(1.5f, flag.PoleHeight, 0.001f, "Standard corner flag height is 1.5m");
            Assert.Greater(flag.PoleRadius, 0f, "Pole radius must be positive");
        }

        [Test]
        public void CornerFlagData_InvalidPoleHeight_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CornerFlagData(poleHeight: 0f));
        }

        // ================================================================
        // New tests: DugoutData
        // ================================================================

        [Test]
        public void DugoutData_DefaultValues_AreReasonable()
        {
            var dugout = new DugoutData();
            Assert.Greater(dugout.Width, 0f);
            Assert.Greater(dugout.Depth, 0f);
            Assert.Greater(dugout.Height, 0f);
            Assert.AreEqual("Home", dugout.TeamLabel);
        }

        [Test]
        public void DugoutData_CustomTeamLabel_IsStored()
        {
            var dugout = new DugoutData(teamLabel: "Away");
            Assert.AreEqual("Away", dugout.TeamLabel);
        }

        [Test]
        public void DugoutData_NullLabel_DefaultsToHome()
        {
            var dugout = new DugoutData(teamLabel: null);
            Assert.AreEqual("Home", dugout.TeamLabel);
        }

        // ================================================================
        // New tests: ScoreboardData
        // ================================================================

        [Test]
        public void ScoreboardData_DefaultValues_AreReasonable()
        {
            var scoreboard = new ScoreboardData();
            Assert.Greater(scoreboard.Width, 0f);
            Assert.Greater(scoreboard.Height, 0f);
            Assert.Greater(scoreboard.Y, 0f, "Scoreboard should be elevated above pitch");
        }

        [Test]
        public void ScoreboardData_InvalidDimensions_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ScoreboardData(width: -1f));
            Assert.Throws<ArgumentException>(() => new ScoreboardData(height: 0f));
        }

        // ================================================================
        // New tests: TunnelData
        // ================================================================

        [Test]
        public void TunnelData_DefaultValues_AreReasonable()
        {
            var tunnel = new TunnelData();
            Assert.Greater(tunnel.Width, 0f);
            Assert.Greater(tunnel.Height, 0f);
            Assert.Greater(tunnel.Depth, 0f);
        }

        [Test]
        public void TunnelData_InvalidDimensions_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TunnelData(width: 0f));
            Assert.Throws<ArgumentException>(() => new TunnelData(height: -1f));
            Assert.Throws<ArgumentException>(() => new TunnelData(depth: -5f));
        }

        // ================================================================
        // New tests: CrowdDensityZone
        // ================================================================

        [Test]
        public void CrowdDensityZone_DefaultValues_AreReasonable()
        {
            var zone = new CrowdDensityZone();
            Assert.AreEqual(0, zone.SectionIndex);
            Assert.That(zone.Density, Is.InRange(0f, 1f), "Density must be between 0 and 1");
        }

        [Test]
        public void CrowdDensityZone_InvalidDensity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CrowdDensityZone(density: -0.1f));
            Assert.Throws<ArgumentException>(() => new CrowdDensityZone(density: 1.1f));
        }

        [Test]
        public void CrowdDensityZone_InvalidSectionIndex_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CrowdDensityZone(sectionIndex: -1));
        }

        // ================================================================
        // New tests: StadiumConfig collections
        // ================================================================

        [Test]
        public void StadiumConfig_Sections_Has8Sections()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(8, config.Sections.Count,
                $"Expected 8 stand sections, found {config.Sections.Count}");
        }

        [Test]
        public void StadiumConfig_FloodlightTowers_Has4Towers()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(4, config.FloodlightTowers.Count,
                $"Expected 4 floodlight towers, found {config.FloodlightTowers.Count}");
            Assert.AreEqual(4, config.FloodlightCount);
        }

        [Test]
        public void StadiumConfig_CornerFlags_Has4Flags()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(4, config.CornerFlags.Count,
                $"Expected 4 corner flags, found {config.CornerFlags.Count}");
        }

        [Test]
        public void StadiumConfig_Dugouts_Has2Dugouts()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(2, config.Dugouts.Count,
                $"Expected 2 dugouts, found {config.Dugouts.Count}");
            Assert.AreEqual("Home", config.Dugouts[0].TeamLabel);
            Assert.AreEqual("Away", config.Dugouts[1].TeamLabel);
        }

        [Test]
        public void StadiumConfig_AdvertisingBoards_HasBoards()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.AdvertisingBoards.Count, 0,
                "Expected at least one advertising board");
            // 6 boards per side * 2 sides = 12
            Assert.AreEqual(12, config.AdvertisingBoards.Count,
                $"Expected 12 advertising boards (6 north + 6 south), found {config.AdvertisingBoards.Count}");
        }

        [Test]
        public void StadiumConfig_Scoreboard_IsPresent()
        {
            var config = new StadiumConfig();
            Assert.IsNotNull(config.Scoreboard);
            Assert.Greater(config.Scoreboard.Y, 0f, "Scoreboard should be elevated");
        }

        [Test]
        public void StadiumConfig_Tunnel_IsPresent()
        {
            var config = new StadiumConfig();
            Assert.IsNotNull(config.Tunnel);
            Assert.Less(config.Tunnel.Z, 0f, "Tunnel should be on the south side (negative Z)");
        }

        [Test]
        public void StadiumConfig_CrowdDensityZones_MatchSections()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(config.Sections.Count, config.CrowdDensityZones.Count,
                "Crowd density zones should match section count");
            for (int i = 0; i < config.CrowdDensityZones.Count; i++)
            {
                Assert.AreEqual(i, config.CrowdDensityZones[i].SectionIndex,
                    $"Zone {i} section index mismatch");
                Assert.That(config.CrowdDensityZones[i].Density, Is.InRange(0f, 1f),
                    $"Zone {i} density out of range");
            }
        }

        [Test]
        public void StadiumConfig_TotalCapacity_IsPositive()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.TotalCapacity, 0, "Total capacity must be positive");
            // Sum manually to verify
            int expected = 0;
            for (int i = 0; i < config.Sections.Count; i++)
            {
                expected += config.Sections[i].Capacity;
            }
            Assert.AreEqual(expected, config.TotalCapacity);
        }

        [Test]
        public void StadiumConfig_FeatureFlags_AllEnabled()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.HasStandsGeometry);
            Assert.IsTrue(config.HasFloodlights);
            Assert.IsTrue(config.HasCrowdGeometry);
            Assert.IsTrue(config.HasAdvertisingBoards);
            Assert.IsTrue(config.HasTunnels);
            Assert.IsTrue(config.HasDugouts);
            Assert.IsTrue(config.HasCornerFlags);
            Assert.IsTrue(config.HasScoreboard);
        }

        // ================================================================
        // New tests: StadiumLightingConfig
        // ================================================================

        [Test]
        public void StadiumLightingConfig_FloodlightPositions_Has4Towers()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            Assert.AreEqual(4, config.FloodlightTowerCount,
                $"Expected 4 floodlight positions, found {config.FloodlightTowerCount}");
            foreach (var pos in config.FloodlightPositions)
            {
                Assert.AreEqual(3, pos.Length, "Each position should have 3 components (xyz)");
            }
        }

        [Test]
        public void StadiumLightingConfig_IntensityCurve_HasKeyframes()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            Assert.Greater(config.IntensityCurve.Count, 0, "Intensity curve must have keyframes");
            // First keyframe at time 0
            Assert.AreEqual(0f, config.IntensityCurve[0].Time, 0.001f);
            // Last keyframe at time 1
            Assert.AreEqual(1f, config.IntensityCurve[config.IntensityCurve.Count - 1].Time, 0.001f);
        }

        [Test]
        public void StadiumLightingConfig_EvaluateIntensity_MiddayIsPeak()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            float midday = config.EvaluateIntensity(0.5f);
            float midnight = config.EvaluateIntensity(0.0f);
            Assert.Greater(midday, midnight,
                $"Midday intensity ({midday}) should be greater than midnight ({midnight})");
        }

        [Test]
        public void StadiumLightingConfig_EvaluateIntensity_InterpolatesCorrectly()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            // At time 0.125 (halfway between 0.0 and 0.25)
            float val = config.EvaluateIntensity(0.125f);
            // Should be between midnight (0.2) and dawn (0.6)
            Assert.That(val, Is.InRange(0.2f, 0.6f),
                $"Intensity at 0.125 should be between midnight and dawn, got {val}");
        }

        [Test]
        public void StadiumLightingConfig_AmbientOcclusion_DefaultValues()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            Assert.IsNotNull(config.AmbientOcclusion);
            Assert.That(config.AmbientOcclusion.Intensity, Is.InRange(0f, 1f));
            Assert.Greater(config.AmbientOcclusion.Radius, 0f);
        }

        [Test]
        public void StadiumLightingConfig_Shadows_DefaultValues()
        {
            var config = StadiumLightingConfig.CreateNightStadium();
            Assert.IsNotNull(config.Shadows);
            Assert.That(config.Shadows.Strength, Is.InRange(0f, 1f));
            Assert.Greater(config.Shadows.Distance, 0f);
            Assert.Greater(config.Shadows.Resolution, 0);
            Assert.IsTrue(config.Shadows.IsPowerOfTwo,
                $"Shadow resolution {config.Shadows.Resolution} should be power of 2");
        }

        [Test]
        public void StadiumLightingConfig_DayPreset_HasHigherFillThanNight()
        {
            var night = StadiumLightingConfig.CreateNightStadium();
            var day = StadiumLightingConfig.CreateDayStadium();
            Assert.Greater(day.FillIntensity, night.FillIntensity,
                $"Day fill ({day.FillIntensity}) should exceed night fill ({night.FillIntensity})");
        }

        [Test]
        public void StadiumLightingConfig_DuskPreset_HasModerateFloodlight()
        {
            var night = StadiumLightingConfig.CreateNightStadium();
            var dusk = StadiumLightingConfig.CreateDuskStadium();
            Assert.Less(dusk.FloodlightIntensity, night.FloodlightIntensity,
                "Dusk floodlight should be less than night (floodlights less needed at dusk)");
        }

        // ================================================================
        // New tests: AmbientOcclusionSettings validation
        // ================================================================

        [Test]
        public void AmbientOcclusionSettings_InvalidIntensity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AmbientOcclusionSettings(intensity: -0.1f));
            Assert.Throws<ArgumentException>(() => new AmbientOcclusionSettings(intensity: 1.1f));
        }

        [Test]
        public void AmbientOcclusionSettings_InvalidRadius_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AmbientOcclusionSettings(radius: 0f));
        }

        // ================================================================
        // New tests: ShadowSettings validation
        // ================================================================

        [Test]
        public void ShadowSettings_InvalidStrength_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ShadowSettings(strength: -0.1f));
            Assert.Throws<ArgumentException>(() => new ShadowSettings(strength: 1.1f));
        }

        [Test]
        public void ShadowSettings_InvalidDistance_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ShadowSettings(distance: 0f));
        }

        [Test]
        public void ShadowSettings_IsPowerOfTwo_CorrectForKnownValues()
        {
            Assert.IsTrue(new ShadowSettings(resolution: 1024).IsPowerOfTwo);
            Assert.IsTrue(new ShadowSettings(resolution: 2048).IsPowerOfTwo);
            Assert.IsTrue(new ShadowSettings(resolution: 4096).IsPowerOfTwo);
            Assert.IsFalse(new ShadowSettings(resolution: 1000).IsPowerOfTwo);
            Assert.IsFalse(new ShadowSettings(resolution: 3000).IsPowerOfTwo);
        }

        // ================================================================
        // New tests: IntensityCurveKeyframe validation
        // ================================================================

        [Test]
        public void IntensityCurveKeyframe_ValidValues_AreStored()
        {
            var kf = new IntensityCurveKeyframe(0.5f, 0.8f);
            Assert.AreEqual(0.5f, kf.Time, 0.001f);
            Assert.AreEqual(0.8f, kf.Value, 0.001f);
        }

        [Test]
        public void IntensityCurveKeyframe_InvalidTime_Throws()
        {
            Assert.Throws<ArgumentException>(() => new IntensityCurveKeyframe(-0.1f, 1f));
            Assert.Throws<ArgumentException>(() => new IntensityCurveKeyframe(1.1f, 1f));
        }

        [Test]
        public void IntensityCurveKeyframe_NegativeValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => new IntensityCurveKeyframe(0.5f, -0.1f));
        }

        // ================================================================
        // New tests: CrowdPlacementConfig
        // ================================================================

        [Test]
        public void CrowdPlacementConfig_DefaultValues_AreReasonable()
        {
            var config = new CrowdPlacementConfig();
            Assert.Greater(config.RowsPerSection, 0);
            Assert.Greater(config.SeatsPerRow, 0);
            Assert.Greater(config.SeatSpacing, 0f);
            Assert.Greater(config.RowSpacing, 0f);
            Assert.Greater(config.QuadWidth, 0f);
            Assert.Greater(config.QuadHeight, 0f);
        }

        [Test]
        public void CrowdPlacementConfig_MaxCrowdPerSection_IsRowsTimesSeats()
        {
            var config = new CrowdPlacementConfig(rowsPerSection: 10, seatsPerRow: 20);
            Assert.AreEqual(200, config.MaxCrowdPerSection);
        }

        [Test]
        public void CrowdPlacementConfig_TotalMaxCrowd_IsPerSectionTimesSections()
        {
            var config = new CrowdPlacementConfig(rowsPerSection: 10, seatsPerRow: 20);
            Assert.AreEqual(200 * 8, config.TotalMaxCrowd,
                "Total max crowd = max per section * 8 sections");
        }

        [Test]
        public void CrowdPlacementConfig_CrowdCountForSection_AccountsForDensity()
        {
            var config = new CrowdPlacementConfig(rowsPerSection: 10, seatsPerRow: 20);
            // Default density for section 0 is 0.95
            int count = config.CrowdCountForSection(0);
            Assert.AreEqual((int)(200 * 0.95f), count,
                $"Section 0 crowd count should be 200*0.95={200 * 0.95f}, got {count}");
        }

        [Test]
        public void CrowdPlacementConfig_CrowdCountForSection_InvalidIndex_ReturnsZero()
        {
            var config = new CrowdPlacementConfig();
            Assert.AreEqual(0, config.CrowdCountForSection(-1));
            Assert.AreEqual(0, config.CrowdCountForSection(999));
        }

        [Test]
        public void CrowdPlacementConfig_SeatHorizontalOffset_IsCentered()
        {
            var config = new CrowdPlacementConfig(seatsPerRow: 5, seatSpacing: 1.0f);
            // 5 seats at 1.0 spacing => offsets: -2, -1, 0, 1, 2
            Assert.AreEqual(-2f, config.SeatHorizontalOffset(0), 0.001f);
            Assert.AreEqual(0f, config.SeatHorizontalOffset(2), 0.001f);
            Assert.AreEqual(2f, config.SeatHorizontalOffset(4), 0.001f);
        }

        [Test]
        public void CrowdPlacementConfig_RowDepthOffset_IncreasesLinearly()
        {
            var config = new CrowdPlacementConfig(rowSpacing: 1.2f);
            Assert.AreEqual(0f, config.RowDepthOffset(0), 0.001f);
            Assert.AreEqual(1.2f, config.RowDepthOffset(1), 0.001f);
            Assert.AreEqual(2.4f, config.RowDepthOffset(2), 0.001f);
        }

        [Test]
        public void CrowdPlacementConfig_RowVerticalOffset_IncreasesLinearly()
        {
            var config = new CrowdPlacementConfig(rowHeightStep: 0.5f);
            Assert.AreEqual(0f, config.RowVerticalOffset(0), 0.001f);
            Assert.AreEqual(0.5f, config.RowVerticalOffset(1), 0.001f);
            Assert.AreEqual(1.0f, config.RowVerticalOffset(2), 0.001f);
        }

        [Test]
        public void CrowdPlacementConfig_SectionAngularOffsets_Has8Sections()
        {
            var config = new CrowdPlacementConfig();
            Assert.AreEqual(8, config.SectionAngularOffsets.Count,
                $"Expected 8 section angular offsets, found {config.SectionAngularOffsets.Count}");
        }

        [Test]
        public void CrowdPlacementConfig_InvalidRowsPerSection_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CrowdPlacementConfig(rowsPerSection: 0));
        }

        [Test]
        public void CrowdPlacementConfig_InvalidSeatsPerRow_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CrowdPlacementConfig(seatsPerRow: -1));
        }

        [Test]
        public void CrowdPlacementConfig_TotalActualCrowd_IsLessThanMax()
        {
            var config = new CrowdPlacementConfig();
            Assert.LessOrEqual(config.TotalActualCrowd, config.TotalMaxCrowd,
                "Total actual crowd should not exceed total max crowd");
            Assert.Greater(config.TotalActualCrowd, 0,
                "Total actual crowd should be positive");
        }
    }
}
