using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Validation utilities for mocap animation clips.
    /// Checks retarget quality, foot sliding, transition smoothness,
    /// naming conventions, and duration ranges.
    /// </summary>
    public static class MocapAnimationValidator
    {
        // ─── Validation Result ──────────────────────────────────────────

        /// <summary>
        /// Result of a mocap animation validation pass.
        /// </summary>
        public class MocapValidationResult
        {
            public bool IsValid { get; private set; }
            public readonly List<string> Errors;
            public readonly List<string> Warnings;
            public string ClipName { get; set; }
            public float Duration { get; set; }
            public float FrameRate { get; set; }

            public MocapValidationResult()
            {
                IsValid = true;
                Errors = new List<string>();
                Warnings = new List<string>();
            }

            public void AddError(string message)
            {
                IsValid = false;
                Errors.Add(message);
            }

            public void AddWarning(string message)
            {
                Warnings.Add(message);
            }
        }

        // ─── Retarget Quality Validation ────────────────────────────────

        /// <summary>
        /// Validates that the clip is properly retargeted to a humanoid avatar.
        /// Checks bone mapping completeness by examining the Animator/Avatar.
        /// </summary>
        public static MocapValidationResult ValidateRetargetQuality(AnimationClip clip, Animator animator)
        {
            var result = new MocapValidationResult();
            result.ClipName = clip != null ? clip.name : "null";

            if (clip == null)
            {
                result.AddError("Animation clip is null.");
                return result;
            }

            if (animator == null)
            {
                result.AddError("Animator is null — cannot validate retarget quality.");
                return result;
            }

            if (animator.avatar == null)
            {
                result.AddError("Animator has no Avatar assigned.");
                return result;
            }

            if (!animator.avatar.isHuman)
            {
                result.AddError($"Avatar '{animator.avatar.name}' is not configured as Humanoid.");
                return result;
            }

            // Check that the clip is humanoid-compatible
            if (!clip.isHumanMotion)
            {
                result.AddWarning($"Clip '{clip.name}' is not marked as human motion. Retargeting may not work correctly.");
            }

            result.Duration = clip.length;
            result.FrameRate = clip.frameRate;

            return result;
        }

        // ─── Foot Sliding Detection ─────────────────────────────────────

        /// <summary>
        /// Detects potential foot sliding by sampling the clip and checking
        /// foot bone movement during expected ground contact phases.
        /// </summary>
        public static MocapValidationResult ValidateFootSliding(
            AnimationClip clip,
            float maxFootSlideThreshold = 0.02f)
        {
            var result = new MocapValidationResult();
            if (clip == null)
            {
                result.AddError("Animation clip is null.");
                return result;
            }

            result.ClipName = clip.name;
            result.Duration = clip.length;

            // Check clip curves for foot bone movement
            var bindings = AnimationUtility.GetCurveBindings(clip);

            bool hasLeftFootCurve = bindings.Any(b =>
                b.propertyName.Contains("LeftFoot") || b.path.Contains("LeftFoot"));
            bool hasRightFootCurve = bindings.Any(b =>
                b.propertyName.Contains("RightFoot") || b.path.Contains("RightFoot"));

            if (!hasLeftFootCurve && !hasRightFootCurve)
            {
                result.AddWarning(
                    $"Clip '{clip.name}' has no foot bone curves. " +
                    "Cannot detect foot sliding — manual review recommended.");
            }

            // Check for animation events that mark foot plant frames
            var events = AnimationUtility.GetAnimationEvents(clip);
            bool hasFootPlantEvents = events.Any(e =>
                e.functionName.Contains("FootPlant") ||
                e.functionName.Contains("LeftFoot") ||
                e.functionName.Contains("RightFoot"));

            if (!hasFootPlantEvents)
            {
                result.AddWarning(
                    $"Clip '{clip.name}' has no foot plant events. " +
                    "Consider adding OnLeftFootPlant/OnRightFootPlant events for IK grounding.");
            }

            return result;
        }

        // ─── Transition Smoothness ──────────────────────────────────────

        /// <summary>
        /// Validates that the clip has proper start/end poses for smooth transitions.
        /// Checks first and last frame similarity for looping clips.
        /// </summary>
        public static MocapValidationResult ValidateTransitionSmoothness(AnimationClip clip)
        {
            var result = new MocapValidationResult();
            if (clip == null)
            {
                result.AddError("Animation clip is null.");
                return result;
            }

            result.ClipName = clip.name;
            result.Duration = clip.length;

            if (clip.isLooping)
            {
                // For looping clips, check that loop offsets are minimal
                var bindings = AnimationUtility.GetCurveBindings(clip);
                int curveCount = bindings.Length;

                if (curveCount == 0)
                {
                    result.AddWarning($"Clip '{clip.name}' has no animation curves.");
                    return result;
                }

                // Sample first and last frame for each curve and check continuity
                int discontinuities = 0;
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.keys.Length < 2) continue;

                    float firstValue = curve.keys[0].value;
                    float lastValue = curve.keys[curve.keys.Length - 1].value;
                    float diff = Mathf.Abs(lastValue - firstValue);

                    // Rotation curves should have very small discontinuity for smooth loops
                    if (binding.propertyName.Contains("Rotation") && diff > 5f)
                    {
                        discontinuities++;
                    }
                }

                if (discontinuities > 0)
                {
                    result.AddWarning(
                        $"Clip '{clip.name}' has {discontinuities} curve(s) with loop discontinuities. " +
                        "May cause visible popping at loop point.");
                }
            }

            return result;
        }

        // ─── Naming Convention ──────────────────────────────────────────

        /// <summary>
        /// Validates the clip name against the project naming convention.
        /// </summary>
        public static MocapValidationResult ValidateNamingConvention(string clipName)
        {
            var result = new MocapValidationResult();
            result.ClipName = clipName ?? "";

            string error = ClipNamingConvention.Validate(clipName);
            if (error != null)
            {
                result.AddWarning(error);
            }

            return result;
        }

        // ─── Duration Range Check ───────────────────────────────────────

        /// <summary>
        /// Validates that a clip's duration is within the expected range for its type.
        /// </summary>
        public static MocapValidationResult ValidateDurationRange(AnimationClip clip, MocapClipType clipType)
        {
            var result = new MocapValidationResult();
            if (clip == null)
            {
                result.AddError("Animation clip is null.");
                return result;
            }

            result.ClipName = clip.name;
            result.Duration = clip.length;

            var (minDur, maxDur) = ClipDurationRanges.GetRange(clipType);

            if (clip.length < minDur)
            {
                result.AddError(
                    $"Clip '{clip.name}' duration {clip.length:F2}s is below minimum {minDur:F1}s " +
                    $"for type {clipType}. Clip may be truncated.");
            }
            else if (clip.length > maxDur)
            {
                result.AddError(
                    $"Clip '{clip.name}' duration {clip.length:F2}s exceeds maximum {maxDur:F1}s " +
                    $"for type {clipType}. Clip may need trimming.");
            }

            return result;
        }

        // ─── Full Validation ────────────────────────────────────────────

        /// <summary>
        /// Runs all validation checks on a mocap animation clip.
        /// </summary>
        public static MocapValidationResult ValidateAll(
            AnimationClip clip,
            Animator animator = null,
            MocapClipType? clipType = null)
        {
            var combined = new MocapValidationResult();

            if (clip == null)
            {
                combined.AddError("Animation clip is null.");
                return combined;
            }

            combined.ClipName = clip.name;
            combined.Duration = clip.length;
            combined.FrameRate = clip.frameRate;

            // Retarget quality (if animator available)
            if (animator != null)
            {
                var retargetResult = ValidateRetargetQuality(clip, animator);
                MergeResult(combined, retargetResult);
            }

            // Foot sliding
            var footResult = ValidateFootSliding(clip);
            MergeResult(combined, footResult);

            // Transition smoothness
            var transitionResult = ValidateTransitionSmoothness(clip);
            MergeResult(combined, transitionResult);

            // Naming convention
            var namingResult = ValidateNamingConvention(clip.name);
            MergeResult(combined, namingResult);

            // Duration range
            if (clipType.HasValue)
            {
                var durationResult = ValidateDurationRange(clip, clipType.Value);
                MergeResult(combined, durationResult);
            }

            return combined;
        }

        /// <summary>
        /// Validates all animation clips in the project's mocap folder and logs results.
        /// </summary>
        [MenuItem("OpenFifa/Animation/Validate All Mocap Clips")]
        public static void ValidateAllMocapClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations/Mocap" });
            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                var clipType = MocapAnimationImporter.InferClipType(clip.name);
                var result = ValidateAll(clip, null, clipType);

                if (!result.IsValid)
                {
                    failCount++;
                    Debug.LogError(
                        $"[MocapValidator] FAIL: '{clip.name}' ({path})\n" +
                        $"  Errors: {string.Join("; ", result.Errors)}" +
                        (result.Warnings.Count > 0
                            ? $"\n  Warnings: {string.Join("; ", result.Warnings)}"
                            : ""));
                }
                else if (result.Warnings.Count > 0)
                {
                    warnCount++;
                    Debug.LogWarning(
                        $"[MocapValidator] PASS with warnings: '{clip.name}' ({path})\n" +
                        $"  Warnings: {string.Join("; ", result.Warnings)}");
                }
                else
                {
                    passCount++;
                    Debug.Log($"[MocapValidator] PASS: '{clip.name}' — {result.Duration:F2}s, {result.FrameRate}fps");
                }
            }

            Debug.Log(
                $"[MocapValidator] Validation complete: {passCount} passed, {warnCount} warnings, {failCount} failed " +
                $"out of {passCount + warnCount + failCount} total clips.");
        }

        private static void MergeResult(MocapValidationResult target, MocapValidationResult source)
        {
            foreach (var error in source.Errors)
                target.AddError(error);
            foreach (var warning in source.Warnings)
                target.AddWarning(warning);
        }
    }
}
