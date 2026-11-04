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
                    LobParams(req.Style, cfg, out elevation, out float back);
                    spinLocal = new SimVec3(back, 0f, 0f);
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
                case IntentAction.Shoot when req.Style == KickStyle.Chip:
                {
                    // High, soft arc with backspin, solved to drop onto the target point (on the goal line, so it
                    // comes down under the bar behind a keeper who is off his line).
                    float d = req.HasTarget ? distance : 10f + 20f * power;
                    elevation = cfg.ChipElevationDeg * SimMath.Deg2Rad;
                    spinLocal = new SimVec3(-cfg.ChipBackSpin, 0f, 0f);
                    speed = SimMath.Min(LoftedSpeed(d, elevation, spinLocal.X, ball), cfg.ChipMaxSpeed);
                    break;
                }
                case IntentAction.Shoot:
                {
                    speed = cfg.ShotSpeedMin + cfg.ShotSpeedRange * power * (0.6f + 0.4f * SimPlayerAttributes.N(a.ShotPower));
                    if (req.Style == KickStyle.Low)
                    {
                        // Low shot: flat along the deck; no power-dependent rise, a little less pace.
                        speed *= cfg.LowShotSpeedFactor;
                        elevation = cfg.LowShotElevationDeg * SimMath.Deg2Rad;
                    }
                    else
                    {
                        elevation = (cfg.ShotElevationDeg + cfg.ShotElevationPowerDeg * power * power) * SimMath.Deg2Rad;
                    }

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
                    if (req.IsThrowIn)
                    {
                        elevation = 25f * SimMath.Deg2Rad;
                        speed = SimMath.Min(LoftedSpeed(SimMath.Max(d, 5f), elevation, 0f, ball), 15f);
                    }
                    else
                    {
                        speed = GroundPassSpeed(req.Style, power, d, cfg, ball);
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

        /// <summary>Elevation (radians) and backspin of a lob for a kick variant (driven lob flatter, lobbed through lower).</summary>
        public static void LobParams(KickStyle style, SimActionConfig cfg, out float elevation, out float topSpin)
        {
            switch (style)
            {
                case KickStyle.Driven:
                    elevation = cfg.DrivenLobElevationDeg * SimMath.Deg2Rad;
                    topSpin = -4f;
                    break;
                case KickStyle.LobbedThrough:
                    elevation = cfg.LobbedThroughElevationDeg * SimMath.Deg2Rad;
                    topSpin = -8f;
                    break;
                default:
                    elevation = cfg.LobElevationDeg * SimMath.Deg2Rad;
                    topSpin = -8f;
                    break;
            }
        }

        /// <summary>Launch speed of an assisted ground pass of <paramref name="distance"/> m (driven passes arrive faster).</summary>
        public static float GroundPassSpeed(KickStyle style, float power, float distance, SimActionConfig cfg, BallSimConfig ball)
        {
            float arrive = cfg.PassArrivalSpeedMin + cfg.PassArrivalSpeedRange * power;
            float cap = cfg.PassMaxSpeed;
            if (style == KickStyle.Driven)
            {
                arrive += cfg.DrivenPassArrivalBonus;
                cap = cfg.DrivenPassMaxSpeed;
            }

            return SimMath.Min(BallFlight.GroundPassSpeedFor(ball, distance, arrive), cap);
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

        /// <summary>
        /// Table-backed <see cref="LoftedSpeed"/>: same answer within ~0.01 m/s, O(log n) and allocation-free after the
        /// first call for an (elevation, spin, ball config) combination. See <see cref="LoftTable"/>.
        /// </summary>
        public static float LoftedSpeedCached(float distance, float elevation, float topSpin, BallSimConfig ball) =>
            LoftTable.For(ball, elevation, topSpin).SpeedForCarry(distance);

        /// <summary>Table-backed <see cref="LoftedTime"/> (seconds to first landing for a launch speed).</summary>
        public static float LoftedTimeCached(float speed, float elevation, float topSpin, BallSimConfig ball) =>
            LoftTable.For(ball, elevation, topSpin).TimeAtSpeed(speed);

        private static readonly PitchSpec FreeFlight = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);

        /// <summary>
        /// Seconds an assisted pass of <paramref name="action"/> at <paramref name="power"/> takes to reach a target
        /// <paramref name="distance"/> metres away, using the same launch speed <see cref="Solve"/> picks (before the
        /// error cone): ground passes roll there (<see cref="BallFlight.GroundPassTime"/>), lobs, crosses and
        /// throw-ins fly to their first landing. A ground pass capped short of the target returns its full roll time.
        /// Lofted times come from a cached <see cref="LoftTable"/> (within ~1/120 s of the exact bisection + flight), so
        /// the pass lead-point search, which calls this many times per kick, runs no flight integrations.
        /// </summary>
        public static float PassFlightTime(IntentAction action, float power, float distance, bool throwIn,
            SimActionConfig cfg, BallSimConfig ball) =>
            PassFlightTime(action, KickStyle.Default, power, distance, throwIn, cfg, ball);

        /// <summary><see cref="PassFlightTime(IntentAction,float,float,bool,SimActionConfig,BallSimConfig)"/> for a kick variant.</summary>
        public static float PassFlightTime(IntentAction action, KickStyle style, float power, float distance, bool throwIn,
            SimActionConfig cfg, BallSimConfig ball)
        {
            if (distance <= 0f) return 0f;
            power = SimMath.Clamp01(power);
            switch (action)
            {
                case IntentAction.Lob:
                {
                    LobParams(style, cfg, out float e, out float back);
                    var lt = LoftTable.For(ball, e, back);
                    return lt.TimeAtSpeed(lt.SpeedForCarry(distance));
                }
                case IntentAction.Cross:
                {
                    var lt = LoftTable.For(ball, cfg.CrossElevationDeg * SimMath.Deg2Rad, -5f);
                    return lt.TimeAtSpeed(lt.SpeedForCarry(distance));
                }
            }

            if (throwIn)
            {
                var lt = LoftTable.For(ball, 25f * SimMath.Deg2Rad, 0f);
                return lt.TimeAtSpeed(SimMath.Min(lt.SpeedForCarry(SimMath.Max(distance, 5f)), 15f));
            }

            float speed = GroundPassSpeed(style, power, distance, cfg, ball);
            float t = BallFlight.GroundPassTime(ball, distance, speed);
            if (float.IsPositiveInfinity(t))
                t = BallFlight.GroundPassTime(ball, BallFlight.GroundRollDistance(ball, speed) * 0.999f, speed);
            return t;
        }

        /// <summary>Seconds from launch to the first ground contact (120 Hz integration).</summary>
        public static float LoftedTime(float speed, float elevation, float topSpin, BallSimConfig ball)
        {
            Carry(speed, elevation, topSpin, ball, out float time);
            return time;
        }

        /// <summary>Horizontal distance to the first ground contact for a launch (120 Hz integration).</summary>
        public static float Carry(float speed, float elevation, float topSpin, BallSimConfig ball) =>
            Carry(speed, elevation, topSpin, ball, out _);

        /// <summary>
        /// Diagnostic: number of full lofted-flight integrations (<see cref="Carry(float,float,float,BallSimConfig)"/>
        /// and friends) run on this thread. Tests use it to prove the lead-point search hits the cached table.
        /// </summary>
        [ThreadStatic] public static int CarrySimulations;

        private static float Carry(float speed, float elevation, float topSpin, BallSimConfig ball, out float time)
        {
            CarrySimulations++;
            float cp = (float)Math.Cos(elevation);
            var b = new SimBall
            {
                Position = new SimVec3(0f, ball.Radius, 0f),
                Velocity = new SimVec3(0f, speed * (float)Math.Sin(elevation), speed * cp),
                Spin = new SimVec3(topSpin, 0f, 0f),
            };
            int steps = 0;
            for (int i = 0; i < 1200; i++)
            {
                steps++;
                BallStepFlags f = BallIntegrator.Step(ref b, ball, FreeFlight, 1f / 120f);
                if ((f & (BallStepFlags.Bounced | BallStepFlags.StartedRolling)) != 0) break;
            }

            time = steps / 120f;
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
        /// <summary>
        /// Launch speed -> carry (flat distance to first landing) and flight time for one (elevation, top spin, ball
        /// config), sampled by integrating real launches at 120 Hz from 2 m/s to <see cref="BallSimConfig.MaxSpeed"/>
        /// in <see cref="SpeedStep"/> steps. Carry grows monotonically with speed, so the inverse (speed for a carry) is a
        /// binary search plus linear interpolation; lookups are allocation-free. Tables live on the ball config
        /// (a handful per config: lob, cross, throw-in) and are rebuilt deterministically when a field the flight
        /// depends on changes.
        /// </summary>
        public sealed class LoftTable
        {
            public const float MinSpeed = 2f;
            public const float SpeedStep = 0.125f;

            private float _elevation, _spin;
            private float _mass, _radius, _gravity, _rho, _cdLow, _cdHigh, _crisisStart, _crisisEnd, _magnus, _spinDecay,
                _restV, _max, _maxSpin;
            private float[] _carry;
            private float[] _time;
            private int _n;

            public static LoftTable For(BallSimConfig cfg, float elevation, float topSpin)
            {
                LoftTable[] cache = cfg.CachedLoftTables;
                if (cache != null)
                {
                    for (int i = 0; i < cache.Length; i++)
                    {
                        LoftTable c = cache[i];
                        if (c != null && c._elevation == elevation && c._spin == topSpin && c.Matches(cfg)) return c;
                    }
                }

                LoftTable t = Build(cfg, elevation, topSpin);
                // Copy-on-write (a cloned config shares the array): drop stale tables for this key, keep the newest 7
                // others and append the new one.
                int n = cache?.Length ?? 0;
                int keep = 0;
                for (int i = 0; i < n; i++)
                    if (cache[i] != null && !(cache[i]._elevation == elevation && cache[i]._spin == topSpin)) keep++;
                int skip = Math.Max(0, keep - 7);
                var next = new LoftTable[keep - skip + 1];
                int k = 0;
                for (int i = 0; i < n; i++)
                {
                    LoftTable c = cache[i];
                    if (c == null || (c._elevation == elevation && c._spin == topSpin)) continue;
                    if (skip > 0) { skip--; continue; }
                    next[k++] = c;
                }

                next[k] = t;
                cfg.CachedLoftTables = next;
                return t;
            }

            private bool Matches(BallSimConfig c) =>
                _mass == c.Mass && _radius == c.Radius && _gravity == c.Gravity && _rho == c.AirDensity
                && _cdLow == c.DragCoefficientLowSpeed && _cdHigh == c.DragCoefficientHighSpeed
                && _crisisStart == c.DragCrisisStartSpeed && _crisisEnd == c.DragCrisisEndSpeed
                && _magnus == c.MagnusCoefficient && _spinDecay == c.AirSpinDecayTime
                && _restV == c.RestVerticalSpeed && _max == c.MaxSpeed && _maxSpin == c.MaxSpin;

            private static LoftTable Build(BallSimConfig c, float elevation, float topSpin)
            {
                var t = new LoftTable
                {
                    _elevation = elevation, _spin = topSpin,
                    _mass = c.Mass, _radius = c.Radius, _gravity = c.Gravity, _rho = c.AirDensity,
                    _cdLow = c.DragCoefficientLowSpeed, _cdHigh = c.DragCoefficientHighSpeed,
                    _crisisStart = c.DragCrisisStartSpeed, _crisisEnd = c.DragCrisisEndSpeed,
                    _magnus = c.MagnusCoefficient, _spinDecay = c.AirSpinDecayTime,
                    _restV = c.RestVerticalSpeed, _max = c.MaxSpeed, _maxSpin = c.MaxSpin,
                };
                int n = (int)((c.MaxSpeed - MinSpeed) / SpeedStep) + 2;
                if (n < 2) n = 2;
                t._n = n;
                t._carry = new float[n];
                t._time = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float v = SpeedAt(i, c.MaxSpeed);
                    t._carry[i] = Carry(v, elevation, topSpin, c, out float time);
                    t._time[i] = time;
                }

                // Guard against tiny non-monotonic wiggles from the 120 Hz landing step: carry must not decrease.
                for (int i = 1; i < n; i++)
                    if (t._carry[i] < t._carry[i - 1]) t._carry[i] = t._carry[i - 1];
                return t;
            }

            private static float SpeedAt(int i, float max) => SimMath.Min(max, MinSpeed + i * SpeedStep);

            public float MaxCarry => _carry[_n - 1];

            /// <summary>Launch speed whose carry is <paramref name="distance"/> (MaxSpeed if out of range, like <see cref="LoftedSpeed"/>).</summary>
            public float SpeedForCarry(float distance)
            {
                if (distance >= _carry[_n - 1]) return _max;
                if (distance <= _carry[0]) return MinSpeed;
                int lo = 0, hi = _n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (_carry[mid] <= distance) lo = mid; else hi = mid;
                }

                float c0 = _carry[lo], c1 = _carry[hi];
                float f = c1 > c0 ? (distance - c0) / (c1 - c0) : 0f;
                return SimMath.Lerp(SpeedAt(lo, _max), SpeedAt(hi, _max), f);
            }

            /// <summary>Seconds to first landing for a launch at <paramref name="speed"/>.</summary>
            public float TimeAtSpeed(float speed)
            {
                float x = (speed - MinSpeed) / SpeedStep;
                if (x <= 0f) return _time[0];
                int i = (int)x;
                if (i >= _n - 1) return _time[_n - 1];
                float v0 = SpeedAt(i, _max), v1 = SpeedAt(i + 1, _max);
                float f = v1 > v0 ? (speed - v0) / (v1 - v0) : 0f;
                return SimMath.Lerp(_time[i], _time[i + 1], SimMath.Clamp01(f));
            }
        }
    }
}
