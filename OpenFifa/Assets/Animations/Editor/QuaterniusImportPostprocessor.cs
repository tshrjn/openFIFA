using System;
using System.Collections.Generic;
using OpenFifa.Core;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// First-import settings for the CC0 Quaternius characters and animation libraries:
    /// Humanoid rig with an avatar created from each model, "Rig|" / "Armature|" prefixes stripped from clip names,
    /// *_Loop clips looping, root transform baked into the pose (root motion stays off: the sim owns position),
    /// and contact events on the library clips used as football actions.
    /// Runs only for files under the two Quaternius folders. It re-applies on every reimport, so change settings
    /// here rather than in the Inspector.
    /// </summary>
    public sealed class QuaterniusImportPostprocessor : AssetPostprocessor
    {
        public const string AnimationsRoot = "Assets/Animations/Quaternius/";
        public const string CharactersRoot = "Assets/Art/Characters/Quaternius/";

        /// <summary>
        /// Blender-built athlete bodies (tools/blender/athlete_body.py, MakeHuman CC0 base mesh, Unreal-style
        /// "game_engine" skeleton in T-pose). Same character import settings as <see cref="CharactersRoot"/>.
        /// </summary>
        public const string AthleteRoot = "Assets/Art/Characters/Athlete/";

        /// <summary>True for every model folder these postprocessors own.</summary>
        public static bool IsCharacterModel(string path) =>
            path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
            (path.StartsWith(AnimationsRoot, StringComparison.Ordinal) ||
             path.StartsWith(CharactersRoot, StringComparison.Ordinal) ||
             path.StartsWith(AthleteRoot, StringComparison.Ordinal));

        /// <summary>Library clips used as actions, with the action whose profile holds their contact time.</summary>
        private static readonly Dictionary<string, AnimActionKind> ActionClips = new Dictionary<string, AnimActionKind>
        {
            { "Slide_Start", AnimActionKind.SlideTackle },
            { "OverhandThrow", AnimActionKind.GoalkeeperThrow },
        };

        private bool IsOurs => IsCharacterModel(assetPath);

        private void OnPreprocessModel()
        {
            if (!IsOurs) return;
            var importer = (ModelImporter)assetImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = assetPath.StartsWith(AnimationsRoot, StringComparison.Ordinal);
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            // Quaternius exports in metres; keep 1:1 so the 1.8 m mannequin matches the sim capsule.
            importer.globalScale = 1f;
            importer.useFileScale = true;
        }

        /// <summary>
        /// Screen-relative heights where the athlete LODs switch (Athlete_LOD0/1/2 from tools/blender/athlete_body.py).
        /// LOD0 for close-ups, LOD1 at broadcast distance, LOD2 for far players; the last LOD never culls (a player
        /// must not vanish at the far touchline).
        /// </summary>
        public static readonly float[] AthleteLodHeights = { 0.22f, 0.05f, 0f };

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(AthleteRoot, StringComparison.Ordinal)) return;
            var group = root.GetComponentInChildren<LODGroup>();
            if (group == null) return;
            var lods = group.GetLODs();
            for (int i = 0; i < lods.Length && i < AthleteLodHeights.Length; i++)
                lods[i].screenRelativeTransitionHeight = AthleteLodHeights[i];
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        private void OnPreprocessAnimation()
        {
            if (!IsOurs) return;
            var importer = (ModelImporter)assetImporter;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            var result = new List<ModelImporterClipAnimation>(clips.Length);
            foreach (ModelImporterClipAnimation src in clips)
            {
                ModelImporterClipAnimation c = src;
                string shortName = StripPrefix(c.takeName);
                c.name = shortName;
                bool loop = shortName.EndsWith("_Loop", StringComparison.Ordinal);
                c.loopTime = loop;
                c.loopPose = loop;
                // Bake root into pose: the Animator never moves the player root.
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                // Root rotation "Based Upon: Original" turned 180 deg: the Quaternius takes are authored travelling along
                // -Z (Blender -Y forward), so without the offset every locomotion clip ran BACKWARDS relative to the player
                // root, and the generated action clips (+Z) flipped the body 180 deg on every kick. "Body Orientation" is
                // not used because it skews the jog/sprint travel 20-30 deg off the axis (the torso twists in those
                // takes). Verified by LocomotionClipMeasure.RuntimeFacingReport (planted-foot drift along -Z = travel +Z).
                c.keepOriginalOrientation = true;
                c.rotationOffset = OrientationOffsetDeg;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;

                if (ActionClips.TryGetValue(shortName, out AnimActionKind kind))
                {
                    ActionClipProfile p = ActionClipLibrary.Default(kind);
                    float length = Mathf.Max(1e-3f, (c.lastFrame - c.firstFrame) / SourceFrameRate);
                    c.events = new[]
                    {
                        new AnimationEvent
                        {
                            functionName = AnimStateNames.ContactEvent,
                            time = Mathf.Clamp01(p.ContactTime / length), // importer events use normalized time
                        },
                    };
                }
                result.Add(c);
            }

            importer.clipAnimations = result.ToArray();
        }

        /// <summary>Quaternius libraries are authored at 30 fps (all clip durations are multiples of 1/30 s).</summary>
        private const float SourceFrameRate = 30f;

        /// <summary>Root yaw offset (deg) applied to every Quaternius clip so it travels along +Z, the sim facing.</summary>
        public const float OrientationOffsetDeg = 180f;

        public static string StripPrefix(string takeName)
        {
            if (string.IsNullOrEmpty(takeName)) return takeName;
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }
    }
}
