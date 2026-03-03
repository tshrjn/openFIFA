using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFifa.Core
{
    /// <summary>
    /// Camera checkpoint definition for visual regression baselines.
    /// </summary>
    public class CameraCheckpoint
    {
        public string Name;
        public float PosX, PosY, PosZ;
        public float RotX, RotY, RotZ;

        public CameraCheckpoint(string name, float px, float py, float pz, float rx, float ry, float rz)
        {
            Name = name;
            PosX = px; PosY = py; PosZ = pz;
            RotX = rx; RotY = ry; RotZ = rz;
        }
    }

    /// <summary>
    /// Configuration for visual regression testing: resolution, thresholds,
    /// camera checkpoints, and baseline storage path.
    /// </summary>
    public class VisualRegressionConfig
    {
        /// <summary>Fixed capture width for consistency.</summary>
        public int CaptureWidth = 1920;

        /// <summary>Fixed capture height for consistency.</summary>
        public int CaptureHeight = 1080;

        /// <summary>Maximum pixel difference percentage for test to pass.</summary>
        public float MaxDiffPercentage = 2f;

        /// <summary>Per-channel difference threshold to account for anti-aliasing.</summary>
        public int PerChannelDiffThreshold = 10;

        /// <summary>Path for baseline screenshot storage (relative to Assets).</summary>
        public string BaselinePath = "Tests/Baselines";

        /// <summary>Camera checkpoints for golden screenshot capture.</summary>
        public List<CameraCheckpoint> CameraCheckpoints = new List<CameraCheckpoint>
        {
            new CameraCheckpoint("Kickoff", 0f, 20f, 0f, 60f, 0f, 0f),
            new CameraCheckpoint("GoalCloseup", 0f, 3f, 12f, 10f, 180f, 0f),
            new CameraCheckpoint("CornerFlag", 24f, 2f, 14f, 15f, -135f, 0f)
        };
    }

    /// <summary>
    /// Compares pixel data between two images using per-channel thresholds
    /// to account for minor anti-aliasing differences.
    /// Operates on raw RGBA byte arrays.
    /// </summary>
    public class PixelComparer
    {
        private readonly int _perChannelThreshold;

        public PixelComparer(int perChannelThreshold)
        {
            _perChannelThreshold = perChannelThreshold;
        }

        /// <summary>
        /// Computes the percentage of pixels that differ beyond the per-channel threshold.
        /// Returns 100 if image sizes don't match.
        /// Input arrays are RGBA: 4 bytes per pixel.
        /// </summary>
        public float ComputeDiffPercentage(byte[] imageA, byte[] imageB)
        {
            if (imageA.Length != imageB.Length) return 100f;
            if (imageA.Length == 0) return 0f;

            int totalPixels = imageA.Length / 4;
            int diffPixels = 0;

            for (int i = 0; i < imageA.Length; i += 4)
            {
                bool pixelDiffers = false;
                // Check R, G, B channels (skip Alpha at i+3)
                for (int c = 0; c < 3; c++)
                {
                    int diff = Math.Abs((int)imageA[i + c] - (int)imageB[i + c]);
                    if (diff > _perChannelThreshold)
                    {
                        pixelDiffers = true;
                        break;
                    }
                }
                if (pixelDiffers) diffPixels++;
            }

            return (float)diffPixels / totalPixels * 100f;
        }
    }

    // ===== New types for US-049 =====

    /// <summary>
    /// Algorithm used for comparing screenshots.
    /// </summary>
    public enum ComparisonAlgorithm
    {
        PixelDiff,
        PerceptualHash,
        SSIM,
        HistogramDiff
    }

    /// <summary>
    /// Specification for a single screenshot capture: camera angle, resolution, scene, and objects.
    /// </summary>
    public class ScreenshotSpec
    {
        public readonly string Name;
        public readonly CameraCheckpoint CameraAngle;
        public readonly int ResolutionWidth;
        public readonly int ResolutionHeight;
        public readonly string SceneName;
        public readonly List<string> ObjectsToInclude;

        public ScreenshotSpec(
            string name,
            CameraCheckpoint cameraAngle,
            int resolutionWidth,
            int resolutionHeight,
            string sceneName,
            List<string> objectsToInclude = null)
        {
            Name = name;
            CameraAngle = cameraAngle;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
            SceneName = sceneName;
            ObjectsToInclude = objectsToInclude ?? new List<string>();
        }
    }

    /// <summary>
    /// A versioned set of baseline screenshots with metadata.
    /// </summary>
    public class BaselineSet
    {
        public readonly string Version;
        public readonly long Timestamp;
        public readonly List<ScreenshotSpec> Specs;
        public readonly string Platform;
        public readonly string RenderPipeline;

        public BaselineSet(
            string version,
            long timestamp,
            List<ScreenshotSpec> specs,
            string platform,
            string renderPipeline)
        {
            Version = version;
            Timestamp = timestamp;
            Specs = specs ?? new List<ScreenshotSpec>();
            Platform = platform;
            RenderPipeline = renderPipeline;
        }
    }

    /// <summary>
    /// Result of comparing a current screenshot against a baseline.
    /// </summary>
    public class ComparisonResult
    {
        public readonly bool Passed;
        public readonly float Similarity;
        public readonly int DiffPixelCount;
        public readonly float DiffPercentage;
        public readonly List<IgnoreRegion> RegionHotspots;

        public ComparisonResult(
            bool passed,
            float similarity,
            int diffPixelCount,
            float diffPercentage,
            List<IgnoreRegion> regionHotspots = null)
        {
            Passed = passed;
            Similarity = similarity;
            DiffPixelCount = diffPixelCount;
            DiffPercentage = diffPercentage;
            RegionHotspots = regionHotspots ?? new List<IgnoreRegion>();
        }
    }

    /// <summary>
    /// Per-algorithm thresholds for comparison pass/fail.
    /// </summary>
    public class ComparisonThresholds
    {
        public readonly float PassThreshold;
        public readonly float MaxDiffPercentage;
        public readonly List<IgnoreRegion> IgnoreRegions;

        public ComparisonThresholds(
            float passThreshold,
            float maxDiffPercentage,
            List<IgnoreRegion> ignoreRegions = null)
        {
            PassThreshold = passThreshold;
            MaxDiffPercentage = maxDiffPercentage;
            IgnoreRegions = ignoreRegions ?? new List<IgnoreRegion>();
        }
    }

    /// <summary>
    /// Rectangular region to ignore during comparison (e.g., dynamic UI elements).
    /// Coordinates are in pixel space.
    /// </summary>
    public class IgnoreRegion
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public IgnoreRegion(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Checks whether this region overlaps with another region.
        /// </summary>
        public bool Overlaps(IgnoreRegion other)
        {
            if (other == null) return false;
            return X < other.X + other.Width
                && X + Width > other.X
                && Y < other.Y + other.Height
                && Y + Height > other.Y;
        }
    }

    /// <summary>
    /// Aggregated report of a visual regression test run.
    /// </summary>
    public class RegressionReport
    {
        public readonly long Timestamp;
        public readonly int TotalComparisons;
        public readonly int PassedCount;
        public readonly int FailedCount;
        public readonly List<PerScreenshotResult> Results;

        public RegressionReport(
            long timestamp,
            List<PerScreenshotResult> results)
        {
            Timestamp = timestamp;
            Results = results ?? new List<PerScreenshotResult>();
            TotalComparisons = Results.Count;
            PassedCount = Results.Count(r => r.Result.Passed);
            FailedCount = Results.Count(r => !r.Result.Passed);
        }

        /// <summary>
        /// Returns the pass rate as a value between 0.0 and 1.0.
        /// Returns 1.0 if there are no comparisons.
        /// </summary>
        public float GetPassRate()
        {
            if (TotalComparisons == 0) return 1f;
            return (float)PassedCount / TotalComparisons;
        }
    }

    /// <summary>
    /// Individual result entry for a single screenshot comparison.
    /// </summary>
    public class PerScreenshotResult
    {
        public readonly string ScreenshotName;
        public readonly ComparisonResult Result;

        public PerScreenshotResult(string screenshotName, ComparisonResult result)
        {
            ScreenshotName = screenshotName;
            Result = result;
        }
    }

    /// <summary>
    /// Validation utilities for visual regression configuration objects.
    /// </summary>
    public static class VisualRegressionValidator
    {
        /// <summary>
        /// Validates that a ScreenshotSpec has all required fields populated.
        /// </summary>
        public static bool IsSpecValid(ScreenshotSpec spec)
        {
            if (spec == null) return false;
            if (string.IsNullOrEmpty(spec.Name)) return false;
            if (spec.CameraAngle == null) return false;
            if (string.IsNullOrEmpty(spec.CameraAngle.Name)) return false;
            if (spec.ResolutionWidth <= 0) return false;
            if (spec.ResolutionHeight <= 0) return false;
            if (string.IsNullOrEmpty(spec.SceneName)) return false;
            return true;
        }

        /// <summary>
        /// Validates that a BaselineSet has all required specs and metadata.
        /// </summary>
        public static bool IsBaselineSetComplete(BaselineSet set)
        {
            if (set == null) return false;
            if (string.IsNullOrEmpty(set.Version)) return false;
            if (set.Timestamp <= 0) return false;
            if (set.Specs == null || set.Specs.Count == 0) return false;
            if (string.IsNullOrEmpty(set.Platform)) return false;
            if (string.IsNullOrEmpty(set.RenderPipeline)) return false;

            foreach (var spec in set.Specs)
            {
                if (!IsSpecValid(spec)) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks that comparison thresholds are within reasonable ranges.
        /// PassThreshold should be in [0, 1], MaxDiffPercentage in [0, 100].
        /// </summary>
        public static bool IsThresholdReasonable(ComparisonThresholds thresholds)
        {
            if (thresholds == null) return false;
            if (thresholds.PassThreshold < 0f || thresholds.PassThreshold > 1f) return false;
            if (thresholds.MaxDiffPercentage < 0f || thresholds.MaxDiffPercentage > 100f) return false;
            return true;
        }
    }
}
