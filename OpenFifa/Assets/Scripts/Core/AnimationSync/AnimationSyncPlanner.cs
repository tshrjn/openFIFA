namespace OpenFifa.Core
{
    /// <summary>What the sim says is about to happen, in sim units (metres, seconds, degrees).</summary>
    public readonly struct ActionSyncRequest
    {
        public readonly AnimActionKind Kind;
        public readonly ActionSide Side;
        /// <summary>Current sim time (s).</summary>
        public readonly float Now;
        /// <summary>Sim time of the contact tick (s).</summary>
        public readonly float ContactTime;
        public readonly SimVec3 RootNow;
        public readonly SimVec3 RootAtContact;
        public readonly float YawAtContact;
        public readonly SimVec3 BallAtContact;
        public readonly SimVec3 ActionDirection;
        public readonly float Speed;
        public readonly ActionSide NextFootfall;
        public readonly float GroundY;

        public ActionSyncRequest(AnimActionKind kind, ActionSide side, float now, float contactTime,
            SimVec3 rootNow, SimVec3 rootAtContact, float yawAtContact, SimVec3 ballAtContact, SimVec3 actionDirection,
            float speed, ActionSide nextFootfall, float groundY = 0f)
        {
            Kind = kind;
            Side = side;
            Now = now;
            ContactTime = contactTime;
            RootNow = rootNow;
            RootAtContact = rootAtContact;
            YawAtContact = yawAtContact;
            BallAtContact = ballAtContact;
            ActionDirection = actionDirection;
            Speed = speed;
            NextFootfall = nextFootfall;
            GroundY = groundY;
        }

        /// <summary>Build a request from tick numbers.</summary>
        public static ActionSyncRequest FromTicks(AnimActionKind kind, ActionSide side, long nowTick, long contactTick, float tickRateHz,
            SimVec3 rootNow, SimVec3 rootAtContact, float yawAtContact, SimVec3 ballAtContact, SimVec3 actionDirection,
            float speed, ActionSide nextFootfall, float groundY = 0f)
        {
            return new ActionSyncRequest(kind, side, ActionTimingSolver.TickToSeconds(nowTick, tickRateHz),
                ActionTimingSolver.TickToSeconds(contactTick, tickRateHz), rootNow, rootAtContact, yawAtContact,
                ballAtContact, actionDirection, speed, nextFootfall, groundY);
        }
    }

    /// <summary>Everything the presentation layer needs to play one action in sync with the sim.</summary>
    public sealed class ActionSyncPlan
    {
        public ActionSyncRequest Request { get; }
        public ActionClipProfile Profile { get; }
        public ActionTimingPlan Timing { get; }
        public WarpPlan Warp { get; }
        public ContactIkTargets Ik { get; }
        public bool HasStridePlan { get; }
        public StridePlan Stride { get; }

        public ActionSyncPlan(ActionSyncRequest request, ActionClipProfile profile, ActionTimingPlan timing, WarpPlan warp,
            ContactIkTargets ik, bool hasStridePlan, StridePlan stride)
        {
            Request = request;
            Profile = profile;
            Timing = timing;
            Warp = warp;
            Ik = ik;
            HasStridePlan = hasStridePlan;
            Stride = stride;
        }

        /// <summary>True when timing and warp both reach the ball within tolerance.</summary>
        public bool Feasible => Timing.Feasible && Warp.Feasible && (!HasStridePlan || Stride.Feasible);

        /// <summary>Sim time when the action is over (clip finished and warp released).</summary>
        public float EndTime
        {
            get
            {
                float clipEnd = Timing.StartTime + (Profile.ClipLength - Timing.StartOffset) / (Timing.PlaybackRate > 0f ? Timing.PlaybackRate : 1f);
                return clipEnd > Warp.ReleaseEnd ? clipEnd : Warp.ReleaseEnd;
            }
        }
    }

    /// <summary>Combines timing, warp, stride and IK solvers. Pure and deterministic.</summary>
    public sealed class AnimationSyncPlanner
    {
        private readonly System.Func<AnimActionKind, ActionClipProfile> _profiles;
        public StrideAdjuster Stride { get; } = new StrideAdjuster();
        public float BallRadius = ContactIkSolver.DefaultBallRadius;
        public float AnkleHeight = ContactIkSolver.DefaultAnkleHeight;

        public AnimationSyncPlanner(System.Func<AnimActionKind, ActionClipProfile> profiles = null)
        {
            _profiles = profiles ?? ActionClipLibrary.Default;
        }

        public ActionClipProfile ProfileFor(AnimActionKind kind) => _profiles(kind);

        public ActionSyncPlan Plan(in ActionSyncRequest r)
        {
            ActionClipProfile profile = _profiles(r.Kind);
            ActionTimingPlan timing = ActionTimingSolver.Solve(profile, r.Now, r.ContactTime);
            WarpPlan warp = KickWarpSolver.Solve(profile, r.Side, r.RootAtContact, r.YawAtContact, r.BallAtContact,
                r.ActionDirection, r.ContactTime);
            ContactIkTargets ik = ContactIkSolver.Solve(profile, r.Side, warp, r.BallAtContact, r.ActionDirection, r.GroundY,
                BallRadius, AnkleHeight);

            bool hasStride = false;
            StridePlan stride = default;
            if (profile.UsesRunUp && ik.PlantEffector != ContactEffector.None)
            {
                // Run-up covers the time until the clip starts plus the clip's pre-plant part; plan footfalls up to the plant.
                float timeToPlant = ik.PlantTime - r.Now;
                float distance = (ik.PlantTarget - r.RootNow).Flat.Magnitude;
                ActionSide plantFoot = ik.PlantEffector == ContactEffector.LeftFoot ? ActionSide.Left : ActionSide.Right;
                stride = Stride.Plan(distance, timeToPlant, r.Speed, r.NextFootfall, plantFoot);
                hasStride = true;
            }
            return new ActionSyncPlan(r, profile, timing, warp, ik, hasStride, stride);
        }
    }
}
