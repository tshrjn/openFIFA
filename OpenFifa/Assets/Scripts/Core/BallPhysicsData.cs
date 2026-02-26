namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# data class holding ball physics configuration values.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class BallPhysicsData
    {
        public float Mass { get; }
        public float Drag { get; }
        public float AngularDrag { get; }
        public float Bounciness { get; }
        public float DynamicFriction { get; }
        public float StaticFriction { get; }

        public BallPhysicsData(
            float mass = 0.43f,
            float drag = 0.1f,
            float angularDrag = 0.5f,
            float bounciness = 0.6f,
            float dynamicFriction = 0.5f,
            float staticFriction = 0.5f)
        {
            Mass = mass;
            Drag = drag;
            AngularDrag = angularDrag;
            Bounciness = bounciness;
            DynamicFriction = dynamicFriction;
            StaticFriction = staticFriction;
        }
    }
}
