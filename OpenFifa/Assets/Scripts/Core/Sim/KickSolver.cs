using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Assisted kick solver: turns an intent (action, power, aim, optional target) into a launch velocity and
    /// spin, then perturbs it inside a skill-based error cone sampled from the sim's seeded RNG.
    /// Ground-pass speeds come from <see cref="BallFlight.GroundPassSpeedFor"/> and lofted speeds from a bisection
    /// against the integrator (<see cref="LoftedSpeed"/>), so an assisted pass arrives where and how it was meant to
    /// before the error cone is applied.
    /// </summary>
    public static class KickSolver
    {
        public static SimKickResult Solve(SimKickRequest req, SimPlayerAttributes a, SimActionConfig cfg,
            BallSimConfig ball, SimRng rng)
        {
            var result = new SimKickResult();
            SimVec3 aim = req.Aim.Flat.Normalized;
            if (aim.SqrMagnitude < 0.5f) aim = SimMath.FromYaw(req.KickerFacing);
            if (req.HasTarget)
            {
                SimVec3 toTarget = (req.TargetPoint - req.BallPosition).Flat;
                if (toTarget.SqrMagnitude > 0.01f) aim = toTarget.Normalized;
            }

            float power = SimMath.Clamp01(req.Power);
            float distance = req.HasTarget ? SimMath.FlatDistance(req.TargetPoint, req.BallPosition) : 0f;

            // Foot: the side of the body the ball is on decides; a centred ball uses the preferred foot.
            SimVec3 right = SimMath.FromYaw(req.KickerFacing + SimMath.Pi * 0.5f);
            float side = SimVec3.Dot((req.BallPosition - req.KickerPosition).Flat, right);
            SimFoot foot = side > 0.15f ? SimFoot.Right : (side < -0.15f ? SimFoot.Left : a.PreferredFoot);
            result.Foot = foot;
            result.UsedWeakFoot = !req.IsHeader && foot != a.PreferredFoot;

            float speed;
            float elevation;
            SimVec3 spinLocal = SimVec3.Zero; // x = topspin (+) / backspin (-), y = sidespin (+ curls toward +right)
            float curlPreAim = 0f;

            switch (req.Action)
            {
                case IntentAction.Lob:
                {
                    float d = req.HasTarget ? distance : 10f + 40f * power;
                    elevation = cfg.LobElevationDeg * SimMath.Deg2Rad;
                    spinLocal = new SimVec3(-8f, 0f, 0f);
                    speed = LoftedSpeed(d, elevation, spinLocal.X, ball);
                    break;
                }
                case IntentAction.Cross:
                {
                    float d = req.HasTarget ? distance : 15f + 35f * power;
                    elevation = cfg.CrossElevationDeg * SimMath.Deg2Rad;
                    speed = LoftedSpeed(d, elevation, -5f, ball);
                    // Right foot whips the ball with the inside: curls to the kicker's left (negative side spin).
                    float s = cfg.CrossSideSpin * (0.5f + 0.5f * SimPlayerAttributes.N(a.Crossing));
                    spinLocal = new SimVec3(-5f, foot == SimFoot.Right ? -s : s, 0f);
                    curlPreAim = CurlPreAim(spinLocal.Y, d, speed, ball) * 0.5f;
                    break;
                }
                case IntentAction.Shoot:
                {
                    speed = cfg.ShotSpeedMin + cfg.ShotSpeedRange * power * (0.6f + 0.4f * SimPlayerAttributes.N(a.ShotPower));
                    elevation = (cfg.ShotElevationDeg + cfg.ShotElevationPowerDeg * power * power) * SimMath.Deg2Rad;
                    spinLocal = new SimVec3(cfg.ShotTopSpin, 0f, 0f);
                    break;
                }
                case IntentAction.FinesseShot:
                {
                    speed = cfg.FinesseSpeedMin + cfg.FinesseSpeedRange * power;
                    elevation = cfg.FinesseElevationDeg * SimMath.Deg2Rad;
                    float s = cfg.FinesseSideSpin * (0.6f + 0.4f * SimPlayerAttributes.N(a.Curve));
                    // Inside of the foot: a right-footer curls right-to-left.
                    float sideSpin = foot == SimFoot.Right ? -s : s;
                    spinLocal = new SimVec3(5f, sideSpin, 0f);
                    float d = req.HasTarget ? distance : 20f;
                    curlPreAim = CurlPreAim(sideSpin, d, speed, ball);
                    break;
                }
                case IntentAction.DrivenShot:
                {
                    speed = cfg.DrivenSpeedMin + cfg.DrivenSpeedRange * power * (0.6f + 0.4f * SimPlayerAttributes.N(a.ShotPower));
                    elevation = cfg.DrivenElevationDeg * SimMath.Deg2Rad * (1f + power);
                    spinLocal = new SimVec3(cfg.DrivenTopSpin, 0f, 0f);
                    break;
                }
                default: // Pass, ThroughPass, throw-in
                {
                    float d = req.HasTarget ? distance : cfg.UnassistedPassMinDistance + cfg.UnassistedPassDistanceRange * power;
                    float arrive = cfg.PassArrivalSpeedMin + cfg.PassArrivalSpeedRange * power;
                    if (req.IsThrowIn)
                    {
                        elevation = 25f * SimMath.Deg2Rad;
                        speed = SimMath.Min(LoftedSpeed(SimMath.Max(d, 5f), elevation, 0f, ball), 15f);
                    }
                    else
                    {
                        speed = SimMath.Min(BallFlight.GroundPassSpeedFor(ball, d, arrive), cfg.PassMaxSpeed);
                        elevation = 0f;
                    }
                    break;
                }
            }

            if (req.IsHeader)
            {
                speed = speed * cfg.HeaderSpeedFactor + req.BallVelocity.Magnitude * 0.2f;
                elevation = PlayerIntent.IsShotAction(req.Action) ? -4f * SimMath.Deg2Rad : elevation * 0.6f + 0.1f;
                spinLocal = SimVec3.Zero;
                curlPreAim = 0f;
            }

            // Error cone.
            float sigma = ErrorSigma(req, a, cfg, power, result.UsedWeakFoot, distance);
            result.ErrorSigma = sigma;
            float hErr = rng.NextGaussian() * sigma;
            float vErr = rng.NextGaussian() * sigma * 0.5f;
            float sErr = 1f + rng.NextGaussian() * sigma * 0.4f;
            result.HorizontalError = hErr;
            // Weak foot costs pace; a 4-5 star weak foot is as strong as the preferred one, never stronger.
            if (result.UsedWeakFoot) speed *= SimMath.Min(1f, 0.92f + 0.02f * SimMath.Clamp(a.WeakFoot, 1, 5));

            float yaw = SimMath.Yaw(aim) + curlPreAim + hErr;
            float pitch = elevation + vErr;
            speed = SimMath.Max(1f, speed * sErr);
            SimVec3 flatDir = SimMath.FromYaw(yaw);
            float cp = (float)Math.Cos(pitch);
            float sp = (float)Math.Sin(pitch);
            result.Velocity = new SimVec3(flatDir.X * cp * speed, sp * speed, flatDir.Z * cp * speed);

            // Spin: topspin axis = Up x dir (right vector); side spin about +Y.
            SimVec3 rightAxis = SimVec3.Cross(SimVec3.Up, flatDir);
            SimVec3 spin = rightAxis * spinLocal.X + SimVec3.Up * spinLocal.Y;
            if (elevation == 0f && !req.IsHeader)
                spin = BallIntegrator.RollingSpin(result.Velocity, ball.Radius, 0f);
            result.Spin = spin;
            return result;
        }

        /// <summary>Error-cone standard deviation (radians) for a kick.</summary>
        public static float ErrorSigma(SimKickRequest req, SimPlayerAttributes a, SimActionConfig cfg, float power,
            bool weakFoot, float distance)
        {
            float baseDeg;
            float skill;
            switch (req.Action)
            {
                case IntentAction.ThroughPass:
                    baseDeg = cfg.ThroughErrorDeg; skill = 0.5f * (a.ShortPassing + a.Vision); break;
                case IntentAction.Lob:
                    baseDeg = cfg.LobErrorDeg; skill = a.LongPassing; break;
                case IntentAction.Cross:
                    baseDeg = cfg.CrossErrorDeg; skill = a.Crossing; break;
                case IntentAction.Shoot:
                    baseDeg = cfg.ShotErrorDeg; skill = distance > 0f && distance < 18f ? a.Finishing : 0.5f * (a.Finishing + a.LongShots); break;
                case IntentAction.FinesseShot:
                    baseDeg = cfg.FinesseErrorDeg; skill = 0.5f * (a.Finishing + a.Curve); break;
                case IntentAction.DrivenShot:
                    baseDeg = cfg.DrivenErrorDeg; skill = 0.5f * (a.Finishing + a.ShotPower); break;
                default:
                    baseDeg = cfg.PassErrorDeg; skill = distance > 25f ? a.LongPassing : a.ShortPassing; break;
            }

            if (req.IsHeader) skill = a.Heading;
            float skillFactor = 1.15f - SimMath.Clamp01(skill / 99f);
            float composure = SimPlayerAttributes.N(a.Composure);
            float pressureFactor = 1f + cfg.PressureErrorFactor * SimMath.Clamp01(req.Pressure) * (1f - 0.5f * composure);
            float wf = weakFoot ? 1f + cfg.WeakFootErrorPerStar * (5 - SimMath.Clamp(a.WeakFoot, 1, 5)) : 1f;
            float powerFactor = 1f + cfg.PowerErrorFactor * power * power;
            float header = req.IsHeader ? cfg.HeaderErrorFactor : 1f;
            return baseDeg * SimMath.Deg2Rad * skillFactor * pressureFactor * wf * powerFactor * header;
        }

        /// <summary>
        /// Launch speed for a lofted ball (launched from the ground) to first land <paramref name="distance"/> away.
        /// Solved by bisection against the real integrator, so drag and spin are accounted for. Deterministic.
        /// </summary>
        public static float LoftedSpeed(float distance, float elevation, float topSpin, BallSimConfig ball)
        {
            float lo = 2f;
            float hi = ball.MaxSpeed;
            if (Carry(hi, elevation, topSpin, ball) < distance) return hi;
            for (int i = 0; i < 14; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Carry(mid, elevation, topSpin, ball) < distance) lo = mid; else hi = mid;
            }

            return 0.5f * (lo + hi);
        }

        private static readonly PitchSpec FreeFlight = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);

        /// <summary>Horizontal distance to the first ground contact for a launch (120 Hz integration).</summary>
        public static float Carry(float speed, float elevation, float topSpin, BallSimConfig ball)
        {
            float cp = (float)Math.Cos(elevation);
            var b = new SimBall
            {
                Position = new SimVec3(0f, ball.Radius, 0f),
                Velocity = new SimVec3(0f, speed * (float)Math.Sin(elevation), speed * cp),
                Spin = new SimVec3(topSpin, 0f, 0f),
            };
            for (int i = 0; i < 1200; i++)
            {
                BallStepFlags f = BallIntegrator.Step(ref b, ball, FreeFlight, 1f / 120f);
                if ((f & (BallStepFlags.Bounced | BallStepFlags.StartedRolling)) != 0) break;
            }

            return b.Position.Z;
        }

        /// <summary>
        /// Yaw offset (radians) to aim outside the target so that a ball with side spin curls back onto the line.
        /// Small-angle estimate: theta = 0.5 * a_lateral * T / v with a_lateral = S * w * v / m, T = d / v.
        /// </summary>
        public static float CurlPreAim(float sideSpin, float distance, float speed, BallSimConfig ball)
        {
            if (speed < 1f) return 0f;
            float theta = 0.5f * ball.MagnusCoefficient * sideSpin * distance / (ball.Mass * speed);
            // Positive side spin curls toward +right, so pre-aim to the left (negative yaw) and vice versa.
            return -theta * 0.85f;
        }
    }
}
