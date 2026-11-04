namespace OpenFifa.Core
{
    /// <summary>Output of <see cref="KickSolver.Solve"/>: the ball's new velocity and spin plus diagnostics.</summary>
    public struct SimKickResult
    {
        public SimVec3 Velocity;
        public SimVec3 Spin;
        public SimFoot Foot;
        public bool UsedWeakFoot;

        /// <summary>Standard deviation (radians) of the error cone that was sampled.</summary>
        public float ErrorSigma;

        /// <summary>Signed horizontal error actually applied (radians).</summary>
        public float HorizontalError;

        /// <summary>The strike is a knuckle shot (<see cref="KickStyle.Knuckle"/>): the sim seeds its wobble.</summary>
        public bool Knuckle;
    }
}
