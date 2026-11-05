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

        // ---- Kick variants (KickStyle). ----
        /// <summary>Driven ground pass: extra arrival speed (m/s) over the plain pass, and its own speed cap.</summary>
        public float DrivenPassArrivalBonus = 6f;
        public float DrivenPassMaxSpeed = 30f;
        /// <summary>Driven lob: a flatter lofted ball.</summary>
        public float DrivenLobElevationDeg = 20f;
        /// <summary>Lobbed through ball: lofted into the receiver's run (led by ThroughLeadDistance).</summary>
        public float LobbedThroughElevationDeg = 32f;
        /// <summary>Chip shot: high arc with backspin, solved to drop on the target point on the goal line.</summary>
        public float ChipElevationDeg = 38f;
        public float ChipBackSpin = 12f;
        public float ChipMaxSpeed = 24f;
        /// <summary>Low shot: launch angle (no power-dependent rise) and pace relative to a normal shot.</summary>
        public float LowShotElevationDeg = 0.5f;
        public float LowShotSpeedFactor = 0.92f;

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

        /// <summary>
        /// First touch on a ball above the grass (chest, thigh, a bouncing ball): it is cushioned down from where it was
        /// met rather than snapped to the turf. Downward speed (m/s) of a clean cushion.
        /// </summary>
        public float FirstTouchCushionDropSpeed = 0.6f;

        /// <summary>Upward pop (m/s per unit of touch error) of a heavy touch: the ball bobbles up off the foot or body.</summary>
        public float FirstTouchPopPerError = 1.1f;

        /// <summary>Touch error below which a ground ball is killed flat (no bobble).</summary>
        public float FirstTouchGroundPopThreshold = 0.8f;

        /// <summary>Backspin (rad/s) a cushioned touch puts on the ball (it checks up rather than running on).</summary>
        public float FirstTouchCushionBackSpin = 8f;

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
        /// <summary>
        /// Law 12: an opponent may not prevent the keeper releasing the ball from his hands. Opponents within this
        /// radius (m) of the ball when it leaves the keeper's hands cannot take it for <see cref="GkReleaseProtectSeconds"/>
        /// (it still bounces off their bodies).
        /// </summary>
        public float GkReleaseProtectRadius = 2f;
        public float GkReleaseProtectSeconds = 0.25f;

        // ---- Bodies (ball-vs-player deflection and player-player separation). ----
        /// <summary>Radius (m) of a player's body capsule for ball deflection and shielding.</summary>
        public float PlayerBodyRadius = 0.3f;

        /// <summary>
        /// Personal-space radius (m) for player-player separation: two players are kept 2x this apart. Larger than the
        /// body capsule because the animated body (arms, stride, lean) reaches ~0.35-0.4 m; at 0.3 m the meshes interpenetrate.
        /// </summary>
        public float PlayerSeparationRadius = 0.38f;

        /// <summary>Top of an outfield player's body capsule (m): the head.</summary>
        public float PlayerHeadHeight = 1.9f;

        /// <summary>Restitution of the ball off a body (bodies are soft) and the tangential speed kept.</summary>
        public float BodyRestitution = 0.35f;
        public float BodyTangentialKeep = 0.75f;

        /// <summary>
        /// Share of the glancing friction impulse off a body that becomes spin (0 = none, 1 = a rigid contact). A ball
        /// clipping a shin or shoulder spins away and curls/skids accordingly.
        /// </summary>
        public float BodyDeflectionSpinTransfer = 0.6f;

        /// <summary>Knuckle shot (<see cref="KickStyle.Knuckle"/>): pace bonus over a plain shot and the residual spin (rad/s).</summary>
        public float KnuckleShotSpeedFactor = 1.05f;
        public float KnuckleShotResidualSpin = 1.5f;

        // ---- Skill moves (SkillMoveCatalog has the per-move shapes). ----
        /// <summary>The ball must be within this flat distance of the carrier's feet to start a skill move.</summary>
        public float SkillStartReach = 1.3f;
        /// <summary>Defenders within this radius (m) of the carrier, and not behind him, can bite on a fake.</summary>
        public float SkillBiteRadius = 4.0f;
        /// <summary>Bite logit at full fake strength for an even matchup, and how much skill vs reading moves it.</summary>
        public float SkillBiteBase = 0.4f;
        public float SkillBiteSkillScale = 22f;
        /// <summary>Bite logit shift for a jockeying defender (he is set and side-on, harder to sell).</summary>
        public float SkillBiteJockeyMalus = 0.5f;
        /// <summary>How long a bitten defender stays wrong-footed (s), and his lunge toward the fake (m/s).</summary>
        public float WrongFootSeconds = 0.5f;
        public float WrongFootLunge = 1.6f;
        /// <summary>Turn rate and acceleration multiplier while wrong-footed or off balance.</summary>
        public float WrongFootResponsiveness = 0.3f;
        /// <summary>Tackle win-logit penalty for a wrong-footed or off-balance tackler.</summary>
        public float WrongFootTackleMalus = 1.2f;
        /// <summary>Release error sd (degrees) at zero quality, and extra loose-touch lead (m) for a botched move.</summary>
        public float SkillReleaseErrorDeg = 25f;
        public float SkillBotchLead = 1.6f;
        /// <summary>Gap to the next dribble touch after the release (s).</summary>
        public float SkillReleaseTouchGap = 0.3f;

        // ---- Body contact (shoulder to shoulder, collisions). ----
        /// <summary>
        /// Share of the closing speed removed on contact (0 = bodies pass through, 1 = fully inelastic). Players are not
        /// rigid: some of the approach is absorbed by leaning in.
        /// </summary>
        public float BodyContactAbsorb = 0.85f;
        /// <summary>Closing speed (m/s) below which a contact is just a lean (no stumble, no event).</summary>
        public float BodyContactMinSpeed = 1.2f;
        /// <summary>A contact that only holds the player up is reported (BodyContact, Detail 0) from this closing speed (m/s).</summary>
        public float BodyContactEventSpeed = 2.5f;
        /// <summary>Speed change (m/s) a 0-balance / 99-balance player absorbs before stumbling.</summary>
        public float StumbleDeltaVMin = 2.0f;
        public float StumbleDeltaVRange = 2.5f;
        /// <summary>Speed change (m/s) above the stumble limit at which a player goes down instead.</summary>
        public float FallDeltaVOver = 2.5f;
        /// <summary>How long a stumble (off balance) and a fall (on the ground) last (s).</summary>
        public float StumbleSeconds = 0.45f;
        public float ContactFallGroundTime = 1.1f;
        /// <summary>
        /// Chance a stumbling carrier loses the ball: (speed change over his limit) / FallDeltaVOver, times
        /// (SpillControlBase - ball control). A good dribbler rides most knocks with the ball still at his feet.
        /// </summary>
        public float SpillControlBase = 1.1f;
        /// <summary>A stumbling carrier's ball spills this far ahead (m/s over his speed); low ball control loses more.</summary>
        public float StumbleSpillSpeed = 2.5f;
        /// <summary>Strength bonus to effective mass while shielding (fraction at 99 strength).</summary>
        public float ShieldMassBonus = 0.6f;

        // ---- Tackle body outcomes. ----
        /// <summary>Chance a carrier beaten by a slide goes down (before balance), and for a standing tackle.</summary>
        public float SlideWinFallChance = 0.55f;
        public float StandingWinFallChance = 0.12f;
        public float TackleFallGroundTime = 1.0f;

        /// <summary>Speed (m/s) at which a caught ball is gathered from the catch point into the hands.</summary>
        public float GkGatherSpeed = 6f;

        public SimActionConfig Clone() => (SimActionConfig)MemberwiseClone();
    }
}
