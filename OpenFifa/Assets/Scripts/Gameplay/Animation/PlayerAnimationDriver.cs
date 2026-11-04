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
    ///
    /// Locomotion presentation: the root follows the sim exactly; the visual root carries (a) the shown facing from
    /// <see cref="BodyFacingSmoother"/> (human turn rate, faces the run direction on a forward run), (b) the action
    /// warp and (c) the lean from <see cref="LocomotionLean"/>. <see cref="LocomotionGaitSolver"/> picks the gait from
    /// the sim velocity and sets the playback rate from the measured clip speeds so the feet match the ground; it shows
    /// strafe/backpedal only when the SIM moves the player off its facing (jockey, short retreats).
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
        [Tooltip("Damping (s) of the blend-tree position: gait changes (walk/jog/sprint, start, stop) crossfade over about twice this.")]
        [SerializeField] private float parameterDampTime = 0.12f;
        [SerializeField] private float accelerationSmoothing = 0.1f;
        [Tooltip("Below this speed (m/s) the body keeps the sim facing; above it, a forward run faces its velocity.")]
        [SerializeField] private float faceVelocityAboveSpeed = 0.5f;
        [Tooltip("Turning on the spot faster than this (deg/s) adds a stepping cycle so the feet move round instead of spinning.")]
        [SerializeField] private float turnInPlaceStepRate = 150f;
        [SerializeField] private bool enableLean = true;

        [Header("Actions")]
        [SerializeField] private float actionCrossFade = 0.12f;
        [SerializeField] private int actionLayer = 0;

        [Header("IK")]
        [SerializeField] private bool enableContactIk = true;
        [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.8f;
        [SerializeField] private float ballRadius = ContactIkSolver.DefaultBallRadius;
        [Tooltip("Peak strike-foot IK weight of a running dribble touch (layered on locomotion). Below 1 keeps some of the run cycle in the leg.")]
        [SerializeField, Range(0f, 1f)] private float dribbleTouchIkWeight = 0.85f;
        [Tooltip("Look-at weight multiplier during a dribble touch (a glance at the ball, not a full head turn).")]
        [SerializeField, Range(0f, 1f)] private float dribbleTouchLookAtScale = 0.5f;

        [Header("Gait phase (dribble-touch foot)")]
        [Tooltip("Pick the touching foot from the Locomotion state's normalised time (measured cycle) instead of a nominal stride.")]
        [SerializeField] private bool useLocomotionPhase = true;
        [Tooltip("Normalised-time phase (0..1) at which the right foot strikes the ground in the locomotion clips; the left strikes half a cycle later.")]
        [SerializeField, Range(0f, 1f)] private float rightFootStrikePhase = LocomotionGaitTable.RightFootStrikePhase;
        [Tooltip("Fraction of the cycle each foot is on the ground.")]
        [SerializeField, Range(0.05f, 0.49f)] private float stanceFraction = DribbleTouchSolver.DefaultStanceFraction;
        [Tooltip("Touch IK weight multiplier while the touching foot is planted (0 = skip: never drag a grounded foot).")]
        [SerializeField, Range(0f, 1f)] private float plantedTouchIkScale = 0f;

        private static readonly int SpeedHash = Animator.StringToHash(AnimStateNames.ParamSpeed);
        private static readonly int ForwardHash = Animator.StringToHash(AnimStateNames.ParamForward);
        private static readonly int StrafeHash = Animator.StringToHash(AnimStateNames.ParamStrafe);
        private static readonly int TurnRateHash = Animator.StringToHash(AnimStateNames.ParamTurnRate);
        private static readonly int LocoRateHash = Animator.StringToHash(AnimStateNames.ParamLocoRate);
        private static readonly int ActionRateHash = Animator.StringToHash(AnimStateNames.ParamActionRate);
        private static readonly int MirrorHash = Animator.StringToHash(AnimStateNames.ParamMirror);
        private static readonly int LocomotionHash = Animator.StringToHash(AnimStateNames.Locomotion);

        private IPlayerAnimSource _source;
        private readonly AnimSnapshotInterpolator _interpolator = new AnimSnapshotInterpolator();
        private bool _hasAlphaOverride;
        private float _alphaOverride;
        private float _fixedDtOverride;
        private ILocomotionBackend _backend;
        private AnimationSyncPlanner _planner;
        private readonly LocomotionGaitSolver _gaitSolver = new LocomotionGaitSolver();
        private readonly BodyFacingSmoother _facing = new BodyFacingSmoother();
        private readonly LocomotionLean _lean = new LocomotionLean();
        private float _simYaw;
        private Vector3 _lastPos;

        private ActionSyncPlan _plan;
        private int _planActionId;
        private bool _actionStarted;
        private bool _contactFired;
        private float _simTime;
        private bool _hasLastTouchFoot;
        private ActionSide _lastTouchFoot;
        private float _lastTouchEnd = float.NegativeInfinity;
        private float _tickSeconds;
        private readonly GaitPhaseEstimator _gait = new GaitPhaseEstimator();

        private Vector3 _prevVelocity;
        private Vector3 _acceleration;
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
        /// <summary>True while a running dribble touch (IK layered on locomotion) is planned or playing.</summary>
        public bool IsLayeredActionActive => _plan != null && _plan.Profile.LayeredOnLocomotion;
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
                _tickSeconds = fixedDt;
                pos = snap.Position.ToUnity();
                yaw = SimYawToDegrees(snap.YawRad);
            }
            else
            {
                _interpolator.Reset();
                _tickSeconds = 0f; // unknown: the policy assumes 60 Hz
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
            }
            else if (_hasPrev)
            {
                Vector3 rawAcc = (vel - _prevVelocity) / dt;
                float k = accelerationSmoothing > 0f ? 1f - Mathf.Exp(-dt / accelerationSmoothing) : 1f;
                _acceleration = Vector3.Lerp(_acceleration, rawAcc, k);
            }
            bool teleported = _hasPrev && (pos - _lastPos).sqrMagnitude > 9f; // kick-off reset, restart placement
            _prevVelocity = vel;
            _lastPos = pos;
            _hasPrev = true;
            _simYaw = yaw;

            SampleGait(dt);
            PollAction(pos, vel, yaw);
            // Gait first (it decides whether the SIM strafes), then the shown facing, then the Animator.
            SimVec3 simLocal = AnimMath.WorldToLocal(vel.ToSim(), yaw);
            _gaitSolver.Solve(simLocal.X, simLocal.Z, _source.IsSprinting, out float gaitStrafe, out float gaitForward);
            UpdateBodyFacing(dt, vel, yaw, teleported);
            DriveLocomotion(dt, pos, vel, yaw, gaitStrafe, gaitForward);
            UpdateActionPlayback();
            ApplyWarp();
        }

        /// <summary>Yaw (deg) the body is shown at: the sim facing, turned at a human rate (see BodyFacingSmoother).</summary>
        public float VisualYaw => _facing.Yaw;

        /// <summary>Current gait mode (diagnostics).</summary>
        public LocomotionMode GaitMode => _gaitSolver.Mode;

        /// <summary>Playback rate applied to the locomotion state this frame (diagnostics).</summary>
        public float LocomotionRate { get; private set; } = 1f;

        /// <summary>
        /// Shown body yaw. A forward run faces its velocity (above <see cref="faceVelocityAboveSpeed"/>); strafing,
        /// standing and slow moves keep the sim facing (which faces the ball while jockeying); a full-body action hands
        /// the yaw back to the sim quickly because the sync planner's yaw warp is relative to it.
        /// </summary>
        private void UpdateBodyFacing(float dt, Vector3 vel, float simYaw, bool teleported)
        {
            float speed = Halted ? 0f : new Vector2(vel.x, vel.z).magnitude;
            bool actionOwnsYaw = _plan != null && !_plan.Profile.LayeredOnLocomotion;
            bool backend = _backend != null && _backend.IsActive;
            if (teleported || backend)
            {
                _facing.Snap(simYaw);
                _lean.Reset();
                _yawRate = 0f;
                return;
            }
            float target = simYaw;
            if (!actionOwnsYaw && !Halted && _gaitSolver.Mode == LocomotionMode.Forward && speed > faceVelocityAboveSpeed)
                target = AnimMath.YawOf(vel.ToSim());
            _facing.Step(target, speed, dt, actionOwnsYaw ? 3f : 1f);
            _yawRate = _facing.YawRate;

            if (!enableLean || (actionOwnsYaw && _actionStarted))
            {
                _lean.Step(0f, 0f, 0f, dt);
                return;
            }
            Vector3 fwd = speed > 0.1f ? vel / speed : Vector3.zero;
            _lean.Step(speed, _facing.YawRate, Vector3.Dot(_acceleration, fwd), dt);
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

        /// <summary>
        /// Applies <see cref="ActionPlaybackPolicy"/>: the tracker refreshes a prediction under the same id and snaps it to
        /// the resolved tick, so a same-id report whose contact moved re-plans (a playing clip is re-timed, not restarted);
        /// a report withdrawn before its contact aborts the action; a new running touch waits for the previous one to
        /// finish blending out.
        /// </summary>
        private void PollAction(Vector3 pos, Vector3 vel, float yaw)
        {
            PlayerAnimAction a = _source.CurrentAction;
            bool layered = !a.IsNone && _planner.ProfileFor(a.Kind).LayeredOnLocomotion;
            var decision = ActionPlaybackPolicy.Decide(_plan, _planActionId, _actionStarted, a.IsNone, a.Id, a.ContactTime,
                a.BallAtContact.ToSim(), layered, _simTime, _tickSeconds);
            switch (decision)
            {
                case ActionPlaybackDecision.Start:
                    StartPlan(a, layered, pos, vel, yaw);
                    break;
                case ActionPlaybackDecision.Replan:
                    Replan(a, pos, vel, yaw);
                    break;
                case ActionPlaybackDecision.Abort:
                    AbortAction();
                    break;
                // Keep, Defer: the current plan continues (a deferred touch is picked up once the old one ends).
            }
        }

        private ActionSyncRequest RequestFor(in PlayerAnimAction a, ActionSide side, Vector3 pos, Vector3 vel, float yaw)
        {
            Vector3 rootAtContact = a.HasRootAtContact
                ? a.RootAtContact
                : pos + vel * Mathf.Max(0f, a.ContactTime - _simTime);
            float yawAtContact = a.HasRootAtContact ? SimYawToDegrees(a.YawAtContact) : yaw;
            return new ActionSyncRequest(a.Kind, side, _simTime, a.ContactTime, pos.ToSim(),
                rootAtContact.ToSim(), yawAtContact, a.BallAtContact.ToSim(), a.Direction.ToSim(), vel.magnitude,
                EstimateNextFootfall(), pos.y);
        }

        private void StartPlan(in PlayerAnimAction a, bool layered, Vector3 pos, Vector3 vel, float yaw)
        {
            ActionSide side = a.Side;
            if (layered)
            {
                // A dribble without touches in between (another action, or a long gap) starts a new alternation.
                if (_plan == null || !_plan.Profile.LayeredOnLocomotion) _hasLastTouchFoot &= _simTime - _lastTouchEnd < 0.6f;
                float ttc = a.ContactTime - _simTime;
                if (useLocomotionPhase && _gait.Valid)
                {
                    // Measured cycle: the foot in late swing at the contact tick.
                    side = DribbleTouchSolver.ChooseFootFromPhase(_gait.Phase, _gait.CyclesPerSecond, ttc, rightFootStrikePhase);
                }
                else
                {
                    bool hasSwing = TryEstimateNextFootfall(out ActionSide swing);
                    side = DribbleTouchSolver.ChooseFoot(hasSwing, swing, ttc, vel.magnitude,
                        _hasLastTouchFoot, _lastTouchFoot, a.Side, _planner.Stride);
                }
                _hasLastTouchFoot = true;
                _lastTouchFoot = side;
            }

            var request = RequestFor(a, side, pos, vel, yaw);
            EndAction();
            _plan = _planner.Plan(request);
            _planActionId = a.Id;
            _actionStarted = false;
            _contactFired = false;
        }

        /// <summary>Same action, new contact (snapped to the resolved tick, or a refreshed prediction).</summary>
        private void Replan(in PlayerAnimAction a, Vector3 pos, Vector3 vel, float yaw)
        {
            ActionSyncPlan old = _plan;
            bool layered = old.Profile.LayeredOnLocomotion;
            // Keep the foot of a touch and the side of a clip already playing (no mid-clip mirror flip).
            ActionSide side = layered || _actionStarted ? old.Request.Side : a.Side;
            ActionSyncPlan fresh = _planner.Plan(RequestFor(a, side, pos, vel, yaw));
            if (_actionStarted && !layered)
            {
                // Clip already playing: continue from the frame shown now at the rate that lands the new contact.
                ActionTimingPlan timing = ActionPlaybackPolicy.Retime(old.Timing, fresh.Profile, _simTime, a.ContactTime);
                fresh = new ActionSyncPlan(fresh.Request, fresh.Profile, timing, fresh.Warp, fresh.Ik, fresh.HasStridePlan, fresh.Stride);
                animator.SetFloat(ActionRateHash, timing.PlaybackRate);
            }
            _plan = fresh;
            // _actionStarted and _contactFired carry over: the same action is still playing.
        }

        /// <summary>The action was withdrawn before its contact (deflection, someone else got the ball): stop it.</summary>
        private void AbortAction()
        {
            if (_plan == null) return;
            if (_plan.Profile.LayeredOnLocomotion) _lastTouchEnd = _simTime;
            else if (_actionStarted && animator.HasState(actionLayer, LocomotionHash))
                animator.CrossFadeInFixedTime(LocomotionHash, actionCrossFade * 2f, actionLayer);
            EndAction();
        }

        /// <summary>Tracks the Locomotion state's normalised time for the touch-foot choice; reset outside it.</summary>
        private void SampleGait(float dt)
        {
            if (!useLocomotionPhase || Halted || (_backend != null && _backend.IsActive) || animator.IsInTransition(0))
            {
                _gait.Reset();
                return;
            }
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.shortNameHash != LocomotionHash)
            {
                _gait.Reset();
                return;
            }
            _gait.Push(st.normalizedTime, dt);
        }

        /// <summary>
        /// Touch IK multiplier for the strike foot now: 1 in swing, down to <see cref="plantedTouchIkScale"/> while it is
        /// planted, so the IK never drags a grounded foot (foot skate). Uses the measured gait when available, otherwise
        /// the pose (the strike foot is the lower foot and at ground height).
        /// </summary>
        private float StrikeFootSwingScale(ContactEffector strike)
        {
            if (strike != ContactEffector.LeftFoot && strike != ContactEffector.RightFoot) return 1f;
            ActionSide foot = strike == ContactEffector.LeftFoot ? ActionSide.Left : ActionSide.Right;
            if (useLocomotionPhase && _gait.Valid && !animator.IsInTransition(0))
            {
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.shortNameHash == LocomotionHash)
                {
                    float w = DribbleTouchSolver.SwingWeight(foot, DribbleTouchSolver.Wrap01(st.normalizedTime),
                        rightFootStrikePhase, stanceFraction);
                    return Mathf.Lerp(plantedTouchIkScale, 1f, w);
                }
            }
            if (!animator.isHuman) return 1f;
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return 1f;
            Transform me = foot == ActionSide.Left ? lf : rf, other = foot == ActionSide.Left ? rf : lf;
            bool planted = me.position.y <= other.position.y
                           && me.position.y - transform.position.y <= ContactIkSolver.DefaultAnkleHeight + 0.05f;
            return planted ? plantedTouchIkScale : 1f;
        }

        private void UpdateActionPlayback()
        {
            if (_plan == null) return;
            ActionTimingPlan timing = _plan.Timing;

            if (_plan.Profile.LayeredOnLocomotion)
            {
                UpdateLayeredAction();
                return;
            }

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

        /// <summary>
        /// Running dribble touch: the Animator (or motion-matching backend) stays in locomotion; the touch is only the
        /// strike-foot IK envelope applied in <see cref="HandleAnimatorIK"/>. There is no clip contact event, so the
        /// contact is reported on the first frame at or past the contact tick.
        /// </summary>
        private void UpdateLayeredAction()
        {
            if (!_actionStarted && _simTime >= _plan.Ik.StrikeRampStart)
            {
                _actionStarted = true;
                ActionStarted?.Invoke(this, _plan);
            }
            if (_actionStarted && !_contactFired && _simTime >= _plan.Request.ContactTime) HandleContactEvent();
            if (_simTime > _plan.EndTime)
            {
                _lastTouchEnd = _simTime;
                EndAction();
            }
        }

        private void EndAction()
        {
            if (_plan == null) return;
            if (_actionStarted && !_plan.Profile.LayeredOnLocomotion && _backend != null && _backend.IsActive)
                _backend.SetActionOverride(false, _plan.Request.Kind);
            _plan = null;
            _actionStarted = false;
            ResetVisualRoot();
        }

        private void ResetVisualRoot()
        {
            if (visualRoot == null || visualRoot == transform) return;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = BodyRotation(0f);
        }

        /// <summary>
        /// Visual-root rotation relative to the sim root: the shown-facing offset (smoothed turn) plus the action yaw
        /// warp, then the lean (pitch forward, bank right) about the feet.
        /// </summary>
        private Quaternion BodyRotation(float warpYaw)
        {
            float yawOffset = AnimMath.DeltaAngle(_simYaw, _facing.Yaw) + warpYaw;
            return Quaternion.Euler(0f, yawOffset, 0f) * Quaternion.Euler(_lean.ForwardDeg, 0f, -_lean.RightDeg);
        }

        private void ApplyWarp()
        {
            if (visualRoot == null || visualRoot == transform)
            {
                // No separate visual: show the smoothed facing on the root itself.
                if (followSimTransform) transform.rotation = Quaternion.Euler(0f, _facing.Yaw, 0f);
                return;
            }
            if (_plan == null)
            {
                ResetVisualRoot();
                return;
            }
            Vector3 worldOffset = _plan.Warp.TranslationAt(_simTime).ToUnity();
            visualRoot.localPosition = transform.InverseTransformVector(worldOffset);
            visualRoot.localRotation = BodyRotation(_plan.Warp.YawAt(_simTime));
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

        private void DriveLocomotion(float dt, Vector3 pos, Vector3 vel, float yaw, float gaitStrafe, float gaitForward)
        {
            float speed = Halted ? 0f : vel.magnitude;
            Vector3 desired = Halted ? Vector3.zero : _source.DesiredVelocity;
            desired.y = 0f;

            if (_backend != null && _backend.IsActive)
            {
                var frame = new LocomotionFrame(_simTime, dt, pos, vel, _acceleration, desired, yaw, _yawRate,
                    _source.IsSprinting, 1f);
                _backend.DriveLocomotion(frame);
                return;
            }

            if (Halted)
            {
                // Stopped clock (whistle, half time): settle to idle over a short blend instead of popping upright.
                gaitStrafe = 0f;
                gaitForward = 0f;
            }
            else if (_gaitSolver.Mode == LocomotionMode.Idle && Mathf.Abs(_facing.YawRate) > turnInPlaceStepRate)
            {
                // Turning on the spot: a short stepping cycle (slow walk) so the feet go round with the body.
                float k = Mathf.Clamp01((Mathf.Abs(_facing.YawRate) - turnInPlaceStepRate) / 300f);
                gaitForward = Mathf.Max(gaitForward, 0.55f * LocomotionGaitTable.WalkSpeed * k);
            }

            float damp = Halted ? parameterDampTime * 1.5f : parameterDampTime;
            animator.SetFloat(SpeedHash, speed, damp, dt);
            animator.SetFloat(ForwardHash, gaitForward, damp, dt);
            animator.SetFloat(StrafeHash, gaitStrafe, damp, dt);
            animator.SetFloat(TurnRateHash, Halted ? 0f : _yawRate, damp, dt);

            // Rate from the blend the tree shows NOW (damped), so the feet match the ground through every transition.
            float shownF = animator.GetFloat(ForwardHash), shownS = animator.GetFloat(StrafeHash);
            float shown = Mathf.Sqrt(shownF * shownF + shownS * shownS);
            float rate = speed < _gaitSolver.IdleSpeed ? 1f : _gaitSolver.RateFor(speed, shown);
            if (!Halted && _plan != null && _plan.HasStridePlan && !_actionStarted && _simTime < _plan.Ik.PlantTime)
                rate = _plan.Stride.CadenceScale; // run-up: land the plant foot on time
            animator.SetFloat(LocoRateHash, rate);
            LocomotionRate = rate;
        }

        private ActionSide EstimateNextFootfall() => TryEstimateNextFootfall(out ActionSide next) ? next : ActionSide.Right;

        /// <summary>The foot in the air now (lands next), read from the pose; false when the rig has no humanoid feet.</summary>
        private bool TryEstimateNextFootfall(out ActionSide next)
        {
            next = ActionSide.Right;
            if (animator == null || !animator.isHuman) return false;
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf == null || rf == null) return false;
            // The lower foot is in stance; the other one lands next.
            next = lf.position.y <= rf.position.y ? ActionSide.Right : ActionSide.Left;
            return true;
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
                bool layered = _plan.Profile.LayeredOnLocomotion;
                // Dribble touch: short in/out envelope around the contact tick, capped so the run cycle still shows.
                float ws = ik.StrikeWeightAt(_simTime) * (layered ? dribbleTouchIkWeight : 1f);
                if (layered && ws > 0f) ws *= StrikeFootSwingScale(ik.StrikeEffector);
                if (ws > 0f) ApplyEffector(ik.StrikeEffector, ik.StrikeTarget.ToUnity(), ws);
                float wp = ik.PlantWeightAt(_simTime);
                if (wp > 0f) ApplyEffector(ik.PlantEffector, ik.PlantTarget.ToUnity(), wp);
                if (_plan.Request.Kind != AnimActionKind.Header && ws > 0f)
                {
                    animator.SetLookAtWeight(ws * lookAtWeight * (layered ? dribbleTouchLookAtScale : 1f), 0.2f, 0.9f, 0f, 0.5f);
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
