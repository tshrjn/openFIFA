using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Off-ball shape motion: what keeps players who are "holding shape" from standing frozen on their spot.
    /// <list type="bullet">
    /// <item><b>Block slide</b>: the formation anchors are an instant function of the ball. <see cref="TeamAIBrain"/>
    /// keeps shape spots that follow them with a <see cref="ShapeSmoothing"/> s lag at no more than
    /// <see cref="SlideSpeed"/>, and passes each spot's velocity as the move target's velocity, so a holder walks or jogs
    /// with the block instead of waiting in the arrival dead zone and then chasing a spot that jumped.</item>
    /// <item><b>Micro-adjustments</b>: real players are almost never still in open play (they walk and jog small
    /// corrections: checking a shoulder, re-angling a body, adjusting to a team-mate). <see cref="Adjustment"/> is a
    /// slow per-player ellipse round the spot (period 6-8 s, about 1 m) whose speed never drops below about
    /// 0.6 m/s, so the arrival logic treats it as a moving target and the player walks it continuously rather than
    /// stopping and restarting (no facing flip-flop). It is a pure function of player id and match time: deterministic,
    /// allocation-free and it draws nothing from the AI's random stream.</item>
    /// </list>
    /// </summary>
    public static class TeamAIShapeMotion
    {
        /// <summary>Lag (s) with which shape spots follow their anchors.</summary>
        public const float ShapeSmoothing = 0.7f;

        /// <summary>
        /// Fastest a shape spot slides (m/s): a block shifts at a jog, quicker out of possession and in transitions, a
        /// quicker for a stronger side (<see cref="TeamAITempo.DifficultyFactor"/>).
        /// </summary>
        public static float SlideSpeed(TeamAIPhase phase, float theta)
        {
            float v;
            switch (phase)
            {
                case TeamAIPhase.Recovery:
                case TeamAIPhase.CounterPress: v = 3.4f; break;
                case TeamAIPhase.HighPress:
                case TeamAIPhase.MidBlock:
                case TeamAIPhase.LowBlock:
                case TeamAIPhase.SetPieceAgainst: v = 2.6f; break;
                case TeamAIPhase.Progression:
                case TeamAIPhase.FinalThird: v = 4.5f; break; // an attack moves up quickly
                default: v = 2.0f; break;
            }
            return v * TeamAITempo.DifficultyFactor(theta);
        }


        /// <summary>Semi-axes (m) of the adjustment ellipse: the long axis, and the short one.</summary>
        public const float MainAmplitude = 1.1f;
        public const float CrossAmplitude = 0.85f;
        /// <summary>Seconds for the drift ellipse's axis to turn once.</summary>
        public const float PrecessionPeriod = 90f;
        /// <summary>A back-line player's depth drift (m): the line must stay level for offside.</summary>
        public const float LineDepthAmplitude = 0.3f;
        /// <summary>Drift id of a team's back line (plus the team index): the whole line shares one depth drift.</summary>
        public const int LineDriftId = 1000;

        /// <summary>
        /// Drift offset and its velocity for one player: a slow ellipse round the spot (semi-axes
        /// <see cref="MainAmplitude"/> and <see cref="CrossAmplitude"/>, period 6-8 s) whose axis turns once every
        /// <see cref="PrecessionPeriod"/> s, so the path never repeats as a visible loop. On an ellipse the speed never
        /// drops below (minor axis x angular frequency), about 0.6 m/s, so the arrival logic always sees a moving target
        /// and the player walks it instead of parking and restarting. <paramref name="wide"/>: touchline player (the long
        /// axis runs up and down the line). <paramref name="lineKeeper"/>: back-line player (depth semi-axis
        /// <see cref="LineDepthAmplitude"/>, long axis along the line, no precession). <paramref name="scale"/> scales the
        /// path (set pieces use a smaller one). Per-player period, phase, size and direction come from a hash of the id.
        /// </summary>
        public static void Adjustment(int playerId, float time, bool lineKeeper, bool wide, out SimVec3 offset, out SimVec3 velocity,
            float scale = 1f)
        {
            uint h = Hash((uint)playerId);
            float period = 6f + 2f * ((h & 0xFF) / 255f);                       // 6..8 s
            float w = 2f * (float)Math.PI / period;
            if ((h >> 24 & 1) != 0) w = -w;                                       // clockwise or not
            float phase = 2f * (float)Math.PI * (((h >> 8) & 0xFF) / 255f);
            float a = scale * MainAmplitude * (0.9f + 0.2f * (((h >> 16) & 0xFF) / 255f));
            float b = scale * (lineKeeper ? LineDepthAmplitude : CrossAmplitude);
            // Axis direction: across the pitch (X) for most, up the line (Z) for wide players; turning slowly.
            float axis0 = (wide ? 0.5f * (float)Math.PI : 0f) + 0.5f * (((h >> 25) & 0x7F) / 127f - 0.5f);
            float omega = lineKeeper ? 0f : 2f * (float)Math.PI / PrecessionPeriod;
            float axis = lineKeeper ? 0f : axis0 + omega * time;

            float u = w * time + phase;
            float su = (float)Math.Sin(u), cu = (float)Math.Cos(u);
            float lx = a * su, lz = b * cu;                 // local ellipse point
            float lvx = a * w * cu, lvz = -b * w * su;      // its velocity
            float ca = (float)Math.Cos(axis), sa = (float)Math.Sin(axis);
            // Rotate local (long axis = x) into the pitch frame.
            float ox = lx * ca - lz * sa, oz = lx * sa + lz * ca;
            float vx = lvx * ca - lvz * sa - omega * oz;    // + omega x offset (precession)
            float vz = lvx * sa + lvz * ca + omega * ox;
            offset = new SimVec3(ox, 0f, oz);
            velocity = new SimVec3(vx, 0f, vz);
        }

        /// <summary>Clamp a flat velocity to <paramref name="max"/> m/s.</summary>
        public static SimVec3 ClampSpeed(SimVec3 v, float max)
        {
            float s = v.Magnitude;
            return s > max && s > 1e-6f ? v * (max / s) : v;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }
    }
}
