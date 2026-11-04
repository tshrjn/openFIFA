namespace OpenFifa.Core
{
    /// <summary>
    /// Mutable per-player sim state. A struct so snapshots copy it by value. Ids 0-10 are Home
    /// (0 = goalkeeper), 11-21 are Away (11 = goalkeeper).
    /// </summary>
    public struct SimPlayer
    {
        public int Id;
        public SimTeam Team;
        public SimVec3 Position;
        public SimVec3 Velocity;

        /// <summary>Body facing as yaw in radians (0 = +Z, positive toward +X). See <see cref="SimMath.Yaw"/>.</summary>
        public float Facing;

        /// <summary>Current stamina 0..1.</summary>
        public float Stamina;

        public SimPlayerActionState ActionState;

        /// <summary>The action being wound up (valid while ActionState is WindUp or Sliding).</summary>
        public IntentAction PendingAction;
        public float PendingPower;
        public SimVec3 PendingAim;
        public int PendingTarget;
        public KickStyle PendingStyle;

        /// <summary>Tick on which the pending action resolves (the contact frame).</summary>
        public int ContactTick;

        /// <summary>Tick on which the current non-Free state ends.</summary>
        public int StateEndTick;

        /// <summary>The player cannot gain control of the ball before this tick.</summary>
        public int ControlBlockedUntilTick;

        /// <summary>Earliest tick for the next dribble touch.</summary>
        public int NextTouchTick;

        /// <summary>Tick of the last dribble touch.</summary>
        public int LastTouchTick;

        /// <summary>Yaw of the last dribble touch direction (for early retouch on sharp turns).</summary>
        public float LastTouchYaw;

        public bool Jockeying;
        public bool Shielding;
        public bool Sprinting;
        public int YellowCards;
        public bool SentOff;

        public bool IsFree => ActionState == SimPlayerActionState.Free;

        public SimVec3 FacingDir => SimMath.FromYaw(Facing);

        public float Speed => Velocity.Flat.Magnitude;
    }
}
