using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Defines the performance budget thresholds for the game.
    /// All values are configurable constants for easy adjustment.
    /// </summary>
    public class PerformanceBudget
    {
        public float MinAverageFPS = 28f;
        public float MaxFrameTimeMs = 100f;
        public int MaxBatches = 99;
        public int MaxGCAllocBytes = 0;
        public float Max95thPercentileMs = 32f;
        public float MeasurementWindowSeconds = 5f;

        /// <summary>
        /// Checks if a measured average FPS meets the budget.
        /// </summary>
        public bool MeetsAverageFPS(float measuredFPS)
        {
            return measuredFPS >= MinAverageFPS;
        }

        /// <summary>
        /// Checks if the worst-case frame time is within budget.
        /// </summary>
        public bool MeetsMaxFrameTime(float worstFrameMs)
        {
            return worstFrameMs <= MaxFrameTimeMs;
        }

        /// <summary>
        /// Checks if measured batch count is within budget.
        /// </summary>
        public bool MeetsBatchBudget(int batchCount)
        {
            return batchCount <= MaxBatches;
        }

        /// <summary>
        /// Checks if GC allocations are within budget.
        /// </summary>
        public bool MeetsGCBudget(int gcAllocBytes)
        {
            return gcAllocBytes <= MaxGCAllocBytes;
        }
    }

    /// <summary>
    /// Analyzes frame time samples to compute statistics (average, percentiles).
    /// Uses a pre-allocated list to avoid GC during measurement.
    /// </summary>
    public class FrameTimeAnalyzer
    {
        private readonly List<float> _samples;

        public FrameTimeAnalyzer(int preAllocCapacity = 300)
        {
            _samples = new List<float>(preAllocCapacity);
        }

        public int SampleCount => _samples.Count;

        /// <summary>
        /// Records a frame time sample in milliseconds.
        /// </summary>
        public void AddSample(float frameTimeMs)
        {
            _samples.Add(frameTimeMs);
        }

        /// <summary>
        /// Clears all recorded samples.
        /// </summary>
        public void Clear()
        {
            _samples.Clear();
        }

        /// <summary>
        /// Returns the arithmetic mean of all recorded frame times.
        /// </summary>
        public float GetAverageMs()
        {
            if (_samples.Count == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < _samples.Count; i++)
            {
                sum += _samples[i];
            }
            return sum / _samples.Count;
        }

        /// <summary>
        /// Returns the maximum frame time recorded.
        /// </summary>
        public float GetMaxMs()
        {
            if (_samples.Count == 0) return 0f;

            float max = _samples[0];
            for (int i = 1; i < _samples.Count; i++)
            {
                if (_samples[i] > max) max = _samples[i];
            }
            return max;
        }

        /// <summary>
        /// Returns the frame time at the given percentile (0-100).
        /// Uses nearest-rank method.
        /// </summary>
        public float GetPercentileMs(int percentile)
        {
            if (_samples.Count == 0) return 0f;
            if (percentile < 0) percentile = 0;
            if (percentile > 100) percentile = 100;

            // Copy and sort to avoid mutating the original sample order
            var sorted = new List<float>(_samples);
            sorted.Sort();

            // Nearest-rank method: index = ceil(percentile/100 * N) - 1
            int index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;

            return sorted[index];
        }

        /// <summary>
        /// Returns the average FPS computed from average frame time.
        /// </summary>
        public float GetAverageFPS()
        {
            float avgMs = GetAverageMs();
            if (avgMs <= 0f) return 0f;
            return 1000f / avgMs;
        }

        /// <summary>
        /// Returns the percentage of frames that exceed the given threshold.
        /// </summary>
        public float GetPercentageAboveMs(float thresholdMs)
        {
            if (_samples.Count == 0) return 0f;

            int count = 0;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i] > thresholdMs) count++;
            }
            return (float)count / _samples.Count * 100f;
        }
    }
}
