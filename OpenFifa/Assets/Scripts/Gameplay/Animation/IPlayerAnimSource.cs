using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// An action the sim has committed to, with the tick at which the body part must meet the ball.
    /// Id must change (increase) for every new action; Id 0 means "no action".
    /// </summary>
    public readonly struct PlayerAnimAction
    {
        public readonly int Id;
        public readonly AnimActionKind Kind;
        public readonly ActionSide Side;
        /// <summary>Sim time (s) of the contact tick.</summary>
        public readonly float ContactTime;
        /// <summary>Ball centre at the contact tick (world).</summary>
        public readonly Vector3 BallAtContact;
        /// <summary>Outgoing ball direction (world). Zero = keep facing.</summary>
        public readonly Vector3 Direction;
        /// <summary>Sim root position at contact, if the sim knows it; otherwise the driver extrapolates from velocity.</summary>
        public readonly Vector3 RootAtContact;
        /// <summary>Sim facing at contact, in RADIANS like the sim (0 = +Z, positive toward +X). The driver converts.</summary>
        public readonly float YawAtContact;
        public readonly bool HasRootAtContact;

        public PlayerAnimAction(int id, AnimActionKind kind, ActionSide side, float contactTime, Vector3 ballAtContact,
            Vector3 direction, Vector3 rootAtContact = default, float yawAtContact = 0f, bool hasRootAtContact = false)
        {
            Id = id;
            Kind = kind;
            Side = side;
            ContactTime = contactTime;
            BallAtContact = ballAtContact;
            Direction = direction;
            RootAtContact = rootAtContact;
            YawAtContact = yawAtContact;
            HasRootAtContact = hasRootAtContact;
        }

        public static PlayerAnimAction None => default;
        public bool IsNone => Id == 0 || Kind == AnimActionKind.None;
    }

    /// <summary>
    /// Read-only view of one player's sim state for the presentation layer. Implement this on the sim bridge
    /// (or an adapter); the animation code never references Sim types and never writes back.
    ///
    /// Units: angles are in RADIANS, the sim's unit (<c>SimPlayer.Facing</c>, <c>SimMath.Yaw</c>). The driver converts
    /// them to degrees exactly once, in <see cref="PlayerAnimationDriver"/>, because Unity and AnimMath use degrees.
    ///
    /// Smoothing: a source may also implement <see cref="IPlayerAnimInterpolation"/>. Then it should return the raw
    /// latest fixed-step snapshot here (no extrapolation) and the driver interpolates between the previous and the
    /// current snapshot. A source without it is shown as-is (the source does its own smoothing, if any).
    /// </summary>
    public interface IPlayerAnimSource
    {
        /// <summary>Sim clock (s). Must be the same clock as PlayerAnimAction.ContactTime.</summary>
        float SimTime { get; }
        /// <summary>Ground position of the sim capsule (world).</summary>
        Vector3 Position { get; }
        /// <summary>Horizontal velocity (m/s, world).</summary>
        Vector3 Velocity { get; }
        /// <summary>Velocity the player is steering toward (intent). Return Velocity if unknown.</summary>
        Vector3 DesiredVelocity { get; }
        /// <summary>Facing yaw in RADIANS, as the sim stores it (0 = +Z, positive toward +X).</summary>
        float FacingYaw { get; }
        bool IsSprinting { get; }
        /// <summary>The current or upcoming committed action.</summary>
        PlayerAnimAction CurrentAction { get; }
    }

    /// <summary>
    /// Optional companion to <see cref="IPlayerAnimSource"/> for fixed-step sims rendered at a different rate.
    /// With it, <see cref="IPlayerAnimSource.SimTime"/>, Position and FacingYaw should be the latest snapshot
    /// (tick * dt, unextrapolated); the driver keeps the previous snapshot and renders between the two.
    /// </summary>
    public interface IPlayerAnimInterpolation
    {
        /// <summary>Fixed-step accumulator remainder / fixed dt, in [0, 1], for the frame being rendered.</summary>
        float InterpolationAlpha { get; }
        /// <summary>The sim's fixed step (s), e.g. 1/60.</summary>
        float FixedDeltaTime { get; }
    }
}
