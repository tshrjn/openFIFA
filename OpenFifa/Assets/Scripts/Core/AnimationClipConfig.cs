using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration mapping animation state names to Mixamo clip names.
    /// Defines loop settings, root motion, and retarget source for each clip.
    /// </summary>
    public class AnimationClipConfig
    {
        /// <summary>Avatar source for retargeting Mixamo clips to Quaternius model.</summary>
        public string RetargetSourceAvatar = "Quaternius_Humanoid";

        private struct ClipEntry
        {
            public string ClipName;
            public bool Loop;
            public bool RootMotion;
        }

        private readonly Dictionary<string, ClipEntry> _clips;

        public AnimationClipConfig()
        {
            _clips = new Dictionary<string, ClipEntry>
            {
                ["Idle"] = new ClipEntry { ClipName = "Soccer_Idle", Loop = true, RootMotion = false },
                ["Run"] = new ClipEntry { ClipName = "Running", Loop = true, RootMotion = false },
                ["Sprint"] = new ClipEntry { ClipName = "Sprinting", Loop = true, RootMotion = false },
                ["Kick"] = new ClipEntry { ClipName = "Soccer_Kick", Loop = false, RootMotion = false },
                ["Tackle"] = new ClipEntry { ClipName = "Slide_Tackle", Loop = false, RootMotion = false },
                ["GKDive"] = new ClipEntry { ClipName = "Goalkeeper_Dive", Loop = false, RootMotion = false },
                ["Celebrate"] = new ClipEntry { ClipName = "Victory_Dance", Loop = true, RootMotion = false }
            };
        }

        /// <summary>
        /// Returns the Mixamo clip name for the given animation state.
        /// Returns null if state is not mapped.
        /// </summary>
        public string GetClipName(string stateName)
        {
            if (_clips.TryGetValue(stateName, out var entry))
                return entry.ClipName;
            return null;
        }

        /// <summary>
        /// Returns whether the clip for the given state should loop.
        /// Locomotion clips loop; action clips do not.
        /// </summary>
        public bool ShouldLoop(string stateName)
        {
            if (_clips.TryGetValue(stateName, out var entry))
                return entry.Loop;
            return false;
        }

        /// <summary>
        /// Returns whether root motion should be enabled for the given state.
        /// Disabled for locomotion (script-driven), may be enabled for specific actions.
        /// </summary>
        public bool UseRootMotion(string stateName)
        {
            if (_clips.TryGetValue(stateName, out var entry))
                return entry.RootMotion;
            return false;
        }

        /// <summary>
        /// Returns all mapped state names.
        /// </summary>
        public IEnumerable<string> GetAllStates()
        {
            return _clips.Keys;
        }
    }
}
