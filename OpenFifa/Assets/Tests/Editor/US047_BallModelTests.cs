using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US047")]
    [Category("Art")]
    public class US047_BallModelTests
    {
        // ============================================================
        // Existing tests (preserved)
        // ============================================================

        [Test]
        public void BallModelConfig_TriangleBudget_InAAARange()
        {
            var config = new BallModelConfig();
            Assert.That(config.MaxTriangles, Is.InRange(2000, 10000), "Ball triangle budget should be in AAA range (2K-10K)");
        }

        [Test]
        public void BallModelConfig_TextureSettings_HasAlbedoTexture()
        {
            var config = new BallModelConfig();
            Assert.IsNotNull(config.AlbedoTextureName);
            Assert.IsNotEmpty(config.AlbedoTextureName);
        }

        [Test]
        public void BallModelConfig_TextureSettings_HasNormalMap()
        {
            var config = new BallModelConfig();
            Assert.IsNotNull(config.NormalMapName);
            Assert.IsNotEmpty(config.NormalMapName);
        }

        [Test]
        public void BallModelConfig_Smoothness_InRange()
        {
            var config = new BallModelConfig();
            Assert.GreaterOrEqual(config.Smoothness, 0f);
            Assert.LessOrEqual(config.Smoothness, 1f);
            // Slight sheen for soccer ball
            Assert.GreaterOrEqual(config.Smoothness, 0.3f);
            Assert.LessOrEqual(config.Smoothness, 0.6f);
        }

        [Test]
        public void BallModelConfig_Shader_IsURPLit()
        {
            var config = new BallModelConfig();
            Assert.AreEqual("Universal Render Pipeline/Lit", config.ShaderName);
        }

        [Test]
        public void BallModelConfig_MeshOrigin_PivotAtCenter()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.PivotAtCenter);
        }

        [Test]
        public void BallModelConfig_ColliderRadius_Positive()
        {
            var config = new BallModelConfig();
            Assert.Greater(config.SphereColliderRadius, 0f);
            Assert.Less(config.SphereColliderRadius, 0.5f);
        }

        [Test]
        public void BallModelConfig_VisualRotation_FromRigidbody()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.RotationFromAngularVelocity);
        }

        // ============================================================
        // New PBR Texture Set tests
        // ============================================================

        [Test]
        public void PBRTextureSet_Default_IsComplete()
        {
            var texSet = new PBRTextureSet();
            Assert.IsTrue(texSet.IsComplete(),
                "Default PBR texture set should have all five texture paths assigned");
        }

        [Test]
        public void PBRTextureSet_MissingAlbedo_IsNotComplete()
        {
            var texSet = new PBRTextureSet { AlbedoPath = null };
            Assert.IsFalse(texSet.IsComplete(),
                "PBR texture set with null albedo should be incomplete");
        }

        [Test]
        public void PBRTextureSet_MissingNormal_ReportsInGetMissing()
        {
            var texSet = new PBRTextureSet { NormalPath = "" };
            var missing = texSet.GetMissingTextures();
            Assert.Contains("Normal", missing,
                "Missing textures list should contain 'Normal' when NormalPath is empty");
        }

        [Test]
        public void PBRTextureSet_AllEmpty_ReportsFiveMissing()
        {
            var texSet = new PBRTextureSet
            {
                AlbedoPath = "",
                NormalPath = "",
                MetallicPath = "",
                RoughnessPath = "",
                AOPath = ""
            };
            var missing = texSet.GetMissingTextures();
            Assert.AreEqual(5, missing.Count,
                "All five texture slots should be reported as missing");
        }

        [Test]
        public void BallModelConfig_IsPBRSetComplete_DelegatesToPBRTextures()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.IsPBRSetComplete(),
                "Default config PBR set should be complete");

            config.PBRTextures.MetallicPath = null;
            Assert.IsFalse(config.IsPBRSetComplete(),
                "Config with missing metallic path should report incomplete PBR set");
        }

        // ============================================================
        // New Mesh Config tests
        // ============================================================

        [Test]
        public void BallMeshConfig_IsVertexCountValid_WithinBudget_ReturnsTrue()
        {
            var meshConfig = new BallMeshConfig();
            Assert.IsTrue(meshConfig.IsVertexCountValid(2000),
                "2000 vertices should be within default budget of 2500");
        }

        [Test]
        public void BallMeshConfig_IsVertexCountValid_OverBudget_ReturnsFalse()
        {
            var meshConfig = new BallMeshConfig();
            Assert.IsFalse(meshConfig.IsVertexCountValid(3000),
                "3000 vertices should exceed default budget of 2500");
        }

        [Test]
        public void BallMeshConfig_IsVertexCountValid_ZeroOrNegative_ReturnsFalse()
        {
            var meshConfig = new BallMeshConfig();
            Assert.IsFalse(meshConfig.IsVertexCountValid(0), "Zero vertices should be invalid");
            Assert.IsFalse(meshConfig.IsVertexCountValid(-1), "Negative vertices should be invalid");
        }

        [Test]
        public void BallMeshConfig_IsTriangleCountValid_WithinBudget_ReturnsTrue()
        {
            var meshConfig = new BallMeshConfig();
            Assert.IsTrue(meshConfig.IsTriangleCountValid(4000),
                "4000 triangles should be within default budget of 5000");
        }

        [Test]
        public void BallMeshConfig_IsTriangleCountValid_OverBudget_ReturnsFalse()
        {
            var meshConfig = new BallMeshConfig();
            Assert.IsFalse(meshConfig.IsTriangleCountValid(6000),
                "6000 triangles should exceed default budget of 5000");
        }

        [Test]
        public void BallMeshConfig_UVMappingType_Default_IsSpherical()
        {
            var meshConfig = new BallMeshConfig();
            Assert.AreEqual("Spherical", meshConfig.UVMappingType);
            Assert.IsTrue(meshConfig.IsUVMappingValid(),
                "Default UV mapping type 'Spherical' should be valid");
        }

        [Test]
        public void BallMeshConfig_IsUVMappingValid_InvalidType_ReturnsFalse()
        {
            var meshConfig = new BallMeshConfig { UVMappingType = "CustomInvalid" };
            Assert.IsFalse(meshConfig.IsUVMappingValid(),
                "Invalid UV mapping type should return false");
        }

        [Test]
        public void BallMeshConfig_PanelCount_IsRealistic()
        {
            var meshConfig = new BallMeshConfig();
            Assert.That(meshConfig.PanelCount, Is.InRange(6, 32),
                "Panel count should be 6 (modern) or 32 (classic)");
        }

        // ============================================================
        // New Collider Config tests
        // ============================================================

        [Test]
        public void BallColliderConfig_DefaultRadius_IsValid()
        {
            var colliderConfig = new BallColliderConfig();
            Assert.IsTrue(colliderConfig.IsRadiusValid(),
                $"Default radius {colliderConfig.Radius} should be valid");
        }

        [Test]
        public void BallColliderConfig_Bounciness_InValidRange()
        {
            var colliderConfig = new BallColliderConfig();
            Assert.IsTrue(colliderConfig.IsBouncinessValid(),
                $"Default bounciness {colliderConfig.Bounciness} should be in 0..1");
        }

        [Test]
        public void BallColliderConfig_Friction_IsNonNegative()
        {
            var colliderConfig = new BallColliderConfig();
            Assert.IsTrue(colliderConfig.IsFrictionValid(),
                "Default friction values should be non-negative");
        }

        [Test]
        public void BallColliderConfig_InvalidRadius_TooSmall_ReturnsFalse()
        {
            var colliderConfig = new BallColliderConfig { Radius = 0.01f };
            Assert.IsFalse(colliderConfig.IsRadiusValid(),
                "Radius 0.01 should be too small (below 0.05)");
        }

        [Test]
        public void BallColliderConfig_InvalidBounciness_Negative_ReturnsFalse()
        {
            var colliderConfig = new BallColliderConfig { Bounciness = -0.1f };
            Assert.IsFalse(colliderConfig.IsBouncinessValid(),
                "Negative bounciness should be invalid");
        }

        // ============================================================
        // New LOD Config tests
        // ============================================================

        [Test]
        public void BallLODConfig_VertexCounts_DescendCorrectly()
        {
            var lodConfig = new BallLODConfig();
            Assert.IsTrue(lodConfig.AreVertexCountsValid(),
                $"LOD vertex counts should descend: {lodConfig.LOD0VertexCount} > {lodConfig.LOD1VertexCount} > {lodConfig.LOD2VertexCount}");
        }

        [Test]
        public void BallLODConfig_TransitionDistances_IncreaseCorrectly()
        {
            var lodConfig = new BallLODConfig();
            Assert.IsTrue(lodConfig.AreTransitionDistancesValid(),
                $"Transition distances should increase: {lodConfig.LOD0TransitionDistance} < {lodConfig.LOD1TransitionDistance} < {lodConfig.CullDistance}");
        }

        [Test]
        public void BallLODConfig_GetLODLevel_CloseDistance_ReturnsLOD0()
        {
            var lodConfig = new BallLODConfig();
            Assert.AreEqual(0, lodConfig.GetLODLevel(5f),
                "Distance 5m should return LOD0");
        }

        [Test]
        public void BallLODConfig_GetLODLevel_MediumDistance_ReturnsLOD1()
        {
            var lodConfig = new BallLODConfig();
            Assert.AreEqual(1, lodConfig.GetLODLevel(20f),
                "Distance 20m should return LOD1");
        }

        [Test]
        public void BallLODConfig_GetLODLevel_FarDistance_ReturnsLOD2()
        {
            var lodConfig = new BallLODConfig();
            Assert.AreEqual(2, lodConfig.GetLODLevel(50f),
                "Distance 50m should return LOD2");
        }

        [Test]
        public void BallLODConfig_GetLODLevel_BeyondCull_ReturnsNegativeOne()
        {
            var lodConfig = new BallLODConfig();
            Assert.AreEqual(-1, lodConfig.GetLODLevel(200f),
                "Distance beyond cull distance should return -1 (culled)");
        }

        [Test]
        public void BallLODConfig_InvalidVertexCounts_LOD0LessThanLOD1_ReturnsFalse()
        {
            var lodConfig = new BallLODConfig { LOD0VertexCount = 100, LOD1VertexCount = 500 };
            Assert.IsFalse(lodConfig.AreVertexCountsValid(),
                "LOD0 vertex count less than LOD1 should be invalid");
        }

        // ============================================================
        // New Validation Result tests
        // ============================================================

        [Test]
        public void BallModelValidationResult_AddError_SetsIsValidFalse()
        {
            var result = new BallModelValidationResult { IsValid = true };
            result.AddError("Test error");
            Assert.IsFalse(result.IsValid,
                "Adding an error should set IsValid to false");
            Assert.AreEqual(1, result.Errors.Count);
        }

        [Test]
        public void BallModelValidationResult_AddWarning_KeepsIsValidTrue()
        {
            var result = new BallModelValidationResult { IsValid = true };
            result.AddWarning("Test warning");
            Assert.IsTrue(result.IsValid,
                "Adding a warning should not change IsValid");
            Assert.AreEqual(1, result.Warnings.Count);
        }

        [Test]
        public void BallModelConfig_ValidateBallModel_DefaultConfig_IsValid()
        {
            var config = new BallModelConfig();
            var result = config.ValidateBallModel();
            Assert.IsTrue(result.IsValid,
                $"Default config should validate. Errors: {string.Join("; ", result.Errors)}");
        }

        [Test]
        public void BallModelConfig_ValidateBallModel_MissingTextures_HasErrors()
        {
            var config = new BallModelConfig();
            config.PBRTextures.AlbedoPath = null;
            config.PBRTextures.NormalPath = "";
            var result = config.ValidateBallModel();
            Assert.IsFalse(result.IsValid,
                "Config with missing PBR textures should fail validation");
            Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void BallModelConfig_ValidateBallModel_EmptyShaderName_HasErrors()
        {
            var config = new BallModelConfig { ShaderName = "" };
            var result = config.ValidateBallModel();
            Assert.IsFalse(result.IsValid,
                "Config with empty shader name should fail validation");
        }

        [Test]
        public void BallModelConfig_ValidateBallModel_InvalidColliderRadius_HasErrors()
        {
            var config = new BallModelConfig();
            config.ColliderConfig.Radius = 0.001f;
            var result = config.ValidateBallModel();
            Assert.IsFalse(result.IsValid,
                "Config with radius 0.001 should fail validation");
        }

        [Test]
        public void BallModelConfig_ValidateBallModel_LowTextureRes_HasWarning()
        {
            var config = new BallModelConfig { TextureResolution = 1024 };
            var result = config.ValidateBallModel();
            Assert.IsTrue(result.IsValid,
                "Low texture res should not cause validation failure");
            Assert.That(result.Warnings.Count, Is.GreaterThanOrEqualTo(1),
                "Low texture res should produce a warning");
        }

        [Test]
        public void BallModelConfig_IsVertexCountValid_DelegatesToMeshConfig()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.IsVertexCountValid(1000));
            Assert.IsFalse(config.IsVertexCountValid(5000));
        }

        [Test]
        public void BallModelConfig_TextureResolution_Is4K()
        {
            var config = new BallModelConfig();
            Assert.AreEqual(4096, config.TextureResolution,
                "Default texture resolution should be 4096 for AAA quality");
        }
    }
}
