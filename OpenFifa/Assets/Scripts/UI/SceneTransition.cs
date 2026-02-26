using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Scene transition singleton with fade-to-black effect.
    /// Persists across scenes via DontDestroyOnLoad.
    /// All scene transitions should use SceneTransition.Instance.LoadScene().
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        private static SceneTransition _instance;

        [SerializeField] private float _fadeDuration = 0.5f;

        private SceneTransitionLogic _logic;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;

        /// <summary>Singleton instance.</summary>
        public static SceneTransition Instance => _instance;

        /// <summary>Whether a transition is currently active.</summary>
        public bool IsTransitioning => _logic != null && _logic.IsTransitioning;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _logic = new SceneTransitionLogic(_fadeDuration);

            SetupFadeUI();
        }

        /// <summary>
        /// Load a scene with fade-to-black transition.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (!_logic.StartTransition(sceneName)) return;
            StartCoroutine(TransitionCoroutine(sceneName));
        }

        private IEnumerator TransitionCoroutine(string sceneName)
        {
            // Fade in (to black)
            while (!_logic.FadeInComplete)
            {
                _logic.UpdateFadeIn(Time.unscaledDeltaTime);
                _canvasGroup.alpha = _logic.CurrentAlpha;
                yield return null;
            }

            // Load scene
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
            if (loadOp != null)
            {
                while (!loadOp.isDone)
                {
                    yield return null;
                }
            }

            // Fade out (reveal)
            _logic.StartFadeOut();
            while (_logic.IsTransitioning)
            {
                _logic.UpdateFadeOut(Time.unscaledDeltaTime);
                _canvasGroup.alpha = _logic.CurrentAlpha;
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void SetupFadeUI()
        {
            // Create overlay canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            // Create canvas group for alpha control
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            // Create black image
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(transform, false);

            var image = imageObj.AddComponent<Image>();
            image.color = Color.black;

            var rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
