using System;
using System.Collections.Generic;
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
        /// <summary>Sub-folder of Rosters/ holding swappable league packs (e.g. premier_league.roster.json).</summary>
        public const string PacksFolderName = "Packs";
        public const string PackExtension = ".roster.json";

        private readonly Func<string> _readText;
        private readonly string _fileName;

        public string SourceId => "bundled:" + (_fileName.EndsWith(PackExtension, StringComparison.Ordinal)
            ? _fileName.Substring(0, _fileName.Length - PackExtension.Length)
            : Path.GetFileNameWithoutExtension(_fileName));
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

        /// <summary>
        /// League packs shipped in &lt;streamingAssetsRoot&gt;/Rosters/Packs/*.roster.json, sorted by file name.
        /// Dropping a file in (or deleting it) swaps leagues without touching code. Display name is the file name
        /// without the extension; the league's own name comes from the file once loaded.
        /// </summary>
        public static List<BundledRosterSource> DiscoverPacks(string streamingAssetsRoot)
        {
            var result = new List<BundledRosterSource>();
            if (string.IsNullOrEmpty(streamingAssetsRoot)) return result;
            string dir = Path.Combine(streamingAssetsRoot, FolderName, PacksFolderName);
            if (!Directory.Exists(dir)) return result;
            var files = new List<string>(Directory.GetFiles(dir, "*" + PackExtension, SearchOption.TopDirectoryOnly));
            files.Sort(StringComparer.Ordinal);
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                string path = f;
                result.Add(new BundledRosterSource(() => File.ReadAllText(path, System.Text.Encoding.UTF8), name,
                    name.Substring(0, name.Length - PackExtension.Length)));
            }
            return result;
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
