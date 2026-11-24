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
    ///
    /// Rig (<see cref="BroadcastRig"/>): fixed height; the camera rides back towards the main stand when play is on
    /// the near side and forward when it is on the far side, and its tilt is clamped to 22-30 deg, so it never swings
    /// towards overhead on near-side play and players stay a steady size. Goals (<see cref="GoalCamera"/>): a smooth
    /// zoom-in while a shot is live near goal, and a hard cut to a low byline camera beside the goal during the pause
    /// after a goal (skippable, <see cref="SkipGoalCamera"/>), then a hard cut back with the follow eased in.
    /// Presentation only: nothing here feeds the sim except <see cref="YawRadians"/>, which stays the broadcast yaw
    /// during the goal camera.
    /// </summary>
    /// <remarks>
    /// Execution order: runs after the match controller (-50, which places the ball and sizes its visual in its own
    /// LateUpdate) and after the animation drivers (100, which move the players), so the framing reads this frame's ball
    /// and player, not last frame's.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]
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
        // Broadcast ("tele") framing. New field names so values serialised by older scenes are not picked up.
        [Tooltip("Broadcast camera height (m); the stand fit may raise it.")] public float BroadcastRigHeight = BroadcastRig.DefaultHeight;
        [Tooltip("Broadcast camera depth behind the near touchline with play on the near touchline (m).")]
        public float BroadcastNearSetback = BroadcastRig.DefaultNearSetback;
        [Tooltip("Broadcast camera depth behind the near touchline with play on the far touchline (m).")]
        public float BroadcastFarSetback = BroadcastRig.DefaultFarSetback;
        [Tooltip("Broadcast vertical field of view (deg).")] public float BroadcastRigFov = BroadcastRig.DefaultFov;
        [Tooltip("Seconds of ball travel (along the pitch) the framing leads by.")] public float LeadSeconds = 0.5f;
        [Tooltip("Largest lead (m).")] public float MaxLead = 6f;
        [Tooltip("Smoothing time of the framed point (s).")] public float FollowDamping = 0.3f;
        [Tooltip("Largest distance (m) the framed point may be from the ball, along and across the pitch.")]
        public float MaxBallOffset = 9f;
        [Tooltip("Ball height on screen as a fraction from the top.")] public float BallFromTop = BroadcastFraming.BallScreenFromTop;
        /// <summary>Set each frame by the match: +1/-1 = the team in possession attacks +Z/-Z, 0 = loose ball (keeps the last lead).</summary>
        public int AttackDirection { get; set; }
        /// <summary>Set each frame by the match: a shot is live (drives the zoom-in near goal).</summary>
        public bool ShotLive { get; set; }
        /// <summary>Set each frame by the match: +1/-1 = dead-ball pause after a goal in the +Z/-Z goal, 0 = otherwise.</summary>
        public int GoalStoppageSide { get; set; }
        /// <summary>The game is paused: the goal camera holds and its timer stops (set by the match controller).</summary>
        public bool Paused { get; set; }
        /// <summary>True while the goal camera holds the byline angle.</summary>
        public bool GoalCameraActive => _goalCam.Active;
        /// <summary>Fired when the goal camera cuts in (true) and back (false).</summary>
        public event System.Action<bool> GoalCameraChanged;
        [Tooltip("Largest broadcast yaw towards a goal (deg).")] public float MaxYawDegrees = 25f;
        /// <summary>Which goal the GoalArea mode looks at (+1 = +Z).</summary>
        public int GoalSide = 1;

        private Vector3 _focus;
        private Vector3 _vel;
        private bool _snapped;
        private Vector3 _lastBall;
        private float _ballVz, _carryVx, _carryVz, _lead, _leadVel, _attackLead, _attackLeadVel, _attackSign;
        private float _sinceResume = float.MaxValue;
        private float _zoom, _zoomVel, _sinceShot = float.MaxValue;
        private readonly GoalCamera.Director _goalCam = new GoalCamera.Director();
        private float _broadcastYaw = float.NaN;

        /// <summary>
        /// Camera yaw in radians, same convention as the sim (0 = +Z, +pi/2 = +X). In broadcast mode this is the
        /// broadcast yaw even while the goal camera holds another angle, so the stick mapping never jumps.
        /// </summary>
        public float YawRadians => CameraMode == Mode.Broadcast && !float.IsNaN(_broadcastYaw)
            ? _broadcastYaw
            : Mathf.Atan2(transform.forward.x, transform.forward.z);

        /// <summary>Ends the goal camera now (any input); hard cut back to the live broadcast framing.</summary>
        public void SkipGoalCamera()
        {
            if (!_goalCam.Active) return;
            _goalCam.Skip();
            EndGoalCamera();
        }

        private void EndGoalCamera()
        {
            ResumeFollow();
            GoalCameraChanged?.Invoke(false);
        }

        /// <summary>
        /// Presentation time: real time live, the fixed capture frame time when recording (wall-clock time in a slow
        /// capture made the follow snap and timed the goal camera out after a few frames).
        /// </summary>
        private void LateUpdate() =>
            UpdateNow((float)PresentationClock.FrameSeconds(Time.captureDeltaTime, Time.unscaledDeltaTime), false);

        public void Snap() => UpdateNow(0f, true);

        /// <summary>
        /// Hand-back from a presentation cut (offside camera cut): keep the framed point (no re-snap), but restart the
        /// follow from rest — spring velocity and carried ball speed zeroed, ball-speed estimate restarted from the
        /// ball's current position — and ease the follow back in over <see cref="BroadcastFraming.ResumeEaseSeconds"/>,
        /// so play resuming after the freeze does not jerk the framing.
        /// </summary>
        public void ResumeFollow()
        {
            _vel = Vector3.zero;
            _carryVx = _carryVz = 0f;
            if (Ball != null) _lastBall = Ball.position;
            _sinceResume = 0f;
        }

        public void UpdateNow(float dt, bool snap)
        {
            var cam = GetComponent<Camera>();
            Vector3 ball = Ball != null ? Ball.position : Vector3.zero;
            Vector3 player = Player != null ? Player.position : ball;
            // Lead in the direction of play: smoothed ball speed along the pitch, damped so it does not jitter on touches.
            if (snap || !_snapped || dt <= 0f)
            {
                _lastBall = ball;
                _carryVx = 0f;
                _carryVz = 0f;
                if (!_snapped) { _ballVz = 0f; _lead = 0f; _leadVel = 0f; _attackLead = 0f; _attackLeadVel = 0f; _attackSign = 0f; }
                if (AttackDirection != 0) _attackSign = AttackDirection;
                _attackLead = _attackSign * BroadcastFraming.AttackLeadMetres;
            }
            else
            {
                float vz = (ball.z - _lastBall.z) / dt;
                float vx = (ball.x - _lastBall.x) / dt;
                _lastBall = ball;
                _ballVz = Mathf.Lerp(_ballVz, Mathf.Clamp(vz, -30f, 30f), 1f - Mathf.Exp(-dt * 3f));
                // Lightly smoothed ball velocity (frame-time noise only), carried by the framing out of the dead-zone edge.
                float kc = 1f - Mathf.Exp(-dt * 15f);
                _carryVx = Mathf.Lerp(_carryVx, Mathf.Clamp(vx, -BroadcastFraming.MaxFollowSpeed, BroadcastFraming.MaxFollowSpeed), kc);
                _carryVz = Mathf.Lerp(_carryVz, Mathf.Clamp(vz, -BroadcastFraming.MaxFollowSpeed, BroadcastFraming.MaxFollowSpeed), kc);
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
            if (snap || !_snapped || dt <= 0f)
            {
                _focus = target;
                _snapped = true;
                _vel = Vector3.zero;
                // Hard dead-zone edge around the ball itself: never more than MaxBallOffset behind it.
                _focus.z = BroadcastFraming.ClampFollow(_focus.z, ball.z, MaxBallOffset);
                _focus.x = BroadcastFraming.ClampFollow(_focus.x, Mathf.Clamp(ball.x, -HalfWidth, HalfWidth), MaxBallOffset * 0.7f);
            }
            else
            {
                // Smooth follow plus the hard dead-zone edge; while the edge holds the framing it moves at the ball's
                // speed, and keeps that speed when the edge lets go (no dead stop after a long ball). Right after a
                // presentation cut hands back (ResumeFollow) the smoothing starts slower and eases back in.
                float damping = BroadcastFraming.ResumeSmoothTime(FollowDamping, _sinceResume);
                if (_sinceResume < float.MaxValue) _sinceResume += dt;
                _focus.z = BroadcastFraming.FollowAxis(_focus.z, ref _vel.z, target.z, ball.z, _carryVz, MaxBallOffset, damping, dt);
                _focus.x = BroadcastFraming.FollowAxis(_focus.x, ref _vel.x, target.x, Mathf.Clamp(ball.x, -HalfWidth, HalfWidth), _carryVx,
                    MaxBallOffset * 0.7f, damping, dt);
                _focus.y = 0f;
            }

            // Shot zoom (smooth, held briefly after the shot ends) and the goal camera director. Presentation time.
            bool snapNow = snap || dt <= 0f;
            if (ShotLive || GoalStoppageSide != 0) _sinceShot = 0f;
            else if (_sinceShot < float.MaxValue) _sinceShot += Mathf.Max(0f, dt);
            float zoomTarget = GoalCamera.ShotZoomAmount(_sinceShot <= GoalCamera.ShotZoomHoldSeconds, ball.z, HalfLength);
            if (snapNow) { _zoom = zoomTarget; _zoomVel = 0f; }
            else _zoom = BroadcastFraming.SmoothDamp(_zoom, zoomTarget, ref _zoomVel, GoalCamera.ShotZoomSmoothSeconds, dt);
            bool goalWasActive = _goalCam.Active;
            if (_goalCam.Update(GoalStoppageSide != 0, GoalStoppageSide, Mathf.Max(0f, dt), Paused)) GoalCameraChanged?.Invoke(true);
            else if (goalWasActive && !_goalCam.Active) EndGoalCamera();

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
                {
                    // Gantry behind the near touchline that slides along the pitch (stopping short of the ends, where
                    // the camera turns towards the goal) and rides back/forward with the play across the pitch, at a
                    // fixed height. Tilt puts the framed point BallFromTop down the frame, clamped to 22-30 deg.
                    float fov = GoalCamera.ZoomedFov(BroadcastRigFov, _zoom);
                    BroadcastRig.Pose(_focus.x, _focus.z, HalfWidth, HalfLength, BroadcastRigHeight, BroadcastNearSetback,
                        BroadcastFarSetback, fov, BallFromTop, MaxYawDegrees,
                        out float cx, out float cy, out float cz, out float yawDeg, out float pitchDeg);
                    // Sim-facing yaw: atan2 of the horizontal aim in single precision, exactly as the old rig's
                    // atan2(forward) gave it, so a straight-across camera reports the same yaw bit for bit (the capture
                    // probe builds its session from it; a one-ulp change reshuffles the whole bot match).
                    BroadcastRig.HorizontalAim(cx, cz, _focus.x, _focus.z, MaxYawDegrees, out float across, out float aimDz);
                    _broadcastYaw = Mathf.Atan2(-across, aimDz);
                    if (_goalCam.Active)
                    {
                        GoalCamera.Pose(_goalCam.GoalSign, ball.x, HalfWidth, HalfLength,
                            out float gx, out float gy, out float gz, out float lx, out float ly, out float lz);
                        cam.fieldOfView = GoalCamera.GoalCamFov;
                        transform.position = new Vector3(gx, gy, gz);
                        transform.LookAt(new Vector3(lx, ly, lz));
                        return;
                    }
                    cam.fieldOfView = fov;
                    transform.SetPositionAndRotation(new Vector3(cx, cy, cz), Quaternion.Euler(pitchDeg, yawDeg, 0f));
                    return;
                }
            }
            transform.position = pos;
            transform.LookAt(look);
        }
    }
}
