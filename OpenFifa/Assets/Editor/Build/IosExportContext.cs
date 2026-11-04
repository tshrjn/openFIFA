namespace OpenFifa.Editor
{
    /// <summary>Parameters of one iPad export, handed from <see cref="IosBuild"/> to the postprocess step.</summary>
    internal sealed class IosExportContext
    {
        public IosExportContext(IosExportKind kind, string bundleId, string teamId, string buildNumber,
            string outputPath, string repoRoot)
        {
            Kind = kind;
            BundleId = bundleId;
            TeamId = teamId;
            BuildNumber = buildNumber;
            OutputPath = outputPath;
            RepoRoot = repoRoot;
        }

        public IosExportKind Kind { get; }
        public string BundleId { get; }
        public string TeamId { get; }
        public string BuildNumber { get; }
        public string OutputPath { get; }
        public string RepoRoot { get; }

        /// <summary>App Store profile name created by ios-factory's `asc profile` for this bundle ID.</summary>
        public string ProvisioningProfileName => IosBuild.ProvisioningProfilePrefix + BundleId;
    }
}
