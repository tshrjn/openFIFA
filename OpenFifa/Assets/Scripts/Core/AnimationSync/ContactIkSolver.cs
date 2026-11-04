namespace OpenFifa.Core
{
    public enum ContactEffector
    {
        None = 0,
        LeftFoot = 1,
        RightFoot = 2,
        Head = 3,
        LeftHand = 4,
        RightHand = 5,
    }

    /// <summary>World-space IK goals for an action, with their weight envelopes.</summary>
    public readonly struct ContactIkTargets
    {
        public readonly ContactEffector StrikeEffector;
        /// <summary>Where the strike effector should be at contact (on the ball surface, opposite the outgoing direction).</summary>
        public readonly SimVec3 StrikeTarget;
        public readonly ContactEffector PlantEffector;
        /// <summary>Plant foot target on the ground (Y = ground + ankle height).</summary>
        public readonly SimVec3 PlantTarget;
        public readonly float StrikeRampStart;
        public readonly float ContactTime;
        public readonly float StrikeReleaseEnd;
        public readonly float PlantTime;
        public readonly float PlantReleaseEnd;

        public ContactIkTargets(ContactEffector strikeEffector, SimVec3 strikeTarget, ContactEffector plantEffector,
            SimVec3 plantTarget, float strikeRampStart, float contactTime, float strikeReleaseEnd, float plantTime, float plantReleaseEnd)
        {
            StrikeEffector = strikeEffector;
            StrikeTarget = strikeTarget;
            PlantEffector = plantEffector;
            PlantTarget = plantTarget;
            StrikeRampStart = strikeRampStart;
            ContactTime = contactTime;
            StrikeReleaseEnd = strikeReleaseEnd;
            PlantTime = plantTime;
            PlantReleaseEnd = plantReleaseEnd;
        }

        /// <summary>Strike IK weight: ramps in before contact, peaks at contact, ramps out after (IK must not fight the follow-through).</summary>
        public float StrikeWeightAt(float t)
        {
            if (StrikeEffector == ContactEffector.None) return 0f;
            if (t <= StrikeRampStart || t >= StrikeReleaseEnd) return 0f;
            if (t <= ContactTime)
            {
                float span = ContactTime - StrikeRampStart;
                return span > 0f ? AnimMath.SmoothStep01((t - StrikeRampStart) / span) : 1f;
            }
            float out_ = StrikeReleaseEnd - ContactTime;
            return out_ > 0f ? 1f - AnimMath.SmoothStep01((t - ContactTime) / out_) : 0f;
        }

        /// <summary>Plant IK weight: 1 from shortly before the plant time until the release.</summary>
        public float PlantWeightAt(float t, float blendIn = 0.08f)
        {
            if (PlantEffector == ContactEffector.None) return 0f;
            if (t <= PlantTime - blendIn || t >= PlantReleaseEnd) return 0f;
            if (t < PlantTime) return AnimMath.SmoothStep01((t - (PlantTime - blendIn)) / blendIn);
            float releaseStart = PlantReleaseEnd - blendIn;
            if (t > releaseStart) return 1f - AnimMath.SmoothStep01((t - releaseStart) / blendIn);
            return 1f;
        }
    }

    /// <summary>Foot-plant and strike IK targets from a warp plan.</summary>
    public static class ContactIkSolver
    {
        public const float DefaultBallRadius = 0.11f;
        public const float DefaultAnkleHeight = 0.08f;

        public static ContactEffector StrikeEffectorFor(AnimActionKind kind, ActionSide side)
        {
            switch (kind)
            {
                case AnimActionKind.Header: return ContactEffector.Head;
                case AnimActionKind.GoalkeeperDive:
                case AnimActionKind.GoalkeeperThrow:
                    return side == ActionSide.Right ? ContactEffector.RightHand : ContactEffector.LeftHand;
                case AnimActionKind.Pass:
                case AnimActionKind.Shot:
                case AnimActionKind.Cross:
                case AnimActionKind.FirstTouch:
                case AnimActionKind.DribbleTouch:
                case AnimActionKind.StandingTackle:
                case AnimActionKind.SlideTackle:
                    return side == ActionSide.Right ? ContactEffector.RightFoot : ContactEffector.LeftFoot;
                default: return ContactEffector.None;
            }
        }

        public static ContactIkTargets Solve(ActionClipProfile profile, ActionSide side, WarpPlan warp, SimVec3 ballAtContact,
            SimVec3 actionDirection, float groundY, float ballRadius = DefaultBallRadius, float ankleHeight = DefaultAnkleHeight)
        {
            ContactEffector strike = StrikeEffectorFor(profile.Kind, side);
            SimVec3 dir = actionDirection.Flat.Normalized;
            if (dir.SqrMagnitude < 0.5f) dir = AnimMath.Forward(warp.VisualYawAtContact);
            // Contact point on the ball surface opposite the outgoing direction; for headers keep full 3D direction.
            SimVec3 approach = profile.Kind == AnimActionKind.Header && actionDirection.SqrMagnitude > 1e-8f
                ? actionDirection.Normalized : dir;
            SimVec3 strikeTarget = ballAtContact - approach * ballRadius;

            ContactEffector plant = ContactEffector.None;
            SimVec3 plantTarget = SimVec3.Zero;
            if (profile.PlantFootOffset.SqrMagnitude > 1e-8f && strike != ContactEffector.None
                && (strike == ContactEffector.LeftFoot || strike == ContactEffector.RightFoot))
            {
                plant = strike == ContactEffector.RightFoot ? ContactEffector.LeftFoot : ContactEffector.RightFoot;
                SimVec3 local = ActionClipProfile.ForSide(profile.PlantFootOffset, side);
                SimVec3 w = warp.VisualRootAtContact + AnimMath.LocalToWorld(local, warp.VisualYawAtContact);
                plantTarget = new SimVec3(w.X, groundY + ankleHeight, w.Z);
            }

            float contact = warp.ContactTime;
            float rampStart = contact - profile.WarpWindow * 0.5f;
            float strikeRelease = contact + profile.FollowThrough;
            float plantTime = contact - profile.PlantLeadTime;
            float plantRelease = contact + profile.FollowThrough + profile.ReleaseTime;
            return new ContactIkTargets(strike, strikeTarget, plant, plantTarget, rampStart, contact, strikeRelease, plantTime, plantRelease);
        }
    }
}
