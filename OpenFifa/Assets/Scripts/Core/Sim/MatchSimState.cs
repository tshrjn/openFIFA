namespace OpenFifa.Core
{
    /// <summary>
    /// All mutable match state. <see cref="MatchSim.CaptureSnapshot"/> returns a deep copy and
    /// <see cref="MatchSim.RestoreSnapshot"/> puts one back, so replays and rewinds are exact.
    /// </summary>
    public sealed class MatchSimState
    {
        public const int PlayerCount = 22;

        public int Tick;
        public int Half = 1;

        /// <summary>Sim seconds elapsed in the current half.</summary>
        public float Clock;

        public SimMatchPhase Phase;

        /// <summary>Tick at which the current Stoppage / HalfTime ends, or the restart is auto-taken.</summary>
        public int PhaseEndTick;

        public RestartType PendingRestart;
        public SimTeam RestartTeam = SimTeam.None;
        public SimVec3 RestartSpot;
        public int RestartTakerId = -1;

        /// <summary>The restart type of the most recent kick (None for open play); used by offside.</summary>
        public RestartType LastKickRestart;

        public int HomeScore;
        public int AwayScore;

        public SimBall Ball;
        public SimPlayer[] Players = new SimPlayer[PlayerCount];
        public SimRng Rng;

        /// <summary>Player in possession (dribbling or holding), -1 if loose.</summary>
        public int Owner = -1;
        public bool KeeperHolding;
        public int LastTouchPlayer = -1;
        public SimTeam LastTouchTeam = SimTeam.None;
        public SimTeam PossessionTeam = SimTeam.None;

        /// <summary>Bit i set: player i was in an offside position at the last kick by a teammate.</summary>
        public int OffsideMask;
        public SimVec3[] OffsidePositions = new SimVec3[PlayerCount];
        public float[] OffsideMargins = new float[PlayerCount];
        public int OffsidePasser = -1;

        public bool PassLive;
        public int PassFrom = -1;
        public int PassTarget = -1;
        public SimTeam PassTeam = SimTeam.None;
        public int AssistCandidate = -1;

        /// <summary>
        /// A shot is on its way. Stays true through keeper saves (parries) and deflections off opponents, so a parried
        /// ball that still goes in is the shooter's goal; any controlled touch or a teammate's deflection ends it.
        /// </summary>
        public bool ShotLive;
        public int Shooter = -1;

        /// <summary>
        /// Taker of a penalty whose outcome is still open (-1 none): set at the contact frame, cleared once the ball
        /// is dead, stops, is held, or is touched by anyone other than the defending keeper. The half cannot end
        /// while it is set (a penalty at the whistle is played out).
        /// </summary>
        public int PenaltyKicker = -1;
        public int PenaltyKickTick;

        /// <summary>
        /// The last kick was an indirect free kick or a throw-in and nobody else has touched the ball since:
        /// it cannot score directly (goal kick, or a corner if it went into the kicker's own goal).
        /// </summary>
        public bool IndirectRestartLive;

        public bool PenaltyInProgress => PenaltyKicker >= 0;

        /// <summary>Incremented on every kick; a keeper gets one save attempt per kick.</summary>
        public int KickSerial;
        public int HomeSaveSerial = -1;
        public int AwaySaveSerial = -1;

        public int HomeShots;
        public int AwayShots;
        public int HomePossessionTicks;
        public int AwayPossessionTicks;

        public MatchSimState Clone()
        {
            var c = (MatchSimState)MemberwiseClone();
            c.Players = (SimPlayer[])Players.Clone();
            c.OffsidePositions = (SimVec3[])OffsidePositions.Clone();
            c.OffsideMargins = (float[])OffsideMargins.Clone();
            c.Rng = Rng?.Clone();
            return c;
        }
    }
}
