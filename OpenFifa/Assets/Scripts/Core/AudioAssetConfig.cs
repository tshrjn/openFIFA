using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration for real audio assets: clip names, compression settings,
    /// and import settings for Freesound.org CC0 audio files.
    /// </summary>
    public class AudioAssetConfig
    {
        /// <summary>Whistle sound effect clip name.</summary>
        public string WhistleClipName = "Referee_Whistle";

        /// <summary>Kick impact sound variation clip names (minimum 3).</summary>
        public List<string> KickClipNames = new List<string>
        {
            "Kick_Impact_01",
            "Kick_Impact_02",
            "Kick_Impact_03"
        };

        /// <summary>Crowd ambient loop clip name.</summary>
        public string CrowdAmbientClipName = "Stadium_Ambience_Loop";

        /// <summary>Goal celebration cheer clip name.</summary>
        public string GoalCheerClipName = "Crowd_Goal_Cheer";

        /// <summary>Audio compression format for all clips.</summary>
        public string CompressionFormat = "Vorbis";

        /// <summary>Compression quality percentage.</summary>
        public int CompressionQuality = 70;

        /// <summary>Whether SFX clips are forced to mono.</summary>
        public bool ForceToMonoSFX = true;

        /// <summary>Load type for ambient loops (Streaming for long clips).</summary>
        public string AmbientLoadType = "Streaming";

        /// <summary>Load type for SFX (DecompressOnLoad for low latency).</summary>
        public string SFXLoadType = "DecompressOnLoad";
    }

    /// <summary>
    /// Selects kick sound variations with anti-repeat logic to avoid
    /// playing the same clip twice in a row.
    /// </summary>
    public class KickVariationSelector
    {
        private readonly int _variationCount;
        private int _lastIndex = -1;
        private uint _seed;

        public KickVariationSelector(int variationCount, uint seed = 12345)
        {
            _variationCount = variationCount > 0 ? variationCount : 1;
            _seed = seed;
        }

        /// <summary>
        /// Returns the next kick variation index, guaranteed to differ
        /// from the previous selection (if count > 1).
        /// </summary>
        public int GetNextIndex()
        {
            if (_variationCount <= 1) return 0;

            int next;
            do
            {
                // LCG pseudo-random
                _seed = _seed * 1664525u + 1013904223u;
                next = (int)((_seed >> 16) % (uint)_variationCount);
            }
            while (next == _lastIndex);

            _lastIndex = next;
            return next;
        }
    }
}
