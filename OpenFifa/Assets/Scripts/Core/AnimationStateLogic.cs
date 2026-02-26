namespace OpenFifa.Core
{
    /// <summary>
    /// All possible animation states for a player.
    /// </summary>
    public enum AnimationStateId
    {
        Idle = 0,
        Run = 1,
        Sprint = 2,
        Kick = 3,
        Tackle = 4,
        Celebrate = 5
    }

    /// <summary>
    /// Action triggers that override locomotion state.
    /// </summary>
    public enum AnimationActionTrigger
    {
        Kick,
        Tackle,
        Celebrate
    }

    /// <summary>
    /// Pure C# animation state machine logic.
    /// Determines which animation state should be active based on
    /// player speed, sprint state, and action triggers.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class AnimationStateLogic
    {
        private const float WalkThreshold = 0.5f;
        private const float MaxSpeed = 10.5f; // Sprint speed for normalization

        private AnimationStateId _currentState;
        private AnimationStateId _lastLocomotionState;
        private float _lastSpeed;
        private bool _lastSprinting;
        private bool _isInAction;

        /// <summary>Current animation state.</summary>
        public AnimationStateId CurrentState => _currentState;

        /// <summary>
        /// Speed parameter normalized to 0-1 for Blend Tree.
        /// 0 = Idle, ~0.5 = Run, 1.0 = Sprint.
        /// </summary>
        public float SpeedParameter { get; private set; }

        /// <summary>Whether the player is in an action state (Kick, Tackle, Celebrate).</summary>
        public bool IsInActionState => _isInAction;

        public AnimationStateLogic()
        {
            _currentState = AnimationStateId.Idle;
            _lastLocomotionState = AnimationStateId.Idle;
            _isInAction = false;
            SpeedParameter = 0f;
        }

        /// <summary>
        /// Update locomotion state based on speed and sprint.
        /// Does not override action states.
        /// </summary>
        /// <param name="speed">Current player speed magnitude.</param>
        /// <param name="isSprinting">Whether sprint is active.</param>
        public void UpdateLocomotion(float speed, bool isSprinting)
        {
            _lastSpeed = speed;
            _lastSprinting = isSprinting;

            // Normalize speed to 0-1
            SpeedParameter = speed / MaxSpeed;
            if (SpeedParameter > 1f) SpeedParameter = 1f;
            if (SpeedParameter < 0f) SpeedParameter = 0f;

            // Determine locomotion state
            AnimationStateId locomotion;
            if (speed < WalkThreshold)
            {
                locomotion = AnimationStateId.Idle;
            }
            else if (isSprinting && speed > 5f)
            {
                locomotion = AnimationStateId.Sprint;
            }
            else
            {
                locomotion = AnimationStateId.Run;
            }

            _lastLocomotionState = locomotion;

            // Only update current state if not in an action
            if (!_isInAction)
            {
                _currentState = locomotion;
            }
        }

        /// <summary>
        /// Trigger an action state (Kick, Tackle, Celebrate).
        /// Overrides locomotion until CompleteAction() is called.
        /// </summary>
        public void TriggerAction(AnimationActionTrigger action)
        {
            _isInAction = true;

            switch (action)
            {
                case AnimationActionTrigger.Kick:
                    _currentState = AnimationStateId.Kick;
                    break;
                case AnimationActionTrigger.Tackle:
                    _currentState = AnimationStateId.Tackle;
                    break;
                case AnimationActionTrigger.Celebrate:
                    _currentState = AnimationStateId.Celebrate;
                    break;
            }
        }

        /// <summary>
        /// Complete the current action. Returns to locomotion state
        /// based on last known speed/sprint values.
        /// </summary>
        public void CompleteAction()
        {
            _isInAction = false;
            _currentState = _lastLocomotionState;
        }
    }
}
