using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Presentation-only goal framing for the broadcast camera (reads sim state, never writes it):
    /// <list type="bullet">
    /// <item>a smooth zoom-in (narrower FOV) while a shot is live close to goal, held briefly after it ends;</item>
    /// <item>a goal camera: during the dead-ball pause after a goal, a hard cut to a low side angle beside the goal
    /// the ball went into, then a hard cut back to the live broadcast framing. Skippable.</item>
    /// </list>
    /// </summary>
    public static class GoalCamera
    {
        /// <summary>FOV multiplier at full shot zoom (27 deg -> ~21 deg).</summary>
        public const float ShotZoomFovScale = 0.78f;
        /// <summary>Zoom starts this far from the goal line (m) and is full at <see cref="ShotZoomFullMetres"/>.</summary>
        public const float ShotZoomStartMetres = 32f, ShotZoomFullMetres = 14f;
        /// <summary>Seconds the zoom holds after the shot ends (save, block, goal) before easing out.</summary>
        public const float ShotZoomHoldSeconds = 1.2f;
        /// <summary>Smoothing time of the zoom (s).</summary>
        public const float ShotZoomSmoothSeconds = 0.45f;

        /// <summary>
        /// Goal camera: on the byline beside the goal, <see cref="GoalCamSide"/> m to the near (+X) side of the centre of
        /// the goal, <see cref="GoalCamBehind"/> m behind the goal line and <see cref="GoalCamHeight"/> m up, looking down
        /// into the goalmouth (aim point <see cref="GoalCamLookOut"/> m in front of the line at
        /// <see cref="GoalCamLookHeight"/>); FOV in degrees.
        /// The first version sat 2 m up looking level with a 40 deg FOV: the open stadium corner behind the goal (no
        /// geometry above the corner fill, so the clear colour) filled the top-left of the frame. This framing keeps the
        /// whole stadium skyline (corner fill rims, roof fronts) above the top of the frame
        /// (<see cref="StadiumSkyline"/>, tested).
        /// </summary>
        public const float GoalCamSide = 14f, GoalCamBehind = 1.5f, GoalCamHeight = 5f, GoalCamLookOut = 4f,
            GoalCamLookHeight = -1f, GoalCamFov = 34f;
        /// <summary>Longest goal camera hold (s, presentation time) if the restart pause runs long.</summary>
        public const float GoalCamMaxSeconds = 4f;
        /// <summary>Input is ignored for this long after the cut (held buttons from the shot do not skip it).</summary>
        public const float GoalCamSkipGraceSeconds = 0.3f;

        /// <summary>
        /// Target zoom amount 0..1 from the distance of the ball to the nearer goal line, while a shot is live (or
        /// within the hold after it): 0 beyond <see cref="ShotZoomStartMetres"/>, 1 inside <see cref="ShotZoomFullMetres"/>,
        /// smoothstep between.
        /// </summary>
        public static float ShotZoomAmount(bool shotActive, float ballZ, float halfLength)
        {
            if (!shotActive) return 0f;
            float toLine = halfLength - Math.Abs(ballZ);
            if (toLine >= ShotZoomStartMetres) return 0f;
            if (toLine <= ShotZoomFullMetres) return 1f;
            float t = (ShotZoomStartMetres - toLine) / (ShotZoomStartMetres - ShotZoomFullMetres);
            return t * t * (3f - 2f * t);
        }

        /// <summary>FOV for a zoom amount 0..1.</summary>
        public static float ZoomedFov(float baseFov, float amount)
        {
            amount = amount < 0f ? 0f : (amount > 1f ? 1f : amount);
            return baseFov * (1f - (1f - ShotZoomFovScale) * amount);
        }

        /// <summary>
        /// Goal camera pose for a goal at <paramref name="goalSign"/> (+1 = the +Z goal): on the byline beside the goal,
        /// looking down across the goalmouth (the line of sight passes in front of the near post, not through the side
        /// netting) at the ball in the net and the players around it.
        /// </summary>
        public static void Pose(int goalSign, float ballX, float halfWidth, float halfLength,
            out float px, out float py, out float pz, out float lx, out float ly, out float lz)
        {
            int s = goalSign >= 0 ? 1 : -1;
            px = Math.Min(GoalCamSide, halfWidth);
            py = GoalCamHeight;
            pz = s * (halfLength + GoalCamBehind);
            lx = Math.Max(-3f, Math.Min(3f, ballX)) * 0.3f;
            ly = GoalCamLookHeight;
            lz = s * (halfLength - GoalCamLookOut);
        }

        /// <summary>
        /// Goal camera state: <c>true</c> while the camera should hold the goal angle. Starts on the first frame of the
        /// dead-ball pause after a goal (<paramref name="ballInNet"/> != 0 during a stoppage); ends when the pause ends,
        /// after <see cref="GoalCamMaxSeconds"/> of unpaused presentation time, or when skipped. While the game is
        /// paused it holds: the timer does not advance, so a long pause does not end it.
        /// </summary>
        public sealed class Director
        {
            private bool _active, _doneForThisGoal;
            private float _elapsed;
            public int GoalSign { get; private set; }
            public bool Active => _active;
            public float Elapsed => _elapsed;

            /// <summary>
            /// Per frame. Returns true on the frame the cut starts. <paramref name="paused"/>: the game is paused (the
            /// state is frozen as it is; nothing starts, ends or times out).
            /// </summary>
            public bool Update(bool goalStoppage, int ballInNet, float dt, bool paused = false)
            {
                if (paused) return false;
                if (!goalStoppage || ballInNet == 0)
                {
                    _doneForThisGoal = false;
                    _active = false;
                    return false;
                }
                if (_active)
                {
                    _elapsed += dt > 0f ? dt : 0f;
                    if (_elapsed >= GoalCamMaxSeconds) { _active = false; _doneForThisGoal = true; }
                    return false;
                }
                if (_doneForThisGoal) return false;
                _active = true;
                _elapsed = 0f;
                GoalSign = ballInNet > 0 ? 1 : -1;
                return true;
            }

            /// <summary>Ends the goal camera for this goal (skip input).</summary>
            public void Skip()
            {
                if (!_active) return;
                _active = false;
                _doneForThisGoal = true;
            }

            public void Reset()
            {
                _active = false;
                _doneForThisGoal = false;
                _elapsed = 0f;
            }
        }

        /// <summary>
        /// Skip input for the goal camera: any NEW button, key, click or tap (<see cref="OffsideSkipGate"/> bitmask)
        /// after <see cref="GoalCamSkipGraceSeconds"/> skips it. Pausing is not a skip: while paused, and on the frame
        /// the game resumes, whatever is down is absorbed as "held" (the pause / resume press itself never skips), and
        /// the grace timer does not advance.
        /// </summary>
        public sealed class SkipInput
        {
            private readonly OffsideSkipGate _gate = new OffsideSkipGate(GoalCamSkipGraceSeconds);
            private bool _armed, _wasPaused;
            private float _elapsed;

            /// <summary>Seconds of unpaused time since the goal camera started.</summary>
            public float Elapsed => _elapsed;

            /// <summary>
            /// Per frame. <paramref name="goalCamActive"/>: the goal camera is showing; <paramref name="down"/>: skip
            /// controls down this frame; <paramref name="paused"/>: the game is paused (after this frame's pause toggle);
            /// <paramref name="dt"/>: presentation seconds. True when this frame's input skips the goal camera.
            /// </summary>
            public bool Update(bool goalCamActive, ulong down, bool paused, float dt)
            {
                if (!goalCamActive)
                {
                    _armed = false;
                    return false;
                }
                if (!_armed)
                {
                    _armed = true;
                    _elapsed = 0f;
                    _wasPaused = paused;
                    _gate.Begin(down);
                    return false;
                }
                if (paused || _wasPaused)
                {
                    _wasPaused = paused;
                    _gate.Begin(down);
                    return false;
                }
                _elapsed += dt > 0f ? dt : 0f;
                return _gate.Update(down, _elapsed);
            }
        }
    }
}
