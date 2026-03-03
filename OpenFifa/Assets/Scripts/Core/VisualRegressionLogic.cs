using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# pixel-level comparison logic for visual regression testing.
    /// Operates on raw RGBA byte arrays (4 bytes per pixel).
    /// </summary>
    public class PixelComparisonLogic
    {
        private readonly int _perChannelThreshold;

        public PixelComparisonLogic(int perChannelThreshold = 10)
        {
            _perChannelThreshold = Math.Max(0, perChannelThreshold);
        }

        /// <summary>
        /// Compares two RGBA byte arrays pixel by pixel.
        /// Returns the count of pixels that differ beyond the per-channel threshold.
        /// </summary>
        public int ComputeDiffCount(byte[] imageA, byte[] imageB)
        {
            if (imageA == null || imageB == null) return -1;
            if (imageA.Length != imageB.Length) return -1;
            if (imageA.Length == 0) return 0;

            int diffCount = 0;
            for (int i = 0; i < imageA.Length; i += 4)
            {
                for (int c = 0; c < 3; c++)
                {
                    int diff = Math.Abs((int)imageA[i + c] - (int)imageB[i + c]);
                    if (diff > _perChannelThreshold)
                    {
                        diffCount++;
                        break;
                    }
                }
            }

            return diffCount;
        }

        /// <summary>
        /// Returns the percentage of differing pixels (0.0 to 100.0).
        /// Returns 100 on size mismatch, 0 on empty/null.
        /// </summary>
        public float ComputeDiffPercentage(byte[] imageA, byte[] imageB)
        {
            if (imageA == null || imageB == null) return 100f;
            if (imageA.Length != imageB.Length) return 100f;
            if (imageA.Length == 0) return 0f;

            int totalPixels = imageA.Length / 4;
            int diffCount = ComputeDiffCount(imageA, imageB);
            if (diffCount < 0) return 100f;

            return (float)diffCount / totalPixels * 100f;
        }

        /// <summary>
        /// Returns similarity as a value between 0.0 (completely different) and 1.0 (identical).
        /// </summary>
        public float ComputeSimilarity(byte[] imageA, byte[] imageB)
        {
            float diffPct = ComputeDiffPercentage(imageA, imageB);
            return 1f - (diffPct / 100f);
        }

        /// <summary>
        /// Generates a diff mask: for each pixel, 255 if different, 0 if same.
        /// Returns null on size mismatch. Output is single-channel (one byte per pixel).
        /// </summary>
        public byte[] GenerateDiffMask(byte[] imageA, byte[] imageB)
        {
            if (imageA == null || imageB == null) return null;
            if (imageA.Length != imageB.Length) return null;
            if (imageA.Length == 0) return new byte[0];

            int totalPixels = imageA.Length / 4;
            var mask = new byte[totalPixels];

            for (int i = 0; i < imageA.Length; i += 4)
            {
                int pixelIndex = i / 4;
                for (int c = 0; c < 3; c++)
                {
                    int diff = Math.Abs((int)imageA[i + c] - (int)imageB[i + c]);
                    if (diff > _perChannelThreshold)
                    {
                        mask[pixelIndex] = 255;
                        break;
                    }
                }
            }

            return mask;
        }
    }

    /// <summary>
    /// Perceptual hash (average hash) for fast image similarity comparison.
    /// Works on RGBA byte arrays. Downsamples to 8x8 grayscale, then computes
    /// a 64-bit hash based on whether each pixel is above/below average luminance.
    /// </summary>
    public class PerceptualHashLogic
    {
        /// <summary>
        /// Computes a 64-bit perceptual hash for an image.
        /// The image is logically assumed to be of the given width x height.
        /// Input is RGBA byte array (4 bytes per pixel).
        /// </summary>
        public long ComputeHash(byte[] rgba, int width, int height)
        {
            if (rgba == null || rgba.Length == 0) return 0;
            if (width <= 0 || height <= 0) return 0;
            if (rgba.Length != width * height * 4) return 0;

            // Downsample to 8x8 grayscale
            var gray8x8 = new float[64];
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    // Map 8x8 grid back to source coordinates
                    int srcX = (int)((col + 0.5f) * width / 8f);
                    int srcY = (int)((row + 0.5f) * height / 8f);
                    srcX = Math.Min(srcX, width - 1);
                    srcY = Math.Min(srcY, height - 1);

                    int pixelIndex = (srcY * width + srcX) * 4;
                    float r = rgba[pixelIndex];
                    float g = rgba[pixelIndex + 1];
                    float b = rgba[pixelIndex + 2];
                    // Standard luminance formula
                    gray8x8[row * 8 + col] = 0.299f * r + 0.587f * g + 0.114f * b;
                }
            }

            // Compute average luminance
            float avg = 0f;
            for (int i = 0; i < 64; i++)
                avg += gray8x8[i];
            avg /= 64f;

            // Build hash: bit = 1 if pixel luminance >= average
            long hash = 0;
            for (int i = 0; i < 64; i++)
            {
                if (gray8x8[i] >= avg)
                    hash |= (1L << i);
            }

            return hash;
        }

        /// <summary>
        /// Computes the Hamming distance between two perceptual hashes.
        /// Returns the number of differing bits (0 = identical, 64 = completely different).
        /// </summary>
        public int HammingDistance(long hashA, long hashB)
        {
            long xor = hashA ^ hashB;
            int distance = 0;
            while (xor != 0)
            {
                distance += (int)(xor & 1);
                xor >>= 1;
            }
            return distance;
        }

        /// <summary>
        /// Returns similarity between two hashes as a value from 0.0 to 1.0.
        /// </summary>
        public float ComputeSimilarity(long hashA, long hashB)
        {
            int distance = HammingDistance(hashA, hashB);
            return 1f - (distance / 64f);
        }
    }

    /// <summary>
    /// Compares color histograms of two images using chi-squared distance.
    /// Each channel (R, G, B) has 256 bins. Operates on RGBA byte arrays.
    /// </summary>
    public class HistogramComparer
    {
        /// <summary>
        /// Builds a histogram for one channel. Returns an array of 256 bin counts,
        /// normalized to [0, 1] by dividing by total pixel count.
        /// </summary>
        public float[] BuildChannelHistogram(byte[] rgba, int channelOffset)
        {
            if (rgba == null || rgba.Length == 0) return new float[256];

            var bins = new float[256];
            int totalPixels = rgba.Length / 4;
            if (totalPixels == 0) return bins;

            for (int i = channelOffset; i < rgba.Length; i += 4)
            {
                bins[rgba[i]]++;
            }

            // Normalize
            for (int i = 0; i < 256; i++)
                bins[i] /= totalPixels;

            return bins;
        }

        /// <summary>
        /// Computes chi-squared distance between two normalized histograms.
        /// Lower values mean more similar. Returns 0.0 for identical histograms.
        /// </summary>
        public float ChiSquaredDistance(float[] histA, float[] histB)
        {
            if (histA == null || histB == null) return float.MaxValue;
            if (histA.Length != histB.Length) return float.MaxValue;

            float chiSq = 0f;
            for (int i = 0; i < histA.Length; i++)
            {
                float sum = histA[i] + histB[i];
                if (sum > 0f)
                {
                    float diff = histA[i] - histB[i];
                    chiSq += (diff * diff) / sum;
                }
            }

            return chiSq;
        }

        /// <summary>
        /// Compares two images by computing chi-squared distance across R, G, B channels.
        /// Returns a similarity score in [0, 1] where 1 = identical histograms.
        /// </summary>
        public float ComputeSimilarity(byte[] imageA, byte[] imageB)
        {
            if (imageA == null || imageB == null) return 0f;
            if (imageA.Length == 0 && imageB.Length == 0) return 1f;
            if (imageA.Length == 0 || imageB.Length == 0) return 0f;

            float totalDistance = 0f;
            for (int channel = 0; channel < 3; channel++)
            {
                var histA = BuildChannelHistogram(imageA, channel);
                var histB = BuildChannelHistogram(imageB, channel);
                totalDistance += ChiSquaredDistance(histA, histB);
            }

            // Normalize: chi-squared max theoretical is 2.0 per channel (6.0 total)
            // but practical max is much lower. Use exponential decay for similarity.
            float similarity = (float)Math.Exp(-totalDistance);
            return Math.Max(0f, Math.Min(1f, similarity));
        }
    }

    /// <summary>
    /// Simplified Structural Similarity Index (SSIM) calculator.
    /// Operates on the luminance channel derived from RGBA byte arrays.
    /// Returns a value in [-1, 1] where 1 = identical.
    /// </summary>
    public class SSIMCalculator
    {
        // SSIM constants (standard values from the paper)
        private const float C1 = 6.5025f;   // (0.01 * 255)^2
        private const float C2 = 58.5225f;  // (0.03 * 255)^2

        /// <summary>
        /// Extracts luminance values from RGBA byte array using standard formula.
        /// Returns an array of float luminance values in [0, 255].
        /// </summary>
        public float[] ExtractLuminance(byte[] rgba)
        {
            if (rgba == null || rgba.Length == 0) return new float[0];

            int totalPixels = rgba.Length / 4;
            var luminance = new float[totalPixels];

            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                luminance[i] = 0.299f * rgba[offset]
                             + 0.587f * rgba[offset + 1]
                             + 0.114f * rgba[offset + 2];
            }

            return luminance;
        }

        /// <summary>
        /// Computes SSIM between two luminance arrays.
        /// Both arrays must be the same length.
        /// Returns 1.0 for identical images, lower for different images.
        /// </summary>
        public float ComputeSSIM(float[] lumA, float[] lumB)
        {
            if (lumA == null || lumB == null) return 0f;
            if (lumA.Length != lumB.Length) return 0f;
            if (lumA.Length == 0) return 1f;

            int n = lumA.Length;

            // Compute means
            float meanA = 0f, meanB = 0f;
            for (int i = 0; i < n; i++)
            {
                meanA += lumA[i];
                meanB += lumB[i];
            }
            meanA /= n;
            meanB /= n;

            // Compute variances and covariance
            float varA = 0f, varB = 0f, covar = 0f;
            for (int i = 0; i < n; i++)
            {
                float diffA = lumA[i] - meanA;
                float diffB = lumB[i] - meanB;
                varA += diffA * diffA;
                varB += diffB * diffB;
                covar += diffA * diffB;
            }
            varA /= n;
            varB /= n;
            covar /= n;

            // SSIM formula
            float numerator = (2f * meanA * meanB + C1) * (2f * covar + C2);
            float denominator = (meanA * meanA + meanB * meanB + C1) * (varA + varB + C2);

            if (denominator == 0f) return 1f;

            return numerator / denominator;
        }

        /// <summary>
        /// Convenience method: computes SSIM directly from two RGBA byte arrays.
        /// </summary>
        public float ComputeFromRGBA(byte[] imageA, byte[] imageB)
        {
            if (imageA == null || imageB == null) return 0f;
            if (imageA.Length != imageB.Length) return 0f;
            if (imageA.Length == 0) return 1f;

            var lumA = ExtractLuminance(imageA);
            var lumB = ExtractLuminance(imageB);
            return ComputeSSIM(lumA, lumB);
        }
    }

    /// <summary>
    /// Manages baseline sets: version tracking, completeness validation, and report generation.
    /// </summary>
    public class BaselineManager
    {
        private readonly Dictionary<string, BaselineSet> _baselines = new Dictionary<string, BaselineSet>();

        /// <summary>
        /// Registers a baseline set under its version key.
        /// Overwrites any existing baseline with the same version.
        /// </summary>
        public void RegisterBaseline(BaselineSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (string.IsNullOrEmpty(set.Version))
                throw new ArgumentException("BaselineSet must have a version.");
            _baselines[set.Version] = set;
        }

        /// <summary>
        /// Retrieves a baseline set by version. Returns null if not found.
        /// </summary>
        public BaselineSet GetBaseline(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;
            _baselines.TryGetValue(version, out var set);
            return set;
        }

        /// <summary>
        /// Returns all registered baseline version strings.
        /// </summary>
        public List<string> GetAllVersions()
        {
            return new List<string>(_baselines.Keys);
        }

        /// <summary>
        /// Returns the number of registered baselines.
        /// </summary>
        public int BaselineCount => _baselines.Count;

        /// <summary>
        /// Removes a baseline set by version. Returns true if removed.
        /// </summary>
        public bool RemoveBaseline(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            return _baselines.Remove(version);
        }

        /// <summary>
        /// Checks if a specific version exists.
        /// </summary>
        public bool HasVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            return _baselines.ContainsKey(version);
        }

        /// <summary>
        /// Validates completeness of a specific baseline version.
        /// </summary>
        public bool IsVersionComplete(string version)
        {
            var set = GetBaseline(version);
            return VisualRegressionValidator.IsBaselineSetComplete(set);
        }
    }

    /// <summary>
    /// Orchestrates visual regression comparison of current vs baseline screenshots.
    /// Operates on raw pixel data (byte arrays) — no Unity dependencies.
    /// </summary>
    public class RegressionTestRunner
    {
        private readonly ComparisonAlgorithm _algorithm;
        private readonly ComparisonThresholds _thresholds;
        private readonly int _perChannelThreshold;

        public RegressionTestRunner(
            ComparisonAlgorithm algorithm,
            ComparisonThresholds thresholds,
            int perChannelThreshold = 10)
        {
            _algorithm = algorithm;
            _thresholds = thresholds;
            _perChannelThreshold = perChannelThreshold;
        }

        /// <summary>
        /// Compares a single current screenshot against its baseline.
        /// Both inputs are RGBA byte arrays of the same dimensions.
        /// Width and height are required for perceptual hash computation.
        /// </summary>
        public ComparisonResult Compare(
            byte[] baseline,
            byte[] current,
            int width = 0,
            int height = 0)
        {
            if (baseline == null || current == null)
            {
                return new ComparisonResult(false, 0f, 0, 100f);
            }

            if (baseline.Length != current.Length)
            {
                return new ComparisonResult(false, 0f, 0, 100f);
            }

            float similarity;
            int diffCount;
            float diffPercentage;

            switch (_algorithm)
            {
                case ComparisonAlgorithm.PixelDiff:
                {
                    var comparer = new PixelComparisonLogic(_perChannelThreshold);
                    diffCount = comparer.ComputeDiffCount(baseline, current);
                    diffPercentage = comparer.ComputeDiffPercentage(baseline, current);
                    similarity = comparer.ComputeSimilarity(baseline, current);
                    break;
                }
                case ComparisonAlgorithm.PerceptualHash:
                {
                    if (width <= 0 || height <= 0)
                    {
                        return new ComparisonResult(false, 0f, 0, 100f);
                    }
                    var hasher = new PerceptualHashLogic();
                    var hashA = hasher.ComputeHash(baseline, width, height);
                    var hashB = hasher.ComputeHash(current, width, height);
                    similarity = hasher.ComputeSimilarity(hashA, hashB);
                    // For perceptual hash, approximate diff metrics
                    var pixelLogic = new PixelComparisonLogic(_perChannelThreshold);
                    diffCount = pixelLogic.ComputeDiffCount(baseline, current);
                    diffPercentage = pixelLogic.ComputeDiffPercentage(baseline, current);
                    break;
                }
                case ComparisonAlgorithm.SSIM:
                {
                    var ssim = new SSIMCalculator();
                    similarity = ssim.ComputeFromRGBA(baseline, current);
                    // Clamp similarity to [0, 1] for threshold comparison
                    similarity = Math.Max(0f, Math.Min(1f, similarity));
                    var pixelLogic = new PixelComparisonLogic(_perChannelThreshold);
                    diffCount = pixelLogic.ComputeDiffCount(baseline, current);
                    diffPercentage = pixelLogic.ComputeDiffPercentage(baseline, current);
                    break;
                }
                case ComparisonAlgorithm.HistogramDiff:
                {
                    var histComparer = new HistogramComparer();
                    similarity = histComparer.ComputeSimilarity(baseline, current);
                    var pixelLogic = new PixelComparisonLogic(_perChannelThreshold);
                    diffCount = pixelLogic.ComputeDiffCount(baseline, current);
                    diffPercentage = pixelLogic.ComputeDiffPercentage(baseline, current);
                    break;
                }
                default:
                    return new ComparisonResult(false, 0f, 0, 100f);
            }

            bool passed = similarity >= _thresholds.PassThreshold
                       && diffPercentage <= _thresholds.MaxDiffPercentage;

            return new ComparisonResult(passed, similarity, Math.Max(0, diffCount), diffPercentage);
        }

        /// <summary>
        /// Runs comparison for multiple named screenshots and generates a report.
        /// </summary>
        public RegressionReport RunBatch(
            Dictionary<string, byte[]> baselines,
            Dictionary<string, byte[]> currentScreenshots,
            int width = 0,
            int height = 0)
        {
            var results = new List<PerScreenshotResult>();
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (var kvp in baselines)
            {
                string name = kvp.Key;
                byte[] baselineData = kvp.Value;

                byte[] currentData;
                if (!currentScreenshots.TryGetValue(name, out currentData))
                {
                    // Missing current screenshot = failure
                    results.Add(new PerScreenshotResult(name,
                        new ComparisonResult(false, 0f, 0, 100f)));
                    continue;
                }

                var result = Compare(baselineData, currentData, width, height);
                results.Add(new PerScreenshotResult(name, result));
            }

            return new RegressionReport(timestamp, results);
        }
    }
}
