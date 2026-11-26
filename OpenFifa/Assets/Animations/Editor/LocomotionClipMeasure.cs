using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenFifa.Core;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Measures the locomotion clips on the player model (UAL2 mannequin, the avatar the clips are retargeted onto):
    /// ground speed implied by the feet, cycle length, right-foot strike phase and trunk lean. The clips are in place
    /// (root baked into pose), so the root velocity is ~0; the ground speed is read from the stance foot instead: a
    /// planted foot moves backwards relative to the body at exactly the speed the body moves over the ground.
    /// The numbers feed <see cref="LocomotionGaitTable"/> (keep them in sync after a clip change).
    /// Menu: OpenFootball/Animation/Measure Locomotion Clips. Batch: -executeMethod OpenFifa.Editor.LocomotionClipMeasure.MeasureAll
    /// </summary>
    public static class LocomotionClipMeasure
    {
        public struct Result
        {
            public string Clip;
            public float Length;
            public float AverageSpeed;      // Unity's root-motion average (≈0 for in-place clips)
            public float StanceSpeed;       // m/s, from the planted foot
            public float StepLength;        // m per step (half cycle)
            public float RightStrikePhase;  // normalised time at which the right foot lands
            public float LeanDeg;           // mean trunk pitch forward (hips -> head) from vertical
        }

        public static readonly string[] Clips =
        {
            "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Walk_Formal_Loop", "Crouch_Fwd_Loop", "Idle_Loop",
        };

        [MenuItem("OpenFootball/Animation/Measure Locomotion Clips")]
        public static void MeasureAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("clip, length_s, avgSpeed, stanceSpeed_mps, stepLength_m, rightStrikePhase, leanDeg");
            foreach (string name in Clips)
            {
                AnimationClip clip = Find(FootballAnimationBuilder.Ual1Path, name) ?? Find(FootballAnimationBuilder.Ual2Path, name);
                if (clip == null) { sb.AppendLine($"{name}, missing"); continue; }
                Result r = Measure(clip);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}, {1:F3}, {2:F3}, {3:F3}, {4:F3}, {5:F3}, {6:F1}",
                    r.Clip, r.Length, r.AverageSpeed, r.StanceSpeed, r.StepLength, r.RightStrikePhase, r.LeanDeg));
            }
            var jockey = AssetDatabase.LoadAssetAtPath<AnimationClip>(FootballAnimationBuilder.GeneratedDir + "/Gen_Jockey_R_Loop.anim");
            if (jockey != null)
            {
                Result r = Measure(jockey, lateral: true);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} (lateral), {1:F3}, {2:F3}, {3:F3}, {4:F3}, {5:F3}, {6:F1}",
                    r.Clip, r.Length, r.AverageSpeed, r.StanceSpeed, r.StepLength, r.RightStrikePhase, r.LeanDeg));
            }
            sb.AppendLine(RuntimeFacingReport());
            Debug.Log("[LocomotionClipMeasure]\n" + sb);
            string outPath = ArgValue("-measureOut");
            if (!string.IsNullOrEmpty(outPath)) System.IO.File.WriteAllText(outPath, sb.ToString());
        }

        private static string ArgValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }

        private static AnimationClip Find(string fbx, string clipName) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                                     QuaterniusImportPostprocessor.StripPrefix(c.name) == clipName);

        public static Result Measure(AnimationClip clip, bool lateral = false)
        {
            var result = new Result { Clip = clip.name, Length = clip.length, AverageSpeed = clip.averageSpeed.magnitude };
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FootballAnimationBuilder.Ual2Path);
            if (model == null) return result;
            GameObject go = UnityEngine.Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null || !animator.isHuman) return result;
                Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? lf;
                Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? rf;
                const int N = 240;
                var l = new Vector3[N + 1];
                var r = new Vector3[N + 1];
                double lean = 0;
                for (int i = 0; i <= N; i++)
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clip.SampleAnimation(go, clip.length * i / N);
                    // Clips may carry a constant body offset; measure relative to the hips' horizontal position.
                    Vector3 h = hips.position; h.y = 0f;
                    // Lowest of ankle and toe: the contact point (heel-strike to toe-off).
                    l[i] = Lowest(lf.position, lt.position) - h;
                    r[i] = Lowest(rf.position, rt.position) - h;
                    Vector3 up = head.position - hips.position;
                    lean += Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg;
                }
                result.LeanDeg = (float)(lean / (N + 1));
                string dump = ArgValue("-measureDump");
                if (!string.IsNullOrEmpty(dump))
                {
                    var csv = new StringBuilder("i,lx,ly,lz,rx,ry,rz\n");
                    for (int i = 0; i <= N; i++)
                        csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}", i, l[i].x, l[i].y, l[i].z, r[i].x, r[i].y, r[i].z));
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dump, clip.name + ".csv"), csv.ToString());
                }
                float dt = clip.length / N;
                var speeds = new List<float>();
                Stance(l, dt, lateral, speeds, out _);
                Stance(r, dt, lateral, speeds, out int rightLand);
                speeds.Sort();
                result.StanceSpeed = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0f;
                result.StepLength = result.StanceSpeed * clip.length * 0.5f;
                result.RightStrikePhase = rightLand >= 0 ? (float)rightLand / N : -1f;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            return result;
        }

        private static Vector3 Lowest(Vector3 a, Vector3 b) => a.y <= b.y ? a : b;

        /// <summary>
        /// Runtime check of the shown facing: plays the built controller on the player model with Forward set to the
        /// jog speed and reports the body forward (Animator.bodyRotation) and the swing direction of the feet in the
        /// model's local frame. Both must point along +Z or players run backwards/sideways relative to the sim facing.
        /// </summary>
        public static string RuntimeFacingReport()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FootballAnimationBuilder.Ual2Path);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FootballAnimationBuilder.ControllerPath);
            if (model == null || ctrl == null) return "runtime facing: model or controller missing";
            GameObject go = UnityEngine.Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            var sb = new StringBuilder();
            try
            {
                var animator = go.GetComponent<Animator>();
                animator.runtimeAnimatorController = ctrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (float fwd in new[] { LocomotionGaitTable.WalkSpeed, LocomotionGaitTable.JogSpeed, LocomotionGaitTable.SprintSpeed })
                {
                    animator.SetFloat(AnimStateNames.ParamForward, fwd);
                    animator.SetFloat(AnimStateNames.ParamStrafe, 0f);
                    animator.SetFloat(AnimStateNames.ParamSpeed, fwd);
                    for (int i = 0; i < 60; i++) animator.Update(1f / 60f);
                    Vector3 bodyFwd = animator.bodyRotation * Vector3.forward;
                    // Planted-foot drift: the lower foot moves opposite to the direction of travel.
                    Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    Vector3 drift = Vector3.zero;
                    Vector3 prevL = lf.position, prevR = rf.position;
                    for (int i = 0; i < 120; i++)
                    {
                        animator.Update(1f / 120f);
                        Vector3 low = lf.position.y < rf.position.y ? lf.position - prevL : rf.position - prevR;
                        drift += low;
                        prevL = lf.position; prevR = rf.position;
                    }
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "Forward={0:F2}: bodyForward=({1:F2},{2:F2},{3:F2}) plantedFootDrift/s=({4:F2},{5:F2}) => travel {6}",
                        fwd, bodyFwd.x, bodyFwd.y, bodyFwd.z, drift.x, drift.z, drift.z < 0f ? "+Z (ok)" : "-Z (BACKWARDS)"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            return sb.ToString();
        }

        /// <summary>Collects the backward (or sideways) speed of a foot while it is planted; returns its landing sample.</summary>
        private static void Stance(Vector3[] foot, float dt, bool lateral, List<float> speeds, out int landing)
        {
            float min = foot.Min(p => p.y);
            const float Tol = 0.025f;
            landing = -1;
            int n = foot.Length - 1;
            for (int i = 0; i < n; i++)
            {
                bool down = foot[i].y < min + Tol;
                int prev = (i - 1 + n) % n;
                bool wasDown = foot[prev].y < min + Tol;
                if (down && !wasDown && landing < 0) landing = i;
                if (!down) continue;
                Vector3 v = (foot[i + 1] - foot[i]) / dt;
                speeds.Add(lateral ? -v.x : -v.z); // planted foot moves opposite to travel: + = travels right / forward
            }
        }
    }
}
