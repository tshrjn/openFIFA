using System;
using System.IO;

namespace OpenFifa.Core
{
    /// <summary>
    /// The roster that ships with the game, read from StreamingAssets/Rosters/. Core has no UnityEngine, so the
    /// Gameplay layer passes Application.streamingAssetsPath (a plain folder on iOS/macOS), or injects a text
    /// reader for platforms where StreamingAssets is not a folder.
    /// </summary>
    public sealed class BundledRosterSource : IRosterSource
    {
        public const string DefaultFileName = "default_league.json";
        public const string FolderName = "Rosters";

        private readonly Func<string> _readText;
        private readonly string _fileName;

        public string SourceId => "bundled:" + Path.GetFileNameWithoutExtension(_fileName);
        public string DisplayName { get; }
        public RosterSourceKind Kind => RosterSourceKind.Bundled;

        /// <summary>Reads &lt;streamingAssetsRoot&gt;/Rosters/&lt;fileName&gt;.</summary>
        public BundledRosterSource(string streamingAssetsRoot, string fileName = DefaultFileName,
            string displayName = "Default league")
        {
            _fileName = fileName;
            string path = Path.Combine(streamingAssetsRoot ?? string.Empty, FolderName, fileName);
            _readText = () => File.ReadAllText(path, System.Text.Encoding.UTF8);
            DisplayName = displayName;
        }

        /// <summary>Uses an injected reader (e.g. a UnityWebRequest wrapper on Android, or a test string).</summary>
        public BundledRosterSource(Func<string> readText, string fileName = DefaultFileName,
            string displayName = "Default league")
        {
            _readText = readText ?? throw new ArgumentNullException(nameof(readText));
            _fileName = fileName;
            DisplayName = displayName;
        }

        public RosterLoadResult Load()
        {
            string text;
            try
            {
                text = _readText();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return RosterLoadResult.Failure(_fileName, "", "bundled roster could not be read: " + ex.Message);
            }
            return RosterLoader.Load(text, _fileName);
        }
    }
}
