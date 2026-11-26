using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Side-on broadcast camera for the 11v11 sim pitch (length along Z). It sits beyond the +X touchline looking at
    /// -X, so +Z (where Home attacks in the first half) is screen-right. The yaw it reports is what the session uses to
    /// map the stick to the pitch. Extra modes exist for review stills.
    ///
    /// Broadcast framing: the camera aims at the framed point (ball, a little of the controlled player, plus a lead
    /// along the pitch from the ball's speed and towards the goal the team in possession attacks) and then pitches up
    /// so that point sits <see cref="BroadcastFraming.BallScreenFromTop"/> down the screen, which keeps the near-side
    /// grass from filling the bottom of the frame. The smoothed framing never trails the ball by more than
    /// <see cref="MaxBallOffset"/> (a hard dead-zone edge), so a fast break or a goalmouth scramble cannot leave shot.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SinglePlayerCamera : MonoBehaviour
    {
        public enum Mode
        {
            Broadcast = 0,
            Sideline = 1,
            CloseUp = 2,
            GoalArea = 3,
            TopDown = 4,
        }

        public Mode CameraMode = Mode.Broadcast;
        [Tooltip("Ball, followed along the pitch.")] public Transform Ball;
        [Tooltip("Controlled player, blended in a little.")] public Transform Player;
        public float HalfWidth = 34f;
        public float HalfLength = 52.5f;
        // Broadcast ("tele") framing, closer to FC's default camera than the old 24 m high / 34 deg view, where players
        // were ~25 px tall at 720p. New field names so values serialised by older scenes are not picked up.
        [Tooltip("Broadcast camera height (m).")] public float BroadcastHeight = 19f;
        [Tooltip("Broadcast camera distance back from the near touchline (m).")] public float BroadcastSetback = 28f;
        [Tooltip("Broadcast vertical field of view (deg).")] public float BroadcastFov = 27f;
        [Tooltip("Seconds of ball travel (along the pitch) the framing leads by.")] public float LeadSeconds = 0.5f;
        [Tooltip("Largest lead (m).")] public float MaxLead = 6f;
        [Tooltip("Smoothing time of the framed point (s).")] public float FollowDamping = 0.3f;
        [Tooltip("Largest distance (m) the framed point may be from the ball, along and across the pitch.")]
        public float MaxBallOffset = 9f;
        [Tooltip("Ball height on screen as a fraction from the top.")] public float BallFromTop = BroadcastFraming.BallScreenFromTop;
        /// <summary>Set each frame by the match: +1/-1 = the team in possession attacks +Z/-Z, 0 = loose ball (keeps the last lead).</summary>
        public int AttackDirection { get; set; }
        /// <summary>How far the camera moves in towards the near touchline per metre the play is on the near side.</summary>
        public const float DollyIn = 0.15f;
        [Tooltip("Largest broadcast yaw towards a goal (deg).")] public float MaxYawDegrees = 25f;
        /// <summary>Which goal the GoalArea mode looks at (+1 = +Z).</summary>
        public int GoalSide = 1;

        private Vector3 _focus;
        private Vector3 _vel;
        private bool _snapped;
        private Vector3 _lastBall;
        private float _ballVz, _lead, _leadVel, _attackLead, _attackLeadVel, _attackSign;

        /// <summary>Camera yaw in radians, same convention as the sim (0 = +Z, +pi/2 = +X).</summary>
        public float YawRadians => Mathf.Atan2(transform.forward.x, transform.forward.z);

        private void LateUpdate() => UpdateNow(Time.unscaledDeltaTime, false);

        public void Snap() => UpdateNow(0f, true);

        public void UpdateNow(float dt, bool snap)
        {
            var cam = GetComponent<Camera>();
            Vector3 ball = Ball != null ? Ball.position : Vector3.zero;
            Vector3 player = Player != null ? Player.position : ball;
            // Lead in the direction of play: smoothed ball speed along the pitch, damped so it does not jitter on touches.
            if (snap || !_snapped || dt <= 0f)
            {
                _lastBall = ball;
                if (!_snapped) { _ballVz = 0f; _lead = 0f; _leadVel = 0f; _attackLead = 0f; _attackLeadVel = 0f; _attackSign = 0f; }
                if (AttackDirection != 0) _attackSign = AttackDirection;
                _attackLead = _attackSign * BroadcastFraming.AttackLeadMetres;
            }
            else
            {
                float vz = (ball.z - _lastBall.z) / dt;
                _lastBall = ball;
                _ballVz = Mathf.Lerp(_ballVz, Mathf.Clamp(vz, -30f, 30f), 1f - Mathf.Exp(-dt * 3f));
                _lead = Mathf.SmoothDamp(_lead, Mathf.Clamp(_ballVz * LeadSeconds, -MaxLead, MaxLead), ref _leadVel, 0.5f, Mathf.Infinity, dt);
                // Lead towards the goal being attacked; a loose ball keeps the last direction (no swing on every touch).
                if (AttackDirection != 0) _attackSign = AttackDirection;
                _attackLead = Mathf.SmoothDamp(_attackLead, _attackSign * BroadcastFraming.AttackLeadMetres, ref _attackLeadVel, 1.2f, Mathf.Infinity, dt);
            }
            Vector3 target = Vector3.Lerp(ball, player, 0.15f);
            target.y = 0f;
            // Ends: the framed point may go to 3 m short of the goal line (it stopped 8 m short, so play on the line
            // sat at the frame edge).
            target.z = Mathf.Clamp(target.z + _lead + _attackLead, -HalfLength + 3f, HalfLength - 3f);
            target.x = Mathf.Clamp(target.x, -HalfWidth, HalfWidth);
            if (snap || !_snapped || dt <= 0f) { _focus = target; _snapped = true; _vel = Vector3.zero; }
            else _focus = Vector3.SmoothDamp(_focus, target, ref _vel, FollowDamping, Mathf.Infinity, dt);
            // Hard dead-zone edge around the ball itself: never more than MaxBallOffset behind it, whatever the smoothing.
            float fz = BroadcastFraming.ClampFollow(_focus.z, ball.z, MaxBallOffset);
            float fx = BroadcastFraming.ClampFollow(_focus.x, Mathf.Clamp(ball.x, -HalfWidth, HalfWidth), MaxBallOffset * 0.7f);
            if (fz != _focus.z) _vel.z = 0f;
            if (fx != _focus.x) _vel.x = 0f;
            _focus.x = fx;
            _focus.z = fz;

            Vector3 pos;
            Vector3 look;
            switch (CameraMode)
            {
                case Mode.Sideline:
                    cam.fieldOfView = 48f;
                    pos = new Vector3(HalfWidth + 6f, 4.5f, _focus.z);
                    look = new Vector3(_focus.x, 0.8f, _focus.z);
                    break;
                case Mode.CloseUp:
                    cam.fieldOfView = 40f;
                    pos = player + new Vector3(6f, 2.6f, -4f);
                    look = player + Vector3.up * 1.0f;
                    break;
                case Mode.GoalArea:
                    cam.fieldOfView = 55f;
                    // Just behind the net, in front of the stadium's LED ring and stand (14 m back put it inside the stand).
                    pos = new Vector3(0f, 6f, GoalSide * (HalfLength + 4f));
                    look = new Vector3(0f, 0.5f, GoalSide * (HalfLength - 14f));
                    break;
                case Mode.TopDown:
                    cam.fieldOfView = 60f;
                    pos = new Vector3(0f, 105f, 0f);
                    look = Vector3.zero;
                    transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, -90f, 0f));
                    return;
                default:
                    // Fixed gantry line behind the near touchline that slides along the pitch; it dollies in a little
                    // when play is near the camera. It aims at the framed point, then pitches up (below) so that point
                    // sits a little below the centre of the frame.
                    cam.fieldOfView = BroadcastFov;
                    // The gantry stops short of the ends, so near a goal the camera turns a little towards it (yaw is
                    // clamped to MaxYawDegrees) instead of sliding out over the stand.
                    pos = new Vector3(HalfWidth + BroadcastSetback - Mathf.Max(0f, _focus.x) * DollyIn, BroadcastHeight,
                        Mathf.Clamp(_focus.z, -(HalfLength - 14f), HalfLength - 14f));
                    look = new Vector3(_focus.x, 0f, _focus.z);
                    float maxDz = Mathf.Tan(MaxYawDegrees * Mathf.Deg2Rad) * Mathf.Max(1f, pos.x - look.x);
                    look.z = pos.z + Mathf.Clamp(look.z - pos.z, -maxDz, maxDz);
                    transform.position = pos;
                    transform.LookAt(look);
                    transform.rotation *= Quaternion.Euler(-BroadcastFraming.PitchUpDegrees(BroadcastFov, BallFromTop), 0f, 0f);
                    return;
            }
            transform.position = pos;
            transform.LookAt(look);
        }
    }
}
