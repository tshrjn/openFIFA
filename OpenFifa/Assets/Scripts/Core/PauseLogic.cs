namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# pause menu logic.
    /// Manages pause/resume state and time scale transitions.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class PauseLogic
    {
        private bool _isPaused;
        private float _previousTimeScale;
        private float _targetTimeScale;

        /// <summary>Whether the game is currently paused.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>The time scale that was active before pausing.</summary>
        public float PreviousTimeScale => _previousTimeScale;

        /// <summary>The target time scale (0 when paused, previous value on resume).</summary>
        public float TargetTimeScale => _targetTimeScale;

        public PauseLogic()
        {
            _isPaused = false;
            _previousTimeScale = 1f;
            _targetTimeScale = 1f;
        }

        /// <summary>
        /// Pause the game. Stores the current time scale.
        /// </summary>
        /// <param name="currentTimeScale">Current Time.timeScale before pausing.</param>
        public void Pause(float currentTimeScale)
        {
            if (_isPaused) return;

            _isPaused = true;
            _previousTimeScale = currentTimeScale;
            _targetTimeScale = 0f;
        }

        /// <summary>
        /// Resume the game. Restores the previous time scale.
        /// </summary>
        public void Resume()
        {
            if (!_isPaused) return;

            _isPaused = false;
            _targetTimeScale = _previousTimeScale;
        }

        /// <summary>
        /// Restart the match. Resets time scale to 1 and unpauses.
        /// </summary>
        public void Restart()
        {
            _isPaused = false;
            _targetTimeScale = 1f;
        }

        /// <summary>
        /// Quit to main menu. Resets time scale to 1.
        /// </summary>
        public void Quit()
        {
            _isPaused = false;
            _targetTimeScale = 1f;
        }
    }
}
