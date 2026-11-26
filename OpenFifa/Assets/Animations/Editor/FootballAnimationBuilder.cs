using System;
using System.Collections.Generic;
using System.Linq;
using OpenFifa.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Builds the footballer animation set on first import:
    ///  1. Placeholder humanoid action clips (muscle-curve blockouts) for the actions the CC0 libraries lack
    ///     (pass, shot, cross, first touch, header, standing tackle, goalkeeper dive, jockey). Each clip keys its contact
    ///     pose at <see cref="ActionClipProfile.ContactTime"/> and fires <see cref="AnimStateNames.ContactEvent"/> there.
    ///  2. Assets/Animations/Controllers/Footballer.controller: locomotion blend tree (Quaternius idle/walk/jog/sprint +
    ///     jockey) and one state per action, with the parameter names PlayerAnimationDriver expects.
    /// Menu: OpenFifa/Animation/Build Footballer Animator. Batchmode: -executeMethod OpenFifa.Editor.FootballAnimationBuilder.BuildAll
    /// Clip poses are deltas on top of the Quaternius idle pose, so they stay plausible on any humanoid. They are
    /// blockouts to be replaced by mocap; tune the deltas in <see cref="Poses"/>.
    /// </summary>
    public static class FootballAnimationBuilder
    {
        public const string GeneratedDir = "Assets/Animations/Generated";
        public const string ControllerDir = "Assets/Animations/Controllers";
        public const string ControllerPath = ControllerDir + "/Footballer.controller";
        public const string Ual1Path = "Assets/Animations/Quaternius/UAL1/UAL1_Standard.fbx";
        public const string Ual2Path = "Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx";

        /// <summary>
        /// Natural speeds (m/s) used as blend-tree positions: the measured clip speeds in <see cref="LocomotionGaitTable"/>
        /// (PlayerAnimationDriver's <see cref="LocomotionGaitSolver"/> uses the same table to set the playback rate).
        /// </summary>
        public static readonly float WalkSpeed = LocomotionGaitTable.WalkSpeed, JogSpeed = LocomotionGaitTable.JogSpeed,
            SprintSpeed = LocomotionGaitTable.SprintSpeed, JockeySpeed = LocomotionGaitTable.JockeySpeed,
            BackpedalSpeed = LocomotionGaitTable.BackpedalSpeed;

        [MenuItem("OpenFootball/Animation/Build Footballer Animator")]
        public static void BuildAll()
        {
            EnsureFolder(GeneratedDir);
            EnsureFolder(ControllerDir);
            ReimportIfStale(Ual1Path);
            ReimportIfStale(Ual2Path);
            var neutral = NeutralPose.Sample();
            var clips = new Dictionary<string, AnimationClip>();
            foreach (ClipSpec spec in Poses.All())
                clips[spec.StateName] = WriteClip(spec, neutral);
            BuildController(clips);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FootballAnimationBuilder] Built {clips.Count} generated clips and {ControllerPath}");
            CheckMeasuredSpeeds(clips.TryGetValue("Jockey", out AnimationClip jk) ? jk : null);
        }

        /// <summary>
        /// Reimports a Quaternius FBX whose stored clip settings predate the current postprocessor (e.g. root rotation
        /// still "Original"), so the controller is never built on clips that face the wrong way.
        /// </summary>
        private static void ReimportIfStale(string fbxPath)
        {
            if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter importer)) return;
            bool stale = importer.clipAnimations.Any(c => !c.keepOriginalOrientation || !c.lockRootRotation ||
                Mathf.Abs(Mathf.DeltaAngle(c.rotationOffset, QuaterniusImportPostprocessor.OrientationOffsetDeg)) > 0.5f);
            if (!stale) return;
            Debug.Log($"[FootballAnimationBuilder] Reimporting {fbxPath} (clip root settings are stale)");
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Warns when a clip's measured natural speed drifts from LocomotionGaitTable by more than 12%.</summary>
        private static void CheckMeasuredSpeeds(AnimationClip jockey)
        {
            void Check(AnimationClip clip, float table, bool lateral)
            {
                if (clip == null) return;
                float measured = Mathf.Abs(LocomotionClipMeasure.Measure(clip, lateral).StanceSpeed);
                string msg = $"[FootballAnimationBuilder] {clip.name}: measured {measured:F2} m/s, table {table:F2} m/s";
                if (Mathf.Abs(measured - table) > 0.12f * table) Debug.LogWarning(msg + " -> update LocomotionGaitTable");
                else Debug.Log(msg);
            }
            Check(LibraryClip(Ual1Path, "Walk_Loop"), WalkSpeed, false);
            Check(LibraryClip(Ual1Path, "Jog_Fwd_Loop"), JogSpeed, false);
            Check(LibraryClip(Ual1Path, "Sprint_Loop"), SprintSpeed, false);
            Check(jockey, JockeySpeed, true);
            string facing = LocomotionClipMeasure.RuntimeFacingReport();
            if (facing.Contains("BACKWARDS")) Debug.LogError("[FootballAnimationBuilder] Locomotion runs backwards:\n" + facing);
            else Debug.Log("[FootballAnimationBuilder] Runtime facing:\n" + facing);
        }

        // ------------------------------------------------------------------ clip writing

        private static AnimationClip WriteClip(ClipSpec spec, NeutralPose neutral)
        {
            string path = $"{GeneratedDir}/{spec.ClipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = spec.ClipName, frameRate = 30f };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();

            // Every muscle that any key touches, plus root channels, is written as a curve over all keys.
            var channels = new HashSet<string>(spec.Keys.SelectMany(k => k.Deltas.Keys));
            foreach (string ch in channels)
            {
                var curve = new AnimationCurve();
                foreach (PoseKey k in spec.Keys)
                {
                    float baseValue = neutral.Get(ch);
                    float delta = k.Deltas.TryGetValue(ch, out float d) ? d : 0f;
                    float v = ch.StartsWith("Root", StringComparison.Ordinal) ? baseValue + delta : Mathf.Clamp(baseValue + delta, -1f, 1f);
                    curve.AddKey(new Keyframe(k.Time, v));
                }
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), ch), curve);
            }

            // Root channels not keyed by the spec hold the neutral body position/rotation (keeps feet on the ground).
            foreach (string q in new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" })
            {
                if (channels.Contains(q)) continue;
                var c = AnimationCurve.Constant(0f, spec.Length, neutral.Get(q));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), q), c);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loop;
            settings.loopBlend = spec.Loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.stopTime = spec.Length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AnimationUtility.SetAnimationEvents(clip, spec.ContactTime > 0f
                ? new[] { new AnimationEvent { time = spec.ContactTime, functionName = AnimStateNames.ContactEvent } }
                : Array.Empty<AnimationEvent>());

            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ------------------------------------------------------------------ controller

        private static void BuildController(Dictionary<string, AnimationClip> generated)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ctrl.AddParameter(AnimStateNames.ParamSpeed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(AnimStateNames.ParamForward, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(AnimStateNames.ParamStrafe, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(AnimStateNames.ParamTurnRate, AnimatorControllerParameterType.Float);
            AddFloat(ctrl, AnimStateNames.ParamLocoRate, 1f);
            AddFloat(ctrl, AnimStateNames.ParamActionRate, 1f);
            ctrl.AddParameter(AnimStateNames.ParamMirror, AnimatorControllerParameterType.Bool);

            AnimatorControllerLayer[] layers = ctrl.layers;
            layers[0].iKPass = true; // PlayerAnimationDriver applies contact/plant IK in OnAnimatorIK
            ctrl.layers = layers;
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // Locomotion
            AnimatorState loco = ctrl.CreateBlendTreeInController(AnimStateNames.Locomotion, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = AnimStateNames.ParamStrafe;
            tree.blendParameterY = AnimStateNames.ParamForward;
            tree.useAutomaticThresholds = false;
            AnimationClip idle = LibraryClip(Ual1Path, "Idle_Loop");
            AnimationClip walk = LibraryClip(Ual1Path, "Walk_Loop");
            AnimationClip jog = LibraryClip(Ual1Path, "Jog_Fwd_Loop");
            AnimationClip sprint = LibraryClip(Ual1Path, "Sprint_Loop");
            generated.TryGetValue("Jockey", out AnimationClip jockey);
            AddChild(tree, idle, Vector2.zero, 1f, false);
            AddChild(tree, walk, new Vector2(0f, WalkSpeed), 1f, false);
            AddChild(tree, jog, new Vector2(0f, JogSpeed), 1f, false);
            AddChild(tree, sprint, new Vector2(0f, SprintSpeed), 1f, false);
            AddChild(tree, walk, new Vector2(0f, -BackpedalSpeed), -1f, false); // backpedal: walk reversed
            AddChild(tree, jockey, new Vector2(JockeySpeed, 0f), 1f, false);
            AddChild(tree, jockey, new Vector2(-JockeySpeed, 0f), 1f, true);
            loco.speedParameterActive = true;
            loco.speedParameter = AnimStateNames.ParamLocoRate;
            sm.defaultState = loco;

            // Actions
            AnimatorState getUp = AddAction(sm, AnimStateNames.GetUp, LibraryClip(Ual2Path, "LayToIdle"), loco, 0.9f, false);
            AddAction(sm, AnimStateNames.Pass, Gen(generated, AnimStateNames.Pass), loco, 0.85f, true);
            AddAction(sm, AnimStateNames.Shot, Gen(generated, AnimStateNames.Shot), loco, 0.85f, true);
            AddAction(sm, AnimStateNames.Cross, Gen(generated, AnimStateNames.Cross), loco, 0.85f, true);
            AddAction(sm, AnimStateNames.FirstTouch, Gen(generated, AnimStateNames.FirstTouch), loco, 0.8f, true);
            AddAction(sm, AnimStateNames.Header, Gen(generated, AnimStateNames.Header), loco, 0.9f, true);
            AddAction(sm, AnimStateNames.StandingTackle, Gen(generated, AnimStateNames.StandingTackle), loco, 0.85f, true);
            AnimatorState slide = AddAction(sm, AnimStateNames.SlideTackle, LibraryClip(Ual2Path, "Slide_Start"), null, 0f, true);
            AnimatorState slideExit = sm.AddState("TackleSlideExit");
            slideExit.motion = LibraryClip(Ual2Path, "Slide_Exit");
            Exit(slide, slideExit, 0.95f, 0.05f);
            Exit(slideExit, loco, 0.9f, 0.15f);
            AnimatorState dive = AddAction(sm, AnimStateNames.GoalkeeperDive, Gen(generated, AnimStateNames.GoalkeeperDive), null, 0f, true);
            Exit(dive, getUp, 0.98f, 0.2f);
            AnimatorState fall = AddAction(sm, AnimStateNames.Fall, LibraryClip(Ual2Path, "Hit_Knockback"), null, 0f, true);
            Exit(fall, getUp, 0.95f, 0.15f);
            AnimatorState celebrate = AddAction(sm, AnimStateNames.Celebrate, LibraryClip(Ual1Path, "Dance_Loop"), null, 0f, true);
            Exit(celebrate, loco, 2.5f, 0.3f); // Dance_Loop is 1 s: play it 2.5 times
            AddAction(sm, AnimStateNames.GoalkeeperThrow, LibraryClip(Ual2Path, "OverhandThrow"), loco, 0.9f, true);

            EditorUtility.SetDirty(ctrl);
        }

        private static void AddFloat(AnimatorController ctrl, string name, float defaultValue)
        {
            ctrl.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue,
            });
        }

        private static void AddChild(BlendTree tree, Motion motion, Vector2 position, float timeScale, bool mirror)
        {
            if (motion == null) return;
            tree.AddChild(motion, position);
            ChildMotion[] children = tree.children;
            children[children.Length - 1].timeScale = timeScale;
            children[children.Length - 1].mirror = mirror;
            tree.children = children;
        }

        private static AnimatorState AddAction(AnimatorStateMachine sm, string name, Motion motion, AnimatorState exitTo,
            float exitTime, bool rateAndMirror)
        {
            AnimatorState s = sm.AddState(name);
            s.motion = motion;
            s.writeDefaultValues = true;
            if (rateAndMirror)
            {
                s.speedParameterActive = true;
                s.speedParameter = AnimStateNames.ParamActionRate;
                s.mirrorParameterActive = true;
                s.mirrorParameter = AnimStateNames.ParamMirror;
            }
            if (exitTo != null) Exit(s, exitTo, exitTime, 0.15f);
            if (motion == null) Debug.LogWarning($"[FootballAnimationBuilder] State '{name}' has no clip yet");
            return s;
        }

        private static void Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
        }

        private static AnimationClip Gen(Dictionary<string, AnimationClip> generated, string state) =>
            generated.TryGetValue(state, out AnimationClip c) ? c : null;

        private static AnimationClip LibraryClip(string fbxPath, string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                                     QuaterniusImportPostprocessor.StripPrefix(c.name) == clipName);
            if (clip == null) Debug.LogWarning($"[FootballAnimationBuilder] Clip '{clipName}' not found in {fbxPath}");
            return clip;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ neutral pose

        /// <summary>Muscle values of the Quaternius idle pose; generated poses are deltas on top of it.</summary>
        private sealed class NeutralPose
        {
            private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

            public float Get(string channel) => _values.TryGetValue(channel, out float v) ? v : Default(channel);

            private static float Default(string channel)
            {
                switch (channel)
                {
                    case "RootT.y": return 1f;      // normalized body height of a standing humanoid
                    case "RootQ.w": return 1f;
                    default: return 0f;
                }
            }

            public static NeutralPose Sample()
            {
                var pose = new NeutralPose();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Ual2Path);
                AnimationClip idle = LibraryClip(Ual1Path, "Idle_Loop");
                if (model == null || idle == null)
                {
                    Debug.LogWarning("[FootballAnimationBuilder] Could not sample the idle pose; using zero muscles.");
                    return pose;
                }

                GameObject go = UnityEngine.Object.Instantiate(model);
                go.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    {
                        Debug.LogWarning("[FootballAnimationBuilder] UAL2 model has no humanoid avatar yet; import it as Humanoid first.");
                        return pose;
                    }
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    idle.SampleAnimation(go, 0f);
                    var handler = new HumanPoseHandler(animator.avatar, go.transform);
                    var hp = new HumanPose();
                    handler.GetHumanPose(ref hp);
                    for (int i = 0; i < hp.muscles.Length && i < HumanTrait.MuscleCount; i++)
                        pose._values[HumanTrait.MuscleName[i]] = hp.muscles[i];
                    float scale = animator.humanScale > 1e-4f ? animator.humanScale : 1f;
                    float bodyY = hp.bodyPosition.y / scale;
                    if (bodyY > 0.6f && bodyY < 1.4f) pose._values["RootT.y"] = bodyY;
                    handler.Dispose();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return pose;
            }
        }

        // ------------------------------------------------------------------ pose specs

        private sealed class PoseKey
        {
            public readonly float Time;
            public readonly Dictionary<string, float> Deltas;
            public PoseKey(float time, Dictionary<string, float> deltas) { Time = time; Deltas = deltas; }
        }

        private sealed class ClipSpec
        {
            public string StateName;
            public string ClipName;
            public float Length;
            public float ContactTime;
            public bool Loop;
            public List<PoseKey> Keys = new List<PoseKey>();
        }

        /// <summary>
        /// Blockout key poses, right-side versions (left uses the Mirror parameter). Values are muscle deltas in [-1,1]
        /// from the idle pose. Unity muscle signs: Upper Leg Front-Back + = leg forward; Lower Leg Stretch - = knee bent;
        /// Spine/Chest Front-Back + = lean forward; Arm Down-Up + = arm up; RootT is body offset in normalized units.
        /// </summary>
        private static class Poses
        {
            private const string RULfb = "Right Upper Leg Front-Back", RULio = "Right Upper Leg In-Out", RULtw = "Right Upper Leg Twist In-Out";
            private const string RLL = "Right Lower Leg Stretch", RFoot = "Right Foot Up-Down";
            private const string LULfb = "Left Upper Leg Front-Back", LULio = "Left Upper Leg In-Out", LLL = "Left Lower Leg Stretch";
            private const string SpFB = "Spine Front-Back", SpLR = "Spine Left-Right", SpTw = "Spine Twist Left-Right";
            private const string ChFB = "Chest Front-Back", Nod = "Head Nod Down-Up";
            private const string LArmDU = "Left Arm Down-Up", LArmFB = "Left Arm Front-Back", RArmDU = "Right Arm Down-Up", RArmFB = "Right Arm Front-Back";

            private static Dictionary<string, float> P(params object[] kv)
            {
                var d = new Dictionary<string, float>();
                for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = Convert.ToSingle(kv[i + 1]);
                return d;
            }

            private static ClipSpec Spec(string state, string clip, AnimActionKind kind, bool loop = false, float length = 0f)
            {
                ActionClipProfile p = ActionClipLibrary.Default(kind);
                return new ClipSpec
                {
                    StateName = state,
                    ClipName = clip,
                    Length = length > 0f ? length : p.ClipLength,
                    ContactTime = kind == AnimActionKind.None ? 0f : p.ContactTime,
                    Loop = loop,
                };
            }

            private static ClipSpec Kick(string state, string clip, AnimActionKind kind, float swing, float follow, float open, float lean)
            {
                ClipSpec s = Spec(state, clip, kind);
                float c = s.ContactTime, len = s.Length;
                var rest = P(RULfb, 0, RLL, 0);
                s.Keys.Add(new PoseKey(0f, rest));
                s.Keys.Add(new PoseKey(c * 0.55f, P(RULfb, -0.55f * swing, RLL, -0.9f * swing, RULtw, open, LLL, -0.15f,
                    SpFB, 0.1f, SpTw, -0.15f * swing, LArmDU, 0.35f, LArmFB, 0.3f, RArmDU, 0.2f, RArmFB, -0.2f, "RootT.y", -0.03f)));
                s.Keys.Add(new PoseKey(c, P(RULfb, 0.35f * swing, RLL, -0.05f, RULtw, open, RULio, -0.1f, RFoot, -0.3f * swing,
                    LLL, -0.25f, SpFB, 0.15f + lean, SpTw, 0.1f, Nod, -0.35f, LArmDU, 0.45f, LArmFB, 0.1f, RArmDU, 0.25f, "RootT.y", -0.05f)));
                s.Keys.Add(new PoseKey(c + (len - c) * 0.45f, P(RULfb, 0.35f * swing + follow, RLL, -0.1f, RULtw, open * 0.5f,
                    LLL, -0.15f, SpFB, 0.05f, SpTw, 0.2f, Nod, -0.15f, LArmDU, 0.25f, RArmDU, 0.15f, "RootT.y", -0.02f)));
                s.Keys.Add(new PoseKey(len, rest));
                return s;
            }

            public static IEnumerable<ClipSpec> All()
            {
                yield return Kick(AnimStateNames.Pass, "Gen_Pass_R", AnimActionKind.Pass, swing: 0.7f, follow: 0.15f, open: 0.55f, lean: 0.05f);
                yield return Kick(AnimStateNames.Shot, "Gen_Shot_R", AnimActionKind.Shot, swing: 1.0f, follow: 0.45f, open: 0.05f, lean: 0.1f);
                yield return Kick(AnimStateNames.Cross, "Gen_Cross_R", AnimActionKind.Cross, swing: 0.95f, follow: 0.35f, open: 0.35f, lean: -0.1f);
                yield return Kick(AnimStateNames.FirstTouch, "Gen_FirstTouch_R", AnimActionKind.FirstTouch, swing: 0.35f, follow: 0.0f, open: 0.5f, lean: 0.1f);

                // Header: crouch, jump, arch back, snap forward through the ball, land.
                ClipSpec h = Spec(AnimStateNames.Header, "Gen_Header", AnimActionKind.Header);
                float hc = h.ContactTime;
                h.Keys.Add(new PoseKey(0f, P("RootT.y", 0f, LLL, 0f, RLL, 0f, LULfb, 0f, RULfb, 0f)));
                h.Keys.Add(new PoseKey(hc * 0.4f, P("RootT.y", -0.12f, LLL, -0.6f, RLL, -0.6f, LULfb, 0.35f, RULfb, 0.35f, SpFB, 0.2f, LArmDU, -0.1f, RArmDU, -0.1f)));
                h.Keys.Add(new PoseKey(hc * 0.75f, P("RootT.y", 0.25f, LLL, 0f, RLL, -0.2f, RULfb, 0.2f, SpFB, -0.35f, ChFB, -0.3f, Nod, 0.3f, LArmDU, 0.5f, RArmDU, 0.5f)));
                h.Keys.Add(new PoseKey(hc, P("RootT.y", 0.3f, LLL, -0.1f, RLL, -0.3f, RULfb, 0.3f, SpFB, 0.3f, ChFB, 0.25f, Nod, -0.4f, LArmDU, 0.3f, RArmDU, 0.3f)));
                h.Keys.Add(new PoseKey(hc + 0.3f, P("RootT.y", -0.1f, LLL, -0.5f, RLL, -0.5f, LULfb, 0.3f, RULfb, 0.3f, SpFB, 0.15f)));
                h.Keys.Add(new PoseKey(h.Length, P("RootT.y", 0f, LLL, 0f, RLL, 0f, LULfb, 0f, RULfb, 0f)));
                yield return h;

                // Standing tackle: lunge and poke the ball with the right foot, support knee bent.
                ClipSpec t = Spec(AnimStateNames.StandingTackle, "Gen_TackleStand_R", AnimActionKind.StandingTackle);
                float tc = t.ContactTime;
                t.Keys.Add(new PoseKey(0f, P(RULfb, 0f, LLL, 0f, RLL, 0f, "RootT.y", 0f)));
                t.Keys.Add(new PoseKey(tc * 0.5f, P(RULfb, 0.2f, RLL, -0.5f, LLL, -0.35f, SpFB, 0.25f, "RootT.y", -0.08f, LArmDU, 0.3f, RArmDU, 0.3f)));
                t.Keys.Add(new PoseKey(tc, P(RULfb, 0.75f, RLL, -0.05f, RULtw, 0.3f, RFoot, 0.2f, LLL, -0.6f, LULfb, -0.1f, SpFB, 0.35f, "RootT.y", -0.18f, LArmDU, 0.45f, RArmDU, 0.35f, Nod, -0.3f)));
                t.Keys.Add(new PoseKey(tc + 0.25f, P(RULfb, 0.5f, RLL, -0.2f, LLL, -0.45f, SpFB, 0.25f, "RootT.y", -0.12f)));
                t.Keys.Add(new PoseKey(t.Length, P(RULfb, 0f, LLL, 0f, RLL, 0f, "RootT.y", 0f)));
                yield return t;

                // Goalkeeper dive to the right: load, launch sideways with arms up, land on the side.
                ClipSpec g = Spec(AnimStateNames.GoalkeeperDive, "Gen_GkDive_R", AnimActionKind.GoalkeeperDive);
                float gc = g.ContactTime;
                g.Keys.Add(new PoseKey(0f, P("RootT.x", 0f, "RootT.y", 0f, SpLR, 0f, LArmDU, 0f, RArmDU, 0f, LLL, 0f, RLL, 0f)));
                g.Keys.Add(new PoseKey(gc * 0.35f, P("RootT.x", 0.05f, "RootT.y", -0.15f, LLL, -0.6f, RLL, -0.5f, LULfb, 0.3f, RULfb, 0.3f, SpFB, 0.2f, LArmDU, 0.2f, RArmDU, 0.2f)));
                g.Keys.Add(new PoseKey(gc, P("RootT.x", 0.75f, "RootT.y", -0.25f, SpLR, 0.6f, LArmDU, 1f, RArmDU, 1f, LArmFB, 0.3f, RArmFB, 0.3f, LLL, 0f, RLL, -0.2f, LULio, 0.3f, Nod, 0.2f)));
                g.Keys.Add(new PoseKey(gc + 0.4f, P("RootT.x", 1.0f, "RootT.y", -0.75f, SpLR, 0.5f, LArmDU, 0.8f, RArmDU, 0.8f, LLL, -0.2f, RLL, -0.4f)));
                g.Keys.Add(new PoseKey(g.Length, P("RootT.x", 1.0f, "RootT.y", -0.8f, SpLR, 0.3f, LArmDU, 0.4f, RArmDU, 0.5f, LLL, -0.4f, RLL, -0.5f)));
                yield return g;

                // Jockey: low side-shuffle to the right (mirrored in the blend tree for the left), 0.7 s loop. Unlike the
                // first blockout (legs splayed in place, 0.4 m/s of foot travel), the planted foot sweeps toward the body's
                // centre while the other leg steps out, so the shuffle covers ground: LocomotionClipMeasure reads its
                // natural lateral speed into LocomotionGaitTable.JockeySpeed.
                ClipSpec j = Spec("Jockey", "Gen_Jockey_R_Loop", AnimActionKind.None, loop: true, length: 0.7f);
                Dictionary<string, float> J(float rio, float lio, float rLift, float lLift, float y) => P(
                    "RootT.y", y, RULio, rio, LULio, lio,
                    RLL, -0.4f - 0.3f * rLift, LLL, -0.4f - 0.3f * lLift,
                    RULfb, 0.25f + 0.15f * rLift, LULfb, 0.25f + 0.15f * lLift,
                    SpFB, 0.2f, SpLR, 0.04f * (rio - lio), LArmDU, 0.2f, RArmDU, 0.2f, LArmFB, 0.15f, RArmFB, 0.15f);
                j.Keys.Add(new PoseKey(0f, J(0.45f, 0.35f, 0f, 0f, -0.1f)));      // wide: right foot just landed
                j.Keys.Add(new PoseKey(0.175f, J(0.25f, 0.12f, 0f, 1f, -0.07f))); // left foot lifts and closes in
                j.Keys.Add(new PoseKey(0.35f, J(0.02f, 0.02f, 0f, 0f, -0.06f)));  // feet together, left lands
                j.Keys.Add(new PoseKey(0.525f, J(0.28f, 0.16f, 1f, 0f, -0.08f))); // right foot lifts and steps out
                j.Keys.Add(new PoseKey(0.7f, J(0.45f, 0.35f, 0f, 0f, -0.1f)));
                yield return j;
            }
        }
    }
}
