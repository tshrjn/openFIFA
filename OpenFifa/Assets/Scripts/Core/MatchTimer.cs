using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# match timer. Tracks remaining time and period transitions.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MatchTimer
    {
        private readonly float _halfDuration;
        private float _remainingSeconds;
        private MatchPeriod _currentPeriod;

        /// <summary>Remaining seconds in the current half.</summary>
        public float RemainingSeconds => _remainingSeconds > 0f ? _remainingSeconds : 0f;

        /// <summary>Current match period.</summary>
        public MatchPeriod CurrentPeriod => _currentPeriod;

        /// <summary>Whether the match has ended (FullTime).</summary>
        public bool IsMatchOver => _currentPeriod == MatchPeriod.FullTime;

        /// <summary>The configured half duration in seconds.</summary>
        public float HalfDuration => _halfDuration;

        /// <summary>Elapsed time in the current half.</summary>
        public float ElapsedSeconds => _halfDuration - _remainingSeconds;

        /// <summary>Fired when the match period changes.</summary>
        public event Action<MatchPeriod> OnPeriodChanged;

        /// <summary>Fired every tick with the remaining seconds.</summary>
        public event Action<float> OnTimeUpdated;

        public MatchTimer(float halfDurationSeconds)
        {
            _halfDuration = halfDurationSeconds;
            _remainingSeconds = halfDurationSeconds;
            _currentPeriod = MatchPeriod.PreKickoff;
        }

        /// <summary>
        /// Transition from PreKickoff to FirstHalf.
        /// </summary>
        public void StartMatch()
        {
            if (_currentPeriod != MatchPeriod.PreKickoff) return;

            _remainingSeconds = _halfDuration;
            SetPeriod(MatchPeriod.FirstHalf);
        }

        /// <summary>
        /// Transition from HalfTime to SecondHalf.
        /// </summary>
        public void StartSecondHalf()
        {
            if (_currentPeriod != MatchPeriod.HalfTime) return;

            _remainingSeconds = _halfDuration;
            SetPeriod(MatchPeriod.SecondHalf);
        }

        /// <summary>
        /// Advance the timer by deltaTime. Only decrements during active play periods.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Only tick during active play periods
            if (_currentPeriod != MatchPeriod.FirstHalf && _currentPeriod != MatchPeriod.SecondHalf)
                return;

            _remainingSeconds -= deltaTime;
            OnTimeUpdated?.Invoke(RemainingSeconds);

            if (_remainingSeconds <= 0f)
            {
                _remainingSeconds = 0f;

                if (_currentPeriod == MatchPeriod.FirstHalf)
                {
                    SetPeriod(MatchPeriod.HalfTime);
                }
                else if (_currentPeriod == MatchPeriod.SecondHalf)
                {
                    SetPeriod(MatchPeriod.FullTime);
                }
            }
        }

        private void SetPeriod(MatchPeriod newPeriod)
        {
            _currentPeriod = newPeriod;
            OnPeriodChanged?.Invoke(newPeriod);
        }
    }
}
