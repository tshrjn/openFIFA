using System;
using System.Collections.Generic;
using System.IO;

namespace OpenFifa.Core
{
    /// <summary>
    /// A user-supplied roster in the device's Documents folder (iPad: Files app &gt; On My iPad &gt; OpenFifa),
    /// in the spirit of PES option files. OpenFifa never ships, downloads or links to these files; it only
    /// reads what the user put there. Files must end in <see cref="Extension"/>.
    /// </summary>
    public sealed class LocalFileRosterSource : IRosterSource
    {
        public const string Extension = ".roster.json";
        public const long MaxBytes = 8L * 1024 * 1024;

        public string FilePath { get; }
        public string SourceId => "local:" + Path.GetFileName(FilePath);
        public string DisplayName { get; }
        public RosterSourceKind Kind => RosterSourceKind.LocalFile;

        public LocalFileRosterSource(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            string name = Path.GetFileName(filePath);
            DisplayName = name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - Extension.Length)
                : name;
        }

        /// <summary>All *.roster.json files directly in <paramref name="documentsDir"/>, sorted by file name (ordinal).</summary>
        public static List<LocalFileRosterSource> Discover(string documentsDir)
        {
            var result = new List<LocalFileRosterSource>();
            if (string.IsNullOrEmpty(documentsDir) || !Directory.Exists(documentsDir)) return result;
            var files = new List<string>(Directory.GetFiles(documentsDir, "*" + Extension, SearchOption.TopDirectoryOnly));
            files.Sort(StringComparer.Ordinal);
            foreach (var f in files) result.Add(new LocalFileRosterSource(f));
            return result;
        }

        public RosterLoadResult Load()
        {
            string name = Path.GetFileName(FilePath);
            try
            {
                var info = new FileInfo(FilePath);
                if (!info.Exists) return RosterLoadResult.Failure(name, "", "file not found: " + FilePath);
                if (info.Length > MaxBytes)
                    return RosterLoadResult.Failure(name, "", "file is " + info.Length + " bytes; the limit is " + MaxBytes);
                return RosterLoader.Load(File.ReadAllText(FilePath, System.Text.Encoding.UTF8), name);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return RosterLoadResult.Failure(name, "", "could not read the file: " + ex.Message);
            }
        }
    }
}
