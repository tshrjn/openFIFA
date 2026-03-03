using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration mapping animation state names to Mixamo clip names.
    /// Defines loop settings, root motion, and retarget source for each clip.
    /// Enhanced for US-045 with mocap metadata, blend config, events, and extended clip sets.
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
            public MocapClipType ClipType;
        }

        private readonly Dictionary<string, ClipEntry> _clips;
        private readonly Dictionary<string, MocapClipMetadata> _metadata;
        private readonly Dictionary<string, List<AnimationEventConfig>> _events;

        /// <summary>Blend configuration for animation transitions.</summary>
        public readonly AnimationBlendConfig BlendConfig;

        /// <summary>Retarget configuration for mocap data.</summary>
        public readonly RetargetConfig Retarget;

        /// <summary>Foot IK configuration for ground contact.</summary>
        public readonly FootIKConfig FootIK;

        /// <summary>Animation quality/compression settings.</summary>
        public readonly AnimationQualityConfig Quality;

        public AnimationClipConfig()
        {
            BlendConfig = new AnimationBlendConfig();
            Retarget = new RetargetConfig();
            FootIK = new FootIKConfig();
            Quality = new AnimationQualityConfig();
            _metadata = new Dictionary<string, MocapClipMetadata>(StringComparer.OrdinalIgnoreCase);
            _events = new Dictionary<string, List<AnimationEventConfig>>(StringComparer.OrdinalIgnoreCase);

            _clips = new Dictionary<string, ClipEntry>
            {
                // Core locomotion
                ["Idle"] = new ClipEntry { ClipName = "Soccer_Idle", Loop = true, RootMotion = false, ClipType = MocapClipType.Idle },
                ["Run"] = new ClipEntry { ClipName = "Running", Loop = true, RootMotion = false, ClipType = MocapClipType.Locomotion },
                ["Sprint"] = new ClipEntry { ClipName = "Sprinting", Loop = true, RootMotion = false, ClipType = MocapClipType.Locomotion },

                // Core actions
                ["Kick"] = new ClipEntry { ClipName = "Soccer_Kick", Loop = false, RootMotion = false, ClipType = MocapClipType.Kick },
                ["Tackle"] = new ClipEntry { ClipName = "Slide_Tackle", Loop = false, RootMotion = false, ClipType = MocapClipType.Tackle },
                ["GKDive"] = new ClipEntry { ClipName = "Goalkeeper_Dive", Loop = false, RootMotion = false, ClipType = MocapClipType.GoalkeeperAction },
                ["Celebrate"] = new ClipEntry { ClipName = "Victory_Dance", Loop = true, RootMotion = false, ClipType = MocapClipType.Celebration },

                // Extended mocap clips (US-045)
                ["Dribble"] = new ClipEntry { ClipName = "Dribble_Run", Loop = true, RootMotion = false, ClipType = MocapClipType.Dribble },
                ["FirstTouch"] = new ClipEntry { ClipName = "First_Touch", Loop = false, RootMotion = false, ClipType = MocapClipType.FirstTouch },
                ["Header"] = new ClipEntry { ClipName = "Header_Jump", Loop = false, RootMotion = false, ClipType = MocapClipType.Header },
                ["BicycleKick"] = new ClipEntry { ClipName = "Bicycle_Kick", Loop = false, RootMotion = false, ClipType = MocapClipType.BicycleKick },

                // Goalkeeper extended
                ["GKCatch"] = new ClipEntry { ClipName = "GK_Catch", Loop = false, RootMotion = false, ClipType = MocapClipType.GoalkeeperAction },
                ["GKPunch"] = new ClipEntry { ClipName = "GK_Punch", Loop = false, RootMotion = false, ClipType = MocapClipType.GoalkeeperAction },
                ["GKDistribute"] = new ClipEntry { ClipName = "GK_Distribute", Loop = false, RootMotion = false, ClipType = MocapClipType.GoalkeeperAction },

                // Celebration variants (5+ unique)
                ["Celebrate_Slide"] = new ClipEntry { ClipName = "Celebration_KneeSlide", Loop = false, RootMotion = false, ClipType = MocapClipType.Celebration },
                ["Celebrate_Flip"] = new ClipEntry { ClipName = "Celebration_Backflip", Loop = false, RootMotion = false, ClipType = MocapClipType.Celebration },
                ["Celebrate_Arms"] = new ClipEntry { ClipName = "Celebration_ArmsWide", Loop = false, RootMotion = false, ClipType = MocapClipType.Celebration },
                ["Celebrate_Dance"] = new ClipEntry { ClipName = "Celebration_Dance", Loop = true, RootMotion = false, ClipType = MocapClipType.Celebration },
                ["Celebrate_Team"] = new ClipEntry { ClipName = "Celebration_TeamHuddle", Loop = false, RootMotion = false, ClipType = MocapClipType.Celebration }
            };

            SetupDefaultMetadata();
            SetupDefaultEvents();
        }

        private void SetupDefaultMetadata()
        {
            _metadata["Idle"] = new MocapClipMetadata("Soccer_Idle", 30f, 90, "Mixamo", MocapClipType.Idle);
            _metadata["Run"] = new MocapClipMetadata("Running", 30f, 30, "Mixamo", MocapClipType.Locomotion);
            _metadata["Sprint"] = new MocapClipMetadata("Sprinting", 30f, 28, "Mixamo", MocapClipType.Locomotion);
            _metadata["Kick"] = new MocapClipMetadata("Soccer_Kick", 30f, 18, "Mixamo", MocapClipType.Kick);
            _metadata["Tackle"] = new MocapClipMetadata("Slide_Tackle", 30f, 30, "Mixamo", MocapClipType.Tackle);
            _metadata["GKDive"] = new MocapClipMetadata("Goalkeeper_Dive", 30f, 36, "Mixamo", MocapClipType.GoalkeeperAction);
            _metadata["Celebrate"] = new MocapClipMetadata("Victory_Dance", 30f, 120, "Mixamo", MocapClipType.Celebration);
            _metadata["Dribble"] = new MocapClipMetadata("Dribble_Run", 30f, 30, "Mixamo", MocapClipType.Dribble);
            _metadata["FirstTouch"] = new MocapClipMetadata("First_Touch", 30f, 18, "Mixamo", MocapClipType.FirstTouch);
            _metadata["Header"] = new MocapClipMetadata("Header_Jump", 30f, 24, "Mixamo", MocapClipType.Header);
            _metadata["BicycleKick"] = new MocapClipMetadata("Bicycle_Kick", 30f, 30, "Mixamo", MocapClipType.BicycleKick);
        }

        private void SetupDefaultEvents()
        {
            // Kick contact frame
            _events["Kick"] = new List<AnimationEventConfig>
            {
                new AnimationEventConfig("OnKickContact", 0.45f, "float", "1.0"),
                new AnimationEventConfig("OnKickFollowThrough", 0.7f, "none")
            };

            // Tackle impact frame
            _events["Tackle"] = new List<AnimationEventConfig>
            {
                new AnimationEventConfig("OnTackleImpact", 0.35f, "float", "0.8")
            };

            // Header contact
            _events["Header"] = new List<AnimationEventConfig>
            {
                new AnimationEventConfig("OnHeaderContact", 0.5f, "float", "1.0")
            };

            // Foot plant events for locomotion (anti-foot-sliding)
            _events["Run"] = new List<AnimationEventConfig>
            {
                new AnimationEventConfig("OnLeftFootPlant", 0.0f, "none"),
                new AnimationEventConfig("OnRightFootPlant", 0.5f, "none")
            };

            _events["Sprint"] = new List<AnimationEventConfig>
            {
                new AnimationEventConfig("OnLeftFootPlant", 0.0f, "none"),
                new AnimationEventConfig("OnRightFootPlant", 0.5f, "none")
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

        /// <summary>
        /// Returns the clip type classification for the given state.
        /// </summary>
        public MocapClipType GetClipType(string stateName)
        {
            if (_clips.TryGetValue(stateName, out var entry))
                return entry.ClipType;
            return MocapClipType.Idle;
        }

        /// <summary>
        /// Returns mocap metadata for the given state, or null if not available.
        /// </summary>
        public MocapClipMetadata GetMetadata(string stateName)
        {
            MocapClipMetadata meta;
            return _metadata.TryGetValue(stateName, out meta) ? meta : null;
        }

        /// <summary>
        /// Returns animation events for the given state, or empty list if none defined.
        /// </summary>
        public List<AnimationEventConfig> GetEvents(string stateName)
        {
            List<AnimationEventConfig> events;
            return _events.TryGetValue(stateName, out events) ? events : new List<AnimationEventConfig>();
        }

        /// <summary>
        /// Returns all celebration state names (for the 5+ unique celebrations).
        /// </summary>
        public List<string> GetCelebrationStates()
        {
            return _clips.Where(kvp => kvp.Value.ClipType == MocapClipType.Celebration)
                         .Select(kvp => kvp.Key)
                         .ToList();
        }

        /// <summary>
        /// Returns all goalkeeper-specific state names.
        /// </summary>
        public List<string> GetGoalkeeperStates()
        {
            return _clips.Where(kvp => kvp.Value.ClipType == MocapClipType.GoalkeeperAction)
                         .Select(kvp => kvp.Key)
                         .ToList();
        }

        /// <summary>
        /// Returns all states of a given clip type.
        /// </summary>
        public List<string> GetStatesByType(MocapClipType clipType)
        {
            return _clips.Where(kvp => kvp.Value.ClipType == clipType)
                         .Select(kvp => kvp.Key)
                         .ToList();
        }

        /// <summary>
        /// Validates that blend config is consistent with the clip definitions.
        /// </summary>
        public bool IsBlendConfigValid()
        {
            return BlendConfig != null && BlendConfig.IsValid();
        }

        /// <summary>
        /// Validates that retarget config is properly set up.
        /// </summary>
        public bool IsRetargetValid()
        {
            return Retarget != null && Retarget.IsValid();
        }

        /// <summary>
        /// Validates that compression settings are within acceptable bounds.
        /// </summary>
        public bool IsCompressionValid()
        {
            return Quality != null && Quality.IsValid();
        }

        /// <summary>
        /// Returns the total number of configured clips.
        /// </summary>
        public int ClipCount => _clips.Count;
    }
}
