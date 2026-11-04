using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>Per-frame locomotion input for a backend (blend tree or motion matching).</summary>
    public readonly struct LocomotionFrame
    {
        public readonly float SimTime;
        public readonly float DeltaTime;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
        public readonly Vector3 Acceleration;
        public readonly Vector3 DesiredVelocity;
        public readonly float FacingYaw;
        public readonly float YawRate;
        public readonly bool IsSprinting;
        /// <summary>Suggested locomotion playback rate (anti-skate, or run-up cadence scale).</summary>
        public readonly float PlaybackRate;

        public LocomotionFrame(float simTime, float deltaTime, Vector3 position, Vector3 velocity, Vector3 acceleration,
            Vector3 desiredVelocity, float facingYaw, float yawRate, bool isSprinting, float playbackRate)
        {
            SimTime = simTime;
            DeltaTime = deltaTime;
            Position = position;
            Velocity = velocity;
            Acceleration = acceleration;
            DesiredVelocity = desiredVelocity;
            FacingYaw = facingYaw;
            YawRate = yawRate;
            IsSprinting = isSprinting;
            PlaybackRate = playbackRate;
        }
    }

    /// <summary>
    /// Optional locomotion backend living on the same GameObject as the driver (e.g. the motion-matching bridge).
    /// When none is present or it reports inactive, the driver feeds the Animator blend tree instead.
    /// </summary>
    public interface ILocomotionBackend
    {
        bool IsActive { get; }
        void DriveLocomotion(in LocomotionFrame frame);
        /// <summary>Called when a full-body action starts (active=true) or ends, so the backend can yield the body.</summary>
        void SetActionOverride(bool active, AnimActionKind kind);
    }
}
