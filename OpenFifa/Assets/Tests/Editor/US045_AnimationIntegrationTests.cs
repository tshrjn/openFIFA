using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US045")]
    [Category("Art")]
    public class US045_AnimationIntegrationTests
    {
        // ─── Original Tests (unchanged) ─────────────────────────────────

        [Test]
        public void AnimationClipConfig_IdleClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.IsNotNull(config.GetClipName("Idle"));
            Assert.AreEqual("Soccer_Idle", config.GetClipName("Idle"));
        }

        [Test]
        public void AnimationClipConfig_RunClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Running", config.GetClipName("Run"));
        }

        [Test]
        public void AnimationClipConfig_SprintClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Sprinting", config.GetClipName("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_KickClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Soccer_Kick", config.GetClipName("Kick"));
        }

        [Test]
        public void AnimationClipConfig_TackleClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Slide_Tackle", config.GetClipName("Tackle"));
        }

        [Test]
        public void AnimationClipConfig_GKDiveClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Goalkeeper_Dive", config.GetClipName("GKDive"));
        }

        [Test]
        public void AnimationClipConfig_LocomotionClips_LoopEnabled()
        {
            var config = new AnimationClipConfig();
            Assert.IsTrue(config.ShouldLoop("Idle"));
            Assert.IsTrue(config.ShouldLoop("Run"));
            Assert.IsTrue(config.ShouldLoop("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_ActionClips_NoLoop()
        {
            var config = new AnimationClipConfig();
            Assert.IsFalse(config.ShouldLoop("Kick"));
            Assert.IsFalse(config.ShouldLoop("Tackle"));
            Assert.IsFalse(config.ShouldLoop("GKDive"));
        }

        [Test]
        public void AnimationClipConfig_RootMotion_DisabledForLocomotion()
        {
            var config = new AnimationClipConfig();
            Assert.IsFalse(config.UseRootMotion("Idle"));
            Assert.IsFalse(config.UseRootMotion("Run"));
            Assert.IsFalse(config.UseRootMotion("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_RetargetSource_IsQuaternius()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Quaternius_Humanoid", config.RetargetSourceAvatar);
        }

        [Test]
        public void AnimationClipConfig_AllRequiredStates_HaveClips()
        {
            var config = new AnimationClipConfig();
            var required = new[] { "Idle", "Run", "Sprint", "Kick", "Tackle", "GKDive" };
            foreach (var state in required)
            {
                Assert.IsNotNull(config.GetClipName(state),
                    $"Missing clip mapping for state: {state}");
            }
        }

        // ─── NEW: MocapClipMetadata Tests ───────────────────────────────

        [Test]
        public void MocapClipMetadata_DefaultConstructor_HasValidDefaults()
        {
            var meta = new MocapClipMetadata();
            Assert.AreEqual("", meta.SourceName);
            Assert.AreEqual(30f, meta.Fps);
            Assert.AreEqual(0, meta.FrameCount);
            Assert.AreEqual(0f, meta.Duration);
            Assert.AreEqual("Mixamo", meta.CaptureStudio);
        }

        [Test]
        public void MocapClipMetadata_ParameterizedConstructor_CalculatesDuration()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 90, "Rokoko", MocapClipType.Idle);
            Assert.AreEqual("TestClip", meta.SourceName);
            Assert.AreEqual(3f, meta.Duration, 0.01f,
                "90 frames at 30fps should produce 3s duration.");
            Assert.AreEqual("Rokoko", meta.CaptureStudio);
            Assert.AreEqual(MocapClipType.Idle, meta.ClipType);
        }

        [Test]
        public void MocapClipMetadata_IsValid_ReturnsTrue_WhenComplete()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 60, "Mixamo", MocapClipType.Kick);
            Assert.IsTrue(meta.IsValid(),
                "Metadata with all required fields populated should be valid.");
        }

        [Test]
        public void MocapClipMetadata_IsValid_ReturnsFalse_WhenSourceNameEmpty()
        {
            var meta = new MocapClipMetadata("", 30f, 60, "Mixamo", MocapClipType.Kick);
            Assert.IsFalse(meta.IsValid(),
                "Metadata with empty source name should be invalid.");
        }

        [Test]
        public void MocapClipMetadata_RecalculateDuration_UpdatesCorrectly()
        {
            var meta = new MocapClipMetadata();
            meta.SourceName = "Test";
            meta.Fps = 60f;
            meta.FrameCount = 120;
            meta.RecalculateDuration();
            Assert.AreEqual(2f, meta.Duration, 0.01f,
                "120 frames at 60fps should recalculate to 2s.");
        }

        // ─── NEW: AnimationBlendConfig Tests ────────────────────────────

        [Test]
        public void AnimationBlendConfig_Defaults_AreValid()
        {
            var blend = new AnimationBlendConfig();
            Assert.IsTrue(blend.IsValid(),
                "Default blend config should be valid.");
        }

        [Test]
        public void AnimationBlendConfig_CrossFadeDuration_LocomotionIsPositive()
        {
            var blend = new AnimationBlendConfig();
            Assert.Greater(blend.LocomotionCrossFadeDuration, 0f,
                "Locomotion cross-fade must be positive for smooth transitions.");
            Assert.LessOrEqual(blend.LocomotionCrossFadeDuration, 0.5f,
                "Locomotion cross-fade should not exceed 0.5s for responsiveness.");
        }

        [Test]
        public void AnimationBlendConfig_GetCrossFadeDuration_ActionEntry_UsesActionDuration()
        {
            var blend = new AnimationBlendConfig();
            float duration = blend.GetCrossFadeDuration(AnimationStateId.Run, AnimationStateId.Kick);
            Assert.AreEqual(blend.ActionCrossFadeDuration, duration,
                "Transition into Kick should use action cross-fade duration.");
        }

        [Test]
        public void AnimationBlendConfig_GetCrossFadeDuration_ActionExit_UsesExitDuration()
        {
            var blend = new AnimationBlendConfig();
            float duration = blend.GetCrossFadeDuration(AnimationStateId.Kick, AnimationStateId.Idle);
            Assert.AreEqual(blend.ActionExitCrossFadeDuration, duration,
                "Transition from Kick back to Idle should use action exit duration.");
        }

        [Test]
        public void AnimationBlendConfig_IsValid_ReturnsFalse_WhenNegativeCrossFade()
        {
            var blend = new AnimationBlendConfig();
            blend.LocomotionCrossFadeDuration = -0.1f;
            Assert.IsFalse(blend.IsValid(),
                "Negative cross-fade duration should invalidate config.");
        }

        // ─── NEW: RetargetConfig Tests ──────────────────────────────────

        [Test]
        public void RetargetConfig_Defaults_AreValid()
        {
            var retarget = new RetargetConfig();
            Assert.IsTrue(retarget.IsValid(),
                "Default retarget config should be valid.");
        }

        [Test]
        public void RetargetConfig_DefaultBoneMapping_ContainsHips()
        {
            var retarget = new RetargetConfig();
            string target = retarget.GetTargetBone("mixamorig:Hips");
            Assert.AreEqual("Hips", target,
                "Default Mixamo -> Unity bone mapping should map Hips.");
        }

        [Test]
        public void RetargetConfig_DefaultBoneMapping_ContainsAllLimbs()
        {
            var retarget = new RetargetConfig();
            Assert.AreEqual("LeftUpperArm", retarget.GetTargetBone("mixamorig:LeftArm"));
            Assert.AreEqual("RightUpperArm", retarget.GetTargetBone("mixamorig:RightArm"));
            Assert.AreEqual("LeftFoot", retarget.GetTargetBone("mixamorig:LeftFoot"));
            Assert.AreEqual("RightFoot", retarget.GetTargetBone("mixamorig:RightFoot"));
        }

        [Test]
        public void RetargetConfig_GetTargetBone_UnmappedBone_ReturnsNull()
        {
            var retarget = new RetargetConfig();
            Assert.IsNull(retarget.GetTargetBone("NonExistentBone"),
                "Unmapped bone should return null.");
        }

        [Test]
        public void RetargetConfig_GetMappingCompleteness_AllMapped_Returns1()
        {
            var retarget = new RetargetConfig();
            var required = new List<string> { "Hips", "Spine", "Head" };
            float completeness = retarget.GetMappingCompleteness(required);
            Assert.AreEqual(1f, completeness, 0.01f,
                "All required bones are mapped — completeness should be 1.0.");
        }

        [Test]
        public void RetargetConfig_MeetsMappingThreshold_WhenComplete_ReturnsTrue()
        {
            var retarget = new RetargetConfig();
            var boneReqs = new CharacterModelConfig().BoneRequirements;
            bool meets = retarget.MeetsMappingThreshold(boneReqs.RequiredBones);
            Assert.IsTrue(meets,
                "Default mapping should meet the 90% threshold for standard bones.");
        }

        [Test]
        public void RetargetConfig_IsValid_ReturnsFalse_WhenNoMapping()
        {
            var retarget = new RetargetConfig();
            retarget.BoneMapping.Clear();
            Assert.IsFalse(retarget.IsValid(),
                "Config with empty bone mapping should be invalid.");
        }

        // ─── NEW: FootIKConfig Tests ────────────────────────────────────

        [Test]
        public void FootIKConfig_GroundContactThreshold_IsPositive()
        {
            var ik = new FootIKConfig();
            Assert.Greater(ik.GroundContactThreshold, 0f,
                "Ground contact threshold must be positive.");
        }

        [Test]
        public void FootIKConfig_IsGrounded_WithinThreshold_ReturnsTrue()
        {
            var ik = new FootIKConfig();
            Assert.IsTrue(ik.IsGrounded(0.01f),
                "Foot at 0.01m should be considered grounded with default 0.05m threshold.");
        }

        [Test]
        public void FootIKConfig_IsGrounded_AboveThreshold_ReturnsFalse()
        {
            var ik = new FootIKConfig();
            Assert.IsFalse(ik.IsGrounded(0.2f),
                "Foot at 0.2m should not be considered grounded.");
        }

        [Test]
        public void FootIKConfig_EvaluateLeftFootWeight_AtStart_ReturnsExpectedWeight()
        {
            var ik = new FootIKConfig();
            float weight = ik.EvaluateLeftFootWeight(0f);
            Assert.AreEqual(1f, weight, 0.01f,
                "Left foot weight at t=0 should be 1 (planted).");
        }

        [Test]
        public void FootIKConfig_EvaluateLeftFootWeight_AtMidpoint_IsReduced()
        {
            var ik = new FootIKConfig();
            float weight = ik.EvaluateLeftFootWeight(0.5f);
            Assert.AreEqual(0f, weight, 0.01f,
                "Left foot weight at t=0.5 should be 0 (in air).");
        }

        // ─── NEW: AnimationEventConfig Tests ────────────────────────────

        [Test]
        public void AnimationEventConfig_KickContact_IsValid()
        {
            var evt = new AnimationEventConfig("OnKickContact", 0.45f, "float", "1.0");
            Assert.IsTrue(evt.IsValid(),
                "Kick contact event with all fields should be valid.");
            Assert.AreEqual("OnKickContact", evt.EventName);
            Assert.AreEqual(0.45f, evt.NormalizedTime, 0.001f);
        }

        [Test]
        public void AnimationEventConfig_IsValid_ReturnsFalse_WhenNoName()
        {
            var evt = new AnimationEventConfig("", 0.5f);
            Assert.IsFalse(evt.IsValid(),
                "Event with empty name should be invalid.");
        }

        [Test]
        public void AnimationEventConfig_NormalizedTime_IsClamped()
        {
            var evt = new AnimationEventConfig("Test", 1.5f);
            Assert.AreEqual(1f, evt.NormalizedTime, 0.001f,
                "Normalized time should be clamped to 1.0 max.");

            var evt2 = new AnimationEventConfig("Test", -0.5f);
            Assert.AreEqual(0f, evt2.NormalizedTime, 0.001f,
                "Normalized time should be clamped to 0.0 min.");
        }

        // ─── NEW: MocapPipeline State Transition Tests ──────────────────

        [Test]
        public void MocapPipeline_InitialState_IsRaw()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 60, "Mixamo", MocapClipType.Kick);
            var pipeline = new MocapPipeline(meta);
            Assert.AreEqual(MocapImportState.Raw, pipeline.CurrentState,
                "New pipeline should start in Raw state.");
            Assert.IsFalse(pipeline.IsReady);
        }

        [Test]
        public void MocapPipeline_FullProgression_RawToReady()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 60, "Mixamo", MocapClipType.Kick);
            var pipeline = new MocapPipeline(meta);
            var retarget = new RetargetConfig();
            var quality = new AnimationQualityConfig();

            Assert.IsTrue(pipeline.AdvanceToRetargeted(retarget));
            Assert.AreEqual(MocapImportState.Retargeted, pipeline.CurrentState);

            Assert.IsTrue(pipeline.AdvanceToCompressed(quality));
            Assert.AreEqual(MocapImportState.Compressed, pipeline.CurrentState);

            Assert.IsTrue(pipeline.AdvanceToValidated());
            Assert.AreEqual(MocapImportState.Validated, pipeline.CurrentState);

            Assert.IsTrue(pipeline.AdvanceToReady());
            Assert.AreEqual(MocapImportState.Ready, pipeline.CurrentState);
            Assert.IsTrue(pipeline.IsReady);
        }

        [Test]
        public void MocapPipeline_InvalidTransition_SkipState_Fails()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 60, "Mixamo", MocapClipType.Idle);
            var pipeline = new MocapPipeline(meta);

            // Try to skip from Raw straight to Compressed
            var quality = new AnimationQualityConfig();
            Assert.IsFalse(pipeline.AdvanceToCompressed(quality),
                "Cannot jump from Raw to Compressed — must go through Retargeted.");
            Assert.IsTrue(pipeline.HasErrors);
        }

        [Test]
        public void MocapPipeline_IsValidTransition_ForwardSteps_ReturnsTrue()
        {
            Assert.IsTrue(MocapPipeline.IsValidTransition(MocapImportState.Raw, MocapImportState.Retargeted));
            Assert.IsTrue(MocapPipeline.IsValidTransition(MocapImportState.Retargeted, MocapImportState.Compressed));
            Assert.IsTrue(MocapPipeline.IsValidTransition(MocapImportState.Compressed, MocapImportState.Validated));
            Assert.IsTrue(MocapPipeline.IsValidTransition(MocapImportState.Validated, MocapImportState.Ready));
        }

        [Test]
        public void MocapPipeline_IsValidTransition_BackwardStep_ReturnsFalse()
        {
            Assert.IsFalse(MocapPipeline.IsValidTransition(MocapImportState.Compressed, MocapImportState.Raw),
                "Backward transitions should not be valid.");
            Assert.IsFalse(MocapPipeline.IsValidTransition(MocapImportState.Ready, MocapImportState.Validated),
                "Ready is a terminal state — no further transitions.");
        }

        [Test]
        public void MocapPipeline_Reset_ReturnsToRaw()
        {
            var meta = new MocapClipMetadata("TestClip", 30f, 60, "Mixamo", MocapClipType.Kick);
            var pipeline = new MocapPipeline(meta);
            pipeline.AdvanceToRetargeted(new RetargetConfig());
            pipeline.Reset();
            Assert.AreEqual(MocapImportState.Raw, pipeline.CurrentState,
                "Pipeline should return to Raw after reset.");
            Assert.IsFalse(pipeline.HasErrors);
        }

        // ─── NEW: ClipTransitionMatrix Tests ────────────────────────────

        [Test]
        public void ClipTransitionMatrix_IdleToRun_Exists()
        {
            var matrix = new ClipTransitionMatrix();
            Assert.IsTrue(matrix.HasTransition(AnimationStateId.Idle, AnimationStateId.Run),
                "Idle -> Run transition must exist for basic locomotion.");
        }

        [Test]
        public void ClipTransitionMatrix_RunToKick_Exists()
        {
            var matrix = new ClipTransitionMatrix();
            Assert.IsTrue(matrix.HasTransition(AnimationStateId.Run, AnimationStateId.Kick),
                "Run -> Kick transition must exist for shooting while running.");
        }

        [Test]
        public void ClipTransitionMatrix_KickToIdle_HasExitTime()
        {
            var matrix = new ClipTransitionMatrix();
            var transition = matrix.GetTransition(AnimationStateId.Kick, AnimationStateId.Idle);
            Assert.IsTrue(transition.HasValue, "Kick -> Idle transition must exist.");
            Assert.IsTrue(transition.Value.HasExitTime,
                "Kick -> Idle should have exit time to play kick animation to completion.");
            Assert.Greater(transition.Value.ExitTime, 0.5f,
                "Exit time should be past the midpoint of the kick animation.");
        }

        [Test]
        public void ClipTransitionMatrix_InvalidTransition_ReturnsFalse()
        {
            var matrix = new ClipTransitionMatrix();
            Assert.IsFalse(matrix.HasTransition(AnimationStateId.Kick, AnimationStateId.Tackle),
                "Direct Kick -> Tackle transition should not exist.");
        }

        [Test]
        public void ClipTransitionMatrix_GetBlendDuration_KnownTransition_ReturnsConfigured()
        {
            var matrix = new ClipTransitionMatrix();
            float duration = matrix.GetBlendDuration(AnimationStateId.Idle, AnimationStateId.Run);
            Assert.Greater(duration, 0f, "Blend duration must be positive.");
            Assert.LessOrEqual(duration, 0.5f, "Blend duration should be reasonable (< 0.5s).");
        }

        [Test]
        public void ClipTransitionMatrix_GetBlendDuration_UnknownTransition_ReturnsDefault()
        {
            var matrix = new ClipTransitionMatrix();
            float duration = matrix.GetBlendDuration(AnimationStateId.Kick, AnimationStateId.Sprint, 0.99f);
            Assert.AreEqual(0.99f, duration,
                "Unknown transition should return the specified default duration.");
        }

        // ─── NEW: BlendTreeConfig Tests ─────────────────────────────────

        [Test]
        public void BlendTreeConfig_Defaults_AreValid()
        {
            var tree = new BlendTreeConfig();
            Assert.IsTrue(tree.IsValid(),
                "Default blend tree config should be valid.");
        }

        [Test]
        public void BlendTreeConfig_HasThreeEntries_IdleRunSprint()
        {
            var tree = new BlendTreeConfig();
            Assert.AreEqual(3, tree.Entries.Count,
                "Default blend tree should have 3 entries: Idle, Run, Sprint.");
            Assert.AreEqual("Soccer_Idle", tree.GetClipNameAtIndex(0));
            Assert.AreEqual("Running", tree.GetClipNameAtIndex(1));
            Assert.AreEqual("Sprinting", tree.GetClipNameAtIndex(2));
        }

        [Test]
        public void BlendTreeConfig_Thresholds_AreAscending()
        {
            var tree = new BlendTreeConfig();
            for (int i = 1; i < tree.Entries.Count; i++)
            {
                Assert.Greater(tree.Entries[i].Threshold, tree.Entries[i - 1].Threshold,
                    $"Threshold at index {i} should be greater than index {i - 1}.");
            }
        }

        [Test]
        public void BlendTreeConfig_GetBlendEntries_AtZero_ReturnsIdle()
        {
            var tree = new BlendTreeConfig();
            var (lower, upper, factor) = tree.GetBlendEntries(0f);
            Assert.AreEqual(AnimationStateId.Idle, lower.State,
                "At speed=0, blend tree should return Idle state.");
        }

        [Test]
        public void BlendTreeConfig_GetBlendEntries_AtMax_ReturnsSprint()
        {
            var tree = new BlendTreeConfig();
            var (lower, upper, factor) = tree.GetBlendEntries(1f);
            Assert.AreEqual(AnimationStateId.Sprint, lower.State,
                "At speed=1, blend tree should return Sprint state.");
        }

        [Test]
        public void BlendTreeConfig_GetBlendEntries_AtMid_ReturnsBlend()
        {
            var tree = new BlendTreeConfig();
            var (lower, upper, factor) = tree.GetBlendEntries(0.75f);
            Assert.AreEqual(AnimationStateId.Run, lower.State,
                "At speed=0.75, lower entry should be Run.");
            Assert.AreEqual(AnimationStateId.Sprint, upper.State,
                "At speed=0.75, upper entry should be Sprint.");
            Assert.Greater(factor, 0f, "Blend factor should be positive at midpoint.");
            Assert.Less(factor, 1f, "Blend factor should not yet be 1.0.");
        }

        [Test]
        public void BlendTreeConfig_ParameterName_IsSpeed()
        {
            var tree = new BlendTreeConfig();
            Assert.AreEqual("Speed", tree.ParameterName,
                "Blend tree parameter must be 'Speed' to match AnimationStateLogic.SpeedParameter.");
        }

        // ─── NEW: AnimationLayerConfig Tests ────────────────────────────

        [Test]
        public void AnimationLayerConfig_CreateDefaultLayers_HasFourLayers()
        {
            var layers = AnimationLayerConfig.CreateDefaultLayers();
            Assert.AreEqual(4, layers.Count,
                "Default layer set should have 4 layers: Base, UpperBody, HeadLook, HandIK.");
        }

        [Test]
        public void AnimationLayerConfig_BaseLayer_IsOverrideWithFullWeight()
        {
            var layers = AnimationLayerConfig.CreateDefaultLayers();
            var baseLayer = layers.First(l => l.LayerType == AnimationLayerType.Base);
            Assert.AreEqual(1f, baseLayer.Weight, "Base layer weight should be 1.0.");
            Assert.AreEqual(AnimationLayerBlendMode.Override, baseLayer.BlendMode);
            Assert.IsTrue(baseLayer.IKPass, "Base layer should have IK pass enabled.");
        }

        [Test]
        public void AnimationLayerConfig_AdditiveLayers_HaveReducedWeight()
        {
            var layers = AnimationLayerConfig.CreateDefaultLayers();
            var additive = layers.Where(l => l.BlendMode == AnimationLayerBlendMode.Additive).ToList();
            Assert.Greater(additive.Count, 0, "Should have at least one additive layer.");

            foreach (var layer in additive)
            {
                Assert.Less(layer.Weight, 1f,
                    $"Additive layer '{layer.LayerName}' weight should be < 1.0 to blend, not override.");
            }
        }

        [Test]
        public void AnimationLayerConfig_IsValid_ReturnsFalse_WhenNoName()
        {
            var layer = new AnimationLayerConfig();
            layer.LayerName = "";
            Assert.IsFalse(layer.IsValid(),
                "Layer with empty name should be invalid.");
        }

        // ─── NEW: ClipDurationRanges Tests ──────────────────────────────

        [Test]
        public void ClipDurationRanges_IdleRange_Is2To5Seconds()
        {
            var (min, max) = ClipDurationRanges.GetRange(MocapClipType.Idle);
            Assert.AreEqual(2f, min);
            Assert.AreEqual(5f, max);
        }

        [Test]
        public void ClipDurationRanges_LocomotionRange_IsUnderTwoSeconds()
        {
            var (min, max) = ClipDurationRanges.GetRange(MocapClipType.Locomotion);
            Assert.AreEqual(0.8f, min);
            Assert.AreEqual(1.2f, max);
        }

        [Test]
        public void ClipDurationRanges_KickRange_IsShort()
        {
            var (min, max) = ClipDurationRanges.GetRange(MocapClipType.Kick);
            Assert.AreEqual(0.3f, min);
            Assert.AreEqual(0.8f, max);
        }

        [Test]
        public void ClipDurationRanges_IsInRange_ValidDuration_ReturnsTrue()
        {
            Assert.IsTrue(ClipDurationRanges.IsInRange(MocapClipType.Idle, 3f),
                "3s idle animation should be in valid range.");
        }

        [Test]
        public void ClipDurationRanges_IsInRange_TooShort_ReturnsFalse()
        {
            Assert.IsFalse(ClipDurationRanges.IsInRange(MocapClipType.Idle, 0.5f),
                "0.5s idle animation should be too short.");
        }

        // ─── NEW: ClipNamingConvention Tests ────────────────────────────

        [Test]
        public void ClipNamingConvention_ValidName_ReturnsNull()
        {
            string error = ClipNamingConvention.Validate("Mocap_Kick_RightFoot");
            Assert.IsNull(error,
                "Valid naming convention should return no error.");
        }

        [Test]
        public void ClipNamingConvention_MissingPrefix_ReturnsError()
        {
            string error = ClipNamingConvention.Validate("Soccer_Kick");
            Assert.IsNotNull(error,
                "Clip without 'Mocap_' prefix should fail validation.");
            Assert.IsTrue(error.Contains("prefix"));
        }

        [Test]
        public void ClipNamingConvention_InvalidType_ReturnsError()
        {
            string error = ClipNamingConvention.Validate("Mocap_Flying_Left");
            Assert.IsNotNull(error,
                "Clip with invalid type token 'Flying' should fail.");
            Assert.IsTrue(error.Contains("unrecognized type"));
        }

        [Test]
        public void ClipNamingConvention_IsValid_ValidName_ReturnsTrue()
        {
            Assert.IsTrue(ClipNamingConvention.IsValid("Mocap_Idle_Stance"));
            Assert.IsTrue(ClipNamingConvention.IsValid("Mocap_GKDive_Left"));
            Assert.IsTrue(ClipNamingConvention.IsValid("Mocap_Celebrate_Backflip"));
        }

        // ─── NEW: AnimationQualityConfig Tests ──────────────────────────

        [Test]
        public void AnimationQualityConfig_Defaults_AreValid()
        {
            var quality = new AnimationQualityConfig();
            Assert.IsTrue(quality.IsValid(),
                "Default quality config should be valid.");
            Assert.AreEqual(2, quality.CompressionLevel,
                "Default compression should be Optimal (level 2).");
        }

        [Test]
        public void AnimationQualityConfig_ForClipType_KickUsesHighQuality()
        {
            var quality = AnimationQualityConfig.ForClipType(MocapClipType.Kick);
            Assert.AreEqual(1, quality.CompressionLevel,
                "Kick clips should use high quality (level 1) for precise contact frames.");
            Assert.Less(quality.RotationError, 0.5f,
                "Kick clips should have tighter rotation error tolerance.");
        }

        [Test]
        public void AnimationQualityConfig_ForClipType_LocomotionUsesOptimal()
        {
            var quality = AnimationQualityConfig.ForClipType(MocapClipType.Locomotion);
            Assert.AreEqual(2, quality.CompressionLevel,
                "Locomotion clips should use optimal compression (level 2).");
        }

        [Test]
        public void AnimationQualityConfig_IsValid_ReturnsFalse_WhenCompressionOutOfRange()
        {
            var quality = new AnimationQualityConfig();
            quality.CompressionLevel = 5;
            Assert.IsFalse(quality.IsValid(),
                "Compression level 5 is out of range (0-3).");
        }

        // ─── NEW: Extended AnimationClipConfig Tests ────────────────────

        [Test]
        public void AnimationClipConfig_ExtendedClips_DribbleExists()
        {
            var config = new AnimationClipConfig();
            Assert.IsNotNull(config.GetClipName("Dribble"),
                "Dribble clip should exist for ball control animations.");
            Assert.IsNotNull(config.GetClipName("FirstTouch"),
                "FirstTouch clip should exist.");
            Assert.IsNotNull(config.GetClipName("Header"),
                "Header clip should exist.");
            Assert.IsNotNull(config.GetClipName("BicycleKick"),
                "BicycleKick clip should exist.");
        }

        [Test]
        public void AnimationClipConfig_GoalkeeperClips_AllExist()
        {
            var config = new AnimationClipConfig();
            var gkStates = config.GetGoalkeeperStates();
            Assert.GreaterOrEqual(gkStates.Count, 4,
                "Should have at least 4 goalkeeper clips: Dive, Catch, Punch, Distribute.");
            Assert.IsTrue(gkStates.Contains("GKDive"));
            Assert.IsTrue(gkStates.Contains("GKCatch"));
            Assert.IsTrue(gkStates.Contains("GKPunch"));
            Assert.IsTrue(gkStates.Contains("GKDistribute"));
        }

        [Test]
        public void AnimationClipConfig_CelebrationVariants_AtLeastFive()
        {
            var config = new AnimationClipConfig();
            var celebrations = config.GetCelebrationStates();
            Assert.GreaterOrEqual(celebrations.Count, 5,
                "Must have 5+ unique celebration animations per acceptance criteria.");
        }

        [Test]
        public void AnimationClipConfig_KickEvents_HaveContactFrame()
        {
            var config = new AnimationClipConfig();
            var events = config.GetEvents("Kick");
            Assert.Greater(events.Count, 0, "Kick should have animation events.");
            Assert.IsTrue(events.Any(e => e.EventName == "OnKickContact"),
                "Kick should have an OnKickContact event for ball physics.");
        }

        [Test]
        public void AnimationClipConfig_IsBlendConfigValid_ReturnsTrue()
        {
            var config = new AnimationClipConfig();
            Assert.IsTrue(config.IsBlendConfigValid(),
                "Default blend config should be valid.");
        }

        [Test]
        public void AnimationClipConfig_IsRetargetValid_ReturnsTrue()
        {
            var config = new AnimationClipConfig();
            Assert.IsTrue(config.IsRetargetValid(),
                "Default retarget config should be valid.");
        }

        [Test]
        public void AnimationClipConfig_IsCompressionValid_ReturnsTrue()
        {
            var config = new AnimationClipConfig();
            Assert.IsTrue(config.IsCompressionValid(),
                "Default compression config should be valid.");
        }

        [Test]
        public void AnimationClipConfig_GetMetadata_Idle_HasValidMetadata()
        {
            var config = new AnimationClipConfig();
            var meta = config.GetMetadata("Idle");
            Assert.IsNotNull(meta, "Idle clip should have metadata.");
            Assert.IsTrue(meta.IsValid(), "Idle metadata should be valid.");
            Assert.AreEqual(MocapClipType.Idle, meta.ClipType);
        }

        [Test]
        public void AnimationClipConfig_ClipCount_IncludesAllExtendedClips()
        {
            var config = new AnimationClipConfig();
            Assert.GreaterOrEqual(config.ClipCount, 17,
                "Should have at least 17 clips: 7 original + extended mocap + celebrations + GK.");
        }
    }
}
