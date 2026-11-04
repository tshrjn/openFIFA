namespace OpenFifa.Core
{
    /// <summary>
    /// Coefficients for the custom ball integrator (<see cref="BallIntegrator"/>).
    /// APPROXIMATIONS: no shipped football game publishes its ball model. These are textbook values
    /// (FIFA ball mass/size, sphere drag with a drag crisis, lift ~ spin parameter, grass restitution)
    /// tuned so a 25 m/s curled free kick bends about 2-4 m. Tune against feel on device, not as facts.
    /// Units are SI (kg, m, s, rad/s).
    /// </summary>
    public sealed class BallSimConfig
    {
        /// <summary>Ball mass. FIFA size 5: 0.41-0.45 kg.</summary>
        public float Mass = 0.43f;

        /// <summary>Ball radius. Size 5 circumference 68-70 cm -> r ~ 0.11 m.</summary>
        public float Radius = 0.11f;

        public float Gravity = 9.81f;

        /// <summary>Air density at sea level, 15 C.</summary>
        public float AirDensity = 1.225f;

        /// <summary>Drag coefficient below the drag crisis (slow ball, laminar boundary layer).</summary>
        public float DragCoefficientLowSpeed = 0.45f;

        /// <summary>Drag coefficient above the drag crisis (fast ball, turbulent boundary layer).</summary>
        public float DragCoefficientHighSpeed = 0.22f;

        /// <summary>Speeds (m/s) over which Cd blends from the low-speed to the high-speed value.</summary>
        public float DragCrisisStartSpeed = 9f;
        public float DragCrisisEndSpeed = 15f;

        /// <summary>
        /// Magnus factor S in F = S (omega x v), in kg (N per (rad/s * m/s)). Roughly 0.5 rho A r C_L / (r omega / v)
        /// with C_L ~ 0.25 at spin parameter ~ 0.25. 0.0020 bends a 25 m/s, 60 rad/s kick ~ 2-4 m over 25 m.
        /// </summary>
        public float MagnusCoefficient = 0.0020f;

        /// <summary>Spin time constant in flight (s). Spin decays as exp(-t / tau).</summary>
        public float AirSpinDecayTime = 5f;

        /// <summary>Coefficient of restitution for the vertical component on grass. 0.6-0.8 is plausible.</summary>
        public float GroundRestitution = 0.65f;

        /// <summary>Restitution falls off for hard impacts: e = GroundRestitution - this * (impact speed / 20).</summary>
        public float GroundRestitutionSpeedLoss = 0.1f;

        /// <summary>Tangential friction coefficient during a bounce (couples spin and velocity).</summary>
        public float BounceFriction = 0.5f;

        /// <summary>Kinetic friction while the ball slides on grass (drives it toward pure rolling).</summary>
        public float SlidingFriction = 0.4f;

        /// <summary>
        /// Rolling resistance as an equivalent coefficient (deceleration = this * g). Grass: ~0.05-0.15.
        /// 0.10 gives ~1 m/s^2; with drag a 12 m/s ground pass rolls ~30-35 m.
        /// </summary>
        public float RollingResistance = 0.10f;

        /// <summary>Vertical speed below which a ground contact stops bouncing and the ball rolls.</summary>
        public float RestVerticalSpeed = 0.5f;

        /// <summary>Speed below which a rolling ball is treated as stopped.</summary>
        public float StopSpeed = 0.05f;

        /// <summary>Goal post / crossbar radius (12 cm diameter frames).</summary>
        public float PostRadius = 0.06f;

        /// <summary>Restitution against the goal frame.</summary>
        public float PostRestitution = 0.7f;

        /// <summary>Depth of the net behind the goal line.</summary>
        public float NetDepth = 2.0f;

        /// <summary>Velocity damping time constant (s) once the ball is in the net.</summary>
        public float NetDampingTime = 0.08f;

        /// <summary>Restitution against the net walls.</summary>
        public float NetRestitution = 0.1f;

        /// <summary>Hard cap on ball speed (m/s) and spin (rad/s) as numerical guards.</summary>
        public float MaxSpeed = 45f;
        public float MaxSpin = 150f;

        /// <summary>Hollow-sphere moment of inertia, I = 2/3 m r^2.</summary>
        public float MomentOfInertia => (2f / 3f) * Mass * Radius * Radius;

        public float CrossSectionArea => 3.14159265f * Radius * Radius;

        public BallSimConfig Clone() => (BallSimConfig)MemberwiseClone();
    }
}
