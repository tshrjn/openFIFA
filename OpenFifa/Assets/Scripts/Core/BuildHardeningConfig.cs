using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Build hardening configuration for macOS and iPad.
    /// Defines minimum versions, architecture, and required frameworks.
    /// </summary>
    public class BuildHardeningConfig
    {
        public string MacOSMinVersion = "14.0";
        public string IPadOSMinVersion = "17.0";
        public string TargetArchitecture = "ARM64";
        public string ScriptingBackend = "IL2CPP";
        public int MaxBundleSizeMB = 199;

        public List<string> MacOSFrameworks = new List<string>
        {
            "Metal", "AppKit", "AVFoundation"
        };

        public List<string> IPadFrameworks = new List<string>
        {
            "Metal", "UIKit", "AVFoundation"
        };
    }
}
