using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFifa.Core
{
    // ─── Enums ──────────────────────────────────────────────────────────

    /// <summary>
    /// Import pipeline states for mocap animation clips.
    /// Each clip progresses through these stages before being gameplay-ready.
    /// </summary>
    public enum MocapImportState
    {
        Raw = 0,
        Retargeted = 1,
        Compressed = 2,
        Validated = 3,
        Ready = 4
    }

    /// <summary>
    /// Classification of animation clips for import rule selection.
    /// </summary>
    public enum MocapClipType
    {
        Idle,
        Locomotion,
        Kick,
        Tackle,
        GoalkeeperAction,
        Celebration,
        Dribble,
        Header,
        FirstTouch,
        BicycleKick
    }

    /// <summary>
    /// Animation layer type for the layered animator architecture.
    /// </summary>
    public enum AnimationLayerType
    {
        Base = 0,
        UpperBody = 1,
        HeadLook = 2,
        HandIK = 3
    }

    /// <summary>
    /// Blend mode for animation layers.
    /// </summary>
    public enum AnimationLayerBlendMode
    {
        Override,
        Additive
    }

    // ─── MocapClipMetadata ──────────────────────────────────────────────

    /// <summary>
    /// Metadata describing a motion-capture animation clip's source and technical details.
    /// Tracks capture provenance for quality audit and re-export.
    /// </summary>
    public class MocapClipMetadata
    {
        /// <summary>Clip source name from the mocap library (e.g., "Mixamo_Soccer_Idle").</summary>
        public string SourceName;

        /// <summary>Frames per second of the original capture.</summary>
        public float Fps;

        /// <summary>Total frame count in the raw capture.</summary>
        public int FrameCount;

        /// <summary>Duration in seconds (derived from FrameCount / Fps).</summary>
        public float Duration;

        /// <summary>Capture studio or library name (e.g., "Mixamo", "Rokoko", "OptiTrack").</summary>
        public string CaptureStudio;

        /// <summary>Type classification for import rule selection.</summary>
        public MocapClipType ClipType;

        /// <summary>Optional performer identifier for the mocap session.</summary>
        public string PerformerTag;

        public MocapClipMetadata()
        {
            SourceName = "";
            Fps = 30f;
            FrameCount = 0;
            Duration = 0f;
            CaptureStudio = "Mixamo";
            ClipType = MocapClipType.Idle;
            PerformerTag = "";
        }

        public MocapClipMetadata(string sourceName, float fps, int frameCount, string captureStudio, MocapClipType clipType)
        {
            SourceName = sourceName ?? "";
            Fps = fps > 0f ? fps : 30f;
            FrameCount = frameCount >= 0 ? frameCount : 0;
            Duration = Fps > 0f ? FrameCount / Fps : 0f;
            CaptureStudio = captureStudio ?? "Mixamo";
            ClipType = clipType;
            PerformerTag = "";
        }

        /// <summary>
        /// Returns true if the metadata has all required fields populated.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(SourceName)
                && Fps > 0f
                && FrameCount > 0
                && Duration > 0f
                && !string.IsNullOrEmpty(CaptureStudio);
        }

        /// <summary>
        /// Recalculates duration from frame count and FPS.
        /// </summary>
        public void RecalculateDuration()
        {
            Duration = Fps > 0f ? FrameCount / Fps : 0f;
        }
    }

    // ─── AnimationBlendConfig ───────────────────────────────────────────

    /// <summary>
    /// Configuration for animation blending: cross-fade durations, blend tree weights,
    /// and transition settings for smooth state changes.
    /// </summary>
    public class AnimationBlendConfig
    {
        /// <summary>Default cross-fade duration in seconds for locomotion transitions.</summary>
        public float LocomotionCrossFadeDuration;

        /// <summary>Cross-fade duration for action (kick, tackle) transitions.</summary>
        public float ActionCrossFadeDuration;

        /// <summary>Cross-fade duration when returning from action to locomotion.</summary>
        public float ActionExitCrossFadeDuration;

        /// <summary>Blend tree weight for idle pose at speed=0.</summary>
        public float IdleWeight;

        /// <summary>Blend tree weight for walk at low speed.</summary>
        public float WalkWeight;

        /// <summary>Blend tree weight for run at medium speed.</summary>
        public float RunWeight;

        /// <summary>Blend tree weight for sprint at max speed.</summary>
        public float SprintWeight;

        /// <summary>Whether to use inertial blending (smoother but more costly).</summary>
        public bool UseInertialBlending;

        /// <summary>Minimum transition duration to prevent animation popping (seconds).</summary>
        public float MinTransitionDuration;

        /// <summary>Maximum transition duration to prevent sluggish response (seconds).</summary>
        public float MaxTransitionDuration;

        public AnimationBlendConfig()
        {
            LocomotionCrossFadeDuration = 0.15f;
            ActionCrossFadeDuration = 0.1f;
            ActionExitCrossFadeDuration = 0.2f;
            IdleWeight = 1f;
            WalkWeight = 1f;
            RunWeight = 1f;
            SprintWeight = 1f;
            UseInertialBlending = true;
            MinTransitionDuration = 0.05f;
            MaxTransitionDuration = 0.5f;
        }

        /// <summary>
        /// Validates that all blend config values are within acceptable ranges.
        /// </summary>
        public bool IsValid()
        {
            if (LocomotionCrossFadeDuration < 0f || LocomotionCrossFadeDuration > 1f) return false;
            if (ActionCrossFadeDuration < 0f || ActionCrossFadeDuration > 1f) return false;
            if (ActionExitCrossFadeDuration < 0f || ActionExitCrossFadeDuration > 1f) return false;
            if (MinTransitionDuration < 0f) return false;
            if (MaxTransitionDuration < MinTransitionDuration) return false;
            if (IdleWeight < 0f || WalkWeight < 0f || RunWeight < 0f || SprintWeight < 0f) return false;
            return true;
        }

        /// <summary>
        /// Returns the appropriate cross-fade duration for a given state transition.
        /// </summary>
        public float GetCrossFadeDuration(AnimationStateId from, AnimationStateId to)
        {
            // Action entry transitions
            if (to == AnimationStateId.Kick || to == AnimationStateId.Tackle)
                return ActionCrossFadeDuration;

            // Action exit transitions (back to locomotion)
            if (from == AnimationStateId.Kick || from == AnimationStateId.Tackle)
                return ActionExitCrossFadeDuration;

            // Locomotion-to-locomotion
            return LocomotionCrossFadeDuration;
        }
    }

    // ─── RetargetConfig ─────────────────────────────────────────────────

    /// <summary>
    /// Configuration for retargeting mocap data from a source skeleton to the game's avatar.
    /// Handles bone mapping, offset corrections, and scale normalization.
    /// </summary>
    public class RetargetConfig
    {
        /// <summary>Source avatar skeleton name (e.g., "Mixamo_Skeleton").</summary>
        public string SourceSkeletonName;

        /// <summary>Target avatar skeleton name (e.g., "Quaternius_Humanoid").</summary>
        public string TargetSkeletonName;

        /// <summary>Bone name mapping from source to target skeleton.</summary>
        public readonly Dictionary<string, string> BoneMapping;

        /// <summary>Per-bone rotation offset corrections in degrees (Euler XYZ).</summary>
        public readonly Dictionary<string, float[]> BoneOffsetCorrections;

        /// <summary>Global scale factor applied to retargeted animations.</summary>
        public float GlobalScaleFactor;

        /// <summary>Per-bone scale overrides (e.g., longer legs on target).</summary>
        public readonly Dictionary<string, float> BoneScaleFactors;

        /// <summary>Whether to apply T-pose correction before retargeting.</summary>
        public bool ApplyTPoseCorrection;

        /// <summary>Tolerance for bone mapping completeness (0-1, 1 = all bones mapped).</summary>
        public float MappingCompletenessThreshold;

        public RetargetConfig()
        {
            SourceSkeletonName = "Mixamo_Skeleton";
            TargetSkeletonName = "Quaternius_Humanoid";
            BoneMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            BoneOffsetCorrections = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            BoneScaleFactors = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            GlobalScaleFactor = 1f;
            ApplyTPoseCorrection = true;
            MappingCompletenessThreshold = 0.9f;

            // Default Mixamo -> Unity humanoid bone mapping
            SetupDefaultBoneMapping();
        }

        private void SetupDefaultBoneMapping()
        {
            BoneMapping["mixamorig:Hips"] = "Hips";
            BoneMapping["mixamorig:Spine"] = "Spine";
            BoneMapping["mixamorig:Spine1"] = "Chest";
            BoneMapping["mixamorig:Spine2"] = "UpperChest";
            BoneMapping["mixamorig:Neck"] = "Neck";
            BoneMapping["mixamorig:Head"] = "Head";
            BoneMapping["mixamorig:LeftShoulder"] = "LeftShoulder";
            BoneMapping["mixamorig:LeftArm"] = "LeftUpperArm";
            BoneMapping["mixamorig:LeftForeArm"] = "LeftLowerArm";
            BoneMapping["mixamorig:LeftHand"] = "LeftHand";
            BoneMapping["mixamorig:RightShoulder"] = "RightShoulder";
            BoneMapping["mixamorig:RightArm"] = "RightUpperArm";
            BoneMapping["mixamorig:RightForeArm"] = "RightLowerArm";
            BoneMapping["mixamorig:RightHand"] = "RightHand";
            BoneMapping["mixamorig:LeftUpLeg"] = "LeftUpperLeg";
            BoneMapping["mixamorig:LeftLeg"] = "LeftLowerLeg";
            BoneMapping["mixamorig:LeftFoot"] = "LeftFoot";
            BoneMapping["mixamorig:LeftToeBase"] = "LeftToes";
            BoneMapping["mixamorig:RightUpLeg"] = "RightUpperLeg";
            BoneMapping["mixamorig:RightLeg"] = "RightLowerLeg";
            BoneMapping["mixamorig:RightFoot"] = "RightFoot";
            BoneMapping["mixamorig:RightToeBase"] = "RightToes";
        }

        /// <summary>
        /// Returns the target bone name for a given source bone, or null if unmapped.
        /// </summary>
        public string GetTargetBone(string sourceBone)
        {
            if (string.IsNullOrEmpty(sourceBone)) return null;
            string target;
            return BoneMapping.TryGetValue(sourceBone, out target) ? target : null;
        }

        /// <summary>
        /// Returns the bone mapping completeness as a ratio (0-1).
        /// Compares mapped bones against a required bone list.
        /// </summary>
        public float GetMappingCompleteness(IEnumerable<string> requiredTargetBones)
        {
            if (requiredTargetBones == null) return 0f;

            var required = requiredTargetBones.ToList();
            if (required.Count == 0) return 1f;

            var mappedTargets = new HashSet<string>(BoneMapping.Values, StringComparer.OrdinalIgnoreCase);
            int matched = required.Count(b => mappedTargets.Contains(b));

            return (float)matched / required.Count;
        }

        /// <summary>
        /// Validates the retarget configuration for completeness and correctness.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(SourceSkeletonName)) return false;
            if (string.IsNullOrEmpty(TargetSkeletonName)) return false;
            if (BoneMapping.Count == 0) return false;
            if (GlobalScaleFactor <= 0f) return false;
            if (MappingCompletenessThreshold < 0f || MappingCompletenessThreshold > 1f) return false;

            // Validate bone offset arrays are all length 3 (XYZ)
            foreach (var kvp in BoneOffsetCorrections)
            {
                if (kvp.Value == null || kvp.Value.Length != 3) return false;
            }

            // Validate scale factors are positive
            foreach (var kvp in BoneScaleFactors)
            {
                if (kvp.Value <= 0f) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the mapping meets the completeness threshold.
        /// </summary>
        public bool MeetsMappingThreshold(IEnumerable<string> requiredTargetBones)
        {
            return GetMappingCompleteness(requiredTargetBones) >= MappingCompletenessThreshold;
        }
    }

    // ─── AnimationQualityConfig ─────────────────────────────────────────

    /// <summary>
    /// Compression and quality settings for animation clips.
    /// Controls file size vs. fidelity trade-off during import.
    /// </summary>
    public class AnimationQualityConfig
    {
        /// <summary>Compression level: 0 = none, 1 = keyframe reduction, 2 = optimal, 3 = aggressive.</summary>
        public int CompressionLevel;

        /// <summary>Keyframe reduction tolerance (degrees). Lower = more keyframes = higher quality.</summary>
        public float KeyframeReductionTolerance;

        /// <summary>Whether to apply Euler angle filtering to prevent gimbal artifacts.</summary>
        public bool ApplyEulerFilter;

        /// <summary>Euler filter tolerance in degrees.</summary>
        public float EulerFilterTolerance;

        /// <summary>Whether to resample curves at the target frame rate.</summary>
        public bool ResampleCurves;

        /// <summary>Target sample rate for resampled curves (FPS).</summary>
        public float TargetSampleRate;

        /// <summary>Position error tolerance for compression (meters).</summary>
        public float PositionError;

        /// <summary>Rotation error tolerance for compression (degrees).</summary>
        public float RotationError;

        /// <summary>Scale error tolerance for compression (ratio).</summary>
        public float ScaleError;

        public AnimationQualityConfig()
        {
            CompressionLevel = 2; // Optimal
            KeyframeReductionTolerance = 0.5f;
            ApplyEulerFilter = true;
            EulerFilterTolerance = 0.25f;
            ResampleCurves = true;
            TargetSampleRate = 60f;
            PositionError = 0.001f;
            RotationError = 0.5f;
            ScaleError = 0.001f;
        }

        /// <summary>
        /// Validates compression settings are within acceptable ranges.
        /// </summary>
        public bool IsValid()
        {
            if (CompressionLevel < 0 || CompressionLevel > 3) return false;
            if (KeyframeReductionTolerance < 0f) return false;
            if (EulerFilterTolerance < 0f) return false;
            if (TargetSampleRate <= 0f) return false;
            if (PositionError < 0f) return false;
            if (RotationError < 0f) return false;
            if (ScaleError < 0f) return false;
            return true;
        }

        /// <summary>
        /// Returns a quality config preset based on clip type.
        /// Action clips use lower compression for accuracy; locomotion uses more compression.
        /// </summary>
        public static AnimationQualityConfig ForClipType(MocapClipType clipType)
        {
            var config = new AnimationQualityConfig();

            switch (clipType)
            {
                case MocapClipType.Kick:
                case MocapClipType.BicycleKick:
                case MocapClipType.Header:
                    // High quality for precise contact animations
                    config.CompressionLevel = 1;
                    config.KeyframeReductionTolerance = 0.25f;
                    config.RotationError = 0.25f;
                    break;

                case MocapClipType.GoalkeeperAction:
                case MocapClipType.Tackle:
                    // Medium-high quality for athletic moves
                    config.CompressionLevel = 1;
                    config.KeyframeReductionTolerance = 0.35f;
                    config.RotationError = 0.35f;
                    break;

                case MocapClipType.Idle:
                case MocapClipType.Locomotion:
                case MocapClipType.Dribble:
                    // Optimal compression for looping animations
                    config.CompressionLevel = 2;
                    config.KeyframeReductionTolerance = 0.5f;
                    config.RotationError = 0.5f;
                    break;

                case MocapClipType.Celebration:
                    // Slightly more compression acceptable for celebrations
                    config.CompressionLevel = 2;
                    config.KeyframeReductionTolerance = 0.6f;
                    config.RotationError = 0.6f;
                    break;

                default:
                    // Default optimal
                    break;
            }

            return config;
        }
    }

    // ─── FootIKConfig ───────────────────────────────────────────────────

    /// <summary>
    /// Inverse Kinematics configuration for foot placement.
    /// Prevents foot sliding and ensures correct ground contact during locomotion.
    /// </summary>
    public class FootIKConfig
    {
        /// <summary>Vertical offset for left foot IK target (meters).</summary>
        public float LeftFootOffset;

        /// <summary>Vertical offset for right foot IK target (meters).</summary>
        public float RightFootOffset;

        /// <summary>Distance threshold below which the foot is considered grounded (meters).</summary>
        public float GroundContactThreshold;

        /// <summary>Maximum raycast distance for ground detection (meters).</summary>
        public float MaxRaycastDistance;

        /// <summary>IK weight curve keyframes: (normalizedTime, weight) pairs for foot plant detection.</summary>
        public readonly List<KeyValuePair<float, float>> LeftFootWeightCurve;

        /// <summary>IK weight curve keyframes for the right foot.</summary>
        public readonly List<KeyValuePair<float, float>> RightFootWeightCurve;

        /// <summary>Whether foot IK is enabled during locomotion.</summary>
        public bool EnableDuringLocomotion;

        /// <summary>Whether foot IK is enabled during idle.</summary>
        public bool EnableDuringIdle;

        /// <summary>Interpolation speed for IK weight changes (prevents snapping).</summary>
        public float WeightInterpolationSpeed;

        /// <summary>Body height adjustment factor to prevent legs stretching on slopes.</summary>
        public float BodyHeightAdjustmentFactor;

        public FootIKConfig()
        {
            LeftFootOffset = 0f;
            RightFootOffset = 0f;
            GroundContactThreshold = 0.05f;
            MaxRaycastDistance = 1.5f;
            LeftFootWeightCurve = new List<KeyValuePair<float, float>>
            {
                new KeyValuePair<float, float>(0.0f, 1f),
                new KeyValuePair<float, float>(0.3f, 1f),
                new KeyValuePair<float, float>(0.5f, 0f),
                new KeyValuePair<float, float>(0.8f, 0f),
                new KeyValuePair<float, float>(1.0f, 1f)
            };
            RightFootWeightCurve = new List<KeyValuePair<float, float>>
            {
                new KeyValuePair<float, float>(0.0f, 0f),
                new KeyValuePair<float, float>(0.3f, 0f),
                new KeyValuePair<float, float>(0.5f, 1f),
                new KeyValuePair<float, float>(0.8f, 1f),
                new KeyValuePair<float, float>(1.0f, 0f)
            };
            EnableDuringLocomotion = true;
            EnableDuringIdle = true;
            WeightInterpolationSpeed = 10f;
            BodyHeightAdjustmentFactor = 0.5f;
        }

        /// <summary>
        /// Evaluates the IK weight at a given normalized time using linear interpolation.
        /// </summary>
        public float EvaluateLeftFootWeight(float normalizedTime)
        {
            return EvaluateCurve(LeftFootWeightCurve, normalizedTime);
        }

        /// <summary>
        /// Evaluates the right foot IK weight at a given normalized time.
        /// </summary>
        public float EvaluateRightFootWeight(float normalizedTime)
        {
            return EvaluateCurve(RightFootWeightCurve, normalizedTime);
        }

        /// <summary>
        /// Returns true if a foot height is within ground contact threshold.
        /// </summary>
        public bool IsGrounded(float footHeightAboveGround)
        {
            return footHeightAboveGround <= GroundContactThreshold;
        }

        private float EvaluateCurve(List<KeyValuePair<float, float>> curve, float t)
        {
            if (curve == null || curve.Count == 0) return 0f;
            if (t <= curve[0].Key) return curve[0].Value;
            if (t >= curve[curve.Count - 1].Key) return curve[curve.Count - 1].Value;

            for (int i = 0; i < curve.Count - 1; i++)
            {
                if (t >= curve[i].Key && t <= curve[i + 1].Key)
                {
                    float segmentLength = curve[i + 1].Key - curve[i].Key;
                    if (segmentLength <= 0f) return curve[i].Value;
                    float localT = (t - curve[i].Key) / segmentLength;
                    return curve[i].Value + (curve[i + 1].Value - curve[i].Value) * localT;
                }
            }

            return curve[curve.Count - 1].Value;
        }
    }

    // ─── AnimationEventConfig ───────────────────────────────────────────

    /// <summary>
    /// Animation event configuration for a specific contact or action frame.
    /// Used to trigger gameplay logic (ball contact, foot plant, tackle impact) at exact animation times.
    /// </summary>
    public class AnimationEventConfig
    {
        /// <summary>Event function name called on the receiving MonoBehaviour.</summary>
        public string EventName;

        /// <summary>Normalized time (0-1) in the animation clip when the event fires.</summary>
        public float NormalizedTime;

        /// <summary>Parameter type: "int", "float", "string", or "none".</summary>
        public string ParameterType;

        /// <summary>Parameter value (as string, parsed by the receiver).</summary>
        public string ParameterValue;

        /// <summary>Human-readable description for editor display.</summary>
        public string Description;

        public AnimationEventConfig()
        {
            EventName = "";
            NormalizedTime = 0f;
            ParameterType = "none";
            ParameterValue = "";
            Description = "";
        }

        public AnimationEventConfig(string eventName, float normalizedTime, string parameterType = "none", string parameterValue = "")
        {
            EventName = eventName ?? "";
            NormalizedTime = Math.Max(0f, Math.Min(1f, normalizedTime));
            ParameterType = parameterType ?? "none";
            ParameterValue = parameterValue ?? "";
            Description = "";
        }

        /// <summary>
        /// Validates the event configuration.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(EventName)) return false;
            if (NormalizedTime < 0f || NormalizedTime > 1f) return false;
            var validTypes = new[] { "none", "int", "float", "string" };
            if (!validTypes.Contains(ParameterType)) return false;
            return true;
        }
    }

    // ─── MocapPipeline ──────────────────────────────────────────────────

    /// <summary>
    /// State machine tracking the import progress of a single mocap clip through
    /// the pipeline stages: Raw -> Retargeted -> Compressed -> Validated -> Ready.
    /// </summary>
    public class MocapPipeline
    {
        private MocapImportState _currentState;
        private readonly List<string> _validationErrors;
        private readonly List<string> _processingLog;

        /// <summary>Current pipeline state.</summary>
        public MocapImportState CurrentState => _currentState;

        /// <summary>Metadata for the clip being processed.</summary>
        public MocapClipMetadata Metadata { get; private set; }

        /// <summary>Retarget configuration applied to this clip.</summary>
        public RetargetConfig RetargetSettings { get; set; }

        /// <summary>Compression quality settings applied to this clip.</summary>
        public AnimationQualityConfig QualitySettings { get; set; }

        /// <summary>Read-only list of validation errors encountered.</summary>
        public IReadOnlyList<string> ValidationErrors => _validationErrors;

        /// <summary>Read-only processing log for debugging.</summary>
        public IReadOnlyList<string> ProcessingLog => _processingLog;

        /// <summary>Whether the pipeline has completed successfully.</summary>
        public bool IsReady => _currentState == MocapImportState.Ready;

        /// <summary>Whether the pipeline has encountered errors.</summary>
        public bool HasErrors => _validationErrors.Count > 0;

        public MocapPipeline(MocapClipMetadata metadata)
        {
            Metadata = metadata ?? new MocapClipMetadata();
            _currentState = MocapImportState.Raw;
            _validationErrors = new List<string>();
            _processingLog = new List<string>();
            _processingLog.Add($"Pipeline created for clip: {Metadata.SourceName}");
        }

        /// <summary>
        /// Valid state transitions. Returns true if transitioning from -> to is allowed.
        /// </summary>
        public static bool IsValidTransition(MocapImportState from, MocapImportState to)
        {
            switch (from)
            {
                case MocapImportState.Raw:
                    return to == MocapImportState.Retargeted;
                case MocapImportState.Retargeted:
                    return to == MocapImportState.Compressed;
                case MocapImportState.Compressed:
                    return to == MocapImportState.Validated;
                case MocapImportState.Validated:
                    return to == MocapImportState.Ready;
                case MocapImportState.Ready:
                    return false; // Terminal state
                default:
                    return false;
            }
        }

        /// <summary>
        /// Advances the pipeline to the Retargeted state.
        /// Requires valid retarget config.
        /// </summary>
        public bool AdvanceToRetargeted(RetargetConfig config)
        {
            if (_currentState != MocapImportState.Raw)
            {
                _validationErrors.Add($"Cannot retarget from state {_currentState}; must be Raw.");
                return false;
            }

            if (config == null || !config.IsValid())
            {
                _validationErrors.Add("Invalid retarget configuration.");
                return false;
            }

            RetargetSettings = config;
            _currentState = MocapImportState.Retargeted;
            _processingLog.Add("Advanced to Retargeted state.");
            return true;
        }

        /// <summary>
        /// Advances the pipeline to the Compressed state.
        /// Requires valid quality config.
        /// </summary>
        public bool AdvanceToCompressed(AnimationQualityConfig config)
        {
            if (_currentState != MocapImportState.Retargeted)
            {
                _validationErrors.Add($"Cannot compress from state {_currentState}; must be Retargeted.");
                return false;
            }

            if (config == null || !config.IsValid())
            {
                _validationErrors.Add("Invalid quality configuration.");
                return false;
            }

            QualitySettings = config;
            _currentState = MocapImportState.Compressed;
            _processingLog.Add("Advanced to Compressed state.");
            return true;
        }

        /// <summary>
        /// Advances the pipeline to the Validated state.
        /// Runs validation checks on the processed clip.
        /// </summary>
        public bool AdvanceToValidated()
        {
            if (_currentState != MocapImportState.Compressed)
            {
                _validationErrors.Add($"Cannot validate from state {_currentState}; must be Compressed.");
                return false;
            }

            // Validate metadata
            if (!Metadata.IsValid())
            {
                _validationErrors.Add("Clip metadata is incomplete or invalid.");
                return false;
            }

            _currentState = MocapImportState.Validated;
            _processingLog.Add("Advanced to Validated state.");
            return true;
        }

        /// <summary>
        /// Advances the pipeline to the Ready (terminal) state.
        /// </summary>
        public bool AdvanceToReady()
        {
            if (_currentState != MocapImportState.Validated)
            {
                _validationErrors.Add($"Cannot mark Ready from state {_currentState}; must be Validated.");
                return false;
            }

            _currentState = MocapImportState.Ready;
            _processingLog.Add("Pipeline complete — clip is Ready.");
            return true;
        }

        /// <summary>
        /// Resets the pipeline back to Raw state, clearing all errors and settings.
        /// </summary>
        public void Reset()
        {
            _currentState = MocapImportState.Raw;
            _validationErrors.Clear();
            _processingLog.Clear();
            _processingLog.Add($"Pipeline reset for clip: {Metadata.SourceName}");
            RetargetSettings = null;
            QualitySettings = null;
        }
    }

    // ─── ClipTransitionMatrix ───────────────────────────────────────────

    /// <summary>
    /// Defines valid animation state transitions with per-transition blend durations.
    /// Used to configure the Animator Controller state machine.
    /// </summary>
    public class ClipTransitionMatrix
    {
        /// <summary>
        /// Transition entry: from state, to state, blend duration, has exit time.
        /// </summary>
        public struct TransitionEntry
        {
            public AnimationStateId FromState;
            public AnimationStateId ToState;
            public float BlendDuration;
            public bool HasExitTime;
            public float ExitTime;
        }

        private readonly List<TransitionEntry> _transitions;

        /// <summary>All configured transitions.</summary>
        public IReadOnlyList<TransitionEntry> Transitions => _transitions;

        public ClipTransitionMatrix()
        {
            _transitions = new List<TransitionEntry>();
            SetupDefaultTransitions();
        }

        private void SetupDefaultTransitions()
        {
            // Locomotion transitions (no exit time — driven by parameters)
            AddTransition(AnimationStateId.Idle, AnimationStateId.Run, 0.15f, false, 0f);
            AddTransition(AnimationStateId.Run, AnimationStateId.Idle, 0.2f, false, 0f);
            AddTransition(AnimationStateId.Run, AnimationStateId.Sprint, 0.1f, false, 0f);
            AddTransition(AnimationStateId.Sprint, AnimationStateId.Run, 0.15f, false, 0f);
            AddTransition(AnimationStateId.Idle, AnimationStateId.Sprint, 0.15f, false, 0f);
            AddTransition(AnimationStateId.Sprint, AnimationStateId.Idle, 0.25f, false, 0f);

            // Action entry transitions (immediate, no exit time)
            AddTransition(AnimationStateId.Idle, AnimationStateId.Kick, 0.1f, false, 0f);
            AddTransition(AnimationStateId.Run, AnimationStateId.Kick, 0.1f, false, 0f);
            AddTransition(AnimationStateId.Sprint, AnimationStateId.Kick, 0.1f, false, 0f);
            AddTransition(AnimationStateId.Idle, AnimationStateId.Tackle, 0.08f, false, 0f);
            AddTransition(AnimationStateId.Run, AnimationStateId.Tackle, 0.08f, false, 0f);
            AddTransition(AnimationStateId.Sprint, AnimationStateId.Tackle, 0.08f, false, 0f);

            // Action exit transitions (has exit time — plays out animation)
            AddTransition(AnimationStateId.Kick, AnimationStateId.Idle, 0.2f, true, 0.85f);
            AddTransition(AnimationStateId.Kick, AnimationStateId.Run, 0.2f, true, 0.85f);
            AddTransition(AnimationStateId.Tackle, AnimationStateId.Idle, 0.25f, true, 0.9f);
            AddTransition(AnimationStateId.Tackle, AnimationStateId.Run, 0.25f, true, 0.9f);

            // Celebrate transitions
            AddTransition(AnimationStateId.Idle, AnimationStateId.Celebrate, 0.15f, false, 0f);
            AddTransition(AnimationStateId.Run, AnimationStateId.Celebrate, 0.15f, false, 0f);
            AddTransition(AnimationStateId.Celebrate, AnimationStateId.Idle, 0.3f, false, 0f);
        }

        /// <summary>
        /// Adds a transition to the matrix.
        /// </summary>
        public void AddTransition(AnimationStateId from, AnimationStateId to, float blendDuration, bool hasExitTime, float exitTime)
        {
            _transitions.Add(new TransitionEntry
            {
                FromState = from,
                ToState = to,
                BlendDuration = blendDuration,
                HasExitTime = hasExitTime,
                ExitTime = exitTime
            });
        }

        /// <summary>
        /// Returns true if a transition from -> to is defined in the matrix.
        /// </summary>
        public bool HasTransition(AnimationStateId from, AnimationStateId to)
        {
            return _transitions.Any(t => t.FromState == from && t.ToState == to);
        }

        /// <summary>
        /// Returns the transition entry for a given from -> to pair, or null if not found.
        /// </summary>
        public TransitionEntry? GetTransition(AnimationStateId from, AnimationStateId to)
        {
            for (int i = 0; i < _transitions.Count; i++)
            {
                if (_transitions[i].FromState == from && _transitions[i].ToState == to)
                    return _transitions[i];
            }
            return null;
        }

        /// <summary>
        /// Returns all transitions originating from the given state.
        /// </summary>
        public List<TransitionEntry> GetTransitionsFrom(AnimationStateId from)
        {
            return _transitions.Where(t => t.FromState == from).ToList();
        }

        /// <summary>
        /// Returns the blend duration for a transition, or a default value if not found.
        /// </summary>
        public float GetBlendDuration(AnimationStateId from, AnimationStateId to, float defaultDuration = 0.15f)
        {
            var entry = GetTransition(from, to);
            return entry.HasValue ? entry.Value.BlendDuration : defaultDuration;
        }
    }

    // ─── AnimationLayerConfig ───────────────────────────────────────────

    /// <summary>
    /// Configuration for a single animation layer in the layered animator system.
    /// Supports base override layers and additive layers for upper body, head look, and hand IK.
    /// </summary>
    public class AnimationLayerConfig
    {
        /// <summary>Layer type identifier.</summary>
        public AnimationLayerType LayerType;

        /// <summary>Layer name for the Animator Controller.</summary>
        public string LayerName;

        /// <summary>Layer weight (0-1). 0 = no effect, 1 = full effect.</summary>
        public float Weight;

        /// <summary>Blend mode: Override or Additive.</summary>
        public AnimationLayerBlendMode BlendMode;

        /// <summary>Avatar mask name to limit which bones this layer affects.</summary>
        public string AvatarMaskName;

        /// <summary>Whether IK pass is enabled on this layer.</summary>
        public bool IKPass;

        public AnimationLayerConfig()
        {
            LayerType = AnimationLayerType.Base;
            LayerName = "Base Layer";
            Weight = 1f;
            BlendMode = AnimationLayerBlendMode.Override;
            AvatarMaskName = "";
            IKPass = false;
        }

        public AnimationLayerConfig(AnimationLayerType type, string name, float weight, AnimationLayerBlendMode blendMode, string avatarMask = "", bool ikPass = false)
        {
            LayerType = type;
            LayerName = name ?? "Layer";
            Weight = Math.Max(0f, Math.Min(1f, weight));
            BlendMode = blendMode;
            AvatarMaskName = avatarMask ?? "";
            IKPass = ikPass;
        }

        /// <summary>
        /// Validates the layer configuration.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(LayerName)) return false;
            if (Weight < 0f || Weight > 1f) return false;
            return true;
        }

        /// <summary>
        /// Creates the default set of animation layers for a soccer player.
        /// </summary>
        public static List<AnimationLayerConfig> CreateDefaultLayers()
        {
            return new List<AnimationLayerConfig>
            {
                new AnimationLayerConfig(
                    AnimationLayerType.Base,
                    "Base Layer",
                    1f,
                    AnimationLayerBlendMode.Override,
                    "",
                    true),
                new AnimationLayerConfig(
                    AnimationLayerType.UpperBody,
                    "Upper Body",
                    0.5f,
                    AnimationLayerBlendMode.Additive,
                    "UpperBody",
                    false),
                new AnimationLayerConfig(
                    AnimationLayerType.HeadLook,
                    "Head Look",
                    0.3f,
                    AnimationLayerBlendMode.Additive,
                    "Head",
                    false),
                new AnimationLayerConfig(
                    AnimationLayerType.HandIK,
                    "Hand IK",
                    0f,
                    AnimationLayerBlendMode.Additive,
                    "Hands",
                    true)
            };
        }
    }

    // ─── BlendTreeConfig ────────────────────────────────────────────────

    /// <summary>
    /// Configuration for a 1D blend tree used for locomotion (idle -> walk -> run -> sprint).
    /// Maps speed parameter thresholds to animation clips.
    /// </summary>
    public class BlendTreeConfig
    {
        /// <summary>
        /// A single entry in the blend tree, mapping a parameter threshold to a clip.
        /// </summary>
        public struct BlendTreeEntry
        {
            /// <summary>Animation state associated with this entry.</summary>
            public AnimationStateId State;

            /// <summary>Clip name in the animation controller.</summary>
            public string ClipName;

            /// <summary>Parameter threshold where this clip has full weight.</summary>
            public float Threshold;

            /// <summary>Playback speed multiplier for this clip.</summary>
            public float Speed;
        }

        /// <summary>Name of the blend tree parameter (matches Animator parameter).</summary>
        public string ParameterName;

        /// <summary>Ordered list of blend tree entries by threshold.</summary>
        public readonly List<BlendTreeEntry> Entries;

        /// <summary>Whether to auto-compute thresholds from entry order.</summary>
        public bool AutoComputeThresholds;

        /// <summary>Minimum speed parameter value.</summary>
        public float MinThreshold;

        /// <summary>Maximum speed parameter value.</summary>
        public float MaxThreshold;

        public BlendTreeConfig()
        {
            ParameterName = "Speed";
            AutoComputeThresholds = false;
            MinThreshold = 0f;
            MaxThreshold = 1f;
            Entries = new List<BlendTreeEntry>();
            SetupDefaultLocomotion();
        }

        private void SetupDefaultLocomotion()
        {
            Entries.Add(new BlendTreeEntry
            {
                State = AnimationStateId.Idle,
                ClipName = "Soccer_Idle",
                Threshold = 0f,
                Speed = 1f
            });
            Entries.Add(new BlendTreeEntry
            {
                State = AnimationStateId.Run,
                ClipName = "Running",
                Threshold = 0.5f,
                Speed = 1f
            });
            Entries.Add(new BlendTreeEntry
            {
                State = AnimationStateId.Sprint,
                ClipName = "Sprinting",
                Threshold = 1f,
                Speed = 1f
            });
        }

        /// <summary>
        /// Returns the clip name at the given index, or null if out of range.
        /// </summary>
        public string GetClipNameAtIndex(int index)
        {
            if (index < 0 || index >= Entries.Count) return null;
            return Entries[index].ClipName;
        }

        /// <summary>
        /// Returns the threshold at the given index, or -1 if out of range.
        /// </summary>
        public float GetThresholdAtIndex(int index)
        {
            if (index < 0 || index >= Entries.Count) return -1f;
            return Entries[index].Threshold;
        }

        /// <summary>
        /// Validates the blend tree configuration.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(ParameterName)) return false;
            if (Entries.Count < 2) return false;

            // Thresholds must be in ascending order
            for (int i = 1; i < Entries.Count; i++)
            {
                if (Entries[i].Threshold <= Entries[i - 1].Threshold)
                    return false;
            }

            // All clips must have names
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.IsNullOrEmpty(Entries[i].ClipName))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the two entries that bracket the given parameter value,
        /// for blend weight calculation.
        /// </summary>
        public (BlendTreeEntry lower, BlendTreeEntry upper, float blendFactor) GetBlendEntries(float parameterValue)
        {
            if (Entries.Count == 0)
                return (default, default, 0f);

            if (parameterValue <= Entries[0].Threshold)
                return (Entries[0], Entries[0], 0f);

            if (parameterValue >= Entries[Entries.Count - 1].Threshold)
                return (Entries[Entries.Count - 1], Entries[Entries.Count - 1], 0f);

            for (int i = 0; i < Entries.Count - 1; i++)
            {
                if (parameterValue >= Entries[i].Threshold && parameterValue <= Entries[i + 1].Threshold)
                {
                    float range = Entries[i + 1].Threshold - Entries[i].Threshold;
                    float factor = range > 0f ? (parameterValue - Entries[i].Threshold) / range : 0f;
                    return (Entries[i], Entries[i + 1], factor);
                }
            }

            return (Entries[Entries.Count - 1], Entries[Entries.Count - 1], 0f);
        }
    }

    // ─── ClipDurationRanges ─────────────────────────────────────────────

    /// <summary>
    /// Expected duration ranges for each clip type. Used by validators to flag
    /// clips that are too short (truncated) or too long (untrimmed).
    /// </summary>
    public static class ClipDurationRanges
    {
        /// <summary>Returns the (min, max) duration range in seconds for the given clip type.</summary>
        public static (float min, float max) GetRange(MocapClipType clipType)
        {
            switch (clipType)
            {
                case MocapClipType.Idle:
                    return (2f, 5f);
                case MocapClipType.Locomotion:
                    return (0.8f, 1.2f);
                case MocapClipType.Kick:
                    return (0.3f, 0.8f);
                case MocapClipType.Tackle:
                    return (0.5f, 1.5f);
                case MocapClipType.GoalkeeperAction:
                    return (0.4f, 2f);
                case MocapClipType.Celebration:
                    return (2f, 8f);
                case MocapClipType.Dribble:
                    return (0.6f, 1.5f);
                case MocapClipType.Header:
                    return (0.3f, 1f);
                case MocapClipType.FirstTouch:
                    return (0.3f, 0.8f);
                case MocapClipType.BicycleKick:
                    return (0.5f, 1.2f);
                default:
                    return (0.1f, 10f);
            }
        }

        /// <summary>
        /// Checks whether a duration is within the expected range for the clip type.
        /// </summary>
        public static bool IsInRange(MocapClipType clipType, float duration)
        {
            var (min, max) = GetRange(clipType);
            return duration >= min && duration <= max;
        }
    }

    // ─── Clip Naming Convention ─────────────────────────────────────────

    /// <summary>
    /// Enforces naming conventions for mocap animation clips.
    /// Expected format: "Mocap_{Type}_{Descriptor}" (e.g., "Mocap_Kick_RightFoot").
    /// </summary>
    public static class ClipNamingConvention
    {
        /// <summary>Required prefix for all mocap clips.</summary>
        public const string RequiredPrefix = "Mocap_";

        /// <summary>Valid type tokens that can appear in clip names.</summary>
        public static readonly string[] ValidTypeTokens = new[]
        {
            "Idle", "Run", "Sprint", "Kick", "Tackle",
            "GKDive", "GKCatch", "GKPunch", "GKDistribute",
            "Celebrate", "Dribble", "Header", "FirstTouch", "BicycleKick"
        };

        /// <summary>
        /// Validates a clip name against the naming convention.
        /// Returns null if valid, or an error message if invalid.
        /// </summary>
        public static string Validate(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return "Clip name is null or empty.";

            if (!clipName.StartsWith(RequiredPrefix))
                return $"Clip name '{clipName}' does not start with required prefix '{RequiredPrefix}'.";

            string afterPrefix = clipName.Substring(RequiredPrefix.Length);
            if (string.IsNullOrEmpty(afterPrefix))
                return $"Clip name '{clipName}' has no content after prefix.";

            // Extract the type token (first segment after prefix)
            string[] segments = afterPrefix.Split('_');
            if (segments.Length < 1)
                return $"Clip name '{clipName}' has no type token after prefix.";

            string typeToken = segments[0];
            if (!ValidTypeTokens.Contains(typeToken))
                return $"Clip name '{clipName}' has unrecognized type token '{typeToken}'. " +
                       $"Valid tokens: {string.Join(", ", ValidTypeTokens)}";

            return null; // Valid
        }

        /// <summary>
        /// Returns true if the clip name follows the naming convention.
        /// </summary>
        public static bool IsValid(string clipName)
        {
            return Validate(clipName) == null;
        }
    }
}
