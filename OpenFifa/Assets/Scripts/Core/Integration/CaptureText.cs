using System;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Text formatting shared by the review capture (commentary.txt, sequences.txt): the broadcast match clock with
    /// tenths and one commentary log line. Engine-free so the formats are tested without Unity.
    /// </summary>
    public static class CaptureText
    {
        /// <summary>
        /// Broadcast clock "mm:ss.s" on the 90-minute scale: <paramref name="clock"/> sim seconds into half
        /// <paramref name="half"/> (1 or 2) of a half lasting <paramref name="halfDurationSeconds"/> sim seconds.
        /// Truncates (like a stopwatch), so it never shows 60.0 seconds.
        /// </summary>
        public static string Clock(float clock, int half, float halfDurationSeconds)
        {
            double minutes = (halfDurationSeconds > 0f ? clock / (double)halfDurationSeconds * 45.0 : 0.0) + (half >= 2 ? 45.0 : 0.0);
            if (minutes < 0.0) minutes = 0.0;
            long tenths = (long)Math.Floor(minutes * 600.0 + 1e-6);
            long m = tenths / 600, rem = tenths % 600;
            var sb = new StringBuilder(8);
            if (m < 10) sb.Append('0');
            sb.Append(m).Append(':');
            if (rem / 10 < 10) sb.Append('0');
            sb.Append(rem / 10).Append('.').Append(rem % 10);
            return sb.ToString();
        }

        /// <summary>"play-by-play" or "analyst".</summary>
        public static string Speaker(CommentaryVoice voice) => voice == CommentaryVoice.Analyst ? "analyst" : "play-by-play";

        /// <summary>
        /// One commentary.txt line: "mm:ss.s | event | speaker | text". The transcript's clip separators (" | ") are
        /// joined with spaces so the line keeps exactly four fields.
        /// </summary>
        public static string CommentaryLine(string clock, CommentaryEventType evt, CommentaryVoice voice, string transcript)
        {
            string text = (transcript ?? string.Empty).Replace(" | ", " ").Replace('\n', ' ').Trim();
            return clock + " | " + evt + " | " + Speaker(voice) + " | " + text;
        }
    }
}
