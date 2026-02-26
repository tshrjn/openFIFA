namespace OpenFifa.Core
{
    /// <summary>
    /// Team data entry with name and colors (as floats for pure C#).
    /// </summary>
    public class TeamDataEntry
    {
        public string Name;
        public float PrimaryR, PrimaryG, PrimaryB;
        public float SecondaryR, SecondaryG, SecondaryB;

        public TeamDataEntry(string name, float pr, float pg, float pb, float sr, float sg, float sb)
        {
            Name = name;
            PrimaryR = pr; PrimaryG = pg; PrimaryB = pb;
            SecondaryR = sr; SecondaryG = sg; SecondaryB = sb;
        }
    }

    /// <summary>
    /// Pure C# team selection logic.
    /// Manages team selection state, confirmation, and AI team assignment.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class TeamSelectLogic
    {
        private readonly int _teamCount;
        private int _selectedTeamIndex;

        /// <summary>Number of available teams.</summary>
        public int TeamCount => _teamCount;

        /// <summary>Currently selected team index, or -1 if none selected.</summary>
        public int SelectedTeamIndex => _selectedTeamIndex;

        /// <summary>Whether the player has selected a team and can confirm.</summary>
        public bool CanConfirm => _selectedTeamIndex >= 0;

        public TeamSelectLogic(int teamCount)
        {
            _teamCount = teamCount;
            _selectedTeamIndex = -1;
        }

        /// <summary>
        /// Select a team by index. Ignored if out of range.
        /// </summary>
        public void SelectTeam(int index)
        {
            if (index < 0 || index >= _teamCount)
                return;
            _selectedTeamIndex = index;
        }

        /// <summary>
        /// Get the AI team index. Picks a team different from the player's selection.
        /// </summary>
        public int GetAITeamIndex()
        {
            if (_selectedTeamIndex < 0) return 0;

            // Pick the next team that is different
            for (int i = 0; i < _teamCount; i++)
            {
                if (i != _selectedTeamIndex)
                    return i;
            }
            return 0;
        }
    }
}
