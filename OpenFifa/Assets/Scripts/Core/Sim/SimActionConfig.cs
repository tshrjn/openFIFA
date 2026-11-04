namespace OpenFifa.Core
{
    /// <summary>
    /// Tuning for kicks, error cones, dribbling, tackles and goalkeeper saves. All values are game-feel
    /// APPROXIMATIONS, not measured data; they exist so balance can change without code edits.
    /// </summary>
    public sealed class SimActionConfig
    {
        // ---- Wind-up (seconds from request to contact frame); animation syncs to the KickWindup event. ----
        public float PassWindup = 0.10f;
        public float PassWindupPerPower = 0.08f;
        public float LobWindup = 0.18f;
        public float CrossWindup = 0.22f;
        public float ShotWindup = 0.20f;
        public float ShotWindupPerPower = 0.15f;
        public float FinesseWindup = 0.24f;
        public float DrivenWindup = 0.22f;
        public float TackleWindup = 0.15f;
        public float SlideWindup = 0.12f;

        /// <summary>Recovery after a kick before the kicker can act again (s).</summary>
        public float KickRecovery = 0.15f;

        /// <summary>The kicker cannot re-control the ball for this long after the contact frame (s).</summary>
        public float KickerControlCooldown = 0.30f;

        // ---- Error cones: sd (degrees) before skill, pressure, weak foot and power scaling. ----
        public float PassErrorDeg = 5f;
        public float ThroughErrorDeg = 6f;
        public float LobErrorDeg = 7f;
        public float CrossErrorDeg = 8f;
        public float ShotErrorDeg = 7f;
        public float FinesseErrorDeg = 6f;
        public float DrivenErrorDeg = 8f;

        /// <summary>Error multiplier at full pressure (opponent touching distance), reduced by composure.</summary>
        public float PressureErrorFactor = 0.9f;

        /// <summary>Error grows with power^2 by this factor (power shots are wilder).</summary>
        public float PowerErrorFactor = 0.8f;

        /// <summary>Error multiplier per missing weak-foot star (5 stars = 1x).</summary>
        public float WeakFootErrorPerStar = 0.22f;

        public float HeaderErrorFactor = 1.6f;

        /// <summary>Distance (m) at which an opponent applies zero pressure; full pressure at 0.5 m.</summary>
        public float PressureRadius = 3.0f;

        // ---- Kick speeds (m/s). ----
        public float PassArrivalSpeedMin = 4f;
        public float PassArrivalSpeedRange = 5f;
        public float PassMaxSpeed = 24f;
        public float UnassistedPassMinDistance = 6f;
        public float UnassistedPassDistanceRange = 34f;
        public float ThroughLeadDistance = 5f;
        public float LobElevationDeg = 40f;
        public float CrossElevationDeg = 22f;
        public float CrossSideSpin = 25f;
        public float ShotSpeedMin = 15f;
        public float ShotSpeedRange = 17f;
        public float ShotElevationDeg = 3f;
        public float ShotElevationPowerDeg = 9f;
        public float ShotTopSpin = 25f;
        public float FinesseSpeedMin = 14f;
        public float FinesseSpeedRange = 9f;
        public float FinesseSideSpin = 60f;
        public float FinesseElevationDeg = 6f;
        public float DrivenSpeedMin = 18f;
        public float DrivenSpeedRange = 14f;
        public float DrivenElevationDeg = 1.5f;
        public float DrivenTopSpin = 35f;
        public float HeaderSpeedFactor = 0.55f;

        /// <summary>Kick reach: ball centre within this flat distance and height at the contact frame.</summary>
        public float KickReach = 1.1f;
        public float KickMaxHeight = 0.9f;
        public float HeaderMinHeight = 1.2f;
        public float HeaderMaxHeight = 2.6f;

        /// <summary>Assisted pass: max angle (deg) between aim and a teammate for auto-targeting.</summary>
        public float PassAssistConeDeg = 35f;

        // ---- Dribbling and possession. ----
        /// <summary>Flat radius (m) within which a player can control a loose ground ball.</summary>
        public float ControlRadius = 0.85f;
        public float ControlMaxHeight = 1.9f;

        /// <summary>Owner keeps possession while the ball is within this flat distance.</summary>
        public float LooseRadius = 2.8f;

        /// <summary>Touch interval at walking pace: lerp(slow, fast, dribbling).</summary>
        public float TouchIntervalSlow = 0.55f;
        public float TouchIntervalFast = 0.32f;

        /// <summary>Touch interval multiplier while sprinting (fewer, longer touches).</summary>
        public float SprintTouchIntervalFactor = 1.5f;

        /// <summary>Lead distance of the ball ahead of the feet after a touch.</summary>
        public float CloseTouchLead = 0.45f;
        public float SprintTouchLead = 1.4f;

        /// <summary>Extra lead added at sprint for a 0-dribbling player (loose touches).</summary>
        public float SprintTouchLeadLowSkill = 1.6f;

        /// <summary>Attachment cap: a touch can never send the ball faster than player speed + this.</summary>
        public float MaxTouchSpeedOverPlayer = 3.0f;

        /// <summary>A sharp change of direction (deg) triggers an early touch.</summary>
        public float RetouchTurnDeg = 40f;
        public float MinTouchInterval = 0.12f;

        /// <summary>First touch: incoming relative speed (m/s) a 0 / 99 control player can kill cleanly.</summary>
        public float FirstTouchCleanSpeedMin = 6f;
        public float FirstTouchCleanSpeedRange = 12f;
        public float FirstTouchErrorPerSpeed = 0.18f;

        // ---- Tackles. ----
        public float StandingTackleRange = 1.6f;
        public float SlideTackleRange = 1.4f;
        public float SlideSpeed = 6.5f;
        public float SlideDuration = 0.5f;
        public float SlideGroundTime = 0.9f;
        public float TackleMissStagger = 0.5f;
        public float TackledStagger = 0.35f;
        public float TackleSkillScale = 15f;
        public float JockeyTackleBonus = 0.4f;
        public float ShieldTackleMalus = 0.6f;
        public float FoulBaseStanding = 0.05f;
        public float FoulBaseSlide = 0.12f;
        public float FoulFromBehind = 0.45f;
        public float FoulOnMissedBall = 0.25f;
        public float YellowFromBehindSlide = 0.5f;
        public float YellowFromBehindStanding = 0.2f;
        public float LostTackleControlCooldown = 0.6f;

        // ---- Goalkeeper. ----
        public float GkStandingReach = 0.9f;
        public float GkDiveReachRange = 1.6f;
        public float GkMaxReachHeight = 2.5f;
        public float GkParrySpeedFactor = 0.35f;

        public SimActionConfig Clone() => (SimActionConfig)MemberwiseClone();
    }
}
