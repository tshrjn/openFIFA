using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# configuration for crowd placement within stadium stands.
    /// No Unity dependencies — fully testable in EditMode.
    /// </summary>
    public class CrowdPlacementConfig
    {
        /// <summary>Number of rows of seating per stand section.</summary>
        public int RowsPerSection { get; }

        /// <summary>Number of seats per row.</summary>
        public int SeatsPerRow { get; }

        /// <summary>Horizontal spacing between seats in meters.</summary>
        public float SeatSpacing { get; }

        /// <summary>Vertical spacing between rows in meters.</summary>
        public float RowSpacing { get; }

        /// <summary>Height offset for each successive row (tiered seating).</summary>
        public float RowHeightStep { get; }

        /// <summary>Angular offsets per section (degrees), 8 sections.</summary>
        public readonly List<float> SectionAngularOffsets;

        /// <summary>Crowd density per zone (section index -> density 0-1).</summary>
        public readonly List<float> CrowdDensityPerZone;

        // --- Color Variation ---
        /// <summary>Minimum RGB values for crowd color randomization.</summary>
        public float ColorMinR { get; }
        public float ColorMinG { get; }
        public float ColorMinB { get; }

        /// <summary>Maximum RGB values for crowd color randomization.</summary>
        public float ColorMaxR { get; }
        public float ColorMaxG { get; }
        public float ColorMaxB { get; }

        /// <summary>Size of each crowd quad in meters.</summary>
        public float QuadWidth { get; }
        public float QuadHeight { get; }

        /// <summary>Random seed for deterministic crowd generation.</summary>
        public int RandomSeed { get; }

        public CrowdPlacementConfig(
            int rowsPerSection = 8,
            int seatsPerRow = 30,
            float seatSpacing = 0.8f,
            float rowSpacing = 1.0f,
            float rowHeightStep = 0.4f,
            List<float> sectionAngularOffsets = null,
            List<float> crowdDensityPerZone = null,
            float colorMinR = 0.15f,
            float colorMinG = 0.10f,
            float colorMinB = 0.10f,
            float colorMaxR = 0.95f,
            float colorMaxG = 0.90f,
            float colorMaxB = 0.95f,
            float quadWidth = 0.5f,
            float quadHeight = 0.8f,
            int randomSeed = 42)
        {
            if (rowsPerSection <= 0) throw new ArgumentException("RowsPerSection must be positive.", nameof(rowsPerSection));
            if (seatsPerRow <= 0) throw new ArgumentException("SeatsPerRow must be positive.", nameof(seatsPerRow));
            if (seatSpacing <= 0f) throw new ArgumentException("SeatSpacing must be positive.", nameof(seatSpacing));
            if (rowSpacing <= 0f) throw new ArgumentException("RowSpacing must be positive.", nameof(rowSpacing));
            if (quadWidth <= 0f) throw new ArgumentException("QuadWidth must be positive.", nameof(quadWidth));
            if (quadHeight <= 0f) throw new ArgumentException("QuadHeight must be positive.", nameof(quadHeight));

            RowsPerSection = rowsPerSection;
            SeatsPerRow = seatsPerRow;
            SeatSpacing = seatSpacing;
            RowSpacing = rowSpacing;
            RowHeightStep = rowHeightStep;
            ColorMinR = colorMinR;
            ColorMinG = colorMinG;
            ColorMinB = colorMinB;
            ColorMaxR = colorMaxR;
            ColorMaxG = colorMaxG;
            ColorMaxB = colorMaxB;
            QuadWidth = quadWidth;
            QuadHeight = quadHeight;
            RandomSeed = randomSeed;

            // Default angular offsets for 8 sections: N, S, E, W, NE, NW, SE, SW
            SectionAngularOffsets = sectionAngularOffsets ?? new List<float>
            {
                0f, 180f, 90f, 270f, 45f, 315f, 135f, 225f
            };

            // Default density per zone
            CrowdDensityPerZone = crowdDensityPerZone ?? new List<float>
            {
                0.95f, 0.90f, 0.85f, 0.85f, 0.75f, 0.75f, 0.70f, 0.70f
            };
        }

        /// <summary>Maximum number of crowd members per section at full density.</summary>
        public int MaxCrowdPerSection => RowsPerSection * SeatsPerRow;

        /// <summary>Total maximum crowd across all sections at full density.</summary>
        public int TotalMaxCrowd => MaxCrowdPerSection * SectionAngularOffsets.Count;

        /// <summary>
        /// Compute the actual crowd count for a given section index,
        /// accounting for that section's density factor.
        /// </summary>
        public int CrowdCountForSection(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= CrowdDensityPerZone.Count)
                return 0;

            float density = CrowdDensityPerZone[sectionIndex];
            return (int)(MaxCrowdPerSection * density);
        }

        /// <summary>
        /// Total actual crowd across all sections considering density.
        /// </summary>
        public int TotalActualCrowd
        {
            get
            {
                int total = 0;
                for (int i = 0; i < CrowdDensityPerZone.Count; i++)
                {
                    total += CrowdCountForSection(i);
                }
                return total;
            }
        }

        /// <summary>
        /// Get the row-local depth offset for a given row index.
        /// Each successive row is placed further back and higher up.
        /// </summary>
        public float RowDepthOffset(int rowIndex)
        {
            return rowIndex * RowSpacing;
        }

        /// <summary>
        /// Get the vertical offset for a given row index.
        /// </summary>
        public float RowVerticalOffset(int rowIndex)
        {
            return rowIndex * RowHeightStep;
        }

        /// <summary>
        /// Get the horizontal offset for a given seat index within a row,
        /// centered around 0.
        /// </summary>
        public float SeatHorizontalOffset(int seatIndex)
        {
            float totalWidth = (SeatsPerRow - 1) * SeatSpacing;
            return -totalWidth / 2f + seatIndex * SeatSpacing;
        }
    }
}
