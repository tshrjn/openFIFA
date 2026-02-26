using System;
using System.Collections.Generic;

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
}
