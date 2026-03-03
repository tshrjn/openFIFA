using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US044")]
    [Category("Art")]
    public class US044_CharacterModelTests
    {
        // ─── Original Tests (unchanged) ─────────────────────────────────

        [Test]
        public void CharacterModelConfig_TriangleBudget_InAAARange()
        {
            var config = new CharacterModelConfig();
            Assert.That(config.MaxTrianglesPerModel, Is.InRange(10000, 50000), "Character triangle budget should be in AAA range (10K-50K)");
        }

        [Test]
        public void CharacterModelConfig_TextureResolution_IsHighFidelity()
        {
            var config = new CharacterModelConfig();
            Assert.That(config.TextureResolution, Is.GreaterThanOrEqualTo(2048),
                "Character texture resolution should be at least 2048 for AAA quality");
        }

        [Test]
        public void CharacterModelConfig_TeamColors_HasTeamAColor()
        {
            var config = new CharacterModelConfig();
            Assert.IsNotNull(config.TeamAColor);
            // Blue: R < 0.5, B > 0.5
            Assert.Less(config.TeamAColor.R, 0.5f);
            Assert.Greater(config.TeamAColor.B, 0.5f);
        }

        [Test]
        public void CharacterModelConfig_TeamColors_HasTeamBColor()
        {
            var config = new CharacterModelConfig();
            Assert.IsNotNull(config.TeamBColor);
            // Red: R > 0.5, B < 0.5
            Assert.Greater(config.TeamBColor.R, 0.5f);
            Assert.Less(config.TeamBColor.B, 0.5f);
        }

        [Test]
        public void CharacterModelConfig_AvatarRig_IsHumanoid()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual("Humanoid", config.AvatarRigType);
        }

        [Test]
        public void CharacterModelConfig_RequiredAnimations_AllPresent()
        {
            var config = new CharacterModelConfig();
            Assert.Contains("Idle", config.RequiredAnimationStates);
            Assert.Contains("Run", config.RequiredAnimationStates);
            Assert.Contains("Sprint", config.RequiredAnimationStates);
            Assert.Contains("Kick", config.RequiredAnimationStates);
            Assert.Contains("Tackle", config.RequiredAnimationStates);
            Assert.Contains("Celebrate", config.RequiredAnimationStates);
        }

        [Test]
        public void CharacterModelConfig_RenderSettings_UsesMaterialPropertyBlock()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.UseMaterialPropertyBlock);
        }

        [Test]
        public void CharacterModelConfig_ShaderProperty_IsBaseColor()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual("_BaseColor", config.TeamColorShaderProperty);
        }

        [Test]
        public void TeamColorAssigner_TeamIndexZero_AssignsTeamA()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(0, config);
            Assert.AreEqual(config.TeamAColor.R, color.R);
            Assert.AreEqual(config.TeamAColor.G, color.G);
            Assert.AreEqual(config.TeamAColor.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_TeamIndexOne_AssignsTeamB()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(1, config);
            Assert.AreEqual(config.TeamBColor.R, color.R);
            Assert.AreEqual(config.TeamBColor.G, color.G);
            Assert.AreEqual(config.TeamBColor.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_InvalidTeam_ReturnsWhite()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(5, config);
            Assert.AreEqual(1f, color.R);
            Assert.AreEqual(1f, color.G);
            Assert.AreEqual(1f, color.B);
        }

        // ─── NEW: LODConfig Tests ───────────────────────────────────────

        [Test]
        public void LODConfig_Defaults_LOD0Is30KTriangles()
        {
            var lod = new LODConfig();
            Assert.AreEqual(30000, lod.LOD0MaxTriangles,
                "LOD0 default should be 30K triangles for AAA character detail.");
        }

        [Test]
        public void LODConfig_Defaults_LOD1Is5KTriangles()
        {
            var lod = new LODConfig();
            Assert.AreEqual(5000, lod.LOD1MaxTriangles,
                "LOD1 default should be 5K triangles for mid-distance rendering.");
        }

        [Test]
        public void LODConfig_ScreenHeights_LOD0HigherThanLOD1()
        {
            var lod = new LODConfig();
            Assert.Greater(lod.LOD0ScreenHeight, lod.LOD1ScreenHeight,
                "LOD0 screen height threshold must be higher than LOD1 (closer = more detail).");
        }

        [Test]
        public void LODConfig_IsWithinBudget_LOD0ValidCount_ReturnsTrue()
        {
            var lod = new LODConfig();
            Assert.IsTrue(lod.IsWithinBudget(25000, 0),
                "25K triangles should be within LOD0 budget of 30K.");
        }

        [Test]
        public void LODConfig_IsWithinBudget_LOD0OverBudget_ReturnsFalse()
        {
            var lod = new LODConfig();
            Assert.IsFalse(lod.IsWithinBudget(35000, 0),
                "35K triangles should exceed LOD0 budget of 30K.");
        }

        [Test]
        public void LODConfig_IsWithinBudget_LOD1ValidCount_ReturnsTrue()
        {
            var lod = new LODConfig();
            Assert.IsTrue(lod.IsWithinBudget(4000, 1),
                "4K triangles should be within LOD1 budget of 5K.");
        }

        [Test]
        public void LODConfig_IsWithinBudget_LOD1OverBudget_ReturnsFalse()
        {
            var lod = new LODConfig();
            Assert.IsFalse(lod.IsWithinBudget(6000, 1),
                "6K triangles should exceed LOD1 budget of 5K.");
        }

        [Test]
        public void LODConfig_IsWithinBudget_NegativeCount_ReturnsFalse()
        {
            var lod = new LODConfig();
            Assert.IsFalse(lod.IsWithinBudget(-1, 0),
                "Negative triangle count is never valid.");
        }

        [Test]
        public void LODConfig_IsWithinBudget_InvalidLODLevel_ReturnsFalse()
        {
            var lod = new LODConfig();
            Assert.IsFalse(lod.IsWithinBudget(1000, 5),
                "LOD level 5 does not exist; should return false.");
        }

        [Test]
        public void LODConfig_GetMaxTriangles_ReturnsCorrectBudgets()
        {
            var lod = new LODConfig();
            Assert.AreEqual(30000, lod.GetMaxTriangles(0));
            Assert.AreEqual(5000, lod.GetMaxTriangles(1));
            Assert.AreEqual(0, lod.GetMaxTriangles(2),
                "Undefined LOD levels should return 0 budget.");
        }

        [Test]
        public void LODConfig_CrossFade_EnabledByDefault()
        {
            var lod = new LODConfig();
            Assert.IsTrue(lod.CrossFadeEnabled,
                "Cross-fade should be enabled by default to prevent LOD popping.");
            Assert.Greater(lod.CrossFadeDuration, 0f,
                "Cross-fade duration should be positive.");
        }

        // ─── NEW: UniformSlot / TeamUniformConfig Tests ─────────────────

        [Test]
        public void UniformSlot_HasThreeSlots()
        {
            var slots = System.Enum.GetValues(typeof(UniformSlot));
            Assert.AreEqual(3, slots.Length,
                "UniformSlot should have exactly 3 values: Jersey, Shorts, Socks.");
        }

        [Test]
        public void TeamUniformConfig_DefaultTeamA_JerseyIsBlue()
        {
            var config = new CharacterModelConfig();
            var jerseyColor = config.TeamAUniform.GetSlotColor(UniformSlot.Jersey);
            Assert.Greater(jerseyColor.B, jerseyColor.R,
                "Team A jersey should be blue (B > R).");
        }

        [Test]
        public void TeamUniformConfig_DefaultTeamB_JerseyIsRed()
        {
            var config = new CharacterModelConfig();
            var jerseyColor = config.TeamBUniform.GetSlotColor(UniformSlot.Jersey);
            Assert.Greater(jerseyColor.R, jerseyColor.B,
                "Team B jersey should be red (R > B).");
        }

        [Test]
        public void TeamUniformConfig_AllSlotsConfigured()
        {
            var config = new CharacterModelConfig();
            foreach (UniformSlot slot in System.Enum.GetValues(typeof(UniformSlot)))
            {
                var colorA = config.TeamAUniform.GetSlotColor(slot);
                var colorB = config.TeamBUniform.GetSlotColor(slot);
                // All colors should have non-zero alpha
                Assert.AreEqual(1f, colorA.A,
                    $"Team A {slot} should have alpha = 1.");
                Assert.AreEqual(1f, colorB.A,
                    $"Team B {slot} should have alpha = 1.");
            }
        }

        [Test]
        public void TeamUniformConfig_SetSlotColor_UpdatesCorrectly()
        {
            var uniform = new TeamUniformConfig(
                "Test",
                SimpleColor.White,
                SimpleColor.White,
                SimpleColor.White);

            var customColor = new SimpleColor(0.5f, 0.3f, 0.9f);
            uniform.SetSlotColor(UniformSlot.Shorts, customColor);

            var result = uniform.GetSlotColor(UniformSlot.Shorts);
            Assert.AreEqual(0.5f, result.R);
            Assert.AreEqual(0.3f, result.G);
            Assert.AreEqual(0.9f, result.B);
        }

        [Test]
        public void TeamUniformConfig_TeamName_IsSet()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual("Team A", config.TeamAUniform.TeamName);
            Assert.AreEqual("Team B", config.TeamBUniform.TeamName);
        }

        // ─── NEW: TeamColorAssigner Per-Slot Tests ──────────────────────

        [Test]
        public void TeamColorAssigner_GetSlotColor_TeamA_Jersey_ReturnsJerseyColor()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetSlotColor(0, UniformSlot.Jersey, config);
            var expected = config.TeamAUniform.GetSlotColor(UniformSlot.Jersey);
            Assert.AreEqual(expected.R, color.R);
            Assert.AreEqual(expected.G, color.G);
            Assert.AreEqual(expected.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_GetSlotColor_TeamB_Shorts_ReturnsShortsColor()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetSlotColor(1, UniformSlot.Shorts, config);
            var expected = config.TeamBUniform.GetSlotColor(UniformSlot.Shorts);
            Assert.AreEqual(expected.R, color.R);
            Assert.AreEqual(expected.G, color.G);
            Assert.AreEqual(expected.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_GetSlotColor_InvalidTeam_FallsBackToWhite()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetSlotColor(99, UniformSlot.Jersey, config);
            // Invalid team with no uniform should fall back to GetTeamColor which returns white
            Assert.AreEqual(1f, color.R);
            Assert.AreEqual(1f, color.G);
            Assert.AreEqual(1f, color.B);
        }

        [Test]
        public void TeamColorAssigner_GetTeamUniform_TeamA_ReturnsNonNull()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var uniform = assigner.GetTeamUniform(0, config);
            Assert.IsNotNull(uniform, "Team A uniform should not be null.");
        }

        [Test]
        public void TeamColorAssigner_GetTeamUniform_InvalidTeam_ReturnsNull()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var uniform = assigner.GetTeamUniform(5, config);
            Assert.IsNull(uniform, "Invalid team index should return null uniform.");
        }

        // ─── NEW: BoneStructureRequirements Tests ───────────────────────

        [Test]
        public void BoneRequirements_MinBoneCount_IsReasonable()
        {
            var config = new CharacterModelConfig();
            Assert.That(config.BoneRequirements.MinBoneCount, Is.InRange(40, 100),
                "Humanoid rig should require between 40 and 100 bones.");
        }

        [Test]
        public void BoneRequirements_RequiredBones_ContainsHips()
        {
            var config = new CharacterModelConfig();
            Assert.Contains("Hips", config.BoneRequirements.RequiredBones,
                "Hips is the root bone of a humanoid rig — must be required.");
        }

        [Test]
        public void BoneRequirements_RequiredBones_ContainsAllLimbs()
        {
            var config = new CharacterModelConfig();
            var required = config.BoneRequirements.RequiredBones;

            // Arms
            Assert.Contains("LeftUpperArm", required);
            Assert.Contains("LeftLowerArm", required);
            Assert.Contains("RightUpperArm", required);
            Assert.Contains("RightLowerArm", required);

            // Legs (critical for soccer)
            Assert.Contains("LeftUpperLeg", required);
            Assert.Contains("LeftLowerLeg", required);
            Assert.Contains("LeftFoot", required);
            Assert.Contains("RightUpperLeg", required);
            Assert.Contains("RightLowerLeg", required);
            Assert.Contains("RightFoot", required);
        }

        [Test]
        public void BoneRequirements_RequiredBones_ContainsToes()
        {
            var config = new CharacterModelConfig();
            Assert.Contains("LeftToes", config.BoneRequirements.RequiredBones,
                "Toes are needed for soccer kick animations.");
            Assert.Contains("RightToes", config.BoneRequirements.RequiredBones);
        }

        [Test]
        public void BoneRequirements_FindMissingBones_AllPresent_ReturnsEmpty()
        {
            var config = new CharacterModelConfig();
            var allBones = new List<string>(config.BoneRequirements.RequiredBones);
            // Add extra bones to exceed minimum
            for (int i = 0; i < 40; i++)
                allBones.Add($"ExtraBone_{i}");

            var missing = config.BoneRequirements.FindMissingBones(allBones);
            Assert.AreEqual(0, missing.Count,
                "No bones should be missing when all required bones are present.");
        }

        [Test]
        public void BoneRequirements_FindMissingBones_MissingHead_ReportsIt()
        {
            var config = new CharacterModelConfig();
            var bones = new List<string>(config.BoneRequirements.RequiredBones);
            bones.Remove("Head");

            var missing = config.BoneRequirements.FindMissingBones(bones);
            Assert.Contains("Head", missing,
                "Missing 'Head' bone should be reported.");
        }

        [Test]
        public void BoneRequirements_FindMissingBones_CaseInsensitive()
        {
            var config = new CharacterModelConfig();
            // Provide all bones but in lowercase
            var bones = config.BoneRequirements.RequiredBones
                .Select(b => b.ToLowerInvariant())
                .ToList();

            var missing = config.BoneRequirements.FindMissingBones(bones);
            Assert.AreEqual(0, missing.Count,
                "Bone name matching should be case-insensitive.");
        }

        [Test]
        public void BoneRequirements_IsBoneCountValid_AboveMinimum_ReturnsTrue()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.BoneRequirements.IsBoneCountValid(config.BoneRequirements.MinBoneCount),
                "Exactly the minimum bone count should be valid.");
            Assert.IsTrue(config.BoneRequirements.IsBoneCountValid(config.BoneRequirements.MinBoneCount + 10));
        }

        [Test]
        public void BoneRequirements_IsBoneCountValid_BelowMinimum_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.BoneRequirements.IsBoneCountValid(10),
                "10 bones is far below the minimum for a humanoid rig.");
        }

        // ─── NEW: Validation Method Tests ───────────────────────────────

        [Test]
        public void IsTriangleCountValid_WithinRange_ReturnsTrue()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.IsTriangleCountValid(20000),
                "20K triangles should be within [10K, 30K] range.");
        }

        [Test]
        public void IsTriangleCountValid_BelowMinimum_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.IsTriangleCountValid(5000),
                "5K triangles is below the 10K minimum for AAA quality.");
        }

        [Test]
        public void IsTriangleCountValid_AboveMaximum_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.IsTriangleCountValid(50000),
                "50K triangles exceeds the 30K maximum budget.");
        }

        [Test]
        public void IsTriangleCountValid_ExactBoundaries_ReturnsTrue()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.IsTriangleCountValid(config.MinTrianglesPerModel),
                "Exact minimum should be valid.");
            Assert.IsTrue(config.IsTriangleCountValid(config.MaxTrianglesPerModel),
                "Exact maximum should be valid.");
        }

        [Test]
        public void IsTextureResolutionValid_WithinRange_ReturnsTrue()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.IsTextureResolutionValid(2048),
                "2048 should be within valid texture range.");
        }

        [Test]
        public void IsTextureResolutionValid_BelowMinimum_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.IsTextureResolutionValid(128),
                "128 is below the minimum texture resolution.");
        }

        [Test]
        public void IsTextureResolutionValid_AboveMaximum_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.IsTextureResolutionValid(8192),
                "8192 exceeds the maximum texture resolution budget.");
        }

        [Test]
        public void IsTextureResolutionPowerOfTwo_PowerOfTwo_ReturnsTrue()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.IsTextureResolutionPowerOfTwo(512));
            Assert.IsTrue(config.IsTextureResolutionPowerOfTwo(1024));
            Assert.IsTrue(config.IsTextureResolutionPowerOfTwo(2048));
            Assert.IsTrue(config.IsTextureResolutionPowerOfTwo(4096));
        }

        [Test]
        public void IsTextureResolutionPowerOfTwo_NotPowerOfTwo_ReturnsFalse()
        {
            var config = new CharacterModelConfig();
            Assert.IsFalse(config.IsTextureResolutionPowerOfTwo(1000));
            Assert.IsFalse(config.IsTextureResolutionPowerOfTwo(1500));
            Assert.IsFalse(config.IsTextureResolutionPowerOfTwo(0));
            Assert.IsFalse(config.IsTextureResolutionPowerOfTwo(-1));
        }

        // ─── NEW: Full ValidateModel Tests ──────────────────────────────

        [Test]
        public void ValidateModel_AllValid_ReturnsIsValid()
        {
            var config = new CharacterModelConfig();
            var allBones = new List<string>(config.BoneRequirements.RequiredBones);
            // Pad with extra bones to meet minimum bone count
            for (int i = allBones.Count; i < config.BoneRequirements.MinBoneCount; i++)
                allBones.Add($"ExtraBone_{i}");

            var result = config.ValidateModel(
                triangleCount: 20000,
                textureResolution: 2048,
                boneCount: allBones.Count,
                boneNames: allBones);

            Assert.IsTrue(result.IsValid,
                $"Model with valid metrics should pass validation. Errors: {string.Join("; ", result.Errors)}");
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(20000, result.TriangleCount);
        }

        [Test]
        public void ValidateModel_TooFewTriangles_ReturnsError()
        {
            var config = new CharacterModelConfig();
            var bones = BuildValidBoneList(config);

            var result = config.ValidateModel(
                triangleCount: 500,
                textureResolution: 2048,
                boneCount: bones.Count,
                boneNames: bones);

            Assert.IsFalse(result.IsValid,
                "Model with 500 triangles should fail validation.");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("below minimum")),
                "Error should mention 'below minimum'.");
        }

        [Test]
        public void ValidateModel_TooManyTriangles_ReturnsError()
        {
            var config = new CharacterModelConfig();
            var bones = BuildValidBoneList(config);

            var result = config.ValidateModel(
                triangleCount: 50000,
                textureResolution: 2048,
                boneCount: bones.Count,
                boneNames: bones);

            Assert.IsFalse(result.IsValid,
                "Model with 50K triangles should fail validation.");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("exceeds maximum")),
                "Error should mention exceeding maximum.");
        }

        [Test]
        public void ValidateModel_InvalidTexture_ReturnsError()
        {
            var config = new CharacterModelConfig();
            var bones = BuildValidBoneList(config);

            var result = config.ValidateModel(
                triangleCount: 20000,
                textureResolution: 64,
                boneCount: bones.Count,
                boneNames: bones);

            Assert.IsFalse(result.IsValid,
                "Model with 64px texture should fail validation.");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("outside valid range")),
                "Error should mention texture outside valid range.");
        }

        [Test]
        public void ValidateModel_InsufficientBones_ReturnsError()
        {
            var config = new CharacterModelConfig();

            var result = config.ValidateModel(
                triangleCount: 20000,
                textureResolution: 2048,
                boneCount: 10,
                boneNames: new List<string> { "Hips", "Spine" });

            Assert.IsFalse(result.IsValid,
                "Model with only 10 bones should fail validation.");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Bone count")),
                "Error should mention bone count.");
        }

        [Test]
        public void ValidateModel_MissingBones_ReportsSpecificBones()
        {
            var config = new CharacterModelConfig();
            var bones = new List<string>(config.BoneRequirements.RequiredBones);
            // Pad to meet count
            for (int i = bones.Count; i < config.BoneRequirements.MinBoneCount; i++)
                bones.Add($"ExtraBone_{i}");
            // Remove a specific bone
            bones.Remove("Head");

            var result = config.ValidateModel(
                triangleCount: 20000,
                textureResolution: 2048,
                boneCount: bones.Count,
                boneNames: bones);

            Assert.IsFalse(result.IsValid,
                "Model missing 'Head' bone should fail.");
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Head")),
                "Error should mention the missing 'Head' bone.");
        }

        [Test]
        public void ValidateModel_NonPowerOfTwoTexture_ProducesWarning()
        {
            var config = new CharacterModelConfig();
            var bones = BuildValidBoneList(config);

            var result = config.ValidateModel(
                triangleCount: 20000,
                textureResolution: 1000, // Not power of two, but within range
                boneCount: bones.Count,
                boneNames: bones);

            Assert.IsTrue(result.Warnings.Any(w => w.Contains("power of two")),
                "Non-power-of-two texture should produce a warning.");
        }

        [Test]
        public void CharacterValidationResult_AddError_SetsIsValidFalse()
        {
            var result = new CharacterValidationResult();
            Assert.IsTrue(result.IsValid, "New result should be valid.");

            result.AddError("Test error");
            Assert.IsFalse(result.IsValid, "Result should be invalid after adding an error.");
            Assert.AreEqual(1, result.Errors.Count);
        }

        [Test]
        public void CharacterValidationResult_AddWarning_KeepsIsValidTrue()
        {
            var result = new CharacterValidationResult();
            result.AddWarning("Test warning");

            Assert.IsTrue(result.IsValid, "Warnings should not invalidate the result.");
            Assert.AreEqual(1, result.Warnings.Count);
        }

        // ─── NEW: SimpleColor Tests ─────────────────────────────────────

        [Test]
        public void SimpleColor_White_IsAllOnes()
        {
            var white = SimpleColor.White;
            Assert.AreEqual(1f, white.R);
            Assert.AreEqual(1f, white.G);
            Assert.AreEqual(1f, white.B);
            Assert.AreEqual(1f, white.A);
        }

        [Test]
        public void SimpleColor_Black_IsAllZeros()
        {
            var black = SimpleColor.Black;
            Assert.AreEqual(0f, black.R);
            Assert.AreEqual(0f, black.G);
            Assert.AreEqual(0f, black.B);
            Assert.AreEqual(1f, black.A, "Black should still have alpha = 1.");
        }

        [Test]
        public void SimpleColor_Constructor_DefaultAlpha_IsOne()
        {
            var color = new SimpleColor(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(1f, color.A,
                "Default alpha should be 1 when not specified.");
        }

        // ─── NEW: CharacterModelConfig LOD Integration Tests ────────────

        [Test]
        public void CharacterModelConfig_LOD_LOD0MatchesMaxTriangles()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual(config.MaxTrianglesPerModel, config.LOD.LOD0MaxTriangles,
                "LOD0 budget should match the model's max triangle count.");
        }

        [Test]
        public void CharacterModelConfig_LOD_LOD1LessThanLOD0()
        {
            var config = new CharacterModelConfig();
            Assert.Less(config.LOD.LOD1MaxTriangles, config.LOD.LOD0MaxTriangles,
                "LOD1 should have fewer triangles than LOD0.");
        }

        // ─── Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Builds a bone list that passes all validation requirements.
        /// </summary>
        private List<string> BuildValidBoneList(CharacterModelConfig config)
        {
            var bones = new List<string>(config.BoneRequirements.RequiredBones);
            for (int i = bones.Count; i < config.BoneRequirements.MinBoneCount; i++)
                bones.Add($"ExtraBone_{i}");
            return bones;
        }
    }
}
