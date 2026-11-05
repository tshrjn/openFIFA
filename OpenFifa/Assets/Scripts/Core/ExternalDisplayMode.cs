namespace OpenFifa.Core
{
    /// <summary>
    /// What the iPad's own screen shows while the game is on an external monitor.
    /// </summary>
    public enum ExternalDisplayMode
    {
        /// <summary>iPad shows a top-down tactical view of the pitch (second screen). Default.</summary>
        Companion = 0,

        /// <summary>iPad shows the same broadcast view as the monitor, rendered at reduced resolution.</summary>
        Mirror = 1,

        /// <summary>iPad shows only its own UI on a dark background (lowest GPU and battery cost).</summary>
        ControllerOnly = 2,
    }
}
