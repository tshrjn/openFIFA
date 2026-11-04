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
        public float Height = 24f;
        public float Distance = 40f;
        public float FieldOfView = 34f;
        public float Damping = 0.35f;
        /// <summary>Which goal the GoalArea mode looks at (+1 = +Z).</summary>
        public int GoalSide = 1;

        private Vector3 _focus;
        private Vector3 _vel;
        private bool _snapped;

        /// <summary>Camera yaw in radians, same convention as the sim (0 = +Z, +pi/2 = +X).</summary>
        public float YawRadians => Mathf.Atan2(transform.forward.x, transform.forward.z);

        private void LateUpdate() => UpdateNow(Time.unscaledDeltaTime, false);

        public void Snap() => UpdateNow(0f, true);

        public void UpdateNow(float dt, bool snap)
        {
            var cam = GetComponent<Camera>();
            Vector3 ball = Ball != null ? Ball.position : Vector3.zero;
            Vector3 player = Player != null ? Player.position : ball;
            Vector3 target = Vector3.Lerp(ball, player, 0.2f);
            target.y = 0f;
            target.z = Mathf.Clamp(target.z, -HalfLength + 12f, HalfLength - 12f);
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
                    pos = new Vector3(0f, 7f, GoalSide * (HalfLength + 14f));
                    look = new Vector3(0f, 0.5f, GoalSide * (HalfLength - 14f));
                    break;
                case Mode.TopDown:
                    cam.fieldOfView = 60f;
                    pos = new Vector3(0f, 105f, 0f);
                    look = Vector3.zero;
                    transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, -90f, 0f));
                    return;
                default:
                    cam.fieldOfView = FieldOfView;
                    pos = new Vector3(_focus.x * 0.3f + HalfWidth + Distance * 0.55f, Height, _focus.z);
                    look = new Vector3(_focus.x * 0.6f, 0f, _focus.z);
                    break;
            }
            transform.position = pos;
            transform.LookAt(look);
        }
    }
}
