namespace OpenFifa.Core
{
    /// <summary>
    /// Visual offset (translation + yaw) applied to the rendered character relative to the sim root, so the
    /// clip's contact point meets the ball. The sim root itself is never moved.
    /// </summary>
    public readonly struct WarpPlan
    {
        public readonly SimVec3 TranslationDelta;
        public readonly float YawDelta;
        /// <summary>Distance that could not be covered within MaxWarpDistance (m).</summary>
        public readonly float ResidualDistance;
        /// <summary>Yaw that could not be covered within MaxWarpYaw (deg).</summary>
        public readonly float ResidualYaw;
        public readonly float WindowStart;
        public readonly float ContactTime;
        public readonly float HoldEnd;
        public readonly float ReleaseEnd;
        /// <summary>Visual root position at contact (sim root + delta).</summary>
        public readonly SimVec3 VisualRootAtContact;
        /// <summary>Visual root yaw at contact.</summary>
        public readonly float VisualYawAtContact;
        public readonly bool Feasible;

        public WarpPlan(SimVec3 translationDelta, float yawDelta, float residualDistance, float residualYaw,
            float windowStart, float contactTime, float holdEnd, float releaseEnd,
            SimVec3 visualRootAtContact, float visualYawAtContact, bool feasible)
        {
            TranslationDelta = translationDelta;
            YawDelta = yawDelta;
            ResidualDistance = residualDistance;
            ResidualYaw = residualYaw;
            WindowStart = windowStart;
            ContactTime = contactTime;
            HoldEnd = holdEnd;
            ReleaseEnd = releaseEnd;
            VisualRootAtContact = visualRootAtContact;
            VisualYawAtContact = visualYawAtContact;
            Feasible = feasible;
        }

        /// <summary>Warp weight at sim time t: 0 before the window, smooth ramp to 1 at contact, hold, smooth release to 0.</summary>
        public float WeightAt(float t)
        {
            if (t <= WindowStart) return 0f;
            if (t < ContactTime)
            {
                float span = ContactTime - WindowStart;
                return span > 0f ? AnimMath.SmoothStep01((t - WindowStart) / span) : 1f;
            }
            if (t <= HoldEnd) return 1f;
            if (t < ReleaseEnd)
            {
                float span = ReleaseEnd - HoldEnd;
                return span > 0f ? 1f - AnimMath.SmoothStep01((t - HoldEnd) / span) : 0f;
            }
            return 0f;
        }

        public SimVec3 TranslationAt(float t) => TranslationDelta * WeightAt(t);
        public float YawAt(float t) => YawDelta * WeightAt(t);
    }

    /// <summary>Time/angle warping so the strike foot (or head / hands) meets the ball at the contact tick.</summary>
    public static class KickWarpSolver
    {
        /// <param name="profile">Clip profile (right-side authored).</param>
        /// <param name="side">Strike side.</param>
        /// <param name="simRootAtContact">Sim root position at the contact tick (ground level).</param>
        /// <param name="simYawAtContact">Sim facing yaw at the contact tick (deg).</param>
        /// <param name="ballAtContact">Ball centre at the contact tick.</param>
        /// <param name="actionDirection">Desired outgoing ball direction (only its horizontal part is used). Zero = keep facing.</param>
        /// <param name="contactTime">Sim contact time (s).</param>
        /// <param name="distanceTolerance">Residual distance still considered a hit (m).</param>
        public static WarpPlan Solve(ActionClipProfile profile, ActionSide side, SimVec3 simRootAtContact, float simYawAtContact,
            SimVec3 ballAtContact, SimVec3 actionDirection, float contactTime, float distanceTolerance = 0.05f)
        {
            float strikeYaw = ActionClipProfile.YawForSide(profile.StrikeYawOffset, side);
            bool hasDir = actionDirection.X * actionDirection.X + actionDirection.Z * actionDirection.Z > 1e-8f;
            float desiredYaw = hasDir ? AnimMath.YawOf(actionDirection) - strikeYaw : simYawAtContact;

            float wantYaw = AnimMath.DeltaAngle(simYawAtContact, desiredYaw);
            float yawDelta = AnimMath.Clamp(wantYaw, -profile.MaxWarpYaw, profile.MaxWarpYaw);
            float residualYaw = wantYaw - yawDelta;
            float visualYaw = AnimMath.WrapAngle(simYawAtContact + yawDelta);

            SimVec3 contactLocal = ActionClipProfile.ForSide(profile.ContactOffset, side);
            SimVec3 authoredTravel = AnimMath.LocalToWorld(ActionClipProfile.ForSide(profile.RootDisplacementToContact, side), visualYaw);
            SimVec3 desiredRoot = ballAtContact - AnimMath.LocalToWorld(contactLocal, visualYaw);
            SimVec3 wantDelta = (desiredRoot - (simRootAtContact + authoredTravel)).Flat;

            SimVec3 delta = SimVec3.ClampMagnitude(wantDelta, profile.MaxWarpDistance);
            float residual = (wantDelta - delta).Magnitude;

            // Height mismatch (e.g. header ball too high) cannot be warped away on the ground; report it.
            float heightResidual = 0f;
            if (profile.ContactOffset.Y > 0.5f)
            {
                heightResidual = ballAtContact.Y - (simRootAtContact.Y + contactLocal.Y);
                if (heightResidual < 0f) heightResidual = -heightResidual;
            }
            float totalResidual = residual + heightResidual;

            float windowStart = contactTime - profile.WarpWindow;
            float holdEnd = contactTime + profile.FollowThrough;
            float releaseEnd = holdEnd + profile.ReleaseTime;
            return new WarpPlan(delta, yawDelta, totalResidual, residualYaw, windowStart, contactTime, holdEnd, releaseEnd,
                simRootAtContact + delta, visualYaw,
                totalResidual <= distanceTolerance && (residualYaw < 0f ? -residualYaw : residualYaw) <= 1e-3f);
        }
    }
}
