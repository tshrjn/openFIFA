using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Side-on broadcast camera for the 11v11 sim pitch (length along Z). It sits beyond the +X touchline looking at
    /// -X, so +Z (where Home attacks in the first half) is screen-right. The yaw it reports is what the session uses to
    /// map the stick to the pitch. Extra modes exist for review stills.
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
        [Tooltip("Seconds of ball travel (along the pitch) the framing leads by.")] public float LeadSeconds = 0.6f;
        [Tooltip("Largest lead (m).")] public float MaxLead = 7f;
        public float Damping = 0.45f;
        /// <summary>How far the camera moves in towards the near touchline per metre the play is on the near side.</summary>
        public const float DollyIn = 0.15f;
        [Tooltip("Largest broadcast yaw towards a goal (deg).")] public float MaxYawDegrees = 25f;
        /// <summary>Which goal the GoalArea mode looks at (+1 = +Z).</summary>
        public int GoalSide = 1;

        private Vector3 _focus;
        private Vector3 _vel;
        private bool _snapped;
        private Vector3 _lastBall;
        private float _ballVz, _lead, _leadVel;

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
                if (!_snapped) { _ballVz = 0f; _lead = 0f; _leadVel = 0f; }
            }
            else
            {
                float vz = (ball.z - _lastBall.z) / dt;
                _lastBall = ball;
                _ballVz = Mathf.Lerp(_ballVz, Mathf.Clamp(vz, -30f, 30f), 1f - Mathf.Exp(-dt * 3f));
                _lead = Mathf.SmoothDamp(_lead, Mathf.Clamp(_ballVz * LeadSeconds, -MaxLead, MaxLead), ref _leadVel, 0.8f, Mathf.Infinity, dt);
            }
            Vector3 target = Vector3.Lerp(ball, player, 0.2f);
            target.y = 0f;
            target.z = Mathf.Clamp(target.z + _lead, -HalfLength + 8f, HalfLength - 8f);
            target.x = Mathf.Clamp(target.x, -HalfWidth, HalfWidth);
            if (snap || !_snapped || dt <= 0f) { _focus = target; _snapped = true; }
            else _focus = Vector3.SmoothDamp(_focus, target, ref _vel, Damping, Mathf.Infinity, dt);

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
                    // when play is near the camera. The look point follows the ball across, biased to the centre so
                    // the far touchline stays in shot, and the near touchline comes in when play is near it.
                    cam.fieldOfView = BroadcastFov;
                    // The gantry stops short of the ends, so near a goal the camera turns a little towards it (yaw is
                    // clamped to MaxYawDegrees) instead of sliding out over the stand.
                    pos = new Vector3(HalfWidth + BroadcastSetback - Mathf.Max(0f, _focus.x) * DollyIn, BroadcastHeight,
                        Mathf.Clamp(_focus.z, -(HalfLength - 14f), HalfLength - 14f));
                    look = new Vector3(_focus.x * 0.75f, 0f, _focus.z);
                    float maxDz = Mathf.Tan(MaxYawDegrees * Mathf.Deg2Rad) * Mathf.Max(1f, pos.x - look.x);
                    look.z = pos.z + Mathf.Clamp(look.z - pos.z, -maxDz, maxDz);
                    break;
            }
            transform.position = pos;
            transform.LookAt(look);
        }
    }
}
