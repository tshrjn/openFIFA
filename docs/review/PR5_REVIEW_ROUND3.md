# PR #5 review round 3 (at 4bd1803)

Sim note: for lobs, PassLeadPoint runs a full LoftedSpeed bisection on every lead iteration (~50k integrator steps at contact). Cache it.

MAJOR
1. SinglePlayerInput.cs:47-52: ReadTouches reads touch phases once per sim tick. On a 120 Hz iPad, frames without a tick drop Began/Ended (lost presses, stuck charge), and frames with 2 ticks see Began twice. Queue touch events in Update and drain them in Read.
2. Players run on the spot whenever the sim is stopped (pause, Frozen, HalfTime, FullTime), because SinglePlayerMatchController.cs:396-398 and PlayerAnimationDriver keep feeding the last velocity. Zero the animator speed and speed parameter while stopped.
3. SimTeamAIAdapter.cs:189-196 steers by cmd.MoveDirection, which only refreshes at 10 Hz. Sprinters overshoot their targets by ~0.8 m and turn back (jitter, foot-skate at formation spots). Steer by the live to/dist each tick.
4. SimAnimActionTracker.cs:60-67: DribbleTouch isn't animated, so the ball jumps up to 3 m with no touch. Save and FirstTouch clips start after the sim already resolved the contact, so the keeper dives after the parry. Drive a touch clip or IK on DribbleTouch, and start dives from the predicted closest-approach time.
5. SinglePlayerHud.cs:100-102: the 7 difficulty buttons need 1110 units, but a 4:3 iPad is ~961 wide (12.9") or ~1031 (11"), so Beginner and Ultimate sit ~74 units off-screen. Size from width w, or use two rows.
6. The ball is drawn at 0.22 m radius but its centre is clamped to the sim's 0.11 m, so half of it sinks into the grass (SinglePlayerMatchController.cs:141 and 440). It never rotates, so it slides. Use y = max(simY, visualR) or scale to 0.11, and drive spin from Ball.Spin/velocity.
7. SinglePlayerCapture.cs:61-75: the probe session runs with null input (AI plays the user's player), but the recorded match uses device input unless -spBot or -spAiVsAi, so the probe's goal never happens and the user's player idles. Force bot or AI-vs-AI when -spCapture is set.
8. HumanPlayerSelector.Update never moves ControlledId off a sent-off player. Switch when Players[ControlledId].SentOff.
9. HumanControlAdapter.cs:64-88 flattens input variants: chip/low shot -> normal shot, driven pass/through -> plain, lobbed through -> plain lob without lead. LB+B, double-tap low and RB+A do nothing distinct. Pass a variant/elevation hint in PlayerIntent, or add sim actions.
MINOR
10. GC every frame: CommentaryEngine.cs:171 allocates a new List per Advance (plus a Push per event), and the IMGUI HUD builds interpolated strings every OnGUI (score, ClockText, status). Reuse lists, cache strings.
11. KickTiming.InterceptPoint O(h^2): fixed by PR #10 (pending merge).
12. Controlled-player ring lags a frame (~13 cm at sprint): controller order -50, driver 100 (:446). Move to LateUpdate.
13. MaterialPropertyBlock tint on every renderer (:360-366) breaks SRP batching and tints skin like the kit. Plan to use the kit shader from PR #3 (OpenFifa/Kit + JerseyRenderer) once merged.
14. No rematch/menu after full time (SinglePlayerHud.DrawFullTime): dead end.
15. SinglePlayerCapture.cs:74 hard-codes 60 Hz; use the sim's TickRate. Capture.Awake clears AutoStart, racing the controller's Awake that sets it from -spAutoStart. Use Start or DefaultExecutionOrder.
