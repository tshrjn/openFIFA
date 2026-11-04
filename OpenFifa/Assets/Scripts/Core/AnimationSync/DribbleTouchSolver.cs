namespace OpenFifa.Core
{
    /// <summary>
    /// Presentation rules for running dribble touches. The sim reports them as <see cref="AnimActionKind.FirstTouch"/>
    /// with <c>SimAnimAction.IsDribbleTouch</c>; the presentation plays them as <see cref="AnimActionKind.DribbleTouch"/>:
    /// no clip, locomotion keeps running, and the strike foot is pulled to the ball by IK over a short envelope
    /// (<see cref="BlendIn"/> before the contact tick, <see cref="BlendOut"/> after). The touching foot follows the
    /// stride: it is the foot that is swinging forward at the contact tick. Pure and deterministic.
    /// </summary>
    public static class DribbleTouchSolver
    {
        /// <summary>Seconds over which the touch IK weight ramps in before the contact tick.</summary>
        public const float BlendIn = 0.12f;
        /// <summary>Seconds over which the touch IK weight ramps out after the contact tick.</summary>
        public const float BlendOut = 0.12f;
        /// <summary>Below this speed (m/s) there is no usable gait cycle to read the stride parity from.</summary>
        public const float MinGaitSpeed = 0.8f;

        /// <summary>Kind the presentation should play for a sim action.</summary>
        public static AnimActionKind PresentationKind(AnimActionKind simKind, bool isDribbleTouch) =>
            isDribbleTouch && (simKind == AnimActionKind.FirstTouch || simKind == AnimActionKind.DribbleTouch)
                ? AnimActionKind.DribbleTouch
                : simKind;

        /// <summary>Seconds per single step (footfall to footfall) at a running speed, from the stride model; 0 when not running.</summary>
        public static float StepPeriod(float speed, StrideAdjuster stride = null)
        {
            if (speed < 0f) speed = -speed;
            if (speed < MinGaitSpeed) return 0f;
            float len = (stride ?? DefaultStride).NominalStepLength(speed);
            return len / speed;
        }

        private static readonly StrideAdjuster DefaultStride = new StrideAdjuster();

        /// <summary>
        /// Footfalls expected between now and the contact tick. The phase inside the current step is unknown, so the
        /// next footfall is assumed half a step away (round to nearest).
        /// </summary>
        public static int FootfallsBefore(float timeToContact, float stepPeriod)
        {
            if (stepPeriod <= 0f || timeToContact <= 0f) return 0;
            return (int)System.Math.Floor(timeToContact / stepPeriod + 0.5f);
        }

        /// <summary>The other foot.</summary>
        public static ActionSide Other(ActionSide side) => side == ActionSide.Right ? ActionSide.Left : ActionSide.Right;

        /// <summary>
        /// Foot that touches the ball.
        /// 1. Stride parity, when the gait is readable (<paramref name="hasSwingFoot"/> and running): the foot swinging
        ///    now keeps swinging until the next footfall, after which the other foot swings, and so on; the foot that is
        ///    swinging at the contact tick takes the touch.
        /// 2. Otherwise alternate with the previous touch, if there was one.
        /// 3. Otherwise the player's preferred foot.
        /// </summary>
        /// <param name="hasSwingFoot">True when <paramref name="swingFootNow"/> was measured from the pose.</param>
        /// <param name="swingFootNow">Foot in the air now (the one that lands next).</param>
        /// <param name="timeToContact">Seconds from now to the contact tick.</param>
        /// <param name="speed">Running speed (m/s).</param>
        /// <param name="hasLastTouch">True when <paramref name="lastTouchFoot"/> is the foot of the previous touch in this dribble.</param>
        /// <param name="preferred">The player's preferred foot.</param>
        public static ActionSide ChooseFoot(bool hasSwingFoot, ActionSide swingFootNow, float timeToContact, float speed,
            bool hasLastTouch, ActionSide lastTouchFoot, ActionSide preferred, StrideAdjuster stride = null)
        {
            float period = StepPeriod(speed, stride);
            if (hasSwingFoot && period > 0f)
            {
                int k = FootfallsBefore(timeToContact, period);
                return (k & 1) == 0 ? swingFootNow : Other(swingFootNow);
            }
            if (hasLastTouch) return Other(lastTouchFoot);
            return preferred;
        }

        /// <summary>
        /// Touch IK weight at time t for a touch at <paramref name="contactTime"/>: smooth ramp from 0 at
        /// contact - BlendIn to <paramref name="peak"/> at contact, smooth release to 0 at contact + BlendOut.
        /// Same curve as <see cref="ContactIkTargets.StrikeWeightAt"/> for the DribbleTouch profile.
        /// </summary>
        public static float WeightAt(float t, float contactTime, float peak = 1f, float blendIn = BlendIn, float blendOut = BlendOut)
        {
            float start = contactTime - blendIn, end = contactTime + blendOut;
            if (t <= start || t >= end) return 0f;
            if (t <= contactTime) return peak * (blendIn > 0f ? AnimMath.SmoothStep01((t - start) / blendIn) : 1f);
            return peak * (blendOut > 0f ? 1f - AnimMath.SmoothStep01((t - contactTime) / blendOut) : 0f);
        }
    }
}
