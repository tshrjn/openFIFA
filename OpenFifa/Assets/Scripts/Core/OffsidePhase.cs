using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Snapshot taken when a player plays or touches the ball: which of the
    /// player's team-mates were in an offside position at that instant.
    /// Stays live until the next touch that ends it (see <see cref="OffsideLogic"/>).
    /// </summary>
    public class OffsidePhase
    {
        private readonly Dictionary<int, float> _flagged = new Dictionary<int, float>();

        /// <summary>Team whose player played the ball.</summary>
        public TeamIdentifier AttackingTeam { get; }

        /// <summary>Player who played or touched the ball.</summary>
        public int PasserId { get; }

        /// <summary>World X of the ball's point nearest the opponents' goal line.</summary>
        public float BallLineX { get; }

        /// <summary>World X of the second-last opponent's point nearest the goal line.</summary>
        public float DefenderLineX { get; }

        /// <summary>Player ids in an offside position at the snapshot.</summary>
        public IReadOnlyCollection<int> FlaggedPlayerIds => _flagged.Keys;

        public int FlaggedCount => _flagged.Count;

        public OffsidePhase(TeamIdentifier attackingTeam, int passerId, float ballLineX, float defenderLineX)
        {
            AttackingTeam = attackingTeam;
            PasserId = passerId;
            BallLineX = ballLineX;
            DefenderLineX = defenderLineX;
        }

        /// <summary>Record a player in an offside position and their foremost world X.</summary>
        public void Flag(int playerId, float attackerLineX)
        {
            _flagged[playerId] = attackerLineX;
        }

        public bool IsFlagged(int playerId)
        {
            return _flagged.ContainsKey(playerId);
        }

        /// <summary>World X of the flagged player's foremost point, or NaN if not flagged.</summary>
        public float GetAttackerLineX(int playerId)
        {
            return _flagged.TryGetValue(playerId, out float x) ? x : float.NaN;
        }
    }
}
