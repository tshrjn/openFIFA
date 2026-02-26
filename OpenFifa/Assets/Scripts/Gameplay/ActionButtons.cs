using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// On-screen action buttons for iPad touch input.
    /// Pass, Shoot, Tackle are single-tap. Sprint is hold (continuous press).
    /// Buttons sized at minimum 80x80 for touch targets.
    /// </summary>
    public class ActionButtons : MonoBehaviour
    {
        [SerializeField] private Button _passButton;
        [SerializeField] private Button _shootButton;
        [SerializeField] private Button _tackleButton;
        [SerializeField] private SprintButton _sprintButton;

        private ActionButtonLogic _logic;

        /// <summary>The underlying action logic.</summary>
        public ActionButtonLogic Logic => _logic;

        /// <summary>Which buttons are currently pressed (for test verification).</summary>
        public bool IsPassPressed => _logic != null && _logic.IsPassPressed;
        public bool IsShootPressed => _logic != null && _logic.IsShootPressed;
        public bool IsTacklePressed => _logic != null && _logic.IsTacklePressed;
        public bool IsSprintPressed => _logic != null && _logic.IsSprintPressed;

        private void Awake()
        {
            _logic = new ActionButtonLogic();
        }

        private void Start()
        {
            if (_passButton != null)
                _passButton.onClick.AddListener(OnPassTapped);

            if (_shootButton != null)
                _shootButton.onClick.AddListener(OnShootTapped);

            if (_tackleButton != null)
                _tackleButton.onClick.AddListener(OnTackleTapped);
        }

        private void OnPassTapped()
        {
            _logic.PressPass();
            AnimateButton(_passButton);
        }

        private void OnShootTapped()
        {
            _logic.PressShoot();
            AnimateButton(_shootButton);
        }

        private void OnTackleTapped()
        {
            _logic.PressTackle();
            AnimateButton(_tackleButton);
        }

        /// <summary>Set sprint state from SprintButton hold behavior.</summary>
        public void SetSprinting(bool pressed)
        {
            _logic.SetSprint(pressed);
        }

        /// <summary>Consume actions after processing in PlayerController.</summary>
        public void ConsumeActions()
        {
            _logic.ConsumeActions();
        }

        private void AnimateButton(Button button)
        {
            if (button == null) return;
            // Visual feedback: scale punch
            button.transform.localScale = Vector3.one * 0.9f;
            // Will revert in next frame via LateUpdate or animation
        }

        private void LateUpdate()
        {
            // Reset button scales
            if (_passButton != null)
                _passButton.transform.localScale = Vector3.Lerp(_passButton.transform.localScale, Vector3.one, 10f * Time.deltaTime);
            if (_shootButton != null)
                _shootButton.transform.localScale = Vector3.Lerp(_shootButton.transform.localScale, Vector3.one, 10f * Time.deltaTime);
            if (_tackleButton != null)
                _tackleButton.transform.localScale = Vector3.Lerp(_tackleButton.transform.localScale, Vector3.one, 10f * Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_passButton != null) _passButton.onClick.RemoveListener(OnPassTapped);
            if (_shootButton != null) _shootButton.onClick.RemoveListener(OnShootTapped);
            if (_tackleButton != null) _tackleButton.onClick.RemoveListener(OnTackleTapped);
        }
    }

    /// <summary>
    /// Sprint button with hold behavior (continuous press).
    /// Responds to pointer down (start sprint) and pointer up (stop sprint).
    /// </summary>
    public class SprintButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private ActionButtons _actionButtons;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_actionButtons != null)
                _actionButtons.SetSprinting(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_actionButtons != null)
                _actionButtons.SetSprinting(false);
        }
    }
}
