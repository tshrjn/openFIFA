using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Presentation-side body yaw that follows the sim facing with a human turn rate. The sim may rotate a standing
    /// player at up to ~1000 deg/s (and snap it outright for a restart taker); shown directly that reads as a spin on the
    /// spot. This keeps the visual yaw within a speed-dependent max rate (fast when slow, slower at sprint), with a short
    /// critically damped catch-up so the turn eases in and out instead of starting and stopping in one frame, and it
    /// reports the yaw rate for lean and turn-in-place stepping. Angles in degrees (Unity convention: 0 = +Z, positive
    /// toward +X). Pure and allocation-free.
    /// </summary>
    public sealed class BodyFacingSmoother
    {
        /// <summary>Max yaw rate (deg/s) when standing: a quick pivot turns 180 deg in about 0.3 s.</summary>
        public float MaxRateStill = 620f;
        /// <summary>Max yaw rate (deg/s) at <see cref="FastSpeed"/> and above.</summary>
        public float MaxRateFast = 260f;
        public float FastSpeed = 7f;
        /// <summary>Time constant (s) of the catch-up; small, so the visual never trails a steady sim turn.</summary>
        public float SmoothTime = 0.06f;
        /// <summary>An error at least this large (deg) is a reversal: keep turning the way the body already turns.</summary>
        public float ReversalDeg = 179f;

        private bool _has;
        public float Yaw { get; private set; }
        /// <summary>Signed yaw rate (deg/s) of the shown body this frame.</summary>
        public float YawRate { get; private set; }

        public void Reset() => _has = false;

        public void Snap(float yawDeg)
        {
            Yaw = AnimMath.WrapAngle(yawDeg);
            YawRate = 0f;
            _has = true;
        }

        public float MaxRate(float speed)
        {
            float t = AnimMath.Clamp01(speed / Math.Max(0.1f, FastSpeed));
            return MaxRateStill + (MaxRateFast - MaxRateStill) * t;
        }

        /// <param name="targetYawDeg">Sim facing (deg).</param>
        /// <param name="speed">Ground speed (m/s).</param>
        /// <param name="rateScale">Multiplies the max rate (e.g. 2 while an action owns the yaw).</param>
        public float Step(float targetYawDeg, float speed, float dt, float rateScale = 1f)
        {
            if (!_has || dt <= 0f)
            {
                if (!_has) Snap(targetYawDeg);
                return Yaw;
            }
            float err = AnimMath.DeltaAngle(Yaw, targetYawDeg);
            if (Math.Abs(err) >= ReversalDeg)
            {
                // A reversal of exactly 180 deg has no shortest way round; keep turning the way we already were.
                if (Math.Abs(YawRate) > 1f) err = Math.Sign(YawRate) * Math.Abs(err);
            }
            float k = SmoothTime > 0f ? 1f - (float)Math.Exp(-dt / SmoothTime) : 1f;
            float step = err * k;
            float max = MaxRate(speed) * rateScale * dt;
            step = AnimMath.Clamp(step, -max, max);
            Yaw = AnimMath.WrapAngle(Yaw + step);
            YawRate = step / dt;
            return Yaw;
        }
    }

    /// <summary>
    /// Body lean from the motion: bank into a curve (centripetal acceleration v * yawRate) and pitch with forward
    /// acceleration / braking, smoothed and clamped. Degrees; positive Right = lean to the body's right, positive
    /// Forward = lean forward. The sprint clip already carries the sprint lean, so only acceleration adds pitch here.
    /// </summary>
    public sealed class LocomotionLean
    {
        public float BankScale = 0.7f;
        public float MaxBankDeg = 14f;
        public float PitchScale = 0.45f;
        public float MaxPitchDeg = 7f;
        public float SmoothTime = 0.12f;
        private const float G = 9.81f;

        public float RightDeg { get; private set; }
        public float ForwardDeg { get; private set; }

        public void Reset()
        {
            RightDeg = 0f;
            ForwardDeg = 0f;
        }

        /// <param name="speed">Ground speed (m/s).</param>
        /// <param name="yawRateDeg">Heading rate (deg/s), positive = turning right (clockwise from above).</param>
        /// <param name="forwardAccel">Acceleration along the body forward (m/s^2), negative = braking.</param>
        public void Step(float speed, float yawRateDeg, float forwardAccel, float dt)
        {
            float lateral = speed * yawRateDeg * AnimMath.Deg2Rad; // centripetal acceleration (m/s^2)
            float bank = AnimMath.Clamp((float)Math.Atan2(lateral, G) * AnimMath.Rad2Deg * BankScale, -MaxBankDeg, MaxBankDeg);
            // No bank when nearly standing: a pivot is not a curve.
            bank *= AnimMath.Clamp01((speed - 0.5f) / 1.5f);
            float pitch = AnimMath.Clamp((float)Math.Atan2(forwardAccel, G) * AnimMath.Rad2Deg * PitchScale, -MaxPitchDeg, MaxPitchDeg);
            float k = SmoothTime > 0f && dt > 0f ? 1f - (float)Math.Exp(-dt / SmoothTime) : 1f;
            RightDeg += (bank - RightDeg) * k;
            ForwardDeg += (pitch - ForwardDeg) * k;
        }
    }
}
