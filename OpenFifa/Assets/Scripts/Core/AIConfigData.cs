namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class for AI configuration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class AIConfigData
    {
        public float ChaseRange { get; }
        public float MoveSpeed { get; }
        public float PositionThreshold { get; }
        public float SprintSpeed { get; }

        public AIConfigData(
            float chaseRange = 10f,
            float moveSpeed = 6f,
            float positionThreshold = 1f,
            float sprintSpeed = 9f)
        {
            ChaseRange = chaseRange;
            MoveSpeed = moveSpeed;
            PositionThreshold = positionThreshold;
            SprintSpeed = sprintSpeed;
        }
    }
}
