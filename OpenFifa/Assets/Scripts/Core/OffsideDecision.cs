namespace OpenFifa.Core
{
    /// <summary>
    /// Result of an offside check. When <see cref="IsOffence"/> is true, an indirect
    /// free kick goes to <see cref="DefendingTeam"/> at (FreeKickX, FreeKickZ).
    /// Line positions are depths along the pitch-length axis from the moment the ball was played,
    /// for the VAR-style overlay: world X in the 4v4 scene, world Z for the 11v11 match sim
    /// (see <see cref="OffsidePitchFrame"/>).
    /// </summary>
    public class OffsideDecision
    {
        /// <summary>Shared "no offence" result.</summary>
        public static readonly OffsideDecision None = new OffsideDecision();

        public bool IsOffence { get; }
        public OffsideReason Reason { get; }
        public TeamIdentifier OffenderTeam { get; }
        public int OffenderId { get; }
        public int PasserId { get; }
        public float FreeKickX { get; }
        public float FreeKickZ { get; }
        /// <summary>Depth (pitch-length axis) of the offender's foremost body point when the ball was played.</summary>
        public float AttackerLineX { get; }
        /// <summary>Depth (pitch-length axis) of the second-last opponent's body point nearest the goal line.</summary>
        public float DefenderLineX { get; }
        /// <summary>Depth (pitch-length axis) of the ball's point nearest the goal line when it was played.</summary>
        public float BallLineX { get; }

        public TeamIdentifier DefendingTeam =>
            OffenderTeam == TeamIdentifier.TeamA ? TeamIdentifier.TeamB : TeamIdentifier.TeamA;

        private OffsideDecision()
        {
            IsOffence = false;
            Reason = OffsideReason.None;
            OffenderId = -1;
            PasserId = -1;
        }

        public OffsideDecision(OffsideReason reason, TeamIdentifier offenderTeam, int offenderId,
            int passerId, float freeKickX, float freeKickZ,
            float attackerLineX, float defenderLineX, float ballLineX)
        {
            IsOffence = true;
            Reason = reason;
            OffenderTeam = offenderTeam;
            OffenderId = offenderId;
            PasserId = passerId;
            FreeKickX = freeKickX;
            FreeKickZ = freeKickZ;
            AttackerLineX = attackerLineX;
            DefenderLineX = defenderLineX;
            BallLineX = ballLineX;
        }
    }
}
