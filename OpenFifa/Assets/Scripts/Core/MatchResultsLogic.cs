using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Record of a single goal.
    /// </summary>
    public struct GoalRecord
    {
        public string ScorerName;
        public float MatchTime;
        public string TeamName;
    }

    /// <summary>
    /// Pure C# match results logic.
    /// Computes final score display, man of the match, and duration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MatchResultsLogic
    {
        private readonly string _teamAName;
        private readonly int _teamAScore;
        private readonly string _teamBName;
        private readonly int _teamBScore;
        private readonly float _matchDuration;
        private readonly List<GoalRecord> _goals;

        /// <summary>Match duration in seconds.</summary>
        public float MatchDuration => _matchDuration;

        /// <summary>Number of recorded goals.</summary>
        public int GoalCount => _goals.Count;

        /// <summary>Final score display string.</summary>
        public string FinalScoreDisplay => $"{_teamAName} {_teamAScore} - {_teamBScore} {_teamBName}";

        /// <summary>Winning team name, or "Draw" if tied.</summary>
        public string WinnerTeam
        {
            get
            {
                if (_teamAScore > _teamBScore) return _teamAName;
                if (_teamBScore > _teamAScore) return _teamBName;
                return "Draw";
            }
        }

        /// <summary>Duration formatted as MM:SS.</summary>
        public string DurationDisplay
        {
            get
            {
                int totalSeconds = (int)_matchDuration;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
        }

        public MatchResultsLogic(string teamAName, int teamAScore, string teamBName, int teamBScore, float matchDuration)
        {
            _teamAName = teamAName;
            _teamAScore = teamAScore;
            _teamBName = teamBName;
            _teamBScore = teamBScore;
            _matchDuration = matchDuration;
            _goals = new List<GoalRecord>();
        }

        /// <summary>Add a goal record.</summary>
        public void AddGoalRecord(string scorerName, float matchTime, string teamName)
        {
            _goals.Add(new GoalRecord
            {
                ScorerName = scorerName,
                MatchTime = matchTime,
                TeamName = teamName
            });
        }

        /// <summary>
        /// Get the man of the match (player with most goals).
        /// Returns "N/A" if no goals were scored.
        /// </summary>
        public string GetManOfTheMatch()
        {
            if (_goals.Count == 0) return "N/A";

            // Count goals per player
            var goalCounts = new Dictionary<string, int>();
            foreach (var goal in _goals)
            {
                if (!goalCounts.ContainsKey(goal.ScorerName))
                    goalCounts[goal.ScorerName] = 0;
                goalCounts[goal.ScorerName]++;
            }

            // Find player with most goals
            string bestPlayer = "N/A";
            int maxGoals = 0;
            foreach (var kvp in goalCounts)
            {
                if (kvp.Value > maxGoals)
                {
                    maxGoals = kvp.Value;
                    bestPlayer = kvp.Key;
                }
            }

            return bestPlayer;
        }
    }
}
