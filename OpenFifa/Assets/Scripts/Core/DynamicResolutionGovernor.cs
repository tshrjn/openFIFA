using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Adaptive render-height governor for a large display. It moves between quantized tiers
    /// (<see cref="ExternalDisplayPolicy.RenderTiers"/>): one tier down after a window of frames over
    /// budget, one tier up only after several consecutive windows with clear headroom. Normally it never
    /// goes below the first regular tier (1440p); at Serious thermal it may use the emergency tier (1080p)
    /// and is capped at the floor tier, and at Critical it is held at the lowest tier.
    /// </summary>
    public sealed class DynamicResolutionGovernor
    {
        private readonly ExternalDisplayConfig _config;
        private readonly int[] _tiers;
        private readonly int _floorIndex;
        private readonly float _budget;
        private DeviceThermalLevel _thermal = DeviceThermalLevel.Nominal;
        private int _index;
        private float _sum;
        private int _count;
        private int _headroomWindows;

        public DynamicResolutionGovernor(ExternalDisplayConfig config, int nativeHeight, int frameRate)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tiers = ExternalDisplayPolicy.RenderTiers(config, nativeHeight);
            int emergency = Math.Min(config.EmergencyHeight, nativeHeight);
            _floorIndex = _tiers.Length > 1 && _tiers[0] == emergency && emergency < FirstRegularTier(config, nativeHeight) ? 1 : 0;
            _budget = 1f / Math.Max(1, frameRate);
            _index = _floorIndex;
        }

        /// <summary>Available render heights, ascending.</summary>
        public int[] Tiers => (int[])_tiers.Clone();

        /// <summary>Current render height.</summary>
        public int RenderHeight => _tiers[_index];

        /// <summary>Lowest regular (non-emergency) render height.</summary>
        public int FloorHeight => _tiers[_floorIndex];

        /// <summary>Feeds the device thermal state. Returns true when <see cref="RenderHeight"/> changed.</summary>
        public bool SetThermal(DeviceThermalLevel thermal)
        {
            _thermal = thermal;
            int clamped = Clamp(_index);
            if (thermal >= DeviceThermalLevel.Critical) clamped = 0;
            return Move(clamped);
        }

        /// <summary>
        /// Record one frame's work time in seconds. Returns true when <see cref="RenderHeight"/> changed.
        /// Frames longer than 0.25 s (loading, app switch) are ignored.
        /// </summary>
        public bool AddFrame(float seconds)
        {
            if (seconds <= 0f || seconds > 0.25f) return false;

            _sum += seconds;
            _count++;
            if (_count < Math.Max(1, _config.SampleFrames)) return false;

            float avg = _sum / _count;
            _sum = 0f;
            _count = 0;

            if (avg > _budget * _config.DownscaleRatio)
            {
                _headroomWindows = 0;
                return Move(Clamp(_index - 1));
            }
            if (avg < _budget * _config.UpscaleRatio)
            {
                if (++_headroomWindows < Math.Max(1, _config.UpscaleWindows)) return false;
                _headroomWindows = 0;
                return Move(Clamp(_index + 1));
            }
            _headroomWindows = 0;
            return false;
        }

        private int Clamp(int index)
        {
            int min = ExternalDisplayPolicy.AllowsEmergency(_thermal) ? 0 : _floorIndex;
            int max = _thermal >= DeviceThermalLevel.Critical ? 0
                : _thermal >= DeviceThermalLevel.Serious ? _floorIndex
                : _tiers.Length - 1;
            if (index < min) index = min;
            if (index > max) index = max;
            return index;
        }

        private bool Move(int index)
        {
            if (index == _index) return false;
            _index = index;
            _sum = 0f;
            _count = 0;
            return true;
        }

        private static int FirstRegularTier(ExternalDisplayConfig config, int nativeHeight)
        {
            int min = int.MaxValue;
            foreach (int h in config.TierHeights)
                if (h < min) min = h;
            return Math.Min(min, nativeHeight);
        }
    }
}
