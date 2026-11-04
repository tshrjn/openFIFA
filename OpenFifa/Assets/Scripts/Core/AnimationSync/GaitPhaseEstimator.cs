namespace OpenFifa.Core
{
    /// <summary>
    /// Tracks the locomotion cycle from the Animator's normalised time, sampled once per rendered frame: the phase (the
    /// fraction part) and the cycle rate (normalised time per second, smoothed), so the presentation can tell which foot
    /// is in swing at a future contact. Unity's normalised time keeps counting loops, so a drop means the state restarted:
    /// the estimate is reset then. Pure and allocation-free.
    /// </summary>
    public sealed class GaitPhaseEstimator
    {
        /// <summary>Time constant of the rate smoothing (s).</summary>
        public float Smoothing = 0.1f;

        private bool _has;
        private float _last;
        private int _samples;

        /// <summary>Phase at the last sample, in [0, 1).</summary>
        public float Phase { get; private set; }
        /// <summary>Smoothed cycle rate (cycles per second).</summary>
        public float CyclesPerSecond { get; private set; }
        /// <summary>True once a rate has been measured from at least two samples.</summary>
        public bool Valid => _samples >= 2 && CyclesPerSecond > 1e-3f;

        public void Reset()
        {
            _has = false;
            _samples = 0;
            CyclesPerSecond = 0f;
        }

        /// <summary>Feed the locomotion state's normalised time (loops included) and the frame's delta time.</summary>
        public void Push(float normalizedTime, float dt)
        {
            Phase = DribbleTouchSolver.Wrap01(normalizedTime);
            if (!_has || normalizedTime < _last || dt <= 0f)
            {
                bool restarted = _has && normalizedTime < _last;
                _has = true;
                _last = normalizedTime;
                if (restarted || _samples == 0) { _samples = 1; CyclesPerSecond = 0f; }
                return;
            }

            float rate = (normalizedTime - _last) / dt;
            _last = normalizedTime;
            if (_samples < 2) CyclesPerSecond = rate;
            else
            {
                float k = Smoothing > 0f ? 1f - (float)System.Math.Exp(-dt / Smoothing) : 1f;
                CyclesPerSecond += (rate - CyclesPerSecond) * k;
            }
            _samples = _samples < 2 ? _samples + 1 : 2;
        }

        /// <summary>Phase <paramref name="seconds"/> after the last sample, at the measured rate.</summary>
        public float PhaseAfter(float seconds) => DribbleTouchSolver.Wrap01(Phase + CyclesPerSecond * seconds);
    }
}
