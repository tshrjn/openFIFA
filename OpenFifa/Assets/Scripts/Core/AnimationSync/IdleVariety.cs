using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Presentation-only variety for players standing or walking slowly, so 20 off-ball players never share one frozen
    /// idle pose in sync:
    /// <list type="bullet">
    /// <item><b>Desync</b>: each player gets his own idle start phase (<see cref="PhaseOffset"/>) and playback rate
    /// (<see cref="RateMultiplier"/>, 0.85-1.15), from a seed.</item>
    /// <item><b>Weight shift</b>: <see cref="IdleVariant"/> eases between the plain idle (0) and a weight-shift idle (1),
    /// holding each for 2.5-6 s and blending over <see cref="VariantBlendTime"/>; the schedule is per player.</item>
    /// <item><b>Ready stance</b>: <see cref="Ready"/> rises toward 1 when the ball is within <see cref="ReadyNear"/> m
    /// (0 beyond <see cref="ReadyFar"/>), only while slow, smoothed over <see cref="ReadySmoothing"/> s.</item>
    /// <item><b>Look-around</b>: the head follows the ball (<see cref="LookYaw"/> relative to the body, clamped to
    /// +-<see cref="MaxLookYaw"/>), retargeting in quick saccades every 0.4-1.4 s; about one saccade in four is a scan
    /// 25-45 deg off the ball (checking a shoulder). <see cref="LookWeight"/> fades with speed and when the ball is behind.</item>
    /// </list>
    /// Deterministic for a seed, allocation-free, no engine types. Angles in degrees (0 = +Z, positive toward +X).
    /// </summary>
    public sealed class IdleVariety
    {
        public const float ReadyNear = 12f;
        public const float ReadyFar = 20f;
        public const float ReadySmoothing = 0.4f;
        /// <summary>Speed (m/s) above which the ready stance and the weight shift fade out (it is a standing/walking layer).</summary>
        public const float SlowSpeed = 2f;
        public const float VariantBlendTime = 0.8f;
        public const float MaxLookYaw = 75f;
        /// <summary>Head-turn time constant (s) of a saccade.</summary>
        public const float SaccadeTime = 0.12f;
        /// <summary>Look weight standing, and the speed (m/s) at which it has faded to zero.</summary>
        public const float StandLookWeight = 0.7f;
        public const float NoLookSpeed = 5.5f;

        private uint _rng;
        private float _variantTarget;
        private float _variantTimer;
        private float _saccadeTimer;
        private float _scanOffset;
        private bool _hasLook;

        public float PhaseOffset { get; }
        public float RateMultiplier { get; }
        public float IdleVariant { get; private set; }
        public float Ready { get; private set; }
        /// <summary>Head yaw relative to the body (deg), clamped to +-<see cref="MaxLookYaw"/>.</summary>
        public float LookYaw { get; private set; }
        public float LookWeight { get; private set; }

        public IdleVariety(int seed)
        {
            _rng = Mix((uint)seed * 2654435761u + 0x9E3779B9u);
            if (_rng == 0) _rng = 1;
            PhaseOffset = Next01();
            RateMultiplier = 0.85f + 0.3f * Next01();
            _variantTarget = Next01() < 0.5f ? 0f : 1f;
            IdleVariant = _variantTarget;
            _variantTimer = 2.5f + 3.5f * Next01();
            _saccadeTimer = 0.4f * Next01();
        }

        /// <summary>
        /// Advance by <paramref name="dt"/>. <paramref name="toBallYawDeg"/>: world yaw of the ball seen from the player;
        /// <paramref name="bodyYawDeg"/>: shown body yaw; <paramref name="ballDistance"/> in m (negative = no ball known).
        /// </summary>
        public void Step(float dt, float speed, float bodyYawDeg, float toBallYawDeg, float ballDistance)
        {
            if (dt < 0f) dt = 0f;
            float slow = 1f - AnimMath.SmoothStep01((speed - SlowSpeed * 0.5f) / SlowSpeed);

            // Weight shift schedule.
            _variantTimer -= dt;
            if (_variantTimer <= 0f)
            {
                _variantTarget = _variantTarget > 0.5f ? 0f : 1f;
                _variantTimer = 2.5f + 3.5f * Next01();
            }
            float step = VariantBlendTime > 0f ? dt / VariantBlendTime : 1f;
            IdleVariant = Approach(IdleVariant, _variantTarget, step);

            // Ready stance when the ball is close.
            bool hasBall = ballDistance >= 0f;
            float readyTarget = hasBall ? (1f - AnimMath.SmoothStep01((ballDistance - ReadyNear) / (ReadyFar - ReadyNear))) * slow : 0f;
            float k = ReadySmoothing > 0f ? 1f - (float)Math.Exp(-dt / ReadySmoothing) : 1f;
            Ready += (readyTarget - Ready) * k;

            // Look-around: saccades toward the ball, sometimes a scan off it.
            if (!hasBall)
            {
                LookWeight += (0f - LookWeight) * k;
                return;
            }
            _saccadeTimer -= dt;
            if (_saccadeTimer <= 0f)
            {
                _saccadeTimer = 0.4f + 1.0f * Next01();
                float r = Next01();
                _scanOffset = r < 0.25f ? (Next01() < 0.5f ? -1f : 1f) * (25f + 20f * Next01()) : 0f;
            }
            float rel = AnimMath.DeltaAngle(bodyYawDeg, toBallYawDeg + _scanOffset);
            float behind = AnimMath.SmoothStep01((Math.Abs(AnimMath.DeltaAngle(bodyYawDeg, toBallYawDeg)) - 100f) / 50f);
            float target = AnimMath.Clamp(rel, -MaxLookYaw, MaxLookYaw);
            if (!_hasLook) { LookYaw = target; _hasLook = true; }
            else
            {
                float ks = SaccadeTime > 0f ? 1f - (float)Math.Exp(-dt / SaccadeTime) : 1f;
                LookYaw += (target - LookYaw) * ks;
            }
            float speedFade = 1f - AnimMath.SmoothStep01(speed / NoLookSpeed);
            float wTarget = StandLookWeight * speedFade * (1f - 0.7f * behind);
            LookWeight += (wTarget - LookWeight) * k;
        }

        /// <summary>Idle playback rate: <see cref="RateMultiplier"/> (the idle clip has no stride to match).</summary>
        public float IdleRate => RateMultiplier;

        private static float Approach(float v, float target, float maxStep) =>
            v < target ? Math.Min(target, v + maxStep) : Math.Max(target, v - maxStep);

        private float Next01()
        {
            // xorshift32
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 16777216f;
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }
    }
}
