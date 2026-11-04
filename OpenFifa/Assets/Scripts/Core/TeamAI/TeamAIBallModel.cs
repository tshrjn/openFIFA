using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Ball timing for the team AI, precomputed from <see cref="BallIntegrator"/> at construction so the AI's pass
    /// and interception timing matches the sim's ball (drag crisis, rolling resistance, lofted flight) instead of a
    /// hand-tuned guess. Construction runs the integrator once (a few ms) and allocates the tables; every lookup
    /// after that is allocation-free and deterministic.
    /// <para>
    /// Ground passes use the sim's own <see cref="BallFlight"/> API (the same launch speed the kick solver picks,
    /// <see cref="BallFlight.GroundPassSpeedFor"/> capped at the max pass speed, then <see cref="BallFlight.GroundPassTime"/>).
    /// For free-ball prediction a rolling ball's state depends only on its speed, so one master rolling run gives
    /// distance after any time from any speed in O(log n).
    /// </para>
    /// <para>
    /// Lofted balls (lobs, crosses): launch speeds are swept at the kick solver's elevation and back-spin, and the
    /// first-landing distance and flight time are tabulated, so LobPassTime(d) is the flight time of the launch
    /// <see cref="KickSolver.LoftedSpeed"/> would pick for d.
    /// </para>
    /// </summary>
    public sealed class TeamAIBallModel
    {
        private static TeamAIBallModel _default;

        /// <summary>Model for the default <see cref="BallSimConfig"/> and <see cref="SimActionConfig"/>.</summary>
        public static TeamAIBallModel Default => _default ?? (_default = new TeamAIBallModel(new BallSimConfig(), new SimActionConfig()));

        public readonly BallSimConfig Ball;
        public readonly float Dt;
        public readonly float PassArrivalSpeedMin;
        public readonly float PassArrivalSpeedRange;
        public readonly float PassMaxSpeed;

        // Master rolling run, one sample per integrator step: distance travelled and speed at time i * Dt.
        private readonly float[] _rollDist;
        private readonly float[] _rollSpeed;
        private readonly int _rollCount;

        // Lofted sweeps: first-landing distance and flight time per launch speed (monotonic in speed).
        private readonly float[] _lobCarry, _lobTime, _lobSpeed;
        private readonly float[] _crossCarry, _crossTime, _crossSpeed;

        private const float MaxRollSeconds = 40f;
        private static readonly PitchSpec FreeFlight = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);

        public TeamAIBallModel(BallSimConfig ball, SimActionConfig actions, float dt = 1f / 120f)
        {
            Ball = ball ?? new BallSimConfig();
            actions = actions ?? new SimActionConfig();
            Dt = dt;
            PassArrivalSpeedMin = actions.PassArrivalSpeedMin;
            PassArrivalSpeedRange = actions.PassArrivalSpeedRange;
            PassMaxSpeed = actions.PassMaxSpeed;

            // ---- Rolling master run ----
            int max = (int)(MaxRollSeconds / dt) + 2;
            _rollDist = new float[max];
            _rollSpeed = new float[max];
            var b = new SimBall
            {
                Position = new SimVec3(0f, Ball.Radius, 0f),
                Velocity = new SimVec3(0f, 0f, Ball.MaxSpeed),
                Grounded = true,
            };
            b.Spin = BallIntegrator.RollingSpin(b.Velocity, Ball.Radius, 0f);
            int n = 0;
            _rollDist[n] = 0f;
            _rollSpeed[n] = Ball.MaxSpeed;
            n++;
            while (n < max)
            {
                BallIntegrator.Step(ref b, Ball, FreeFlight, dt);
                _rollDist[n] = b.Position.Z;
                _rollSpeed[n] = b.Velocity.Magnitude;
                n++;
                if (b.Velocity.SqrMagnitude <= 0f) break;
            }
            _rollCount = n;

            // Build the sim's ground-roll table now so the first AI pass in a match allocates nothing.
            BallFlight.GroundPassTime(Ball, 1f, PassMaxSpeed);

            // ---- Lofted sweeps ----
            float lobElev = actions.LobElevationDeg * SimMath.Deg2Rad;
            float crossElev = actions.CrossElevationDeg * SimMath.Deg2Rad;
            int count = (int)((Ball.MaxSpeed - 2f) / 0.25f) + 1;
            _lobCarry = new float[count]; _lobTime = new float[count]; _lobSpeed = new float[count];
            _crossCarry = new float[count]; _crossTime = new float[count]; _crossSpeed = new float[count];
            for (int i = 0; i < count; i++)
            {
                float v = 2f + 0.25f * i;
                _lobSpeed[i] = v;
                _crossSpeed[i] = v;
                Flight(v, lobElev, -8f, out _lobCarry[i], out _lobTime[i]);
                Flight(v, crossElev, -5f, out _crossCarry[i], out _crossTime[i]);
            }
        }

        /// <summary>Distance and time to the first ground contact for a launch from the grass along +Z.</summary>
        private void Flight(float speed, float elevation, float topSpin, out float carry, out float time)
        {
            var b = new SimBall
            {
                Position = new SimVec3(0f, Ball.Radius, 0f),
                Velocity = new SimVec3(0f, speed * (float)Math.Sin(elevation), speed * (float)Math.Cos(elevation)),
                Spin = new SimVec3(topSpin, 0f, 0f),
            };
            int steps = 0;
            for (; steps < 2400; steps++)
            {
                BallStepFlags f = BallIntegrator.Step(ref b, Ball, FreeFlight, Dt);
                if ((f & (BallStepFlags.Bounced | BallStepFlags.StartedRolling)) != 0) { steps++; break; }
            }
            carry = b.Position.Z;
            time = steps * Dt;
        }

        // ------------------------------------------------------------------ ground passes

        /// <summary>Arrival speed the kick solver aims for at this pass power (0..1).</summary>
        public float ArrivalSpeedForPower(float power) => PassArrivalSpeedMin + PassArrivalSpeedRange * TeamAIMath.Clamp01(power);

        /// <summary>The power the team AI uses for a ground pass of this length (see TeamAIOnBall).</summary>
        public static float GroundPassPower(float dist) => TeamAIMath.Clamp(dist / TeamAIOnBall.MaxGroundPass, 0.12f, 1f);

        /// <summary>Seconds for an AI ground pass of <paramref name="dist"/> m (power from the AI's own mapping).</summary>
        public float GroundPassTime(float dist) => GroundPassTime(dist, ArrivalSpeedForPower(GroundPassPower(dist)));

        /// <summary>
        /// Seconds for a ground pass of <paramref name="dist"/> m whose launch speed is chosen so it arrives at
        /// <paramref name="arriveSpeed"/> (capped at the solver's maximum pass speed).
        /// </summary>
        public float GroundPassTime(float dist, float arriveSpeed)
        {
            if (dist <= 0f) return 0f;
            return BallFlight.GroundPassTime(Ball, dist, GroundLaunchSpeed(dist, arriveSpeed));
        }

        /// <summary>
        /// Launch speed the kick solver uses for a ground pass of <paramref name="dist"/> m arriving at
        /// <paramref name="arriveSpeed"/>: <see cref="BallFlight.GroundPassSpeedFor"/> capped at the max pass speed.
        /// </summary>
        public float GroundLaunchSpeed(float dist, float arriveSpeed) =>
            Math.Min(BallFlight.GroundPassSpeedFor(Ball, dist, arriveSpeed), PassMaxSpeed);

        /// <summary>Seconds a ball rolling at <paramref name="speed"/> needs to cover <paramref name="dist"/> m (+inf if it stops short).</summary>
        public float RollTime(float speed, float dist) => TimeToCover(IndexOfSpeed(speed), dist);

        /// <summary>Distance a ball rolling at <paramref name="speed"/> covers in <paramref name="t"/> seconds.</summary>
        public float RollDistance(float speed, float t)
        {
            float i0 = IndexOfSpeed(speed);
            return DistAtIndex(i0 + t / Dt) - DistAtIndex(i0);
        }

        /// <summary>Total distance a ball rolling at <paramref name="speed"/> covers before stopping.</summary>
        public float RollRange(float speed) => _rollDist[_rollCount - 1] - DistAtIndex(IndexOfSpeed(speed));

        private float TimeToCover(float i0, float dist)
        {
            float target = DistAtIndex(i0) + dist;
            if (target > _rollDist[_rollCount - 1]) return float.PositiveInfinity;
            return (IndexOfDist(target) - i0) * Dt;
        }

        /// <summary>Fractional sample index where the master run reaches <paramref name="speed"/> (speed decreases).</summary>
        private float IndexOfSpeed(float speed)
        {
            if (speed >= _rollSpeed[0]) return 0f;
            int lo = 0, hi = _rollCount - 1;
            if (speed <= _rollSpeed[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_rollSpeed[mid] > speed) lo = mid; else hi = mid;
            }
            float a = _rollSpeed[lo], c = _rollSpeed[hi];
            return lo + (a - c > 1e-6f ? (a - speed) / (a - c) : 0f);
        }

        private float IndexOfDist(float d)
        {
            if (d <= 0f) return 0f;
            int lo = 0, hi = _rollCount - 1;
            if (d >= _rollDist[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_rollDist[mid] < d) lo = mid; else hi = mid;
            }
            float a = _rollDist[lo], c = _rollDist[hi];
            return lo + (c - a > 1e-6f ? (d - a) / (c - a) : 0f);
        }

        private float DistAtIndex(float i) => Sample(_rollDist, i);

        private float Sample(float[] a, float i)
        {
            if (i <= 0f) return a[0];
            int k = (int)i;
            if (k >= _rollCount - 1) return a[_rollCount - 1];
            float f = i - k;
            return a[k] + (a[k + 1] - a[k]) * f;
        }

        // ------------------------------------------------------------------ lofted balls

        /// <summary>Flight time (s) of a lob that first lands <paramref name="dist"/> m away.</summary>
        public float LobPassTime(float dist) => Lookup(_lobCarry, _lobTime, dist);

        /// <summary>Flight time (s) of a cross that first lands <paramref name="dist"/> m away.</summary>
        public float CrossTime(float dist) => Lookup(_crossCarry, _crossTime, dist);

        /// <summary>Launch speed of a lob landing <paramref name="dist"/> m away.</summary>
        public float LobLaunchSpeed(float dist) => Lookup(_lobCarry, _lobSpeed, dist);

        private static float Lookup(float[] x, float[] y, float v)
        {
            int n = x.Length;
            if (v <= x[0]) return x[0] > 1e-4f ? y[0] * v / x[0] : y[0];
            if (v >= x[n - 1]) return y[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (x[mid] < v) lo = mid; else hi = mid;
            }
            float t = (v - x[lo]) / Math.Max(x[hi] - x[lo], 1e-6f);
            return y[lo] + (y[hi] - y[lo]) * t;
        }

        // ------------------------------------------------------------------ free ball prediction

        /// <summary>
        /// Flat ball position after <paramref name="t"/> s from the given state, as the sim would move it with no
        /// players: rolling balls use the master curve (O(log n)), airborne balls run the integrator.
        /// </summary>
        public SimVec3 Predict(SimVec3 pos, SimVec3 vel, SimVec3 spin, float t)
        {
            if (t <= 0f) return new SimVec3(pos.X, 0f, pos.Z);
            if (IsGrounded(pos, vel))
            {
                float sx = vel.X, sz = vel.Z;
                float s = (float)Math.Sqrt(sx * sx + sz * sz);
                if (s < 1e-4f) return new SimVec3(pos.X, 0f, pos.Z);
                float d = RollDistance(s, t);
                return new SimVec3(pos.X + sx / s * d, 0f, pos.Z + sz / s * d);
            }
            var b = MakeBall(pos, vel, spin);
            int steps = (int)(t / Dt + 0.5f);
            for (int i = 0; i < steps; i++) BallIntegrator.Step(ref b, Ball, FreeFlight, Dt);
            return new SimVec3(b.Position.X, 0f, b.Position.Z);
        }

        public bool IsGrounded(SimVec3 pos, SimVec3 vel) =>
            pos.Y <= Ball.Radius + 0.02f && vel.Y < Ball.RestVerticalSpeed && vel.Y > -Ball.RestVerticalSpeed;

        /// <summary>Sim ball for a world ball state; a grounded ball with no spin given is assumed to be rolling.</summary>
        public SimBall MakeBall(SimVec3 pos, SimVec3 vel, SimVec3 spin)
        {
            bool grounded = IsGrounded(pos, vel);
            var b = new SimBall
            {
                Position = new SimVec3(pos.X, pos.Y < Ball.Radius ? Ball.Radius : pos.Y, pos.Z),
                Velocity = grounded ? new SimVec3(vel.X, 0f, vel.Z) : vel,
                Spin = spin,
                Grounded = grounded,
            };
            if (grounded && spin.SqrMagnitude < 1e-8f) b.Spin = BallIntegrator.RollingSpin(b.Velocity, Ball.Radius, 0f);
            return b;
        }

        internal static PitchSpec FlightPitch => FreeFlight;
    }

    /// <summary>
    /// One decision tick's free-ball trajectory (sampled every <see cref="SampleDt"/> s for <see cref="Horizon"/> s),
    /// integrated once with <see cref="BallIntegrator"/> and then queried many times. Preallocated, allocation-free.
    /// </summary>
    public sealed class TeamAIBallTrajectory
    {
        public const float SampleDt = 0.05f;
        public const float Horizon = 4f;
        public const int Samples = 81;

        private readonly SimVec3[] _pos = new SimVec3[Samples];
        /// <summary>Height of the ball at each sample.</summary>
        private readonly float[] _height = new float[Samples];
        private float _builtTime = float.NaN;
        private SimVec3 _builtPos, _builtVel;

        /// <summary>Integrate the world ball forward (no players). Cached per (time, ball state).</summary>
        public void Build(TeamAIWorld world, TeamAIBallModel model)
        {
            var ball = world.Ball;
            if (world.Time == _builtTime && ball.Position.Equals(_builtPos) && ball.Velocity.Equals(_builtVel)) return;
            _builtTime = world.Time;
            _builtPos = ball.Position;
            _builtVel = ball.Velocity;

            var b = model.MakeBall(ball.Position, ball.Velocity, ball.Spin);
            var pitch = world.Pitch;
            int sub = (int)(SampleDt / model.Dt + 0.5f);
            if (sub < 1) sub = 1;
            float dt = SampleDt / sub;
            _pos[0] = new SimVec3(b.Position.X, 0f, b.Position.Z);
            _height[0] = b.Position.Y;
            for (int k = 1; k < Samples; k++)
            {
                if (b.Grounded && b.InNet == 0 && b.Velocity.Y == 0f && SafeToRoll(b.Position, pitch))
                {
                    // Rolling from here on: the master rolling curve gives the rest exactly, without stepping.
                    float vx = b.Velocity.X, vz = b.Velocity.Z;
                    float speed = (float)Math.Sqrt(vx * vx + vz * vz);
                    var from = _pos[k - 1];
                    float y = b.Position.Y;
                    for (int j = k; j < Samples; j++)
                    {
                        float d = speed > 1e-4f ? model.RollDistance(speed, (j - k + 1) * SampleDt) : 0f;
                        _pos[j] = speed > 1e-4f ? new SimVec3(from.X + vx / speed * d, 0f, from.Z + vz / speed * d) : from;
                        _height[j] = y;
                    }
                    return;
                }
                for (int s = 0; s < sub; s++) BallIntegrator.Step(ref b, model.Ball, pitch, dt);
                _pos[k] = new SimVec3(b.Position.X, 0f, b.Position.Z);
                _height[k] = b.Position.Y;
            }
        }

        /// <summary>Away from the goal frames (the only thing a rolling ball can hit in the integrator).</summary>
        private static bool SafeToRoll(SimVec3 p, PitchSpec pitch) =>
            pitch.HalfLength - TeamAIMath.Abs(p.Z) > 25f;

        /// <summary>Flat position at time t (clamped to the horizon), linearly interpolated.</summary>
        public SimVec3 At(float t)
        {
            float f = t / SampleDt;
            if (f <= 0f) return _pos[0];
            int k = (int)f;
            if (k >= Samples - 1) return _pos[Samples - 1];
            float u = f - k;
            var a = _pos[k];
            var c = _pos[k + 1];
            return new SimVec3(a.X + (c.X - a.X) * u, 0f, a.Z + (c.Z - a.Z) * u);
        }

        /// <summary>Ball height at time t.</summary>
        public float HeightAt(float t)
        {
            float f = t / SampleDt;
            if (f <= 0f) return _height[0];
            int k = (int)f;
            if (k >= Samples - 1) return _height[Samples - 1];
            float u = f - k;
            return _height[k] + (_height[k + 1] - _height[k]) * u;
        }
    }
}
