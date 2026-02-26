using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# match state machine with enforced valid transitions.
    /// Supports pause/resume with state preservation.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MatchStateMachine
    {
        private static readonly Dictionary<MatchState, HashSet<MatchState>> ValidTransitions =
            new Dictionary<MatchState, HashSet<MatchState>>
            {
                { MatchState.PreKickoff, new HashSet<MatchState> { MatchState.FirstHalf } },
                { MatchState.FirstHalf, new HashSet<MatchState> { MatchState.HalfTime, MatchState.GoalCelebration } },
                { MatchState.HalfTime, new HashSet<MatchState> { MatchState.SecondHalf } },
                { MatchState.SecondHalf, new HashSet<MatchState> { MatchState.FullTime, MatchState.GoalCelebration } },
                { MatchState.FullTime, new HashSet<MatchState>() },
                { MatchState.GoalCelebration, new HashSet<MatchState> { MatchState.PreKickoff } },
                { MatchState.Paused, new HashSet<MatchState>() } // Resume handled separately
            };

        private MatchState _currentState;
        private MatchState _previousState;

        /// <summary>Current match state.</summary>
        public MatchState CurrentState => _currentState;

        /// <summary>Previous match state (before current transition or pause).</summary>
        public MatchState PreviousState => _previousState;

        /// <summary>Fired on every state transition. Args: (oldState, newState).</summary>
        public event Action<MatchState, MatchState> OnStateChanged;

        public MatchStateMachine()
        {
            _currentState = MatchState.PreKickoff;
            _previousState = MatchState.PreKickoff;
        }

        /// <summary>
        /// Transition to a new state. Throws InvalidOperationException for invalid transitions.
        /// </summary>
        public void TransitionTo(MatchState newState)
        {
            if (newState == MatchState.Paused)
            {
                Pause();
                return;
            }

            if (!ValidTransitions.ContainsKey(_currentState) ||
                !ValidTransitions[_currentState].Contains(newState))
            {
                throw new InvalidOperationException(
                    $"Invalid match state transition: {_currentState} -> {newState}");
            }

            var oldState = _currentState;
            _previousState = oldState;
            _currentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// Pause the match. Preserves current state for resume.
        /// </summary>
        public void Pause()
        {
            if (_currentState == MatchState.Paused) return;

            _previousState = _currentState;
            var oldState = _currentState;
            _currentState = MatchState.Paused;
            OnStateChanged?.Invoke(oldState, MatchState.Paused);
        }

        /// <summary>
        /// Resume from pause. Restores the previous state.
        /// </summary>
        public void Resume()
        {
            if (_currentState != MatchState.Paused) return;

            var oldState = _currentState;
            _currentState = _previousState;
            OnStateChanged?.Invoke(oldState, _currentState);
        }
    }
}
