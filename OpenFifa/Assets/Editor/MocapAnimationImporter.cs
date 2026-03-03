using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// AssetPostprocessor for mocap animation FBX/anim files under Assets/Animations/Mocap/.
    /// Auto-configures humanoid retargeting, compression, loop settings per clip type,
    /// and inserts animation events for contact frames.
    /// </summary>
    public class MocapAnimationImporter : AssetPostprocessor
    {
        /// <summary>Path prefix that triggers mocap animation import processing.</summary>
        public const string MocapAnimationPath = "Assets/Animations/Mocap/";

        /// <summary>Humanoid avatar for retargeting all mocap clips.</summary>
        public const string AvatarRigType = "Humanoid";

        /// <summary>Default keyframe reduction error for compression.</summary>
        public const float DefaultRotationError = 0.5f;

        /// <summary>Default position error for compression.</summary>
        public const float DefaultPositionError = 0.001f;

        /// <summary>Default scale error for compression.</summary>
        public const float DefaultScaleError = 0.001f;

        private bool IsMocapAsset => assetPath.StartsWith(MocapAnimationPath) &&
                                      (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase));

        // ─── Pre-Process: Configure Rig & Animation Settings ────────────

        /// <summary>
        /// Called before the model is imported. Configures humanoid rig for animation retargeting.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!IsMocapAsset) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Configure humanoid rig for retargeting
            importer.animationType = ModelImporterAnimationType.Human;

            // Do not import mesh for animation-only files
            importer.importBlendShapes = false;
            importer.importVisibility = false;

            // Enable animation import
            importer.importAnimation = true;

            // Compression — use optimal compression for file size
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            // Resample curves at 60fps for smooth playback
            importer.resampleCurves = true;

            Debug.Log($"[MocapImporter] Configured humanoid rig for mocap: {assetPath}");
        }

        /// <summary>
        /// Called before animation clips are processed. Configures loop, root motion,
        /// and compression per clip based on naming convention.
        /// </summary>
        private void OnPreprocessAnimation()
        {
            if (!IsMocapAsset) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            var clipAnimations = importer.defaultClipAnimations;
            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = importer.clipAnimations;
            }

            if (clipAnimations == null || clipAnimations.Length == 0) return;

            var config = new AnimationClipConfig();
            var modifiedClips = new List<ModelImporterClipAnimation>();

            foreach (var clip in clipAnimations)
            {
                var modified = clip;
                var clipType = InferClipType(clip.name);

                // Configure loop based on clip type
                modified.loopTime = IsLoopingClipType(clipType);
                modified.loopPose = IsLoopingClipType(clipType);

                // Root motion: disabled for locomotion (script-driven), enabled only for tackle slide
                modified.lockRootRotation = true;
                modified.lockRootHeightY = true;
                modified.lockRootPositionXZ = true;

                if (clipType == MocapClipType.Tackle)
                {
                    modified.lockRootPositionXZ = false; // Allow forward slide
                }

                // Insert animation events for contact frames
                var events = GetEventsForClipType(clipType, clip.name);
                if (events.Length > 0)
                {
                    modified.events = events;
                }

                modifiedClips.Add(modified);
            }

            importer.clipAnimations = modifiedClips.ToArray();

            Debug.Log($"[MocapImporter] Configured {modifiedClips.Count} clip(s) in: {assetPath}");
        }

        // ─── Post-Process: Validate & Log ───────────────────────────────

        /// <summary>
        /// Called after animation clips are imported. Logs validation summary.
        /// </summary>
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsMocapAsset) return;

            float duration = clip.length;
            float fps = clip.frameRate;
            bool isLooping = clip.isLooping;

            var clipType = InferClipType(clip.name);
            var (minDur, maxDur) = ClipDurationRanges.GetRange(clipType);

            if (duration < minDur || duration > maxDur)
            {
                Debug.LogWarning(
                    $"[MocapImporter] Clip '{clip.name}' duration {duration:F2}s is outside expected range " +
                    $"[{minDur:F1}s, {maxDur:F1}s] for type {clipType}.");
            }

            Debug.Log(
                $"[MocapImporter] Imported clip '{clip.name}': {duration:F2}s, {fps}fps, " +
                $"loop={isLooping}, type={clipType}");
        }

        // ─── Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Infers the clip type from the clip or file name.
        /// </summary>
        public static MocapClipType InferClipType(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return MocapClipType.Idle;

            string lower = clipName.ToLowerInvariant();

            if (lower.Contains("idle") || lower.Contains("stance"))
                return MocapClipType.Idle;
            if (lower.Contains("sprint"))
                return MocapClipType.Locomotion;
            if (lower.Contains("run") || lower.Contains("jog") || lower.Contains("walk"))
                return MocapClipType.Locomotion;
            if (lower.Contains("bicycle") || lower.Contains("overhead"))
                return MocapClipType.BicycleKick;
            if (lower.Contains("kick") || lower.Contains("shot") || lower.Contains("pass"))
                return MocapClipType.Kick;
            if (lower.Contains("tackle") || lower.Contains("slide"))
                return MocapClipType.Tackle;
            if (lower.Contains("header") || lower.Contains("head"))
                return MocapClipType.Header;
            if (lower.Contains("dribble") || lower.Contains("touch") && lower.Contains("ball"))
                return MocapClipType.Dribble;
            if (lower.Contains("first") && lower.Contains("touch"))
                return MocapClipType.FirstTouch;
            if (lower.Contains("gk") || lower.Contains("goalkeeper") || lower.Contains("dive") ||
                lower.Contains("catch") || lower.Contains("punch") || lower.Contains("distribute"))
                return MocapClipType.GoalkeeperAction;
            if (lower.Contains("celebrat") || lower.Contains("victory") || lower.Contains("dance"))
                return MocapClipType.Celebration;

            return MocapClipType.Idle;
        }

        /// <summary>
        /// Returns true if the given clip type should be configured as looping.
        /// </summary>
        public static bool IsLoopingClipType(MocapClipType clipType)
        {
            switch (clipType)
            {
                case MocapClipType.Idle:
                case MocapClipType.Locomotion:
                case MocapClipType.Dribble:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Generates animation events for the given clip type.
        /// </summary>
        private AnimationEvent[] GetEventsForClipType(MocapClipType clipType, string clipName)
        {
            var events = new List<AnimationEvent>();

            switch (clipType)
            {
                case MocapClipType.Kick:
                    events.Add(CreateEvent("OnKickContact", 0.45f, 1f));
                    events.Add(CreateEvent("OnKickFollowThrough", 0.7f, 0f));
                    break;

                case MocapClipType.Tackle:
                    events.Add(CreateEvent("OnTackleImpact", 0.35f, 0.8f));
                    break;

                case MocapClipType.Header:
                    events.Add(CreateEvent("OnHeaderContact", 0.5f, 1f));
                    break;

                case MocapClipType.BicycleKick:
                    events.Add(CreateEvent("OnKickContact", 0.55f, 1.2f));
                    break;

                case MocapClipType.Locomotion:
                    events.Add(CreateEvent("OnLeftFootPlant", 0.0f, 0f));
                    events.Add(CreateEvent("OnRightFootPlant", 0.5f, 0f));
                    break;
            }

            return events.ToArray();
        }

        private AnimationEvent CreateEvent(string functionName, float normalizedTime, float floatParameter)
        {
            return new AnimationEvent
            {
                functionName = functionName,
                time = normalizedTime,
                floatParameter = floatParameter,
                messageOptions = SendMessageOptions.DontRequireReceiver
            };
        }

        // ─── MenuItem: Generate Animator Controller ─────────────────────

        /// <summary>
        /// Generates an AnimatorController with blend trees for locomotion
        /// and states for all mocap clips, using the configured transitions.
        /// </summary>
        [MenuItem("OpenFifa/Animation/Generate Mocap Animator Controller")]
        public static void GenerateMocapAnimatorController()
        {
            string controllerPath = "Assets/Animations/Controllers/MocapPlayerController.controller";

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(controllerPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                Debug.LogError("[MocapImporter] Failed to create AnimatorController.");
                return;
            }

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsKicking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsTackling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCelebrating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHeading", AnimatorControllerParameterType.Bool);

            // The base layer [0] is auto-created
            var rootStateMachine = controller.layers[0].stateMachine;

            // Add blend tree for locomotion
            var blendTree = new UnityEditor.Animations.BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = UnityEditor.Animations.BlendTreeType.Simple1D
            };

            // Add idle, run, sprint motions (placeholders — clips assigned when imported)
            blendTree.AddChild(null, 0f);    // Idle at Speed=0
            blendTree.AddChild(null, 0.5f);  // Run at Speed=0.5
            blendTree.AddChild(null, 1f);    // Sprint at Speed=1

            var locomotionState = rootStateMachine.AddState("Locomotion");
            locomotionState.motion = blendTree;
            rootStateMachine.defaultState = locomotionState;

            // Add action states
            var kickState = rootStateMachine.AddState("Kick");
            var tackleState = rootStateMachine.AddState("Tackle");
            var celebrateState = rootStateMachine.AddState("Celebrate");
            var headerState = rootStateMachine.AddState("Header");

            // Add transitions from Locomotion to actions
            var toKick = locomotionState.AddTransition(kickState);
            toKick.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "IsKicking");
            toKick.duration = 0.1f;
            toKick.hasExitTime = false;

            var toTackle = locomotionState.AddTransition(tackleState);
            toTackle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "IsTackling");
            toTackle.duration = 0.08f;
            toTackle.hasExitTime = false;

            var toCelebrate = locomotionState.AddTransition(celebrateState);
            toCelebrate.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "IsCelebrating");
            toCelebrate.duration = 0.15f;
            toCelebrate.hasExitTime = false;

            var toHeader = locomotionState.AddTransition(headerState);
            toHeader.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "IsHeading");
            toHeader.duration = 0.1f;
            toHeader.hasExitTime = false;

            // Add transitions back to Locomotion
            var fromKick = kickState.AddTransition(locomotionState);
            fromKick.hasExitTime = true;
            fromKick.exitTime = 0.85f;
            fromKick.duration = 0.2f;

            var fromTackle = tackleState.AddTransition(locomotionState);
            fromTackle.hasExitTime = true;
            fromTackle.exitTime = 0.9f;
            fromTackle.duration = 0.25f;

            var fromCelebrate = celebrateState.AddTransition(locomotionState);
            fromCelebrate.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "IsCelebrating");
            fromCelebrate.duration = 0.3f;
            fromCelebrate.hasExitTime = false;

            var fromHeader = headerState.AddTransition(locomotionState);
            fromHeader.hasExitTime = true;
            fromHeader.exitTime = 0.85f;
            fromHeader.duration = 0.2f;

            // Save
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MocapImporter] Generated Animator Controller at: {controllerPath}");
        }

        /// <summary>
        /// Reimports all mocap animation files.
        /// </summary>
        [MenuItem("OpenFifa/Animation/Reimport All Mocap Animations")]
        public static void ReimportAllMocapAnimations()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations/Mocap" });
            int count = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }

            // Also reimport FBX files
            guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations/Mocap" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }

            Debug.Log($"[MocapImporter] Reimported {count} mocap animation asset(s).");
        }
    }
}
