namespace OpenFifa.Core
{
    /// <summary>
    /// Authored timing and geometry of one action clip, always described for the RIGHT side
    /// (right foot strikes / dive to the right). Left-side actions use the mirrored clip, so local X offsets are negated.
    /// Offsets are in character-local space: X right, Y up, Z forward, metres, relative to the root (between the feet, on the ground).
    /// </summary>
    public sealed class ActionClipProfile
    {
        public AnimActionKind Kind { get; }
        /// <summary>Total clip length in seconds at playback rate 1.</summary>
        public float ClipLength { get; }
        /// <summary>Clip time (s) of the contact frame.</summary>
        public float ContactTime { get; }
        /// <summary>Minimum clip time (s) that must play before contact; the solver may skip the wind-up but not below this.</summary>
        public float MinWindup { get; }
        public float MinRate { get; }
        public float MaxRate { get; }
        /// <summary>Ball centre (or head/hand target) relative to the root at the contact frame, right-side version.</summary>
        public SimVec3 ContactOffset { get; }
        /// <summary>Support (plant) foot position relative to the root at contact, right-side version. Zero when there is no plant foot.</summary>
        public SimVec3 PlantFootOffset { get; }
        /// <summary>Root travel authored in the clip between clip start and contact (zero for in-place clips).</summary>
        public SimVec3 RootDisplacementToContact { get; }
        /// <summary>Yaw (deg) between the root forward and the ball's outgoing direction at contact, right-side version (inside-foot pass goes slightly left: negative).</summary>
        public float StrikeYawOffset { get; }
        public float MaxWarpDistance { get; }
        public float MaxWarpYaw { get; }
        /// <summary>Seconds before contact over which the warp is blended in.</summary>
        public float WarpWindow { get; }
        /// <summary>Seconds after contact the warp/IK is held before releasing.</summary>
        public float FollowThrough { get; }
        /// <summary>Seconds to release the warp/IK after the follow-through.</summary>
        public float ReleaseTime { get; }
        /// <summary>Seconds before contact at which the plant foot lands.</summary>
        public float PlantLeadTime { get; }
        /// <summary>True when the action is preceded by a run-up whose strides should be adjusted.</summary>
        public bool UsesRunUp { get; }

        public ActionClipProfile(AnimActionKind kind, float clipLength, float contactTime, float minWindup,
            float minRate, float maxRate, SimVec3 contactOffset, SimVec3 plantFootOffset,
            SimVec3 rootDisplacementToContact, float strikeYawOffset, float maxWarpDistance, float maxWarpYaw,
            float warpWindow, float followThrough, float releaseTime, float plantLeadTime, bool usesRunUp)
        {
            Kind = kind;
            ClipLength = clipLength;
            ContactTime = AnimMath.Clamp(contactTime, 0f, clipLength);
            MinWindup = AnimMath.Clamp(minWindup, 0f, ContactTime);
            MinRate = minRate;
            MaxRate = maxRate < minRate ? minRate : maxRate;
            ContactOffset = contactOffset;
            PlantFootOffset = plantFootOffset;
            RootDisplacementToContact = rootDisplacementToContact;
            StrikeYawOffset = strikeYawOffset;
            MaxWarpDistance = maxWarpDistance;
            MaxWarpYaw = maxWarpYaw;
            WarpWindow = warpWindow;
            FollowThrough = followThrough;
            ReleaseTime = releaseTime;
            PlantLeadTime = plantLeadTime;
            UsesRunUp = usesRunUp;
        }

        /// <summary>Normalized clip time of the contact frame.</summary>
        public float ContactNormalized => ClipLength > 0f ? ContactTime / ClipLength : 0f;

        /// <summary>Mirror a right-side local offset to the requested side.</summary>
        public static SimVec3 ForSide(SimVec3 rightSideLocal, ActionSide side) =>
            side == ActionSide.Right ? rightSideLocal : new SimVec3(-rightSideLocal.X, rightSideLocal.Y, rightSideLocal.Z);

        public static float YawForSide(float rightSideYaw, ActionSide side) => side == ActionSide.Right ? rightSideYaw : -rightSideYaw;
    }

    /// <summary>
    /// Default profiles. These numbers are the contract with the generated placeholder clips
    /// (Assets/Animations/Editor/FootballClipBuilder.cs keys the contact pose at ContactTime and fires the contact event there).
    /// Replace per clip when real mocap lands.
    /// </summary>
    public static class ActionClipLibrary
    {
        private static readonly SimVec3 NoOffset = SimVec3.Zero;

