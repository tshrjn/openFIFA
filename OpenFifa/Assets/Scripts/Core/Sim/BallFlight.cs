namespace OpenFifa.Core
{
    /// <summary>
    /// Ball-flight prediction API for the AI and the kick solver. Every answer comes from <see cref="BallIntegrator"/>,
    /// so AI timing matches what the sim will actually do.
    ///
    /// Ground balls (rolling, no bounce):
    /// - <see cref="GroundPassTime"/>: seconds for a ball rolled at a launch speed to cover a flat distance
    ///   (+infinity if it stops short).
    /// - <see cref="GroundPassSpeedFor"/>: launch speed so a rolled ball arrives at a distance with a given speed.
    /// - <see cref="GroundSpeedAfter"/>, <see cref="GroundRollDistance"/>: speed after a distance, total roll.
    /// These read a per-config table built once by integrating a pure-rolling ball from <c>MaxSpeed</c> to rest at
    /// 120 Hz. Pure rolling depends only on speed, so any launch speed follows a suffix of that trajectory; lookups
    /// are O(log n) and allocation-free, and match direct integration to within one 120 Hz step.
    ///
    /// Any ball (lofted, spinning, bouncing, near the goal frame):
    /// - <see cref="PredictBall"/>: state after N seconds of free flight (no players).
    /// - <see cref="TimeToFlatDistance"/>: when the ball has moved a flat distance from where it is now.
    /// Both step the real integrator on a copy of the struct: allocation-free, cost ~ seconds * 120 steps.
    ///
    /// Thread-safety: the ground table is rebuilt (deterministically) whenever the config's ball parameters change;
    /// concurrent first use may build it twice, which is harmless.
    /// </summary>
    public static class BallFlight
    {
        /// <summary>The match sim's ball integration step (120 Hz).</summary>
        public const float DefaultDt = 1f / 120f;

        private static readonly PitchSpec FreeFlight = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);

        /// <summary>Seconds for a ball rolled at <paramref name="speed"/> to cover <paramref name="distance"/> metres.
        /// Returns +infinity if it stops before getting there.</summary>
        public static float GroundPassTime(BallSimConfig cfg, float distance, float speed)
        {
            if (distance <= 0f) return 0f;
            var t = RollTable.For(cfg);
            float d0 = t.DistanceAtSpeed(speed);
            float d1 = d0 + distance;
            if (d1 > t.TotalDistance) return float.PositiveInfinity;
            return t.TimeAtDistance(d1) - t.TimeAtDistance(d0);
        }

        /// <summary>
        /// Launch speed for a rolled ball to arrive <paramref name="distance"/> metres away still moving at
        /// <paramref name="arrivalSpeed"/>. Capped at <see cref="BallSimConfig.MaxSpeed"/>.
        /// </summary>
        public static float GroundPassSpeedFor(BallSimConfig cfg, float distance, float arrivalSpeed)
        {
            var t = RollTable.For(cfg);
            float arrive = SimMath.Clamp(arrivalSpeed, 0f, cfg.MaxSpeed);
            if (distance <= 0f) return arrive;
            float dLaunch = t.DistanceAtSpeed(arrive) - distance;
            if (dLaunch <= 0f) return cfg.MaxSpeed;
            return t.SpeedAtDistance(dLaunch);
        }

        /// <summary>Speed of a ball rolled at <paramref name="speed"/> after <paramref name="distance"/> metres (0 if it stopped).</summary>
        public static float GroundSpeedAfter(BallSimConfig cfg, float speed, float distance)
        {
            var t = RollTable.For(cfg);
            float d = t.DistanceAtSpeed(speed) + SimMath.Max(0f, distance);
            return d >= t.TotalDistance ? 0f : t.SpeedAtDistance(d);
        }

        /// <summary>Total distance a ball rolled at <paramref name="speed"/> covers before stopping.</summary>
        public static float GroundRollDistance(BallSimConfig cfg, float speed)
        {
            var t = RollTable.For(cfg);
            return t.TotalDistance - t.DistanceAtSpeed(speed);
        }

        /// <summary>Ball state after <paramref name="seconds"/> of free flight. See <see cref="BallIntegrator.PredictBall"/>.</summary>
        public static SimBall PredictBall(SimBall ball, BallSimConfig cfg, PitchSpec pitch, float seconds) =>
            BallIntegrator.PredictBall(ball, cfg, pitch, seconds, DefaultDt);

        /// <summary>
        /// Seconds until the ball's flat distance from its current position reaches <paramref name="flatDistance"/>,
        /// searching at most <paramref name="maxSeconds"/>; +infinity if it never does (stops, or is caught by the net).
        /// <paramref name="atArrival"/> is the ball state at that moment (or at the end of the search).
        /// </summary>
        public static float TimeToFlatDistance(SimBall ball, BallSimConfig cfg, PitchSpec pitch, float flatDistance,
            float maxSeconds, out SimBall atArrival)
        {
            SimVec3 start = ball.Position;
            float d2 = flatDistance * flatDistance;
            int steps = (int)(maxSeconds / DefaultDt + 0.5f);
            for (int i = 0; i < steps; i++)
            {
                BallIntegrator.Step(ref ball, cfg, pitch ?? FreeFlight, DefaultDt);
                SimVec3 d = (ball.Position - start).Flat;
                if (d.SqrMagnitude >= d2)
                {
                    atArrival = ball;
                    return (i + 1) * DefaultDt;
                }

                if (ball.Grounded && ball.Velocity.SqrMagnitude == 0f) break;
            }

            atArrival = ball;
            return float.PositiveInfinity;
        }

        /// <summary>Speed, cumulative distance and time of a pure-rolling ball from MaxSpeed to rest.</summary>
        internal sealed class RollTable
        {
            private float[] _speed;
            private float[] _dist;
            private int _n;
            private float _dt;

            // Fingerprint of the config values the table depends on.
            private float _mass, _radius, _gravity, _rho, _cdLow, _cdHigh, _crisisStart, _crisisEnd, _roll, _rollDrag, _stop, _max;

            public float TotalDistance => _dist[_n - 1];

            public static RollTable For(BallSimConfig cfg)
            {
                RollTable t = cfg.CachedRollTable;
                if (t != null && t.Matches(cfg)) return t;
                t = Build(cfg);
                cfg.CachedRollTable = t;
                return t;
            }

            private bool Matches(BallSimConfig c) =>
                _mass == c.Mass && _radius == c.Radius && _gravity == c.Gravity && _rho == c.AirDensity
                && _cdLow == c.DragCoefficientLowSpeed && _cdHigh == c.DragCoefficientHighSpeed
                && _crisisStart == c.DragCrisisStartSpeed && _crisisEnd == c.DragCrisisEndSpeed
                && _roll == c.RollingResistance && _rollDrag == c.RollingDragPerSpeed && _stop == c.StopSpeed && _max == c.MaxSpeed;

            private static RollTable Build(BallSimConfig cfg)
            {
                var t = new RollTable
                {
                    _mass = cfg.Mass, _radius = cfg.Radius, _gravity = cfg.Gravity, _rho = cfg.AirDensity,
                    _cdLow = cfg.DragCoefficientLowSpeed, _cdHigh = cfg.DragCoefficientHighSpeed,
                    _crisisStart = cfg.DragCrisisStartSpeed, _crisisEnd = cfg.DragCrisisEndSpeed,
                    _roll = cfg.RollingResistance, _rollDrag = cfg.RollingDragPerSpeed, _stop = cfg.StopSpeed, _max = cfg.MaxSpeed,
                    _dt = DefaultDt,
                };

                const int cap = 120 * 120; // two minutes of rolling is far more than any ball needs
                var speed = new float[cap];
                var dist = new float[cap];
                SimVec3 v = new SimVec3(0f, 0f, cfg.MaxSpeed);
                var ball = new SimBall
                {
                    Position = new SimVec3(0f, cfg.Radius, 0f),
                    Velocity = v,
                    Spin = BallIntegrator.RollingSpin(v, cfg.Radius, 0f),
                    Grounded = true,
                };
                int n = 0;
                speed[n] = cfg.MaxSpeed;
                dist[n] = 0f;
                n++;
                while (n < cap)
                {
                    BallIntegrator.Step(ref ball, cfg, FreeFlight, DefaultDt);
                    speed[n] = ball.Velocity.Flat.Magnitude;
                    dist[n] = ball.Position.Z;
                    n++;
                    if (ball.Velocity.SqrMagnitude == 0f) break;
                }

                t._speed = new float[n];
                t._dist = new float[n];
                System.Array.Copy(speed, t._speed, n);
                System.Array.Copy(dist, t._dist, n);
                t._n = n;
                return t;
            }

            /// <summary>Distance along the reference roll at which the ball has slowed to <paramref name="v"/>.</summary>
            public float DistanceAtSpeed(float v)
            {
                if (v >= _speed[0]) return 0f;
                if (v <= 0f) return TotalDistance;
                // Speeds decrease monotonically: find the last index with speed >= v.
                int lo = 0, hi = _n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (_speed[mid] >= v) lo = mid; else hi = mid;
                }

                float s0 = _speed[lo], s1 = _speed[hi];
                float f = s0 > s1 ? (s0 - v) / (s0 - s1) : 0f;
                return SimMath.Lerp(_dist[lo], _dist[hi], f);
            }

            public float SpeedAtDistance(float d) => Sample(d, _speed);

            public float TimeAtDistance(float d)
            {
                int i = Index(d, out float f);
                return (i + f) * _dt;
            }

            private float Sample(float d, float[] values)
            {
                int i = Index(d, out float f);
                return i + 1 < _n ? SimMath.Lerp(values[i], values[i + 1], f) : values[_n - 1];
            }

            private int Index(float d, out float f)
            {
                f = 0f;
                if (d <= 0f) return 0;
                if (d >= TotalDistance) return _n - 1;
                int lo = 0, hi = _n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (_dist[mid] <= d) lo = mid; else hi = mid;
                }

                float span = _dist[hi] - _dist[lo];
                f = span > 0f ? (d - _dist[lo]) / span : 0f;
                return lo;
            }
        }
    }
}
