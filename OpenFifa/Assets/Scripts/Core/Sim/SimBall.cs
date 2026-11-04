namespace OpenFifa.Core
{
    /// <summary>Mutable ball state for the sim. A struct so snapshots copy it by value.</summary>
    public struct SimBall
    {
        /// <summary>Centre of the ball (metres). Y = Radius when resting on the grass.</summary>
        public SimVec3 Position;

        public SimVec3 Velocity;

        /// <summary>Angular velocity (rad/s), axis-angle. +Y spin on a +Z ball curls it toward +X.</summary>
        public SimVec3 Spin;

        /// <summary>0 = not in a net, +1 = in the net behind the +Z goal line, -1 = behind the -Z goal line.</summary>
        public int InNet;

        /// <summary>True while the ball is in contact with the ground (rolling or sliding).</summary>
        public bool Grounded;

        public static SimBall AtRest(SimVec3 groundPoint, float radius) => new SimBall
        {
            Position = new SimVec3(groundPoint.X, radius, groundPoint.Z),
            Velocity = SimVec3.Zero,
            Spin = SimVec3.Zero,
            InNet = 0,
            Grounded = true,
        };
    }
}
