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
        /// Legacy linear Magnus factor S in F = S (omega x v), in kg. Only used when <see cref="UseSpinParameterLift"/>
        /// is false. 0.0020 bends a 25 m/s, 60 rad/s kick ~ 2-4 m over 25 m.
        /// </summary>
        public float MagnusCoefficient = 0.0020f;

        /// <summary>
        /// Magnus lift from the spin parameter Sp = r|omega|/|v| (wind-tunnel fits for footballs, e.g. Goff and Carre,
        /// Hong and Asai): F = 0.5 rho A C_L(Sp) |v|^2 along (omega x v), with C_L = Sp / (LiftSaturation Sp + LiftLinear).
        /// C_L grows almost linearly for small Sp and saturates (~0.3-0.35) for heavily spun slow balls, so a soft
        /// chip with lots of spin does not swerve like a rocket, and a fast ball with a little spin still curls.
        /// False falls back to the linear <see cref="MagnusCoefficient"/>.
        /// </summary>
        public bool UseSpinParameterLift = true;

        /// <summary>C_L fit, denominator slope (saturation). C_L tends to 1 / this for very large spin parameters.</summary>
        public float LiftSaturation = 2.022f;

        /// <summary>C_L fit, denominator intercept (initial slope 1 / this).</summary>
        public float LiftLinear = 0.981f;

        /// <summary>Overall multiplier on the fitted lift, for feel tuning (1 = the fit).</summary>
        public float LiftScale = 1.2f;

        /// <summary>
        /// Extra drag from spin while airborne: Cd += this * min(Sp, 1). A heavily spun ball loses pace a little
        /// faster (curlers and dipping drives die late). Not applied on the ground (a rolling ball has Sp = 1).
        /// </summary>
        public float DragSpinFactor = 0.12f;

        /// <summary>
        /// Knuckleball: a fast ball with almost no spin sheds its wake asymmetrically, so it wobbles and dips
        /// unpredictably. Modelled as a side + vertical force coefficient (times 0.5 rho A v^2) that follows a smooth,
        /// deterministic pattern along the flight path. 0 disables it.
        /// </summary>
        public float KnuckleForceCoefficient = 0.18f;

        /// <summary>Knuckle effect fades in between these speeds (m/s).</summary>
        public float KnuckleMinSpeed = 19f;
        public float KnuckleFullSpeed = 27f;

        /// <summary>Spin parameter above which there is no knuckle effect (it fades out linearly below it).</summary>
        public float KnuckleMaxSpinParameter = 0.03f;

        /// <summary>Distance (m) over which the knuckle side force completes one wobble.</summary>
        public float KnuckleWavelength = 16f;

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

        /// <summary>
        /// Extra rolling deceleration proportional to speed (1/s): long or wet grass drags a fast ball more than a slow
        /// one. 0 on a normal pitch; see <see cref="ApplySurface"/>.
        /// </summary>
        public float RollingDragPerSpeed = 0f;

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

        /// <summary>Lazily built ground-roll lookup for <see cref="BallFlight"/> (validated against the fields above).</summary>
        [System.NonSerialized] internal BallFlight.RollTable CachedRollTable;

        /// <summary>Lazily built lofted-launch lookups for <see cref="KickSolver.LoftTable"/> (validated against the fields above).</summary>
        [System.NonSerialized] internal KickSolver.LoftTable[] CachedLoftTables;

        /// <summary>The surface last applied with <see cref="ApplySurface"/> (Normal for a fresh config).</summary>
        public PitchSurface Surface = PitchSurface.Normal;

        /// <summary>
        /// Sets the ground coefficients for a pitch condition. Air values are untouched.
        /// <list type="bullet">
        /// <item>Normal: the defaults above (well-kept, watered before kick-off).</item>
        /// <item>Dry: firm and fast. Bounces higher and runs on, rolls a little further.</item>
        /// <item>Wet: rain on the surface. The ball skids on after a bounce (low friction), bounces lower, rolls further,
        /// but standing water adds speed-dependent drag.</item>
        /// <item>Heavy: long, soft or worn grass. Dead bounce, slows quickly.</item>
        /// </list>
        /// APPROXIMATIONS chosen for feel (sports-turf studies give rolling deceleration of ~0.5-1.5 m/s^2 and
        /// vertical restitution of ~0.5-0.8 across these conditions).
        /// </summary>
        public BallSimConfig ApplySurface(PitchSurface surface)
        {
            var d = Defaults;
            GroundRestitution = d.GroundRestitution;
            BounceFriction = d.BounceFriction;
            SlidingFriction = d.SlidingFriction;
            RollingResistance = d.RollingResistance;
            RollingDragPerSpeed = d.RollingDragPerSpeed;
            switch (surface)
            {
                case PitchSurface.Dry:
                    GroundRestitution = 0.74f;
                    BounceFriction = 0.55f;
                    RollingResistance = 0.085f;
                    break;
                case PitchSurface.Wet:
                    GroundRestitution = 0.55f;
                    BounceFriction = 0.22f;
                    SlidingFriction = 0.22f;
                    RollingResistance = 0.07f;
                    RollingDragPerSpeed = 0.035f;
                    break;
                case PitchSurface.Heavy:
                    GroundRestitution = 0.52f;
                    BounceFriction = 0.6f;
                    SlidingFriction = 0.5f;
                    RollingResistance = 0.13f;
                    RollingDragPerSpeed = 0.02f;
                    break;
            }

            Surface = surface;
            return this;
        }

        private static readonly BallSimConfig Defaults = new BallSimConfig();

        public BallSimConfig Clone() => (BallSimConfig)MemberwiseClone();
    }
}
