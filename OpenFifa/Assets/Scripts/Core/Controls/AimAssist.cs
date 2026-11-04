using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>Minimal player snapshot used by aim assist and player switching.</summary>
    public struct ControlPlayerInfo
    {
        public int Id;
        public SimVec3 Position;
        /// <summary>Top running speed in m/s (used for interception estimates).</summary>
        public float MaxSpeed;

        public ControlPlayerInfo(int id, SimVec3 position, float maxSpeed = 7f)
        {
            Id = id;
            Position = position;
            MaxSpeed = maxSpeed;
        }
    }

    /// <summary>
    /// Assisted aiming for passes and shots. All directions are on the pitch plane (X across, Z along);
    /// the glue code converts the stick to a pitch direction with the camera before calling these.
    /// Assist level: 1 = fully assisted, 0 = manual (stick direction is used as is).
    /// </summary>
    public static class AimAssist
    {
        public const float ManualConeDegrees = 10f;
        public const float AssistedConeDegrees = 75f;
        public const float MaxPassDistance = 50f;

        /// <summary>Half-angle of the search cone around the stick for a given assist level.</summary>
        public static float PassConeDegrees(float assist)
            => ManualConeDegrees + (AssistedConeDegrees - ManualConeDegrees) * Clamp01(assist);

        /// <summary>
        /// Picks the teammate best matching the stick and returns the pass direction, blended from the stick
        /// direction toward that teammate by the assist level. Returns the stick direction and target -1 when
        /// no teammate lies inside the cone. Teammates are scanned in list order; ties go to the lower id.
        /// </summary>
        public static SimVec3 PassDirection(SimVec3 passer, int passerId, SimVec3 stickDir,
            IReadOnlyList<ControlPlayerInfo> teammates, float assist, out int targetId)
        {
            targetId = -1;
            SimVec3 dir = stickDir.Flat.Normalized;
            if (dir.SqrMagnitude < 1e-6f || teammates == null) return dir;
            assist = Clamp01(assist);

            double coneRad = PassConeDegrees(assist) * Math.PI / 180.0;
            float bestScore = float.MaxValue;
            SimVec3 bestDir = SimVec3.Zero;
            for (int i = 0; i < teammates.Count; i++)
            {
                var t = teammates[i];
                if (t.Id == passerId) continue;
                SimVec3 to = (t.Position - passer).Flat;
                float dist = to.Magnitude;
                if (dist < 1f) continue;
                SimVec3 toDir = to / dist;
                float cos = Math.Max(-1f, Math.Min(1f, SimVec3.Dot(toDir, dir)));
                double angle = Math.Acos(cos);
                if (angle > coneRad) continue;
                float score = (float)(angle / coneRad) + 0.25f * Math.Min(dist, MaxPassDistance) / MaxPassDistance;
                if (score < bestScore || (score == bestScore && t.Id < targetId))
                {
                    bestScore = score;
                    bestDir = toDir;
                    targetId = t.Id;
                }
            }
            if (targetId < 0) return dir;
            SimVec3 blended = SimVec3.Lerp(dir, bestDir, assist).Normalized;
            return blended.SqrMagnitude < 1e-6f ? bestDir : blended;
        }

        /// <summary>Blends the player's power-bar value toward the ideal power for the pass distance.</summary>
        public static float PassPower(float rawPower, float distance, float assist)
        {
            float ideal = Clamp01(distance / MaxPassDistance);
            if (ideal < 0.1f) ideal = 0.1f;
            float a = Clamp01(assist);
            return Clamp01(rawPower + (ideal - rawPower) * a);
        }

        /// <summary>
        /// Shot target on the goal line. Assisted aim maps stick left/right to a point inside the posts
        /// (up to 75% of the half-width); manual aim follows the stick ray and can miss. The two are blended.
        /// <paramref name="goalCenter"/> is the centre of the target goal line; the shooter attacks toward it.
        /// </summary>
        public static SimVec3 ShotTarget(SimVec3 shooter, SimVec3 stickDir, SimVec3 goalCenter, float goalHalfWidth, float assist)
        {
            assist = Clamp01(assist);
            SimVec3 toGoal = (goalCenter - shooter).Flat;
            float zSign = toGoal.Z >= 0f ? 1f : -1f;
            SimVec3 dir = stickDir.Flat.Normalized;

            // Assisted: the stick's X component picks a point between the posts, 75% of the half-width at most.
            float lateral = Math.Max(-1f, Math.Min(1f, dir.X));
            float assistedX = goalCenter.X + lateral * goalHalfWidth * 0.75f;

            float manualX;
            if (dir.Z * zSign > 1e-3f)
            {
                manualX = shooter.X + dir.X / dir.Z * (goalCenter.Z - shooter.Z);
            }
            else if (dir.SqrMagnitude < 1e-6f)
            {
                manualX = goalCenter.X;
            }
            else
            {
                // Stick points sideways or backwards: the shot goes well wide on that side.
                manualX = goalCenter.X + (dir.X >= 0f ? 1f : -1f) * goalHalfWidth * 3f;
            }

            float x = manualX + (assistedX - manualX) * assist;
            return new SimVec3(x, 0f, goalCenter.Z);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
