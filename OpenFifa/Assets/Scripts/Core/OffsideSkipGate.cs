namespace OpenFifa.Core
{
    /// <summary>
    /// Decides when an input skips the offside presentation: only a NEW press counts. Each frame the caller passes a
    /// bitmask of which skip controls are down (one bit per key / button / mouse button; a touch counts as one-frame
    /// pulses on phase Began). A skip needs a bit to go from up to down after the grace time:
    /// - a control already held when the sequence starts (a held sprint button, the pass that played the ball) never
    ///   skips until it is released and pressed again;
    /// - a press during the grace time is swallowed the same way (it must be released and pressed again).
    /// Sticks are not skip controls, so a held or pushed stick never skips.
    /// Pure C#: EditMode testable.
    /// </summary>
    public sealed class OffsideSkipGate
    {
        private ulong _down;

        /// <summary>Seconds from the start during which presses are ignored.</summary>
        public float GraceSeconds { get; set; }

        public OffsideSkipGate(float graceSeconds = 0.25f)
        {
            GraceSeconds = graceSeconds;
        }

        /// <summary>Start of a sequence: whatever is down now is "held", not a press.</summary>
        public void Begin(ulong downNow)
        {
            _down = downNow;
        }

        /// <summary>
        /// One frame. <paramref name="downNow"/>: controls down this frame; <paramref name="elapsed"/>: seconds since
        /// <see cref="Begin"/>. True when a control went down this frame after the grace time.
        /// </summary>
        public bool Update(ulong downNow, float elapsed)
        {
            ulong pressed = downNow & ~_down;
            _down = downNow;
            return pressed != 0 && elapsed >= GraceSeconds;
        }
    }
}
