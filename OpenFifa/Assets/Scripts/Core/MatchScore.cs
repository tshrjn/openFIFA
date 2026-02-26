using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# match score tracker.
    /// Tracks goals for TeamA and TeamB.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MatchScore
    {
        private int _scoreA;
        private int _scoreB;

        /// <summary>TeamA score.</summary>
        public int ScoreA => _scoreA;

        /// <summary>TeamB score.</summary>
        public int ScoreB => _scoreB;

        /// <summary>Fired when a score changes. Args: (team, newScore).</summary>
        public event Action<TeamIdentifier, int> OnScoreChanged;

        /// <summary>
        /// Get the score for a specific team.
        /// </summary>
        public int GetScore(TeamIdentifier team)
        {
            return team == TeamIdentifier.TeamA ? _scoreA : _scoreB;
        }

        /// <summary>
        /// Add a goal for the specified team.
        /// </summary>
        public void AddGoal(TeamIdentifier team)
        {
            if (team == TeamIdentifier.TeamA)
            {
                _scoreA++;
                OnScoreChanged?.Invoke(TeamIdentifier.TeamA, _scoreA);
            }
            else
            {
                _scoreB++;
                OnScoreChanged?.Invoke(TeamIdentifier.TeamB, _scoreB);
            }
        }

        /// <summary>
        /// Reset all scores to zero.
        /// </summary>
        public void Reset()
        {
            _scoreA = 0;
            _scoreB = 0;
        }

        /// <summary>
        /// Get formatted score display string.
        /// </summary>
        public string GetScoreDisplay()
        {
            return $"{_scoreA} - {_scoreB}";
        }
    }
}
