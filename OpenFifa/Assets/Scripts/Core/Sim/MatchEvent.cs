namespace OpenFifa.Core
{
    /// <summary>One entry in the sim's event stream, for commentary, UI, audio and stats. Plain data.</summary>
    public struct MatchEvent
    {
        public MatchEventType Type;
        public int Tick;

        /// <summary>Match clock in sim seconds since the start of the current half.</summary>
        public float Time;

        public int Half;

        /// <summary>Main player id, or -1.</summary>
        public int PlayerId;

        /// <summary>Secondary player id (receiver, fouled player, passer...), or -1.</summary>
        public int OtherPlayerId;

        public SimTeam Team;
        public SimVec3 Position;

        /// <summary>Ball speed (m/s) where relevant.</summary>
        public float Speed;

        /// <summary>Type-specific value: xG for Shot, quality for touches, offside margin.</summary>
        public float Value;

        /// <summary>Type-specific integer: card level, caught/parried, won/missed, contact tick, restart type.</summary>
        public int Detail;

        public IntentAction Action;

        /// <summary>Kick variant for KickWindup, Pass and Shot events (Default otherwise).</summary>
        public KickStyle Style;

        public override string ToString() =>
            $"[{Tick}] {Type} p{PlayerId} o{OtherPlayerId} {Team} {Position} v={Value:F2} d={Detail}";
    }
}
