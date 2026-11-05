using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Computes the per-vertex kit body map for a skinned humanoid from its rest (bind) pose, for the kit shader's
    /// SkinnedBody mapping. Everything is measured in the rest pose, so the values are constant per vertex and
    /// kit regions, stripes and prints stay glued to the body while it animates.
    ///
    /// Output per vertex (two float4 channels, written to mesh UV2 and UV3 by the importer):
    ///   body0 = (armT, legT, armWeight, legWeight)
    ///     armT: 0 shoulder joint, 1 elbow, 2 wrist, 3 fingertips (nearest point on the arm chain of the side
    ///           the vertex is skinned to). legT: 0 hip joint, 1 knee, 2 ankle, 3 toe tip.
    ///     armWeight / legWeight: summed skin weight of the arm / leg bones (0..1).
    ///   body1 = (x, y, z, 1 + h) rest position in kit space, metres: origin on the torso axis at neck-base height,
    ///           +X the character's right, +Y up, +Z the character's front. h = height above the soles (metres);
    ///           w >= 1 marks a baked mesh.
    /// Pure C#.
    /// </summary>
    public static class KitBodyMapBaker
    {
        /// <summary>Floats per vertex in each output channel.</summary>
        public const int Stride = 4;

        /// <summary>
        /// Bakes the map. <paramref name="positions"/> are rest positions in model space (metres).
        /// <paramref name="boneIndices"/>/<paramref name="boneWeights"/> hold <paramref name="influences"/> entries
        /// per vertex; <paramref name="boneLimbs"/> classifies each bone. Returns the measured torso frame.
        /// </summary>
        public static KitBodyFrame Bake(KitBodySkeleton skeleton, IReadOnlyList<SimVec3> positions,
            int[] boneIndices, float[] boneWeights, int influences, KitBodyLimb[] boneLimbs,
            float[] body0, float[] body1)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            int n = positions.Count;
            if (body0 == null || body0.Length < n * Stride) throw new ArgumentException("body0 too small.", nameof(body0));
            if (body1 == null || body1.Length < n * Stride) throw new ArgumentException("body1 too small.", nameof(body1));

            var frame = ComputeFrame(skeleton);
            var limbW = new float[5];

            // Pass 1: limb weights, chain parameters and kit-space positions about the neck joint.
            for (int v = 0; v < n; v++)
            {
                Array.Clear(limbW, 0, limbW.Length);
                if (boneIndices != null && boneWeights != null && boneLimbs != null)
                {
                    for (int k = 0; k < influences; k++)
                    {
                        int i = v * influences + k;
                        if (i >= boneIndices.Length || i >= boneWeights.Length) break;
                        int b = boneIndices[i];
                        if (b < 0 || b >= boneLimbs.Length) continue;
                        limbW[(int)boneLimbs[b]] += boneWeights[i];
                    }
                }

                var p = positions[v];
                var local = frame.ToKit(p);
                float armW = limbW[(int)KitBodyLimb.ArmLeft] + limbW[(int)KitBodyLimb.ArmRight];
                float legW = limbW[(int)KitBodyLimb.LegLeft] + limbW[(int)KitBodyLimb.LegRight];

                bool rightArm = limbW[(int)KitBodyLimb.ArmRight] > limbW[(int)KitBodyLimb.ArmLeft]
                                || (armW <= 0f && local.X > 0f);
                bool rightLeg = limbW[(int)KitBodyLimb.LegRight] > limbW[(int)KitBodyLimb.LegLeft]
                                || (legW <= 0f && local.X > 0f);

                int o = v * Stride;
                body0[o + 0] = ChainParameter(p, rightArm ? skeleton.ArmRight : skeleton.ArmLeft);
                body0[o + 1] = ChainParameter(p, rightLeg ? skeleton.LegRight : skeleton.LegLeft);
                body0[o + 2] = Clamp01(armW);
                body0[o + 3] = Clamp01(legW);
                body1[o + 0] = local.X;
                body1[o + 1] = local.Y;
                body1[o + 2] = local.Z;
                body1[o + 3] = 1f;
            }

            // Pass 2: centre X/Z on the torso (chest to belly, unskinned to limbs) so front/back and the
            // around-the-body angle are measured from the torso axis, and measure its half extents.
            double sx = 0, sz = 0;
            int count = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int v = 0; v < n; v++)
            {
                int o = v * Stride;
                if (body0[o + 2] > 0.1f || body0[o + 3] > 0.1f) continue;
                float y = body1[o + 1];
                if (y > -0.12f || y < -0.42f) continue;
                sx += body1[o]; sz += body1[o + 2]; count++;
                minX = Math.Min(minX, body1[o]); maxX = Math.Max(maxX, body1[o]);
                minZ = Math.Min(minZ, body1[o + 2]); maxZ = Math.Max(maxZ, body1[o + 2]);
            }
            float floorY = float.MaxValue;
            for (int v = 0; v < n; v++) floorY = Math.Min(floorY, body1[v * Stride + 1]);
            for (int v = 0; v < n; v++) body1[v * Stride + 3] = 1f + (body1[v * Stride + 1] - floorY);
            frame.FloorY = floorY;

            if (count > 0)
            {
                // Centre on the bounding box (mean is biased towards densely tessellated areas).
                float cx = 0.5f * (minX + maxX);
                float cz = 0.5f * (minZ + maxZ);
                for (int v = 0; v < n; v++)
                {
                    body1[v * Stride] -= cx;
                    body1[v * Stride + 2] -= cz;
                }
                frame.TorsoCentre = new SimVec3(cx, 0f, cz);
                frame.TorsoHalfWidth = 0.5f * (maxX - minX);
                frame.TorsoHalfDepth = 0.5f * (maxZ - minZ);
            }
            return frame;
        }

        /// <summary>
        /// Body frame from the skeleton: up from hips to neck, right from left to right shoulder, front from the
        /// toes. Origin at the neck joint.
        /// </summary>
        public static KitBodyFrame ComputeFrame(KitBodySkeleton s)
        {
            var up = (s.Neck - s.Hips).Normalized;
            if (up.SqrMagnitude < 0.5f) up = SimVec3.Up;
            var across = s.ArmRight[0] - s.ArmLeft[0];
            var right = (across - up * SimVec3.Dot(across, up)).Normalized;
            if (right.SqrMagnitude < 0.5f) right = new SimVec3(1f, 0f, 0f);
            var forward = SimVec3.Cross(right, up);

            // The toes point forward. If they disagree, the rig's left/right labels are mirrored.
            var toes = (s.LegLeft[3] - s.LegLeft[2]) + (s.LegRight[3] - s.LegRight[2]);
            if (SimVec3.Dot(toes, forward) < 0f)
            {
                forward = -forward;
                right = -right;
            }
            return new KitBodyFrame { Origin = s.Neck, Right = right, Up = up, Forward = forward };
        }

        /// <summary>
        /// Position along a joint chain: segment index + fraction along it, for the nearest point of the polyline.
        /// Points before the first joint clamp to 0, points past the last to the chain length.
        /// </summary>
        public static float ChainParameter(SimVec3 p, SimVec3[] chain)
        {
            if (chain == null || chain.Length < 2) return 0f;
            float best = float.MaxValue;
            float param = 0f;
            for (int i = 0; i < chain.Length - 1; i++)
            {
                var a = chain[i];
                var d = chain[i + 1] - a;
                float len2 = d.SqrMagnitude;
                float t = len2 > 1e-10f ? Clamp01(SimVec3.Dot(p - a, d) / len2) : 0f;
                float dist = (p - (a + d * t)).SqrMagnitude;
                if (dist < best - 1e-9f)
                {
                    best = dist;
                    param = i + t;
                }
            }
            return param;
        }

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
