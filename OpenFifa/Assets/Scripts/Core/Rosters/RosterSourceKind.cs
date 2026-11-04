namespace OpenFifa.Core
{
    /// <summary>Where a roster comes from.</summary>
    public enum RosterSourceKind
    {
        /// <summary>Ships inside the app (StreamingAssets). Must be fictional / cleared data.</summary>
        Bundled = 0,
        /// <summary>A file the user placed in the device's Documents folder. Never shipped by OpenFifa.</summary>
        LocalFile = 1
    }
}
