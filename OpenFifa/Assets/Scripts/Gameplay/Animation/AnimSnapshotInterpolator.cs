using System;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>One fixed-step sim snapshot of a player root. Yaw is in radians (sim units).</summary>
    public struct AnimSnapshot
    {
        public float Time;
        public SimVec3 Position;
        public float YawRad;

        public AnimSnapshot(float time, SimVec3 position, float yawRad)
        {
            Time = time;
            Position = position;
            YawRad = yawRad;
        }
    }

    /// <summary>
    /// Engine-free render interpolation between the previous and the current fixed-step snapshot, so a 60 Hz sim
    /// moves smoothly on a 120 Hz (or 30 Hz) display. The rendered state lags the latest snapshot by
    /// (1 - alpha) steps: render time = current time - (1 - alpha) * fixed dt, then position and yaw are
    /// interpolated in time between the two snapshots (correct even when a frame skipped several sim steps).
    /// </summary>
    public sealed class AnimSnapshotInterpolator
    {
        /// <summary>A jump larger than this between consecutive snapshots (m) is a teleport (kick-off reset): no blend.</summary>
        public float TeleportDistance = 3f;

        private AnimSnapshot _prev;
        private AnimSnapshot _curr;
        private bool _hasPrev;
        private bool _hasCurr;

        public bool HasSnapshot => _hasCurr;
        public AnimSnapshot Previous => _hasPrev ? _prev : _curr;
        public AnimSnapshot Current => _curr;

        public void Reset()
        {
            _hasPrev = false;
            _hasCurr = false;
        }

        /// <summary>
        /// Feed the source's latest snapshot every frame. A snapshot with the same time as the current one is
        /// ignored; a newer one shifts current to previous; an older one (clock reset, replay) restarts.
        /// </summary>
        public void Push(float time, SimVec3 position, float yawRad)
        {
            var s = new AnimSnapshot(time, position, yawRad);
            if (!_hasCurr || time < _curr.Time)
            {
                _curr = s;
                _hasCurr = true;
                _hasPrev = false;
                return;
            }
            if (time == _curr.Time) return;
            SimVec3 d = position - _curr.Position;
            bool teleport = d.X * d.X + d.Z * d.Z > TeleportDistance * TeleportDistance;
            _prev = _curr;
            _hasPrev = !teleport;
            _curr = s;
        }

        /// <summary>
        /// State to render. alpha = accumulator remainder / fixedDt in [0, 1]; fixedDt the sim step.
        /// With no previous snapshot, or fixedDt &lt;= 0, returns the current snapshot.
        /// </summary>
        public AnimSnapshot Sample(float alpha, float fixedDt)
        {
            if (!_hasCurr) return default;
            if (!_hasPrev || fixedDt <= 0f) return _curr;
            float a = alpha < 0f ? 0f : (alpha > 1f ? 1f : alpha);
            float renderTime = _curr.Time - (1f - a) * fixedDt;
            float span = _curr.Time - _prev.Time;
            if (span <= 0f || renderTime >= _curr.Time) return _curr;
            if (renderTime <= _prev.Time) return _prev;
            float t = (renderTime - _prev.Time) / span;
            return new AnimSnapshot(renderTime,
                _prev.Position + (_curr.Position - _prev.Position) * t,
                _prev.YawRad + WrapPi(_curr.YawRad - _prev.YawRad) * t);
        }

        /// <summary>Wraps an angle (rad) to (-pi, pi].</summary>
        public static float WrapPi(float a)
        {
            const float TwoPi = (float)(2.0 * Math.PI);
            a %= TwoPi;
            if (a > Math.PI) a -= TwoPi;
            else if (a <= -Math.PI) a += TwoPi;
            return a;
        }
    }
}
