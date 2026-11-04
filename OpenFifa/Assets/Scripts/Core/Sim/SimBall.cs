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

        /// <summary>
        /// Per-strike knuckle phase (0..1) for a <see cref="KickStyle.Knuckle"/> shot; 0 = not knuckling. Set from the
        /// sim RNG at the kick and cleared by the first ground contact or any touch, so only deliberate knuckle shots
        /// wobble (never headers, lobs or crosses) and AI flight tables built at the origin stay exact.
        /// </summary>
        public float KnuckleSeed;

        /// <summary>Where the knuckle shot was struck: its wobble is a function of flat distance from here.</summary>
        public SimVec3 KnuckleOrigin;

        public bool Knuckling => KnuckleSeed != 0f;

        /// <summary>Stops any knuckle wobble (the ball was touched, bounced or deflected).</summary>
        public void ClearKnuckle()
        {
            KnuckleSeed = 0f;
            KnuckleOrigin = SimVec3.Zero;
        }

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
