namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class holding player stats configuration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class PlayerStatsData
    {
        public float BaseSpeed { get; }
        public float SprintMultiplier { get; }
        public float Acceleration { get; }
        public float Deceleration { get; }

        /// <summary>Sprint speed = BaseSpeed * SprintMultiplier.</summary>
        public float SprintSpeed => BaseSpeed * SprintMultiplier;

        public PlayerStatsData(
            float baseSpeed = 7f,
            float sprintMultiplier = 1.5f,
            float acceleration = 5f,
            float deceleration = 8f)
        {
            BaseSpeed = baseSpeed;
            SprintMultiplier = sprintMultiplier;
            Acceleration = acceleration;
            Deceleration = deceleration;
        }
    }
}
