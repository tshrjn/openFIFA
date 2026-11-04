using System;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Presentation-only animation driver for one player. Reads sim state through <see cref="IPlayerAnimSource"/>,
    /// drives either the Animator blend tree or an <see cref="ILocomotionBackend"/> (motion matching), plays
    /// contact-synchronised actions planned by <see cref="AnimationSyncPlanner"/>, and applies warp offsets and
    /// foot/ball IK to the visual model. Root motion is always off: the sim owns the position.
    ///
    /// Hierarchy: PlayerRoot (this, follows the sim) / VisualRoot (warp offsets) / Model (Animator + PlayerAnimatorHooks).
    ///
    /// Units: the source reports yaw in radians (sim units); this class converts once, on read, with
    /// <see cref="SimYawToDegrees"/>, and works in degrees everywhere else (Unity, AnimMath, the sync planner).
    ///
    /// Smoothing: when an interpolation alpha is available (the source implements <see cref="IPlayerAnimInterpolation"/>,
    /// or code calls <see cref="SetInterpolation"/>), the root is rendered between the previous and the current sim
    /// snapshot with <see cref="AnimSnapshotInterpolator"/>, and the presentation clock used for action timing is the
    /// same interpolated time, so contact frames stay in step with an interpolated ball. Without an alpha the latest
    /// snapshot is shown as-is.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Component implementing IPlayerAnimSource. Can also be set from code via Source.")]
        [SerializeField] private MonoBehaviour sourceBehaviour;
        [SerializeField] private Animator animator;
        [Tooltip("Transform that receives the warp offset. Should be a child of this transform and the parent of the model.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Copy the sim position/facing onto this transform every frame. Turn off if another component already does.")]
        [SerializeField] private bool followSimTransform = true;

        [Header("Locomotion (blend tree fallback)")]
        [Tooltip("Natural speeds (m/s) of the moving clips in the locomotion blend tree, ascending.")]
        [SerializeField] private float[] authoredSpeeds = { 1.4f, 3.4f, 6.5f };
        [SerializeField] private float minLocoRate = 0.6f;
        [SerializeField] private float maxLocoRate = 1.4f;
        [SerializeField] private float parameterDampTime = 0.08f;
        [SerializeField] private float accelerationSmoothing = 0.1f;

        [Header("Actions")]
        [SerializeField] private float actionCrossFade = 0.08f;
        [SerializeField] private int actionLayer = 0;

        [Header("IK")]
        [SerializeField] private bool enableContactIk = true;
        [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.8f;
        [SerializeField] private float ballRadius = ContactIkSolver.DefaultBallRadius;

        private static readonly int SpeedHash = Animator.StringToHash(AnimStateNames.ParamSpeed);
        private static readonly int ForwardHash = Animator.StringToHash(AnimStateNames.ParamForward);
        private static readonly int StrafeHash = Animator.StringToHash(AnimStateNames.ParamStrafe);
        private static readonly int TurnRateHash = Animator.StringToHash(AnimStateNames.ParamTurnRate);
        private static readonly int LocoRateHash = Animator.StringToHash(AnimStateNames.ParamLocoRate);
        private static readonly int ActionRateHash = Animator.StringToHash(AnimStateNames.ParamActionRate);
        private static readonly int MirrorHash = Animator.StringToHash(AnimStateNames.ParamMirror);

        private IPlayerAnimSource _source;
        private readonly AnimSnapshotInterpolator _interpolator = new AnimSnapshotInterpolator();
        private bool _hasAlphaOverride;
        private float _alphaOverride;
        private float _fixedDtOverride;
        private ILocomotionBackend _backend;
        private AnimationSyncPlanner _planner;
        private LocomotionRateSolver _rateSolver;

        private ActionSyncPlan _plan;
        private int _planActionId;
        private bool _actionStarted;
        private bool _contactFired;
        private float _simTime;

        private Vector3 _prevVelocity;
        private Vector3 _acceleration;
        private float _prevYaw;
        private float _yawRate;
        private bool _hasPrev;

        private bool _poseFrozen;
        private float _speedBeforeFreeze = 1f;

        private readonly Vector3[] _extIkPos = new Vector3[4];
        private readonly float[] _extIkWeight = new float[4];

        /// <summary>Fired when an action clip starts playing.</summary>
        public event Action<PlayerAnimationDriver, ActionSyncPlan> ActionStarted;
        /// <summary>Fired on the clip's contact frame, with (presentation time - sim contact time) in seconds.</summary>
        public event Action<PlayerAnimationDriver, ActionSyncPlan, float> ContactReached;
        /// <summary>Fired inside OnAnimatorIK after contact IK: hook for dribble touches, foot locking, look-at.</summary>
        public event Action<Animator, int> IkPass;

        public IPlayerAnimSource Source
        {
            get => _source;
            set
            {
                if (!ReferenceEquals(_source, value)) _interpolator.Reset();
                _source = value;
            }
        }

        /// <summary>Converts a sim yaw (radians, 0 = +Z, positive toward +X) to the degrees Unity and AnimMath use.</summary>
        public static float SimYawToDegrees(float yawRad) => yawRad * AnimMath.Rad2Deg;

        /// <summary>
        /// Supplies the interpolation alpha (fixed-step accumulator remainder / fixedDt) for this frame, for sources
        /// that do not implement <see cref="IPlayerAnimInterpolation"/>. Overrides the source's value until cleared.
        /// </summary>
        public void SetInterpolation(float alpha, float fixedDt)
        {
            _hasAlphaOverride = true;
            _alphaOverride = alpha;
            _fixedDtOverride = fixedDt;
        }

        public void ClearInterpolation() => _hasAlphaOverride = false;

        private bool TryGetInterpolation(out float alpha, out float fixedDt)
        {
            if (_hasAlphaOverride)
            {
                alpha = _alphaOverride;
                fixedDt = _fixedDtOverride;
                return fixedDt > 0f;
            }
            if (_source is IPlayerAnimInterpolation interp)
            {
                alpha = interp.InterpolationAlpha;
                fixedDt = interp.FixedDeltaTime;
                return fixedDt > 0f;
            }
            alpha = 1f;
            fixedDt = 0f;
            return false;
        }

        public Animator Animator => animator;

        /// <summary>
        /// The sim is not advancing (paused, frozen, half time, full time): locomotion gets zero speed, so players
        /// stand instead of running on the spot with the last sim velocity. Set by the match controller every frame.
        /// </summary>
        public bool Halted { get; set; }

        /// <summary>Hold the current pose (Animator.speed 0) while set, e.g. on the pause menu; restored when cleared.</summary>
        public bool FreezePose { get; set; }
        public ActionSyncPlan CurrentPlan => _plan;
        public bool IsActionActive => _plan != null;
        /// <summary>Last measured contact error (s): positive = the animation reached contact late.</summary>
        public float LastContactError { get; private set; }
        /// <summary>Current warp translation (world) of the visual relative to the sim root. Backends that place the body themselves (motion matching) add this.</summary>
        public Vector3 CurrentWarpOffset => _plan != null ? _plan.Warp.TranslationAt(_simTime).ToUnity() : Vector3.zero;
        /// <summary>Current warp yaw (deg) of the visual relative to the sim facing.</summary>
        public float CurrentWarpYaw => _plan != null ? _plan.Warp.YawAt(_simTime) : 0f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (visualRoot == null && animator != null) visualRoot = animator.transform;
            if (_source == null) _source = sourceBehaviour as IPlayerAnimSource;
            if (_source == null) _source = GetComponent<IPlayerAnimSource>();
            _backend = GetComponent<ILocomotionBackend>();
            _planner = new AnimationSyncPlanner { BallRadius = ballRadius };
            _rateSolver = new LocomotionRateSolver(authoredSpeeds, minLocoRate, maxLocoRate);

            if (animator != null)
            {
                animator.applyRootMotion = false; // the sim owns position
                var hooks = animator.GetComponent<PlayerAnimatorHooks>();
                if (hooks == null) hooks = animator.gameObject.AddComponent<PlayerAnimatorHooks>();
                hooks.Driver = this;
            }
        }

        private void OnValidate()
        {
            if (sourceBehaviour != null && !(sourceBehaviour is IPlayerAnimSource))
            {
                Debug.LogWarning($"{name}: sourceBehaviour does not implement IPlayerAnimSource", this);
                sourceBehaviour = null;
            }
        }

        private void Update()
        {
            if (_source == null || animator == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float rawTime = _source.SimTime;
            Vector3 rawPos = _source.Position;
            float rawYawRad = _source.FacingYaw;
            Vector3 pos;
            float yaw; // degrees from here on
            if (TryGetInterpolation(out float alpha, out float fixedDt))
            {
                _interpolator.Push(rawTime, rawPos.ToSim(), rawYawRad);
                AnimSnapshot snap = _interpolator.Sample(alpha, fixedDt);
                _simTime = snap.Time;
                pos = snap.Position.ToUnity();
                yaw = SimYawToDegrees(snap.YawRad);
            }
            else
            {
                _interpolator.Reset();
                _simTime = rawTime;
                pos = rawPos;
                yaw = SimYawToDegrees(rawYawRad);
            }
            ApplyPoseFreeze();
            Vector3 vel = Halted ? Vector3.zero : _source.Velocity;
            vel.y = 0f;

            if (followSimTransform)
                transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            if (Halted)
            {
                _acceleration = Vector3.zero;
                _yawRate = 0f;
            }
            else if (_hasPrev)
            {
                Vector3 rawAcc = (vel - _prevVelocity) / dt;
                float k = accelerationSmoothing > 0f ? 1f - Mathf.Exp(-dt / accelerationSmoothing) : 1f;
                _acceleration = Vector3.Lerp(_acceleration, rawAcc, k);
                _yawRate = Mathf.Lerp(_yawRate, AnimMath.DeltaAngle(_prevYaw, yaw) / dt, k);
            }
            _prevVelocity = vel;
            _prevYaw = yaw;
            _hasPrev = true;

            PollAction(pos, vel, yaw);
            DriveLocomotion(dt, pos, vel, yaw);
            UpdateActionPlayback();
            ApplyWarp();
        }

        private void ApplyPoseFreeze()
        {
            if (FreezePose == _poseFrozen) return;
            if (FreezePose)
            {
                _speedBeforeFreeze = animator.speed;
                animator.speed = 0f;
            }
            else
            {
                animator.speed = _speedBeforeFreeze;
            }
            _poseFrozen = FreezePose;
        }

        // ------------------------------------------------------------------ actions

        private void PollAction(Vector3 pos, Vector3 vel, float yaw)
        {
            PlayerAnimAction a = _source.CurrentAction;
            if (a.IsNone || a.Id == _planActionId) return;

            Vector3 rootAtContact = a.HasRootAtContact
                ? a.RootAtContact
                : pos + vel * Mathf.Max(0f, a.ContactTime - _simTime);
            float yawAtContact = a.HasRootAtContact ? SimYawToDegrees(a.YawAtContact) : yaw;

            var request = new ActionSyncRequest(a.Kind, a.Side, _simTime, a.ContactTime, pos.ToSim(),
                rootAtContact.ToSim(), yawAtContact, a.BallAtContact.ToSim(), a.Direction.ToSim(), vel.magnitude,
                EstimateNextFootfall(), pos.y);

            EndAction();
            _plan = _planner.Plan(request);
            _planActionId = a.Id;
            _actionStarted = false;
            _contactFired = false;
        }

        private void UpdateActionPlayback()
        {
            if (_plan == null) return;
            ActionTimingPlan timing = _plan.Timing;

            if (!_actionStarted && _simTime >= timing.StartTime)
            {
                // Compensate for frame granularity: we may already be slightly past the planned start.
                float clipOffset = timing.StartOffset + (_simTime - timing.StartTime) * timing.PlaybackRate;
                string stateName = AnimStateNames.StateFor(_plan.Request.Kind);
                int state = Animator.StringToHash(stateName);
                animator.SetFloat(ActionRateHash, timing.PlaybackRate);
                animator.SetBool(MirrorHash, _plan.Request.Side == ActionSide.Left);
                if (animator.HasState(actionLayer, state))
                    animator.CrossFadeInFixedTime(state, actionCrossFade, actionLayer, clipOffset);
                else
                    Debug.LogWarning($"{name}: Animator has no state '{stateName}'", this);
                _actionStarted = true;
                if (_backend != null && _backend.IsActive) _backend.SetActionOverride(true, _plan.Request.Kind);
                ActionStarted?.Invoke(this, _plan);
            }

            if (_simTime > _plan.EndTime) EndAction();
        }

        private void EndAction()
        {
            if (_plan == null) return;
            if (_actionStarted && _backend != null && _backend.IsActive)
                _backend.SetActionOverride(false, _plan.Request.Kind);
            _plan = null;
            _actionStarted = false;
            ResetVisualRoot();
        }

        private void ResetVisualRoot()
        {
            if (visualRoot == null || visualRoot == transform) return;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        private void ApplyWarp()
        {
            if (visualRoot == null || visualRoot == transform) return;
            if (_plan == null)
            {
                ResetVisualRoot();
                return;
            }
            Vector3 worldOffset = _plan.Warp.TranslationAt(_simTime).ToUnity();
            visualRoot.localPosition = transform.InverseTransformVector(worldOffset);
            visualRoot.localRotation = Quaternion.Euler(0f, _plan.Warp.YawAt(_simTime), 0f);
        }

        /// <summary>Called by <see cref="PlayerAnimatorHooks"/> on the clip's contact frame.</summary>
        public void HandleContactEvent()
        {
            if (_plan == null || _contactFired) return;
            _contactFired = true;
            LastContactError = _simTime - _plan.Request.ContactTime;
            ContactReached?.Invoke(this, _plan, LastContactError);
        }

        // ------------------------------------------------------------------ locomotion

        private void DriveLocomotion(float dt, Vector3 pos, Vector3 vel, float yaw)
        {
            float speed = vel.magnitude;
            float rate = _rateSolver.RateFor(speed);
            if (!Halted && _plan != null && _plan.HasStridePlan && !_actionStarted && _simTime < _plan.Ik.PlantTime)
                rate = _plan.Stride.CadenceScale; // run-up: land the plant foot on time

            Vector3 desired = Halted ? Vector3.zero : _source.DesiredVelocity;
            desired.y = 0f;
            var frame = new LocomotionFrame(_simTime, dt, pos, vel, _acceleration, desired, yaw, _yawRate,
                _source.IsSprinting, rate);

            if (_backend != null && _backend.IsActive)
            {
                _backend.DriveLocomotion(frame);
                return;
            }

            if (Halted)
            {
                // Stopped clock: snap to idle instead of damping down from a sprint over several frames.
                animator.SetFloat(SpeedHash, 0f);
                animator.SetFloat(ForwardHash, 0f);
                animator.SetFloat(StrafeHash, 0f);
                animator.SetFloat(TurnRateHash, 0f);
                animator.SetFloat(LocoRateHash, 1f);
                return;
            }

            SimVec3 local = AnimMath.WorldToLocal(vel.ToSim(), yaw);
            animator.SetFloat(SpeedHash, speed, parameterDampTime, dt);
            animator.SetFloat(ForwardHash, local.Z, parameterDampTime, dt);
            animator.SetFloat(StrafeHash, local.X, parameterDampTime, dt);
            animator.SetFloat(TurnRateHash, _yawRate, parameterDampTime, dt);
            animator.SetFloat(LocoRateHash, rate);
        }

        private ActionSide EstimateNextFootfall()
        {
            if (animator == null || !animator.isHuman) return ActionSide.Right;
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return ActionSide.Right;
            // The lower foot is in stance; the other one lands next.
            return lf.position.y <= rf.position.y ? ActionSide.Right : ActionSide.Left;
        }

        // ------------------------------------------------------------------ IK

        /// <summary>Set an external IK goal (dribble touch, foot lock). Weight 0 clears it.</summary>
        public void SetExternalIkGoal(AvatarIKGoal goal, Vector3 position, float weight)
        {
            int i = (int)goal;
            _extIkPos[i] = position;
            _extIkWeight[i] = Mathf.Clamp01(weight);
        }

        public void ClearExternalIkGoals()
        {
            for (int i = 0; i < _extIkWeight.Length; i++) _extIkWeight[i] = 0f;
        }

        /// <summary>Called by <see cref="PlayerAnimatorHooks"/>. Requires "IK Pass" on the layer.</summary>
        public void HandleAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            for (int i = 0; i < 4; i++)
            {
                animator.SetIKPositionWeight((AvatarIKGoal)i, 0f);
                animator.SetIKRotationWeight((AvatarIKGoal)i, 0f);
            }
            animator.SetLookAtWeight(0f);

            if (enableContactIk && _plan != null && _actionStarted)
            {
                ContactIkTargets ik = _plan.Ik;
                float ws = ik.StrikeWeightAt(_simTime);
                if (ws > 0f) ApplyEffector(ik.StrikeEffector, ik.StrikeTarget.ToUnity(), ws);
                float wp = ik.PlantWeightAt(_simTime);
                if (wp > 0f) ApplyEffector(ik.PlantEffector, ik.PlantTarget.ToUnity(), wp);
                if (_plan.Request.Kind != AnimActionKind.Header && ws > 0f)
                {
                    animator.SetLookAtWeight(ws * lookAtWeight, 0.2f, 0.9f, 0f, 0.5f);
                    animator.SetLookAtPosition(_plan.Request.BallAtContact.ToUnity());
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (_extIkWeight[i] <= 0f) continue;
                animator.SetIKPositionWeight((AvatarIKGoal)i, _extIkWeight[i]);
                animator.SetIKPosition((AvatarIKGoal)i, _extIkPos[i]);
            }

            IkPass?.Invoke(animator, layerIndex);
        }

        private void ApplyEffector(ContactEffector effector, Vector3 target, float weight)
        {
            switch (effector)
            {
                case ContactEffector.LeftFoot: SetGoal(AvatarIKGoal.LeftFoot, target, weight); break;
                case ContactEffector.RightFoot: SetGoal(AvatarIKGoal.RightFoot, target, weight); break;
                case ContactEffector.LeftHand: SetGoal(AvatarIKGoal.LeftHand, target, weight); break;
                case ContactEffector.RightHand: SetGoal(AvatarIKGoal.RightHand, target, weight); break;
                case ContactEffector.Head:
                    // Mecanim has no head position goal: aim head and torso at the ball; the warp moves the body under it.
                    animator.SetLookAtWeight(weight * lookAtWeight, 0.4f, 1f, 0f, 0.5f);
                    animator.SetLookAtPosition(target);
                    break;
            }
        }

        private void SetGoal(AvatarIKGoal goal, Vector3 target, float weight)
        {
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, target);
        }
    }
}
