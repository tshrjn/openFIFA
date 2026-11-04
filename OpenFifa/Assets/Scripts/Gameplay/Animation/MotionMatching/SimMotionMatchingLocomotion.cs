using MotionMatching;
using OpenFifa.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    using TrajectoryFeature = MotionMatchingData.TrajectoryFeature;

    /// <summary>
    /// Motion-matching locomotion backend (JLPM22/MotionMatching) driven by the sim instead of player input.
    /// Put it on the player root next to <see cref="PlayerAnimationDriver"/>. The sim owns position, so this
    /// controller sits at the sim position, predicts the future trajectory from sim velocity and intent
    /// (<see cref="TrajectoryPredictor"/>), and pulls the motion-matching simulation bone onto the sim position
    /// (plus the driver's warp offset) with a short damping and a hard clamp.
    /// Compiled only when the com.jlpm.motionmatching package is installed (see the asmdef).
    /// </summary>
    [RequireComponent(typeof(PlayerAnimationDriver))]
    public sealed class SimMotionMatchingLocomotion : MotionMatchingCharacterController, ILocomotionBackend
    {
        [Header("Features (names in the MotionMatchingData asset)")]
        public string TrajectoryPositionFeatureName = "FuturePosition";
        public string TrajectoryDirectionFeatureName = "FutureDirection";

        [Header("Avatar")]
        [Tooltip("Disabled during full-body actions so the Animator owns the pose; re-enabling inertializes back.")]
        public MotionMatchingSkinnedMeshRenderer AvatarRenderer;

        [Header("Prediction")]
        [Range(0.01f, 1f)] public float VelocityHalflife = 0.2f;
        [Range(0.01f, 1f)] public float YawHalflife = 0.15f;
        [Tooltip("Dot product between consecutive intent directions below which a new search is forced.")]
        [Range(-1f, 1f)] public float BigChangeDot = 0.5f;

        [Header("Sim ownership")]
        [Range(0.01f, 1f)] public float PositionAdjustmentHalflife = 0.08f;
        [Tooltip("Maximum distance (m) the animated body may drift from the sim position.")]
        [Range(0f, 1f)] public float MaxDistanceToSim = 0.12f;

        private PlayerAnimationDriver _driver;
        private TrajectoryPredictor _predictor;
        private LocomotionFrame _frame;
        private bool _hasFrame;
        private Vector3 _lastIntentDir;

        private int _posIndex = -1;
        private int _dirIndex = -1;
        private float[] _posTimes;
        private float[] _dirTimes;
        private TrajectorySample[] _posSamples;
        private TrajectorySample[] _dirSamples;
        private float _timesForFrameTime = -1f;

        public bool IsActive => isActiveAndEnabled && MotionMatching != null && MotionMatching.MMData != null;

        private void Awake()
        {
            _driver = GetComponent<PlayerAnimationDriver>();
            _predictor = new TrajectoryPredictor();
        }

        private void Start()
        {
            var features = MotionMatching.MMData.TrajectoryFeatures;
            for (int i = 0; i < features.Count; ++i)
            {
                if (features[i].Name == TrajectoryPositionFeatureName) _posIndex = i;
                if (features[i].Name == TrajectoryDirectionFeatureName) _dirIndex = i;
            }
            Debug.Assert(_posIndex >= 0, $"{name}: trajectory feature '{TrajectoryPositionFeatureName}' not found");
            Debug.Assert(_dirIndex >= 0, $"{name}: trajectory feature '{TrajectoryDirectionFeatureName}' not found");
        }

        public void DriveLocomotion(in LocomotionFrame frame)
        {
            _frame = frame;
            _hasFrame = true;
            Vector3 intent = frame.DesiredVelocity.sqrMagnitude > 1e-4f ? frame.DesiredVelocity.normalized : Vector3.zero;
            if (Vector3.Dot(intent, _lastIntentDir) < BigChangeDot && (intent != Vector3.zero || _lastIntentDir != Vector3.zero))
                NotifyInputChangedQuickly();
            _lastIntentDir = intent;
        }

        public void SetActionOverride(bool active, AnimActionKind kind)
        {
            if (AvatarRenderer != null) AvatarRenderer.enabled = !active;
        }

        protected override void OnUpdate()
        {
            if (!_hasFrame || _posIndex < 0 || _dirIndex < 0) return;
            EnsureHorizonTimes();

            _predictor.VelocityHalflife = VelocityHalflife;
            _predictor.YawHalflife = YawHalflife;
            SimVec3 pos = _frame.Position.ToSim();
            SimVec3 vel = _frame.Velocity.ToSim();
            SimVec3 acc = _frame.Acceleration.ToSim();
            SimVec3 desired = _frame.DesiredVelocity.ToSim();
            float desiredYaw = desired.Flat.SqrMagnitude > 0.04f && Vector3.Dot(_frame.DesiredVelocity, Forward(_frame.FacingYaw)) > -0.2f
                ? AnimMath.YawOf(desired)
                : _frame.FacingYaw; // keep facing when backpedalling / jockeying / stopping
            _predictor.PredictMany(pos, vel, acc, desired, _frame.FacingYaw, _frame.YawRate, desiredYaw, _posTimes, _posSamples);
            _predictor.PredictMany(pos, vel, acc, desired, _frame.FacingYaw, _frame.YawRate, desiredYaw, _dirTimes, _dirSamples);

            PullTowardSim();
        }

        private void EnsureHorizonTimes()
        {
            float frameTime = DatabaseDeltaTime;
            if (frameTime <= 0f || Mathf.Approximately(frameTime, _timesForFrameTime)) return;
            int[] posFrames = MotionMatching.MMData.TrajectoryFeatures[_posIndex].FramesPrediction;
            int[] dirFrames = MotionMatching.MMData.TrajectoryFeatures[_dirIndex].FramesPrediction;
            _posTimes = new float[posFrames.Length];
            _dirTimes = new float[dirFrames.Length];
            for (int i = 0; i < posFrames.Length; i++) _posTimes[i] = posFrames[i] * frameTime;
            for (int i = 0; i < dirFrames.Length; i++) _dirTimes[i] = dirFrames[i] * frameTime;
            _posSamples = new TrajectorySample[_posTimes.Length];
            _dirSamples = new TrajectorySample[_dirTimes.Length];
            _timesForFrameTime = frameTime;
        }

        private void PullTowardSim()
        {
            Vector3 target = transform.position + (_driver != null ? _driver.CurrentWarpOffset : Vector3.zero);
            Vector3 mm = MotionMatching.transform.position;
            Vector3 diff = target - mm;
            diff.y = 0f;
            float k = 1f - Mathf.Exp(-0.69314718f * Time.deltaTime / Mathf.Max(1e-4f, PositionAdjustmentHalflife));
            Vector3 adjust = diff * k;
            Vector3 after = mm + adjust;
            Vector3 off = after - target;
            off.y = 0f;
            if (off.magnitude > MaxDistanceToSim) adjust += -off + off.normalized * MaxDistanceToSim;
            MotionMatching.SetPosAdjustment(new float3(adjust.x, 0f, adjust.z));
        }

        private static Vector3 Forward(float yaw) => AnimMath.Forward(yaw).ToUnity();

        public override void GetTrajectoryFeature(TrajectoryFeature feature, int index, Transform character, NativeArray<float> output)
        {
            if (!feature.SimulationBone)
            {
                output[0] = 0f;
                if (output.Length > 1) output[1] = 0f;
                return;
            }
            switch (feature.FeatureType)
            {
                case TrajectoryFeature.Type.Position:
                {
                    Vector3 world = _posSamples != null && index < _posSamples.Length
                        ? _posSamples[index].Position.ToUnity() + (_driver != null ? _driver.CurrentWarpOffset : Vector3.zero)
                        : transform.position;
                    Vector3 local = character.InverseTransformPoint(new Vector3(world.x, character.position.y, world.z));
                    output[0] = local.x;
                    output[1] = local.z;
                    break;
                }
                case TrajectoryFeature.Type.Direction:
                {
                    float yaw = _dirSamples != null && index < _dirSamples.Length ? _dirSamples[index].Yaw : _frame.FacingYaw;
                    if (_driver != null) yaw += _driver.CurrentWarpYaw;
                    Vector3 local = character.InverseTransformDirection(Forward(yaw));
                    output[0] = local.x;
                    output[1] = local.z;
                    break;
                }
                default:
                    for (int i = 0; i < output.Length; i++) output[i] = 0f;
                    break;
            }
        }

        public override float3 GetWorldInitPosition() => transform.position;
        public override float3 GetWorldInitDirection() => transform.forward;
        public override float3 GetPosition() => transform.position;
        public override float GetTargetSpeed() => _hasFrame ? _frame.DesiredVelocity.magnitude : 0f;
    }
}