        public static ActionClipProfile Default(AnimActionKind kind)
        {
            switch (kind)
            {
                case AnimActionKind.Pass:
                    return new ActionClipProfile(kind, 0.90f, 0.40f, 0.12f, 0.75f, 1.6f,
                        new SimVec3(0.10f, 0.11f, 0.32f), new SimVec3(-0.14f, 0f, 0.22f), NoOffset,
                        -8f, 0.45f, 50f, 0.30f, 0.12f, 0.25f, 0.18f, true);
                case AnimActionKind.Shot:
                    return new ActionClipProfile(kind, 1.10f, 0.50f, 0.15f, 0.75f, 1.5f,
                        new SimVec3(0.08f, 0.11f, 0.38f), new SimVec3(-0.18f, 0f, 0.25f), NoOffset,
                        0f, 0.50f, 45f, 0.35f, 0.15f, 0.30f, 0.20f, true);
                case AnimActionKind.Cross:
                    return new ActionClipProfile(kind, 1.10f, 0.50f, 0.15f, 0.75f, 1.5f,
                        new SimVec3(0.12f, 0.11f, 0.35f), new SimVec3(-0.20f, 0f, 0.20f), NoOffset,
                        -15f, 0.50f, 60f, 0.35f, 0.15f, 0.30f, 0.20f, true);
                case AnimActionKind.FirstTouch:
                    return new ActionClipProfile(kind, 0.60f, 0.22f, 0.08f, 0.8f, 1.8f,
                        new SimVec3(0.10f, 0.11f, 0.30f), new SimVec3(-0.14f, 0f, 0.15f), NoOffset,
                        0f, 0.35f, 60f, 0.20f, 0.08f, 0.20f, 0.10f, false);
                case AnimActionKind.Header:
                    return new ActionClipProfile(kind, 1.00f, 0.45f, 0.15f, 0.7f, 1.5f,
                        new SimVec3(0f, 1.80f, 0.18f), NoOffset, NoOffset,
                        0f, 0.60f, 60f, 0.35f, 0.10f, 0.25f, 0f, false);
                case AnimActionKind.StandingTackle:
                    return new ActionClipProfile(kind, 0.90f, 0.35f, 0.10f, 0.8f, 1.6f,
                        new SimVec3(0.15f, 0.11f, 0.65f), new SimVec3(-0.12f, 0f, 0.05f), NoOffset,
                        0f, 0.45f, 45f, 0.30f, 0.15f, 0.25f, 0.15f, false);
                case AnimActionKind.SlideTackle:
                    // Plays Quaternius UAL2 "Slide_Start" (0.833 s). Contact time is an estimate: verify in the Editor.
                    return new ActionClipProfile(kind, 0.83f, 0.55f, 0.15f, 0.8f, 1.4f,
                        new SimVec3(0.10f, 0.11f, 0.95f), NoOffset, NoOffset,
                        0f, 0.70f, 30f, 0.40f, 0.30f, 0.30f, 0f, false);
                case AnimActionKind.GoalkeeperDive:
                    return new ActionClipProfile(kind, 1.40f, 0.40f, 0.12f, 0.8f, 1.6f,
                        new SimVec3(1.70f, 0.70f, 0.10f), NoOffset, NoOffset,
                        0f, 0.80f, 25f, 0.35f, 0.20f, 0.30f, 0f, false);
                case AnimActionKind.GoalkeeperThrow:
                    // Plays Quaternius UAL2 "OverhandThrow" (1.333 s); release frame estimated.
                    return new ActionClipProfile(kind, 1.33f, 0.70f, 0.20f, 0.8f, 1.4f,
                        new SimVec3(0.25f, 1.90f, 0.20f), NoOffset, NoOffset,
                        0f, 0.20f, 30f, 0.30f, 0.10f, 0.25f, 0f, false);
                case AnimActionKind.Fall:
                    // Plays Quaternius UAL2 "Hit_Knockback" (0.833 s), then GetUp ("LayToIdle").
                    return new ActionClipProfile(kind, 0.83f, 0f, 0f, 0.8f, 1.3f,
                        NoOffset, NoOffset, NoOffset, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);
                case AnimActionKind.Celebrate:
                    // Plays Quaternius UAL1 "Dance_Loop" (1 s loop) for 2.5 s.
                    return new ActionClipProfile(kind, 2.50f, 0f, 0f, 1f, 1f,
                        NoOffset, NoOffset, NoOffset, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);
                default:
                    return new ActionClipProfile(AnimActionKind.None, 0f, 0f, 0f, 1f, 1f,
                        NoOffset, NoOffset, NoOffset, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);
            }
        }
    }
}
