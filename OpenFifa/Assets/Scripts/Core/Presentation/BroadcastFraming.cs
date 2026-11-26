using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Engine-free framing maths for the side-on broadcast camera: where the ball sits on screen, how far the framing
    /// may trail the ball (so fast play into a goalmouth is never lost) and how tall things are in pixels.
    /// </summary>
    public static class BroadcastFraming
    {
        /// <summary>Ball height on screen, as a fraction from the top (FC-style: a little below centre).</summary>
        public const float BallScreenFromTop = 0.57f;

        /// <summary>Horizontal lead (m) towards the goal the team in possession attacks.</summary>
        public const float AttackLeadMetres = 5f;

        /// <summary>
        /// Upward pitch (degrees) to add after aiming straight at a point so that the point lands
        /// <paramref name="fromTop"/> of the way down the screen (0.5 = centre), for a vertical FOV in degrees.
        /// </summary>
        public static float PitchUpDegrees(float verticalFovDegrees, float fromTop)
        {
            double half = verticalFovDegrees * 0.5 * Math.PI / 180.0;
            double ndc = (fromTop - 0.5) * 2.0; // 0 at centre, +1 at the bottom edge
            return (float)(Math.Atan(ndc * Math.Tan(half)) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Screen fraction from the top at which a point lies when it is <paramref name="belowAxisDegrees"/> below the
        /// optical axis (inverse of <see cref="PitchUpDegrees"/>).
        /// </summary>
        public static float FromTopForAngle(float verticalFovDegrees, float belowAxisDegrees)
        {
            double half = verticalFovDegrees * 0.5 * Math.PI / 180.0;
            double ndc = Math.Tan(belowAxisDegrees * Math.PI / 180.0) / Math.Tan(half);
            return (float)(0.5 + ndc * 0.5);
        }

        /// <summary>
        /// Hard follow limit: the smoothed framing value never trails <paramref name="target"/> by more than
        /// <paramref name="maxOffset"/> (the edge of the dead zone), whatever the damping.
        /// </summary>
        public static float ClampFollow(float framed, float target, float maxOffset)
        {
            if (framed > target + maxOffset) return target + maxOffset;
            if (framed < target - maxOffset) return target - maxOffset;
            return framed;
        }

        /// <summary>On-screen size in pixels of an object <paramref name="sizeMetres"/> tall at a distance (perspective).</summary>
        public static float Pixels(float sizeMetres, float distance, float verticalFovDegrees, float screenHeight)
        {
            if (distance <= 1e-3f) return float.PositiveInfinity;
            double half = verticalFovDegrees * 0.5 * Math.PI / 180.0;
            return (float)(sizeMetres * screenHeight / (2.0 * distance * Math.Tan(half)));
        }

        /// <summary>
        /// Visual ball scale so it is at least <paramref name="minPixels"/> across: 1 when it already is, else the
        /// factor needed, capped at <paramref name="maxScale"/>.
        /// </summary>
        public static float BallScale(float truePixels, float minPixels, float maxScale)
        {
            if (truePixels <= 0.01f) return 1f;
            float k = minPixels / truePixels;
            return k < 1f ? 1f : (k > maxScale ? maxScale : k);
        }

        /// <summary>Attack direction along the pitch (+1 = +Z, -1 = -Z, 0 = nobody in possession).</summary>
        public static int AttackDirection(MatchSim sim)
        {
            if (sim == null) return 0;
            int owner = sim.State.Owner;
            if (owner < 0 || owner >= MatchSimState.PlayerCount) return 0;
            return sim.AttackSign(MatchSim.TeamOf(owner));
        }
    }
}
