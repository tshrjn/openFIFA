namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class representing a single position slot in a formation.
    /// Stores role and offset relative to the team's defensive half center.
    /// </summary>
    public class FormationSlotData
    {
        public PositionRole Role { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public FormationSlotData(PositionRole role, float offsetX, float offsetY, float offsetZ)
        {
            Role = role;
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
        }
    }
}
