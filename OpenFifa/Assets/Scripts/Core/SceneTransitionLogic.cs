namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# scene transition logic with fade-to-black.
    /// Manages fade-in (screen goes dark), scene load, and fade-out (reveal).
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class SceneTransitionLogic
    {
        private readonly float _fadeDuration;
        private bool _isTransitioning;
        private float _currentAlpha;
        private float _fadeElapsed;
        private bool _fadeInComplete;
        private bool _isFadingOut;
        private string _targetScene;

        /// <summary>Whether a transition is currently active.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Current fade alpha (0 = transparent, 1 = black).</summary>
        public float CurrentAlpha => _currentAlpha;

        /// <summary>Whether the fade-in (to black) phase is complete.</summary>
        public bool FadeInComplete => _fadeInComplete;

        /// <summary>Target scene name.</summary>
        public string TargetScene => _targetScene;

        public SceneTransitionLogic(float fadeDuration)
        {
            _fadeDuration = fadeDuration;
            _isTransitioning = false;
            _currentAlpha = 0f;
        }

        /// <summary>
        /// Start a transition to the target scene.
        /// Returns false if a transition is already active (blocked).
        /// </summary>
        public bool StartTransition(string targetScene)
        {
            if (_isTransitioning) return false;

            _isTransitioning = true;
            _targetScene = targetScene;
            _currentAlpha = 0f;
            _fadeElapsed = 0f;
            _fadeInComplete = false;
            _isFadingOut = false;
            return true;
        }

        /// <summary>
        /// Update fade-in (screen going dark). Call each frame with deltaTime.
        /// </summary>
        public void UpdateFadeIn(float deltaTime)
        {
            if (!_isTransitioning || _fadeInComplete) return;

            _fadeElapsed += deltaTime;
            _currentAlpha = _fadeElapsed / _fadeDuration;
            if (_currentAlpha >= 1f)
            {
                _currentAlpha = 1f;
                _fadeInComplete = true;
            }
        }

        /// <summary>
        /// Begin fade-out phase (revealing new scene).
        /// Called after scene load completes.
        /// </summary>
        public void StartFadeOut()
        {
            _fadeElapsed = 0f;
            _isFadingOut = true;
        }

        /// <summary>
        /// Update fade-out (screen becoming transparent). Call each frame.
        /// </summary>
        public void UpdateFadeOut(float deltaTime)
        {
            if (!_isTransitioning || !_isFadingOut) return;

            _fadeElapsed += deltaTime;
            _currentAlpha = 1f - (_fadeElapsed / _fadeDuration);
            if (_currentAlpha <= 0f)
            {
                _currentAlpha = 0f;
                _isTransitioning = false;
                _isFadingOut = false;
            }
        }
    }
}
