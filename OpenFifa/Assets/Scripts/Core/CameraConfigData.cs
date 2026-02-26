namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class holding broadcast camera configuration.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class CameraConfigData
    {
        public float ElevationAngle { get; }
        public float FollowDamping { get; }
        public float Distance { get; }
        public float FieldOfView { get; }
        public float BallTrackingWeight { get; }
        public float PlayerTrackingWeight { get; }
        public float MinHeight { get; }

        public CameraConfigData(
            float elevationAngle = 35f,
            float followDamping = 1f,
            float distance = 25f,
            float fieldOfView = 60f,
            float ballTrackingWeight = 1f,
            float playerTrackingWeight = 0.5f,
            float minHeight = 5f)
        {
            ElevationAngle = elevationAngle;
            FollowDamping = followDamping;
            Distance = distance;
            FieldOfView = fieldOfView;
            BallTrackingWeight = ballTrackingWeight;
            PlayerTrackingWeight = playerTrackingWeight;
            MinHeight = minHeight;
        }
    }
}
