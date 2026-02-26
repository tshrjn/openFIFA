namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class representing a complete team formation layout.
    /// Contains an array of FormationSlotData defining all player positions.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class FormationLayoutData
    {
        private readonly FormationSlotData[] _slots;

        public string Name { get; }

        public FormationLayoutData(string name, FormationSlotData[] slots)
        {
            Name = name;
            _slots = slots;
        }

        /// <summary>
        /// Returns a copy of the formation slots.
        /// </summary>
        public FormationSlotData[] GetSlots()
        {
            var copy = new FormationSlotData[_slots.Length];
            System.Array.Copy(_slots, copy, _slots.Length);
            return copy;
        }

        /// <summary>
        /// Returns world positions for this formation.
        /// Home team defends negative X, away team defends positive X.
        /// The offsets are relative to the team's half center.
        /// </summary>
        /// <param name="isHomeTeam">True for home team (defends -X), false for away (defends +X)</param>
        /// <param name="pitchLength">Total pitch length in meters</param>
        public FormationPosition[] GetWorldPositions(bool isHomeTeam, float pitchLength)
        {
            float halfLength = pitchLength / 2f;
            float teamCenterX = isHomeTeam ? -halfLength / 2f : halfLength / 2f;
            float mirror = isHomeTeam ? 1f : -1f;

            var positions = new FormationPosition[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                positions[i] = new FormationPosition(
                    x: teamCenterX + slot.OffsetX * mirror,
                    y: slot.OffsetY,
                    z: slot.OffsetZ * mirror,
                    role: slot.Role
                );
            }
            return positions;
        }

        /// <summary>
        /// Creates the default 2-1-2 formation.
        /// GK at back, 2 defenders, 1 midfielder, 2 forwards.
        /// Offsets are relative to team center (negative X = defensive, positive X = attacking).
        /// </summary>
        public static FormationLayoutData CreateDefault212()
        {
            return new FormationLayoutData("2-1-2", new FormationSlotData[]
            {
                new FormationSlotData(PositionRole.Goalkeeper, 0f,   0f, -12f),  // GK
                new FormationSlotData(PositionRole.Defender,  -6f,   0f,  -6f),  // LB
                new FormationSlotData(PositionRole.Defender,   6f,   0f,  -6f),  // RB
                new FormationSlotData(PositionRole.Midfielder, 0f,   0f,   0f),  // CM
                new FormationSlotData(PositionRole.Forward,   -6f,   0f,   8f),  // LW
                new FormationSlotData(PositionRole.Forward,    6f,   0f,   8f),  // RW
            });
        }
    }

    /// <summary>
    /// Represents a computed world position for a formation slot.
    /// </summary>
    public struct FormationPosition
    {
        public float x;
        public float y;
        public float z;
        public PositionRole role;

        public FormationPosition(float x, float y, float z, PositionRole role)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.role = role;
        }
    }
}
