namespace OpenFifa.Core
{
    /// <summary>
    /// What the game knows about the session when a player presses "Send feedback".
    /// Only technical facts: no names, accounts or other personal data. The player sees all of it in the
    /// prefilled issue before they submit.
    /// </summary>
    public struct FeedbackContext
    {
        public string BuildVersion;   // Application.version
        public string GitSha;         // short sha baked in at build time, may be null
        public string Platform;       // e.g. "iPadOS", "macOS", "Linux"
        public string DeviceModel;    // SystemInfo.deviceModel
        public string OsVersion;      // SystemInfo.operatingSystem
        public float MeanFps;         // FramePacer sample; <= 0 means unknown
        public string InputScheme;    // e.g. "Touch", "Gamepad (Classic)", "Keyboard and mouse"
        public string League;         // e.g. "opd"
        public string Difficulty;
        public int MatchMinutes;      // <= 0 means unknown
    }
}
