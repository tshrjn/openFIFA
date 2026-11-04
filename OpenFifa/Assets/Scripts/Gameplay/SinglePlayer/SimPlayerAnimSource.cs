using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// <see cref="IPlayerAnimSource"/> for one sim player of a <see cref="SinglePlayerMatchSession"/>. Read-only. It
    /// reports the raw latest fixed-step snapshot (SimTime = tick * dt, unextrapolated Position and radians FacingYaw)
    /// and implements <see cref="IPlayerAnimInterpolation"/> so the driver renders between the previous and the current
    /// snapshot; the controller interpolates the ball the same way so contact frames line up.
    /// </summary>
    public sealed class SimPlayerAnimSource : IPlayerAnimSource, IPlayerAnimInterpolation
    {
        private readonly SinglePlayerMatchSession _session;
        public readonly int PlayerId;

        public SimPlayerAnimSource(SinglePlayerMatchSession session, int playerId)
        {
            _session = session;
            PlayerId = playerId;
        }

        private ref readonly SimPlayer P => ref _session.Sim.State.Players[PlayerId];

        public float SimTime => _session.SimTime;

        public float InterpolationAlpha => Mathf.Clamp01(_session.InterpolationAlpha);

        public float FixedDeltaTime => _session.Sim.Dt;

        public Vector3 Position
        {
            get
            {
                var p = P.Position;
                return new Vector3(p.X, 0f, p.Z);
            }
        }

        public Vector3 Velocity
        {
            get
            {
                var v = P.Velocity;
                return new Vector3(v.X, 0f, v.Z);
            }
        }

        public Vector3 DesiredVelocity
        {
            get
            {
                var intents = _session.LastIntents;
                if (intents == null || PlayerId >= intents.Count) return Velocity;
                var m = intents[PlayerId].Move.Flat;
                var p = P;
                var a = _session.Sim.AttributesOf(PlayerId);
                float top = PlayerKinematics.SprintSpeed(a, _session.Sim.Config.Kinematics, p.Stamina);
                if (!intents[PlayerId].Sprint) top *= _session.Sim.Config.Kinematics.JogFraction;
                return new Vector3(m.X * top, 0f, m.Z * top);
            }
        }

        /// <summary>Radians, like the sim.</summary>
        public float FacingYaw => P.Facing;

        public bool IsSprinting => P.Sprinting;

        public PlayerAnimAction CurrentAction
        {
            get
            {
                var a = _session.Animation.Get(PlayerId);
                if (a.IsNone) return PlayerAnimAction.None;
                return new PlayerAnimAction(a.Id, a.Kind, a.Side, a.ContactTime, a.BallAtContact.ToUnity(),
                    a.Direction.ToUnity(), a.RootAtContact.ToUnity(), a.YawAtContact, a.HasRootAtContact);
            }
        }
    }
}
