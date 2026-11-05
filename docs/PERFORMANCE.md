# Performance

Targets: a locked 60 fps for a full match on iPad (M1 and newer, A-series Pro models) and M1+ Macs such as
the MacBook Air, with no loss in visual or gameplay quality, and without the device throttling before full time.

## Frame pacing (iPad)

iOS runs Unity at 30 fps unless `Application.targetFrameRate` is set, and nothing in the project set it.
`FramePacer` (Gameplay) now sets the target at startup and re-checks every 2 s:

| State | Target |
|---|---|
| Default (Auto) | 60 |
| Preference Max on a ProMotion iPad | 120, falling back to 60 when Low Power Mode is on or thermal state is Serious |
| Thermal state Critical | 30 (iOS is about to throttle hard; a steady 30 beats an uneven 40-50) |
| macOS | unchanged, vSync paces to the display |

The rules live in `FramePacingPolicy` (Core, unit-tested). Systems that need a specific rate, such as the
external display, should call `FramePacer.SetOverride(fps)` / `ClearOverride()` rather than writing
`Application.targetFrameRate`, so the thermal caps still apply. Thermal state and Low Power Mode come from
`NSProcessInfo` through `Plugins/iOS/OFThermalState.mm`.

## Player settings

- `metalAPIValidation` is off. It only applies when the app is launched from Xcode, where it adds CPU cost to every
  Metal call and makes debug-session frame times misleading.

## Simulation and AI budget

Measured with the headless benchmark (full AI-vs-AI 11v11 match, 60 Hz sim, 43,266 ticks), on CodeBox2 (x86, shared and loaded):

| Metric | Value |
|---|---|
| Mean sim + AI cost per tick | 30-37 us |
| Median tick | 10 us |
| 99th percentile tick (10 Hz AI decision ticks) | 165-280 us |
| Managed allocation per tick | 0.3 B (effectively zero; no GC pressure) |

That is well under 1% of a 16.7 ms frame, so the sim and team AI are not the bottleneck; the remaining budget belongs to
rendering, skinning and animation. Spikes above 1 ms in these runs line up with load from other jobs on the box, not the code.
