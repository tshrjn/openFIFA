namespace OpenFifa.Core
{
    /// <summary>
    /// The clock commentary runs on: presentation time, i.e. what the viewer experiences. In the live game that is real
    /// (unscaled) time. In a capture (Time.captureFramerate set) it is the fixed capture frame time: the recording plays
    /// back at that rate however slowly the frames were rendered, so wall-clock time would race ahead of the video and
    /// fire lines many times too often (a 0.4 s median gap between lines in the 2026-11-05 capture).
    /// </summary>
    public static class PresentationClock
    {
        /// <summary>Seconds of presentation time this frame: the capture frame time when capturing, else real time.</summary>
        public static double FrameSeconds(float captureDeltaTime, float unscaledDeltaTime)
        {
            if (captureDeltaTime > 0f) return captureDeltaTime;
            return unscaledDeltaTime > 0f ? unscaledDeltaTime : 0.0;
        }
    }
}
