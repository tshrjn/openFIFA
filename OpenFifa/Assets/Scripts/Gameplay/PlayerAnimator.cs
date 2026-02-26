using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Bridges PlayerController state to Animator parameters.
    /// Uses AnimationStateLogic (pure C#) to determine which state to play.
    /// Placeholder animations use simple transform manipulations until Mixamo integration.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int KickHash = Animator.StringToHash("Kick");
        private static readonly int TackleHash = Animator.StringToHash("Tackle");
        private static readonly int CelebrateHash = Animator.StringToHash("Celebrate");

        [SerializeField] private PlayerController _playerController;

        private Animator _animator;
        private AnimationStateLogic _logic;
        private Rigidbody _rb;

        /// <summary>The underlying animation state logic.</summary>
        public AnimationStateLogic Logic => _logic;

        /// <summary>Current animation state.</summary>
        public AnimationStateId CurrentAnimationState => _logic.CurrentState;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _logic = new AnimationStateLogic();
            _rb = GetComponent<Rigidbody>();

            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (_playerController == null) return;

            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            bool isSprinting = _playerController.IsSprinting;

            _logic.UpdateLocomotion(speed, isSprinting);

            // Update animator parameters
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, _logic.SpeedParameter);
            }
        }

        /// <summary>
        /// Trigger kick animation.
        /// </summary>
        public void TriggerKick()
        {
            _logic.TriggerAction(AnimationActionTrigger.Kick);
            if (_animator != null)
                _animator.SetTrigger(KickHash);
        }

        /// <summary>
        /// Trigger tackle animation.
        /// </summary>
        public void TriggerTackle()
        {
            _logic.TriggerAction(AnimationActionTrigger.Tackle);
            if (_animator != null)
                _animator.SetTrigger(TackleHash);
        }

        /// <summary>
        /// Trigger celebrate animation.
        /// </summary>
        public void TriggerCelebrate()
        {
            _logic.TriggerAction(AnimationActionTrigger.Celebrate);
            if (_animator != null)
                _animator.SetTrigger(CelebrateHash);
        }

        /// <summary>
        /// Called by animation event or timer when action animation completes.
        /// </summary>
        public void OnActionComplete()
        {
            _logic.CompleteAction();
        }
    }
}
