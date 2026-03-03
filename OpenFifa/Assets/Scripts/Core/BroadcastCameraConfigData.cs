using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Camera angle presets for broadcast-style TV presentation.
    /// </summary>
    public enum CameraAngle
    {
        Wide = 0,
        Medium = 1,
        Close = 2,
        BehindGoal = 3,
        HighAngle = 4,
        Tactical = 5,
        Celebration = 6,
        Replay = 7
    }

    /// <summary>
    /// Defines a camera preset with position, rotation, FOV, and movement speeds.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class CameraPreset
    {
        /// <summary>Position offset X from tracking target.</summary>
        public float PositionX { get; }

        /// <summary>Position offset Y from tracking target.</summary>
        public float PositionY { get; }

        /// <summary>Position offset Z from tracking target.</summary>
        public float PositionZ { get; }

        /// <summary>Euler rotation X (pitch).</summary>
        public float RotationX { get; }

        /// <summary>Euler rotation Y (yaw).</summary>
        public float RotationY { get; }

        /// <summary>Euler rotation Z (roll).</summary>
        public float RotationZ { get; }

        /// <summary>Camera field of view in degrees.</summary>
        public float FieldOfView { get; }

        /// <summary>Speed at which the camera dollies along a track.</summary>
        public float DollySpeed { get; }

        /// <summary>Speed at which the camera tracks the target.</summary>
        public float TrackSpeed { get; }

        /// <summary>The camera angle this preset represents.</summary>
        public CameraAngle Angle { get; }

        public CameraPreset(
            CameraAngle angle,
            float positionX = 0f,
            float positionY = 15f,
            float positionZ = -20f,
            float rotationX = 35f,
            float rotationY = 0f,
            float rotationZ = 0f,
            float fieldOfView = 60f,
            float dollySpeed = 5f,
            float trackSpeed = 3f)
        {
            Angle = angle;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            FieldOfView = fieldOfView;
            DollySpeed = dollySpeed;
            TrackSpeed = trackSpeed;
        }
    }

    /// <summary>
    /// Configuration for automatic camera cut decisions.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class AutoCutConfig
    {
        /// <summary>Minimum time between automatic cuts in seconds.</summary>
        public float MinCutDuration { get; }

        /// <summary>Maximum time before a forced cut in seconds.</summary>
        public float MaxCutDuration { get; }

        /// <summary>Whether event-triggered cuts (goals, fouls) are enabled.</summary>
        public bool EventTriggeredCutsEnabled { get; }

        /// <summary>Duration of the smooth blend between cameras in seconds.</summary>
        public float SmoothBlendDuration { get; }

        public AutoCutConfig(
            float minCutDuration = 3f,
            float maxCutDuration = 12f,
            bool eventTriggeredCutsEnabled = true,
            float smoothBlendDuration = 0.8f)
        {
            if (minCutDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minCutDuration), "Min cut duration must be positive.");
            if (maxCutDuration < minCutDuration)
                throw new ArgumentOutOfRangeException(nameof(maxCutDuration), "Max cut duration must be >= min cut duration.");
            if (smoothBlendDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(smoothBlendDuration), "Smooth blend duration must be non-negative.");

            MinCutDuration = minCutDuration;
            MaxCutDuration = maxCutDuration;
            EventTriggeredCutsEnabled = eventTriggeredCutsEnabled;
            SmoothBlendDuration = smoothBlendDuration;
        }
    }

    /// <summary>
    /// Configuration for replay camera behavior.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class ReplayCameraConfig
    {
        /// <summary>Slow-motion speed for first angle (e.g. 0.25x).</summary>
        public float SlowMoSpeedFirst { get; }

        /// <summary>Slow-motion speed for second angle (e.g. 0.5x).</summary>
        public float SlowMoSpeedSecond { get; }

        /// <summary>Normal playback speed (1.0x).</summary>
        public float NormalSpeed { get; }

        /// <summary>Number of camera angles to cycle through during replay.</summary>
        public int MultiAngleCount { get; }

        /// <summary>Opacity of the replay overlay (0-1).</summary>
        public float OverlayOpacity { get; }

        /// <summary>Total replay duration in seconds.</summary>
        public float ReplayDuration { get; }

        public ReplayCameraConfig(
            float slowMoSpeedFirst = 0.25f,
            float slowMoSpeedSecond = 0.5f,
            float normalSpeed = 1f,
            int multiAngleCount = 3,
            float overlayOpacity = 0.85f,
            float replayDuration = 5f)
        {
            if (slowMoSpeedFirst <= 0f || slowMoSpeedFirst > 1f)
                throw new ArgumentOutOfRangeException(nameof(slowMoSpeedFirst), "Must be in (0, 1].");
            if (slowMoSpeedSecond <= 0f || slowMoSpeedSecond > 1f)
                throw new ArgumentOutOfRangeException(nameof(slowMoSpeedSecond), "Must be in (0, 1].");
            if (multiAngleCount < 1)
                throw new ArgumentOutOfRangeException(nameof(multiAngleCount), "Must have at least 1 angle.");
            if (overlayOpacity < 0f || overlayOpacity > 1f)
                throw new ArgumentOutOfRangeException(nameof(overlayOpacity), "Must be in [0, 1].");

            SlowMoSpeedFirst = slowMoSpeedFirst;
            SlowMoSpeedSecond = slowMoSpeedSecond;
            NormalSpeed = normalSpeed;
            MultiAngleCount = multiAngleCount;
            OverlayOpacity = overlayOpacity;
            ReplayDuration = replayDuration;
        }
    }

    /// <summary>
    /// Configuration for TV-style overlay elements (score bug, replay label, etc.).
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class TVOverlayConfig
    {
        /// <summary>Normalized X position for the score bug (0=left, 1=right).</summary>
        public float ScoreBugPositionX { get; }

        /// <summary>Normalized Y position for the score bug (0=bottom, 1=top).</summary>
        public float ScoreBugPositionY { get; }

        /// <summary>Normalized X position for the replay label.</summary>
        public float ReplayLabelPositionX { get; }

        /// <summary>Normalized Y position for the replay label.</summary>
        public float ReplayLabelPositionY { get; }

        /// <summary>Normalized X position for the home team name.</summary>
        public float HomeTeamNamePositionX { get; }

        /// <summary>Normalized X position for the away team name.</summary>
        public float AwayTeamNamePositionX { get; }

        public TVOverlayConfig(
            float scoreBugPositionX = 0.5f,
            float scoreBugPositionY = 0.95f,
            float replayLabelPositionX = 0.5f,
            float replayLabelPositionY = 0.1f,
            float homeTeamNamePositionX = 0.35f,
            float awayTeamNamePositionX = 0.65f)
        {
            ScoreBugPositionX = Clamp01(scoreBugPositionX);
            ScoreBugPositionY = Clamp01(scoreBugPositionY);
            ReplayLabelPositionX = Clamp01(replayLabelPositionX);
            ReplayLabelPositionY = Clamp01(replayLabelPositionY);
            HomeTeamNamePositionX = Clamp01(homeTeamNamePositionX);
            AwayTeamNamePositionX = Clamp01(awayTeamNamePositionX);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }

    /// <summary>
    /// Configuration for the broadcast director's cut frequency and momentum sensitivity.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class DirectorConfig
    {
        /// <summary>Base cut frequency in cuts per minute when play is calm.</summary>
        public float BaseCutFrequency { get; }

        /// <summary>Maximum cut frequency in cuts per minute during intense play.</summary>
        public float MaxCutFrequency { get; }

        /// <summary>Momentum threshold (0-1) above which cut frequency increases.</summary>
        public float MomentumThreshold { get; }

        /// <summary>How quickly momentum decays per second (0-1 range per second).</summary>
        public float MomentumDecayRate { get; }

        /// <summary>Momentum boost applied when a goal is scored.</summary>
        public float GoalMomentumBoost { get; }

        /// <summary>Momentum boost applied on a near miss.</summary>
        public float NearMissMomentumBoost { get; }

        /// <summary>Momentum boost applied on a foul.</summary>
        public float FoulMomentumBoost { get; }

        public DirectorConfig(
            float baseCutFrequency = 5f,
            float maxCutFrequency = 12f,
            float momentumThreshold = 0.5f,
            float momentumDecayRate = 0.15f,
            float goalMomentumBoost = 1.0f,
            float nearMissMomentumBoost = 0.6f,
            float foulMomentumBoost = 0.4f)
        {
            if (baseCutFrequency <= 0f)
                throw new ArgumentOutOfRangeException(nameof(baseCutFrequency), "Must be positive.");
            if (maxCutFrequency < baseCutFrequency)
                throw new ArgumentOutOfRangeException(nameof(maxCutFrequency), "Must be >= baseCutFrequency.");
            if (momentumThreshold < 0f || momentumThreshold > 1f)
                throw new ArgumentOutOfRangeException(nameof(momentumThreshold), "Must be in [0, 1].");

            BaseCutFrequency = baseCutFrequency;
            MaxCutFrequency = maxCutFrequency;
            MomentumThreshold = momentumThreshold;
            MomentumDecayRate = momentumDecayRate;
            GoalMomentumBoost = goalMomentumBoost;
            NearMissMomentumBoost = nearMissMomentumBoost;
            FoulMomentumBoost = foulMomentumBoost;
        }
    }

    /// <summary>
    /// Factory providing default camera presets for each CameraAngle.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public static class CameraPresetFactory
    {
        /// <summary>
        /// Get the default camera preset for the given angle.
        /// </summary>
        public static CameraPreset GetDefaultPreset(CameraAngle angle)
        {
            switch (angle)
            {
                case CameraAngle.Wide:
                    return new CameraPreset(
                        angle: CameraAngle.Wide,
                        positionX: 0f, positionY: 22f, positionZ: -30f,
                        rotationX: 30f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 65f, dollySpeed: 4f, trackSpeed: 2f);

                case CameraAngle.Medium:
                    return new CameraPreset(
                        angle: CameraAngle.Medium,
                        positionX: 0f, positionY: 15f, positionZ: -20f,
                        rotationX: 35f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 55f, dollySpeed: 5f, trackSpeed: 3f);

                case CameraAngle.Close:
                    return new CameraPreset(
                        angle: CameraAngle.Close,
                        positionX: 0f, positionY: 8f, positionZ: -10f,
                        rotationX: 25f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 45f, dollySpeed: 7f, trackSpeed: 5f);

                case CameraAngle.BehindGoal:
                    return new CameraPreset(
                        angle: CameraAngle.BehindGoal,
                        positionX: 0f, positionY: 6f, positionZ: -28f,
                        rotationX: 15f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 50f, dollySpeed: 3f, trackSpeed: 2f);

                case CameraAngle.HighAngle:
                    return new CameraPreset(
                        angle: CameraAngle.HighAngle,
                        positionX: 0f, positionY: 35f, positionZ: -15f,
                        rotationX: 60f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 50f, dollySpeed: 3f, trackSpeed: 2f);

                case CameraAngle.Tactical:
                    return new CameraPreset(
                        angle: CameraAngle.Tactical,
                        positionX: 0f, positionY: 40f, positionZ: 0f,
                        rotationX: 90f, rotationY: 0f, rotationZ: 0f,
                        fieldOfView: 55f, dollySpeed: 2f, trackSpeed: 1.5f);

                case CameraAngle.Celebration:
                    return new CameraPreset(
                        angle: CameraAngle.Celebration,
                        positionX: 3f, positionY: 4f, positionZ: -6f,
                        rotationX: 10f, rotationY: -15f, rotationZ: 0f,
                        fieldOfView: 40f, dollySpeed: 8f, trackSpeed: 6f);

                case CameraAngle.Replay:
                    return new CameraPreset(
                        angle: CameraAngle.Replay,
                        positionX: 5f, positionY: 10f, positionZ: -15f,
                        rotationX: 25f, rotationY: -10f, rotationZ: 0f,
                        fieldOfView: 50f, dollySpeed: 6f, trackSpeed: 4f);

                default:
                    return new CameraPreset(angle: CameraAngle.Wide);
            }
        }
    }
}
