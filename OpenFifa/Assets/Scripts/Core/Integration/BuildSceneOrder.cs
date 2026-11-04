using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Scene list for player builds (iPad and desktop). The build boots into the 11v11 flow: the single-player match
    /// first (scene index 0 is what the player opens), then the local-multiplayer match, then every other enabled
    /// scene from EditorBuildSettings in its own order (e.g. the legacy Match.unity). The committed
    /// EditorBuildSettings.asset is not edited: the build scripts apply this order at build time.
    /// </summary>
    public static class BuildSceneOrder
    {
        public const string SinglePlayerScene = "Assets/Scenes/SinglePlayerMatch.unity";
        public const string LocalMultiplayerScene = "Assets/Scenes/LocalMultiplayerMatch.unity";

        /// <summary>Scene name for SceneManager.LoadScene (the file name without folder or extension).</summary>
        public static string SceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            int slash = Math.Max(scenePath.LastIndexOf('/'), scenePath.LastIndexOf('\\'));
            string file = scenePath.Substring(slash + 1);
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        /// <summary>The local-multiplayer scene's name, loaded from the single-player picker.</summary>
        public static readonly string LocalMultiplayerSceneName = SceneName(LocalMultiplayerScene);

        /// <summary>The scenes that lead a build, in order.</summary>
        public static readonly string[] Leading = { SinglePlayerScene, LocalMultiplayerScene };

        /// <summary>
        /// Leading scenes that exist (<paramref name="exists"/>, null = assume all do), then the enabled settings scenes
        /// without duplicates, in their original order.
        /// </summary>
        public static string[] For(IEnumerable<string> enabledSettingsScenes, Func<string, bool> exists = null)
        {
            var result = new List<string>();
            foreach (var s in Leading)
                if (exists == null || exists(s)) result.Add(s);
            if (enabledSettingsScenes != null)
                foreach (var s in enabledSettingsScenes)
                    if (!string.IsNullOrEmpty(s) && !result.Contains(s)) result.Add(s);
            return result.ToArray();
        }
    }
}
