using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Power bar driven by hold time. The curve eases out: power rises quickly at first and flattens near
    /// full, so small differences in a long hold matter less than in a short one.
    /// power = minPower + (1 - minPower) * (1 - (1 - x)^2), x = clamp01(hold / fullChargeSeconds).
    /// </summary>
    public sealed class ChargeMeter
    {
        private float _startTime;

        public bool IsCharging { get; private set; }
        public float FullChargeSeconds { get; private set; } = 1f;
        public float MinPower { get; private set; } = 0.1f;

        public static float PowerFromHold(float holdSeconds, float fullChargeSeconds, float minPower)
        {
            if (fullChargeSeconds <= 0f) return 1f;
            float x = holdSeconds / fullChargeSeconds;
            if (x < 0f) x = 0f;
            if (x > 1f) x = 1f;
            float eased = 1f - (1f - x) * (1f - x);
            float p = minPower + (1f - minPower) * eased;
            return Math.Min(1f, Math.Max(0f, p));
        }

        public void Begin(float time, float fullChargeSeconds, float minPower)
        {
            _startTime = time;
            FullChargeSeconds = fullChargeSeconds;
            MinPower = minPower;
            IsCharging = true;
        }

        /// <summary>Current power while charging (0 when idle).</summary>
        public float Current(float time) => IsCharging ? PowerFromHold(time - _startTime, FullChargeSeconds, MinPower) : 0f;

        /// <summary>Stops charging and returns the final power.</summary>
        public float Release(float time)
        {
            float p = Current(time);
            IsCharging = false;
            return p;
        }

        public void Cancel() => IsCharging = false;
    }

    /// <summary>
    /// Holds one released kick until the contact frame. A kick released during the wind-up (or while the
    /// ball is still arriving) fires on the first tick with <see cref="ControlContext.CanStrike"/> inside
    /// the buffer window; a newer kick replaces an older one.
    /// </summary>
    public sealed class ControlKickBuffer
    {
        private ControlAction _action;
        private float _power;
        private ControlVec2 _aim;
        private float _queuedAt;

        public bool HasPending => _action != ControlAction.None;
        public ControlAction PendingAction => _action;
        public float QueuedAt => _queuedAt;

        public void Push(ControlAction action, float power, ControlVec2 aim, float time)
        {
            _action = action;
            _power = power;
            _aim = aim;
            _queuedAt = time;
        }

        /// <summary>Replaces the pending action if it is <paramref name="from"/> (for example Shoot -> LowShot on double tap).</summary>
        public bool TryUpgrade(ControlAction from, ControlAction to)
        {
            if (_action != from) return false;
            _action = to;
            return true;
        }

        /// <summary>Fires the pending kick if the player can strike this tick; drops it once the window has passed.</summary>
        public bool TryConsume(in ControlContext ctx, float windowSeconds, out ControlAction action, out float power, out ControlVec2 aim)
        {
            action = ControlAction.None;
            power = 0f;
            aim = ControlVec2.Zero;
            if (!HasPending) return false;
            if (ctx.Time - _queuedAt > windowSeconds)
            {
                Clear();
                return false;
            }
            if (!ctx.CanStrike) return false;
            action = _action;
            power = _power;
            aim = _aim;
            Clear();
            return true;
        }

        public void Clear()
        {
            _action = ControlAction.None;
            _power = 0f;
            _aim = ControlVec2.Zero;
        }
    }
}
