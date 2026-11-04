namespace OpenFifa.Core
{
    /// <summary>
    /// What one player wants to do this tick. Humans (input layer) and bots (AI layer) produce the same struct,
    /// so recorded intents replay exactly.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>World-space flat move direction. Magnitude 0..1 is the stick throttle (clamped to 1).</summary>
        public SimVec3 Move;

        public bool Sprint;

        public IntentAction Action;

        /// <summary>Kick power 0..1 (the power bar). Ignored by tackles, jockey and shield.</summary>
        public float Power;

        /// <summary>World-space flat aim direction for the action. Zero = use the player's facing.</summary>
        public SimVec3 Aim;

        /// <summary>
        /// Optional receiver for passes (player id 0-21). -1 lets the sim pick the best teammate in the aim cone
        /// (assisted passing). Ignored by shots.
        /// </summary>
        public int TargetPlayerId;

        /// <summary>
        /// Kick variant (chip, low shot, driven pass, lobbed through...). <see cref="KickStyle.Default"/> (the zero value)
        /// keeps the plain action, so intents built without it behave as before.
        /// </summary>
        public KickStyle Style;

        public static PlayerIntent Idle => new PlayerIntent { TargetPlayerId = -1 };

        public static PlayerIntent MoveOnly(SimVec3 move, bool sprint = false) =>
            new PlayerIntent { Move = move, Sprint = sprint, TargetPlayerId = -1 };

        public static PlayerIntent Kick(IntentAction action, float power, SimVec3 aim, int target = -1) =>
            new PlayerIntent { Action = action, Power = power, Aim = aim, TargetPlayerId = target };

        public static PlayerIntent Kick(IntentAction action, float power, SimVec3 aim, int target, KickStyle style) =>
            new PlayerIntent { Action = action, Power = power, Aim = aim, TargetPlayerId = target, Style = style };

        public bool IsKick => IsKickAction(Action);

        public static bool IsKickAction(IntentAction a) => a >= IntentAction.Pass && a <= IntentAction.DrivenShot;

        public static bool IsShotAction(IntentAction a) =>
            a == IntentAction.Shoot || a == IntentAction.FinesseShot || a == IntentAction.DrivenShot;

        public static bool IsPassAction(IntentAction a) =>
            a == IntentAction.Pass || a == IntentAction.ThroughPass || a == IntentAction.Lob || a == IntentAction.Cross;
    }
}
