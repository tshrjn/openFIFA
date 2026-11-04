namespace OpenFifa.Core
{
    /// <summary>
    /// Deterministic semi-implicit Euler ball integrator, meant to run at 120 Hz or faster.
    /// Models gravity, quadratic drag with a drag crisis, Magnus lift, spin decay, sliding and rolling friction,
    /// bounces with restitution and spin/velocity coupling, goal-frame collisions and net capture.
    /// Out-of-play is a rules decision and lives in <see cref="MatchSim"/>; this class only moves the ball.
    /// </summary>
    public static class BallIntegrator
    {
        private const float Epsilon = 1e-4f;

        /// <summary>Advances <paramref name="ball"/> by one substep of <paramref name="dt"/> seconds.</summary>
        public static BallStepFlags Step(ref SimBall ball, BallSimConfig cfg, PitchSpec pitch, float dt)
        {
            if (ball.InNet != 0)
            {
                StepInNet(ref ball, cfg, pitch, dt);
                return BallStepFlags.None;
            }

            BallStepFlags flags = BallStepFlags.None;
            float m = cfg.Mass;
            float r = cfg.Radius;
            SimVec3 v = ball.Velocity;
            SimVec3 w = ball.Spin;

            // Aerodynamic forces: drag opposes velocity, Magnus is S (w x v).
            float speed = v.Magnitude;
            float cd = DragCoefficient(cfg, speed);
            SimVec3 drag = v * (-0.5f * cfg.AirDensity * cd * cfg.CrossSectionArea * speed);
            SimVec3 force = drag;

            if (ball.Grounded)
            {
                // Ground contact: the normal force cancels gravity, Magnus only acts in the plane.
                force = new SimVec3(force.X, 0f, force.Z);
                v = new SimVec3(v.X, 0f, v.Z);
                v += force * (dt / m);
                ApplyGroundFriction(ref v, ref w, cfg, dt);
            }
            else
            {
                force += SimVec3.Cross(w, v) * cfg.MagnusCoefficient;
                v += force * (dt / m) + new SimVec3(0f, -cfg.Gravity * dt, 0f);
                float spinDecay = SimMath.Decay(dt, cfg.AirSpinDecayTime);
                w *= spinDecay;
            }

            SimVec3 p = ball.Position + v * dt;

            // Ground contact / bounce.
            if (p.Y < r)
            {
                p = new SimVec3(p.X, r, p.Z);
                if (!ball.Grounded && v.Y < -cfg.RestVerticalSpeed)
                {
                    Bounce(ref v, ref w, cfg);
                    flags |= BallStepFlags.Bounced;
                }
                else
                {
                    if (!ball.Grounded) flags |= BallStepFlags.StartedRolling;
                    v = new SimVec3(v.X, 0f, v.Z);
                    ball.Grounded = true;
                }
            }
            else if (ball.Grounded && p.Y > r + Epsilon)
            {
                ball.Grounded = false;
            }

            flags |= CollideGoalFrames(ref p, ref v, ref w, cfg, pitch);

            // Net capture: the ball has wholly crossed the goal line inside the mouth.
            float halfGoal = pitch.GoalWidth * 0.5f;
            if (SimMath.Abs(p.Z) > pitch.HalfLength + r && SimMath.Abs(p.X) < halfGoal && p.Y < pitch.GoalHeight)
            {
                ball.InNet = p.Z > 0f ? 1 : -1;
                flags |= BallStepFlags.EnteredNet;
            }

            v = SimVec3.ClampMagnitude(v, cfg.MaxSpeed);
            w = SimVec3.ClampMagnitude(w, cfg.MaxSpin);
            if (ball.Grounded && v.SqrMagnitude < cfg.StopSpeed * cfg.StopSpeed)
            {
                v = SimVec3.Zero;
                w = new SimVec3(0f, w.Y * 0.9f, 0f);
            }

            ball.Position = p;
            ball.Velocity = v;
            ball.Spin = w;
            return flags;
        }

        /// <summary>Runs <paramref name="steps"/> substeps on a copy of the ball (AI prediction, tests).</summary>
        public static SimBall Predict(SimBall ball, BallSimConfig cfg, PitchSpec pitch, float dt, int steps)
        {
            for (int i = 0; i < steps; i++) Step(ref ball, cfg, pitch, dt);
            return ball;
        }

        public static float DragCoefficient(BallSimConfig cfg, float speed)
        {
            if (speed <= cfg.DragCrisisStartSpeed) return cfg.DragCoefficientLowSpeed;
            if (speed >= cfg.DragCrisisEndSpeed) return cfg.DragCoefficientHighSpeed;
            float t = (speed - cfg.DragCrisisStartSpeed) / (cfg.DragCrisisEndSpeed - cfg.DragCrisisStartSpeed);
            return SimMath.Lerp(cfg.DragCoefficientLowSpeed, cfg.DragCoefficientHighSpeed, t);
        }

        /// <summary>Velocity of the contact point (bottom of the ball) relative to the ground.</summary>
        public static SimVec3 ContactSlip(SimVec3 v, SimVec3 w, float r) =>
            new SimVec3(v.X + r * w.Z, 0f, v.Z - r * w.X);

        /// <summary>Spin for pure rolling at velocity v (topspin axis = Up x direction).</summary>
        public static SimVec3 RollingSpin(SimVec3 v, float r, float keepSideSpin) =>
            new SimVec3(v.Z / r, keepSideSpin, -v.X / r);

        private static void ApplyGroundFriction(ref SimVec3 v, ref SimVec3 w, BallSimConfig cfg, float dt)
        {
            float m = cfg.Mass;
            float r = cfg.Radius;
            SimVec3 slip = ContactSlip(v, w, r);
            float slipSpeed = slip.Magnitude;
            float sideSpin = w.Y * SimMath.Decay(dt, 0.6f);

            if (slipSpeed > 0.05f)
            {
                // Kinetic friction at the contact point, capped at the impulse that would reach pure rolling.
                float jToRoll = slipSpeed * m / (1f + m * r * r / cfg.MomentOfInertia);
                float j = SimMath.Min(cfg.SlidingFriction * m * cfg.Gravity * dt, jToRoll);
                ApplyContactImpulse(ref v, ref w, slip / slipSpeed * -j, cfg);
                w = new SimVec3(w.X, sideSpin, w.Z);
                return;
            }

            // Pure rolling: rolling resistance slows the ball, spin follows velocity.
            float flatSpeed = SimMath.Sqrt(v.X * v.X + v.Z * v.Z);
            if (flatSpeed > 0f)
            {
                float newSpeed = SimMath.Max(0f, flatSpeed - cfg.RollingResistance * cfg.Gravity * dt);
                v = new SimVec3(v.X, 0f, v.Z) * (newSpeed / flatSpeed);
            }

            w = RollingSpin(v, r, sideSpin);
        }

        /// <summary>Applies an impulse J at the contact point (0, -r, 0): changes v and spin together.</summary>
        private static void ApplyContactImpulse(ref SimVec3 v, ref SimVec3 w, SimVec3 impulse, BallSimConfig cfg)
        {
            v += impulse / cfg.Mass;
            SimVec3 contactArm = new SimVec3(0f, -cfg.Radius, 0f);
            w += SimVec3.Cross(contactArm, impulse) / cfg.MomentOfInertia;
        }

        private static void Bounce(ref SimVec3 v, ref SimVec3 w, BallSimConfig cfg)
        {
            float m = cfg.Mass;
            float impactSpeed = -v.Y;
            float e = SimMath.Clamp(cfg.GroundRestitution - cfg.GroundRestitutionSpeedLoss * (impactSpeed / 20f), 0.2f, 0.95f);
            float jn = m * (1f + e) * impactSpeed;
            v = new SimVec3(v.X, e * impactSpeed, v.Z);

            // Tangential friction impulse (Cross's bouncing-ball model): topspin kicks on, backspin checks up.
            SimVec3 slip = ContactSlip(v, w, cfg.Radius);
            float slipSpeed = slip.Magnitude;
            if (slipSpeed > 1e-4f)
            {
                float jToRoll = slipSpeed * m / (1f + m * cfg.Radius * cfg.Radius / cfg.MomentOfInertia);
                float jt = SimMath.Min(cfg.BounceFriction * jn, jToRoll);
                ApplyContactImpulse(ref v, ref w, slip / slipSpeed * -jt, cfg);
            }
        }

        private static BallStepFlags CollideGoalFrames(ref SimVec3 p, ref SimVec3 v, ref SimVec3 w,
            BallSimConfig cfg, PitchSpec pitch)
        {
            // Only bother near either goal line.
            float distToLine = pitch.HalfLength - SimMath.Abs(p.Z);
            if (distToLine > 1.5f || distToLine < -1.5f) return BallStepFlags.None;

            float side = p.Z >= 0f ? 1f : -1f;
            float pr = cfg.PostRadius;
            float zc = side * (pitch.HalfLength - pr);
            float px = pitch.GoalWidth * 0.5f + pr;
            float barY = pitch.GoalHeight + pr;
            BallStepFlags flags = BallStepFlags.None;

            if (CollideSegment(ref p, ref v, ref w, new SimVec3(-px, 0f, zc), new SimVec3(-px, barY, zc), cfg))
                flags |= BallStepFlags.HitPost;
            if (CollideSegment(ref p, ref v, ref w, new SimVec3(px, 0f, zc), new SimVec3(px, barY, zc), cfg))
                flags |= BallStepFlags.HitPost;
            if (CollideSegment(ref p, ref v, ref w, new SimVec3(-px, barY, zc), new SimVec3(px, barY, zc), cfg))
                flags |= BallStepFlags.HitCrossbar;
            return flags;
        }

        private static bool CollideSegment(ref SimVec3 p, ref SimVec3 v, ref SimVec3 w, SimVec3 a, SimVec3 b,
            BallSimConfig cfg)
        {
            SimVec3 ab = b - a;
            float t = SimMath.Clamp01(SimVec3.Dot(p - a, ab) / ab.SqrMagnitude);
            SimVec3 q = a + ab * t;
            SimVec3 d = p - q;
            float minDist = cfg.Radius + cfg.PostRadius;
            float distSqr = d.SqrMagnitude;
            if (distSqr >= minDist * minDist || distSqr < 1e-10f) return false;

            float dist = SimMath.Sqrt(distSqr);
            SimVec3 n = d / dist;
            p = q + n * minDist;
            float vn = SimVec3.Dot(v, n);
            if (vn >= 0f) return false;
            v -= n * ((1f + cfg.PostRestitution) * vn);
            w *= 0.6f;
            return true;
        }

        private static void StepInNet(ref SimBall ball, BallSimConfig cfg, PitchSpec pitch, float dt)
        {
            float r = cfg.Radius;
            SimVec3 v = ball.Velocity * SimMath.Decay(dt, cfg.NetDampingTime);
            v += new SimVec3(0f, -cfg.Gravity * dt, 0f);
            SimVec3 p = ball.Position + v * dt;

            float side = ball.InNet;
            float maxX = pitch.GoalWidth * 0.5f - r;
            float maxY = pitch.GoalHeight - r;
            float nearZ = pitch.HalfLength + r;
            float farZ = pitch.HalfLength + cfg.NetDepth - r;
            float e = cfg.NetRestitution;

            float x = p.X, y = p.Y, z = p.Z * side;
            float vx = v.X, vy = v.Y, vz = v.Z * side;
            if (x > maxX) { x = maxX; vx = -vx * e; }
            if (x < -maxX) { x = -maxX; vx = -vx * e; }
            if (y > maxY) { y = maxY; vy = -vy * e; }
            if (y < r) { y = r; vy = vy < 0f ? -vy * e : vy; }
            if (z > farZ) { z = farZ; vz = -vz * e; }
            if (z < nearZ) { z = nearZ; vz = vz < 0f ? -vz * e : vz; }

            ball.Position = new SimVec3(x, y, z * side);
            ball.Velocity = new SimVec3(vx, vy, vz * side);
            ball.Spin *= SimMath.Decay(dt, 0.3f);
            ball.Grounded = y <= r + Epsilon;
        }
    }
}
