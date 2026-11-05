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

        /// <summary>
        /// Critically damped spring towards <paramref name="target"/> (the same integrator as Unity's Mathf.SmoothDamp,
        /// without a speed limit), engine-free so the follow logic can be tested.
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float dt)
        {
            if (dt <= 0f) return current;
            smoothTime = Math.Max(1e-4f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if ((target - current > 0f) == (output > target))
            {
                // No overshoot past the target.
                output = target;
                velocity = 0f;
            }
            return output;
        }

        /// <summary>Largest framing speed (m/s) carried out of the dead-zone edge (a hard-hit ball, not a teleport).</summary>
        public const float MaxFollowSpeed = 35f;

        /// <summary>
        /// One axis of the broadcast follow: smooth towards <paramref name="target"/>, then hold the hard dead-zone edge
        /// (<see cref="ClampFollow"/>) around the ball. While the edge holds the framing, the framing moves with the ball,
        /// so the spring's velocity becomes the ball's (<paramref name="ballVelocity"/> on this axis). When the edge
        /// releases (the ball slows after a long ball) the spring carries on from that speed: zeroing it (the old code)
        /// made the camera stop dead at the release and then crawl back up to speed.
        /// </summary>
        public static float FollowAxis(float framed, ref float velocity, float target, float ball, float ballVelocity,
            float maxOffset, float smoothTime, float dt)
        {
            float next = SmoothDamp(framed, target, ref velocity, smoothTime, dt);
            float held = ClampFollow(next, ball, maxOffset);
            if (held != next)
                velocity = ballVelocity > MaxFollowSpeed ? MaxFollowSpeed : (ballVelocity < -MaxFollowSpeed ? -MaxFollowSpeed : ballVelocity);
            return held;
        }

        /// <summary>Seconds the follow eases back in after a presentation cut hands the camera back.</summary>
        public const float ResumeEaseSeconds = 0.4f;
        /// <summary>Follow responsiveness right at the hand-back, as a fraction of normal (eases up to 1).</summary>
        public const float ResumeMinResponse = 0.25f;

        /// <summary>
        /// Follow smoothing time <paramref name="sinceResume"/> seconds after a hand-back (e.g. the offside camera
        /// cut ends): starts at <paramref name="baseSmoothTime"/> / <see cref="ResumeMinResponse"/> (a gentle start
        /// from rest) and eases (smoothstep) back to <paramref name="baseSmoothTime"/> over
        /// <paramref name="easeSeconds"/>. The hard dead-zone edge still applies, so the ball stays in frame.
        /// </summary>
        public static float ResumeSmoothTime(float baseSmoothTime, float sinceResume, float easeSeconds = ResumeEaseSeconds)
        {
            if (easeSeconds <= 0f || sinceResume >= easeSeconds) return baseSmoothTime;
            float t = sinceResume <= 0f ? 0f : sinceResume / easeSeconds;
            float s = t * t * (3f - 2f * t);
            float response = ResumeMinResponse + (1f - ResumeMinResponse) * s;
            return baseSmoothTime / response;
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
