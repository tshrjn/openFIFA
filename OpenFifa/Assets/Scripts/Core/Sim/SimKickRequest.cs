namespace OpenFifa.Core
{
    /// <summary>Inputs to <see cref="KickSolver.Solve"/>, captured at the contact frame.</summary>
    public struct SimKickRequest
    {
        public IntentAction Action;

        /// <summary>Kick variant (chip, low shot, driven pass, lobbed through). Default = the plain action.</summary>
        public KickStyle Style;
        public float Power;

        /// <summary>Flat world aim direction (normalised by the solver; zero = facing).</summary>
        public SimVec3 Aim;

        /// <summary>Point the kick should arrive at (assisted pass lead point or shot target). Optional.</summary>
        public SimVec3 TargetPoint;
        public bool HasTarget;

        public SimVec3 KickerPosition;
        public SimVec3 KickerVelocity;
        public float KickerFacing;
        public SimVec3 BallPosition;
        public SimVec3 BallVelocity;

        /// <summary>0..1 from the nearest opponent's distance.</summary>
        public float Pressure;

        public bool IsHeader;

        /// <summary>Throw-ins use the pass solver with a capped speed and a release height.</summary>
        public bool IsThrowIn;
    }
}
