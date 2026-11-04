using System;
using System.IO;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    /// <summary>File locations and fixtures for roster/commentary tests. No UnityEngine, so the same tests run
    /// in the Unity Test Runner (cwd = project folder) and in a plain console harness.</summary>
    internal static class RosterTestData
    {
        public static string StreamingAssets => Path.Combine(ProjectRoot, "Assets", "StreamingAssets");
        public static string BundledRosterPath => Path.Combine(StreamingAssets, "Rosters", BundledRosterSource.DefaultFileName);
        public static string PremierLeaguePath =>
            Path.Combine(StreamingAssets, "Rosters", BundledRosterSource.PacksFolderName, "premier_league.roster.json");
        public static string LinesPath => Path.Combine(StreamingAssets, "Commentary", "lines.en.json");
        public static string ExampleImportPath =>
            Path.Combine(ProjectRoot, "..", "tools", "rosters", "import_template", "examples", "example_import.roster.json");

        /// <summary>The Unity project folder (the one containing Assets/StreamingAssets).</summary>
        public static string ProjectRoot
        {
            get
            {
                string dir = Directory.GetCurrentDirectory();
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    if (Directory.Exists(Path.Combine(dir, "Assets", "StreamingAssets"))) return dir;
                    string nested = Path.Combine(dir, "OpenFifa");
                    if (Directory.Exists(Path.Combine(nested, "Assets", "StreamingAssets"))) return nested;
                    dir = Path.GetDirectoryName(dir);
                }
                throw new DirectoryNotFoundException("Assets/StreamingAssets not found above " + Directory.GetCurrentDirectory());
            }
        }

        public static string BundledText() => File.ReadAllText(BundledRosterPath);

        /// <summary>A valid two-club league (the first two bundled clubs, 25 players each) as a mutable tree.</summary>
        public static JsonNode SmallLeague()
        {
            var root = JsonParser.Parse(BundledText());
            var clubs = root.Get("clubs");
            var two = JsonNode.NewArray();
            two.Add(clubs[0]);
            two.Add(clubs[1]);
            root.Set("clubs", two);
            return root;
        }

        public static JsonNode FirstPlayer(JsonNode root) => root.Get("clubs")[0].Get("players")[0];

        public static RosterLoadResult Load(JsonNode root) => RosterLoader.Load(JsonWriter.Serialize(root), "test");

        public static int[] Uniform(int value)
        {
            var a = new int[RosterSchema.AttributeCount];
            for (int i = 0; i < a.Length; i++) a[i] = value;
            return a;
        }

        public static bool HasError(RosterLoadResult r, string pathFragment, string messageFragment)
        {
            foreach (var i in r.Issues)
                if (i.IsError && i.Path.IndexOf(pathFragment, StringComparison.Ordinal) >= 0 &&
                    i.Message.IndexOf(messageFragment, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }
    }
}
