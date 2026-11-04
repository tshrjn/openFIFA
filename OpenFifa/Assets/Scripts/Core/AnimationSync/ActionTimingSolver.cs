namespace OpenFifa.Core
{
    /// <summary>When and how fast to play an action clip so its contact frame lands on the sim's contact time.</summary>
    public readonly struct ActionTimingPlan
    {
        /// <summary>Sim time (s) at which the clip should start playing.</summary>
        public readonly float StartTime;
        /// <summary>Clip time (s) to start from (non-zero when part of the wind-up is skipped).</summary>
        public readonly float StartOffset;
        /// <summary>Playback rate multiplier.</summary>
        public readonly float PlaybackRate;
        /// <summary>Sim time (s) at which the contact frame will play with this plan.</summary>
        public readonly float PredictedContactTime;
        /// <summary>PredictedContactTime minus the requested contact time (positive = late).</summary>
        public readonly float TimingError;
        public readonly bool Feasible;

        public ActionTimingPlan(float startTime, float startOffset, float playbackRate, float predictedContactTime,
            float timingError, bool feasible)
        {
            StartTime = startTime;
            StartOffset = startOffset;
            PlaybackRate = playbackRate;
            PredictedContactTime = predictedContactTime;
            TimingError = timingError;
            Feasible = feasible;
        }

        /// <summary>Wind-up duration in sim seconds (from StartTime to the contact frame).</summary>
        public float WindupDuration => PredictedContactTime - StartTime;

        /// <summary>Clip time (s) being shown at sim time t, or -1 before the clip starts.</summary>
        public float ClipTimeAt(float simTime) => simTime < StartTime ? -1f : StartOffset + (simTime - StartTime) * PlaybackRate;
    }

    /// <summary>
    /// Wind-up timing: prefers rate 1 with a delayed start; if there is not enough time it speeds the clip up
    /// (up to MaxRate) and then skips into the wind-up (never past MinWindup before contact).
    /// </summary>
    public static class ActionTimingSolver
    {
        /// <summary>Convert a sim tick to seconds.</summary>
        public static float TickToSeconds(long tick, float tickRateHz) => tickRateHz > 0f ? (float)(tick / (double)tickRateHz) : 0f;

        public static ActionTimingPlan Solve(ActionClipProfile profile, float now, float contactTime, float toleranceSeconds = 1e-4f)
        {
            float natural = profile.ContactTime;
            float available = contactTime - now;

            if (natural <= 0f)
            {
                // Clip has no contact frame (fall, celebration): just start now.
                return new ActionTimingPlan(now, 0f, 1f, now, now - contactTime, true);
            }

            if (available >= natural)
            {
                float start = contactTime - natural;
                return new ActionTimingPlan(start, 0f, 1f, contactTime, 0f, true);
            }

            if (available > 0f)
            {
                float rate = natural / available;
                if (rate <= profile.MaxRate)
                    return new ActionTimingPlan(now, 0f, rate, contactTime, 0f, true);
            }

            // Too little time even at MaxRate: skip part of the wind-up.
            float maxRate = profile.MaxRate;
            float maxSkip = natural - profile.MinWindup;
            float wantSkip = available > 0f ? natural - available * maxRate : maxSkip;
            float skip = AnimMath.Clamp(wantSkip, 0f, maxSkip);
            float predicted = now + (natural - skip) / maxRate;
            float error = predicted - contactTime;
            return new ActionTimingPlan(now, skip, maxRate, predicted, error, error <= toleranceSeconds);
        }
    }
}
