using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Chooses which defender the user should control next.
    /// - Smart switch (LB / Switch button): the player who can reach the ball's path soonest.
    /// - Directional switch (right-stick flick / swipe): the nearest player inside a cone around the flick.
    /// Candidates are scanned in list order and ties go to the lower id, so results are deterministic.
    /// </summary>
    public static class SmartSwitchSelector
    {
        public const float Horizon = 3f;
        public const float Step = 0.1f;
        public const float ControlRadius = 1f;
        public const float DirectionalConeDegrees = 60f;

        /// <summary>
        /// Earliest time (s) the player can meet the ball moving at constant velocity, sampling the path up to
        /// <see cref="Horizon"/>. If the player can't get there in time, returns Horizon plus the shortfall.
        /// </summary>
        public static float InterceptScore(ControlPlayerInfo p, SimVec3 ballPos, SimVec3 ballVel)
        {
            float speed = p.MaxSpeed > 0.1f ? p.MaxSpeed : 0.1f;
            float bestShortfall = float.MaxValue;
            int steps = (int)Math.Round(Horizon / Step);
            for (int i = 0; i <= steps; i++)
            {
                float t = i * Step;
                SimVec3 ball = (ballPos + ballVel * t).Flat;
                float dist = SimVec3.Distance(p.Position.Flat, ball);
                float reach = Math.Max(0f, dist - ControlRadius) / speed;
                if (reach <= t) return t;
                float shortfall = reach - t;
                if (shortfall < bestShortfall) bestShortfall = shortfall;
            }
            return Horizon + bestShortfall;
        }

        /// <summary>
        /// Best player to switch to, excluding <paramref name="currentId"/> (unless it is the only candidate).
        /// Returns -1 for an empty list.
        /// </summary>
        public static int SelectNearestToBallPath(IReadOnlyList<ControlPlayerInfo> players, SimVec3 ballPos, SimVec3 ballVel, int currentId)
        {
            if (players == null || players.Count == 0) return -1;
            int best = -1;
            float bestScore = float.MaxValue;
            float bestDist = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.Id == currentId && players.Count > 1) continue;
                float score = InterceptScore(p, ballPos, ballVel);
                float dist = SimVec3.Distance(p.Position.Flat, ballPos.Flat);
                bool better = score < bestScore - 1e-4f
                    || (Math.Abs(score - bestScore) <= 1e-4f && (dist < bestDist - 1e-4f
                        || (Math.Abs(dist - bestDist) <= 1e-4f && p.Id < best)));
                if (best < 0 || better)
                {
                    best = p.Id;
                    bestScore = score;
                    bestDist = dist;
                }
            }
            return best;
        }

        /// <summary>
        /// Player in the flick direction from the current player. Score favours small angle, then distance.
        /// <paramref name="direction"/> is on the pitch plane. Returns -1 when nobody is inside the cone.
        /// </summary>
        public static int SelectDirectional(IReadOnlyList<ControlPlayerInfo> players, SimVec3 currentPos, int currentId, SimVec3 direction)
        {
            SimVec3 dir = direction.Flat.Normalized;
            if (players == null || dir.SqrMagnitude < 1e-6f) return -1;
            double cone = DirectionalConeDegrees * Math.PI / 180.0;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.Id == currentId) continue;
                SimVec3 to = (p.Position - currentPos).Flat;
                float dist = to.Magnitude;
                if (dist < 0.5f) continue;
                float cos = Math.Max(-1f, Math.Min(1f, SimVec3.Dot(to / dist, dir)));
                double angle = Math.Acos(cos);
                if (angle > cone) continue;
                float score = dist * (1f + 2f * (float)(angle / cone));
                if (score < bestScore || (score == bestScore && p.Id < best))
                {
                    bestScore = score;
                    best = p.Id;
                }
            }
            return best;
        }
    }
}
