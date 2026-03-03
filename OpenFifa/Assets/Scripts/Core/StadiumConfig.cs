using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Dimensions, tier count, distance from pitch, and capacity for one stand section.
    /// </summary>
    public class StandsSection
    {
        public float Width { get; }
        public float Depth { get; }
        public float Height { get; }
        public int TierCount { get; }
        public float DistanceFromPitch { get; }
        public int Capacity { get; }
        public float AngleOffset { get; }

        public StandsSection(
            float width = 30f,
            float depth = 10f,
            float height = 8f,
            int tierCount = 3,
            float distanceFromPitch = 5f,
            int capacity = 2000,
            float angleOffset = 0f)
        {
            if (width <= 0f) throw new ArgumentException("Width must be positive.", nameof(width));
            if (depth <= 0f) throw new ArgumentException("Depth must be positive.", nameof(depth));
            if (height <= 0f) throw new ArgumentException("Height must be positive.", nameof(height));
            if (tierCount <= 0) throw new ArgumentException("TierCount must be positive.", nameof(tierCount));
            if (distanceFromPitch < 0f) throw new ArgumentException("DistanceFromPitch must be non-negative.", nameof(distanceFromPitch));
            if (capacity < 0) throw new ArgumentException("Capacity must be non-negative.", nameof(capacity));

            Width = width;
            Depth = depth;
            Height = height;
            TierCount = tierCount;
            DistanceFromPitch = distanceFromPitch;
            Capacity = capacity;
            AngleOffset = angleOffset;
        }

        /// <summary>Height of each individual tier.</summary>
        public float TierHeight => Height / TierCount;
    }

    /// <summary>
    /// Position and lighting data for one floodlight tower.
    /// </summary>
    public class FloodlightTowerData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Intensity { get; }
        public float ConeAngle { get; }

        public FloodlightTowerData(
            float x = 0f,
            float y = 25f,
            float z = 0f,
            float intensity = 150f,
            float coneAngle = 100f)
        {
            if (intensity < 0f) throw new ArgumentException("Intensity must be non-negative.", nameof(intensity));
            if (coneAngle <= 0f || coneAngle > 180f) throw new ArgumentException("ConeAngle must be between 0 and 180.", nameof(coneAngle));

            X = x;
            Y = y;
            Z = z;
            Intensity = intensity;
            ConeAngle = coneAngle;
        }
    }

    /// <summary>
    /// Position, dimensions, and rotation for one advertising board.
    /// </summary>
    public class AdvertisingBoardData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float Height { get; }
        public float RotationY { get; }

        public AdvertisingBoardData(
            float x = 0f,
            float y = 0.5f,
            float z = 0f,
            float width = 6f,
            float height = 1f,
            float rotationY = 0f)
        {
            if (width <= 0f) throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0f) throw new ArgumentException("Height must be positive.", nameof(height));

            X = x;
            Y = y;
            Z = z;
            Width = width;
            Height = height;
            RotationY = rotationY;
        }
    }

    /// <summary>
    /// Position data for a corner flag.
    /// </summary>
    public class CornerFlagData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float PoleHeight { get; }
        public float PoleRadius { get; }

        public CornerFlagData(
            float x = 0f,
            float y = 0f,
            float z = 0f,
            float poleHeight = 1.5f,
            float poleRadius = 0.02f)
        {
            if (poleHeight <= 0f) throw new ArgumentException("PoleHeight must be positive.", nameof(poleHeight));
            if (poleRadius <= 0f) throw new ArgumentException("PoleRadius must be positive.", nameof(poleRadius));

            X = x;
            Y = y;
            Z = z;
            PoleHeight = poleHeight;
            PoleRadius = poleRadius;
        }
    }

    /// <summary>
    /// Position and dimensions for a team dugout.
    /// </summary>
    public class DugoutData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float Depth { get; }
        public float Height { get; }
        public string TeamLabel { get; }

        public DugoutData(
            float x = 0f,
            float y = 0f,
            float z = 0f,
            float width = 6f,
            float depth = 2f,
            float height = 2.5f,
            string teamLabel = "Home")
        {
            if (width <= 0f) throw new ArgumentException("Width must be positive.", nameof(width));
            if (depth <= 0f) throw new ArgumentException("Depth must be positive.", nameof(depth));
            if (height <= 0f) throw new ArgumentException("Height must be positive.", nameof(height));

            X = x;
            Y = y;
            Z = z;
            Width = width;
            Depth = depth;
            Height = height;
            TeamLabel = teamLabel ?? "Home";
        }
    }

    /// <summary>
    /// Position and dimensions for the scoreboard.
    /// </summary>
    public class ScoreboardData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float Height { get; }

        public ScoreboardData(
            float x = 0f,
            float y = 12f,
            float z = 0f,
            float width = 8f,
            float height = 3f)
        {
            if (width <= 0f) throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0f) throw new ArgumentException("Height must be positive.", nameof(height));

            X = x;
            Y = y;
            Z = z;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Position and dimensions for the player tunnel entrance.
    /// </summary>
    public class TunnelData
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float Height { get; }
        public float Depth { get; }

        public TunnelData(
            float x = 0f,
            float y = 0f,
            float z = 0f,
            float width = 4f,
            float height = 3f,
            float depth = 3f)
        {
            if (width <= 0f) throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0f) throw new ArgumentException("Height must be positive.", nameof(height));
            if (depth <= 0f) throw new ArgumentException("Depth must be positive.", nameof(depth));

            X = x;
            Y = y;
            Z = z;
            Width = width;
            Height = height;
            Depth = depth;
        }
    }

    /// <summary>
    /// Crowd density zone mapping a stand section index to a density factor (0-1).
    /// </summary>
    public class CrowdDensityZone
    {
        public int SectionIndex { get; }
        public float Density { get; }

        public CrowdDensityZone(int sectionIndex = 0, float density = 0.8f)
        {
            if (sectionIndex < 0) throw new ArgumentException("SectionIndex must be non-negative.", nameof(sectionIndex));
            if (density < 0f || density > 1f) throw new ArgumentException("Density must be between 0 and 1.", nameof(density));

            SectionIndex = sectionIndex;
            Density = density;
        }
    }

    /// <summary>
    /// Configuration for stadium environment: skybox, pitch texture, goal posts,
    /// goal nets, stands, and screenshot baseline settings.
    /// </summary>
    public class StadiumConfig
    {
        // --- Skybox ---
        /// <summary>Name of the Poly Haven HDRI used for the skybox.</summary>
        public string SkyboxHDRIName = "kloppenheim_stadium";

        /// <summary>Shader used for the skybox material.</summary>
        public string SkyboxShaderName = "Skybox/Panoramic";

        // --- Pitch Texture ---
        /// <summary>Whether to use alternating mowed grass band pattern.</summary>
        public bool UsePitchGrassBands = true;

        /// <summary>Number of visible grass band stripes on the pitch.</summary>
        public int GrassBandCount = 10;

        // --- Pitch Dimensions (for stadium element placement) ---
        /// <summary>Pitch length in meters (X axis).</summary>
        public float PitchLength = 50f;

        /// <summary>Pitch width in meters (Z axis).</summary>
        public float PitchWidth = 30f;

        // --- Goal Posts ---
        /// <summary>Goal opening width in meters (FIFA 5-a-side: ~3.66m).</summary>
        public float GoalPostWidth = 3.66f;

        /// <summary>Goal crossbar height in meters (standard: 2.44m).</summary>
        public float GoalPostHeight = 2.44f;

        /// <summary>Radius of goal post cylinders in meters.</summary>
        public float PostRadius = 0.06f;

        /// <summary>Whether goal posts have MeshCollider for ball deflection.</summary>
        public bool PostsHaveMeshCollider = true;

        /// <summary>Whether post MeshColliders are convex (required for dynamic collision).</summary>
        public bool PostColliderConvex = true;

        // --- Goal Net ---
        /// <summary>Net material alpha (semi-transparent).</summary>
        public float NetAlpha = 0.4f;

        // --- Stands ---
        /// <summary>Whether stands/bleacher geometry is present around the pitch.</summary>
        public bool HasStandsGeometry = true;

        /// <summary>Number of stand sections around the pitch perimeter.</summary>
        public int StandsSections = 8;

        /// <summary>Whether floodlight towers are present in the stadium.</summary>
        public bool HasFloodlights = true;

        /// <summary>Whether animated crowd geometry is present in the stands.</summary>
        public bool HasCrowdGeometry = true;

        /// <summary>Whether advertising boards line the pitch perimeter.</summary>
        public bool HasAdvertisingBoards = true;

        /// <summary>Whether player tunnels are present at the stadium entrance.</summary>
        public bool HasTunnels = true;

        /// <summary>Whether dugouts are present on the south touchline.</summary>
        public bool HasDugouts = true;

        /// <summary>Whether corner flags are present at the pitch corners.</summary>
        public bool HasCornerFlags = true;

        /// <summary>Whether a scoreboard position marker is present.</summary>
        public bool HasScoreboard = true;

        // --- Screenshots ---
        /// <summary>Baseline screenshot width for visual regression.</summary>
        public int BaselineScreenshotWidth = 1920;

        /// <summary>Baseline screenshot height for visual regression.</summary>
        public int BaselineScreenshotHeight = 1080;

        // --- Stand Sections ---
        /// <summary>Stand section definitions for the 8 sections around the pitch.</summary>
        public readonly List<StandsSection> Sections;

        // --- Floodlight Towers ---
        /// <summary>Floodlight tower definitions (4 corners).</summary>
        public readonly List<FloodlightTowerData> FloodlightTowers;

        // --- Advertising Boards ---
        /// <summary>Advertising board placements along the touchlines.</summary>
        public readonly List<AdvertisingBoardData> AdvertisingBoards;

        // --- Corner Flags ---
        /// <summary>Corner flag placements at the 4 pitch corners.</summary>
        public readonly List<CornerFlagData> CornerFlags;

        // --- Dugouts ---
        /// <summary>Dugout placements on the south touchline.</summary>
        public readonly List<DugoutData> Dugouts;

        // --- Scoreboard ---
        /// <summary>Scoreboard position marker.</summary>
        public readonly ScoreboardData Scoreboard;

        // --- Tunnel ---
        /// <summary>Tunnel entrance data.</summary>
        public readonly TunnelData Tunnel;

        // --- Crowd Density Zones ---
        /// <summary>Crowd density per stand section.</summary>
        public readonly List<CrowdDensityZone> CrowdDensityZones;

        public StadiumConfig()
        {
            float halfLength = PitchLength / 2f;
            float halfWidth = PitchWidth / 2f;
            float standDistance = 5f;

            // Build 8 stand sections: 2 long sides (North/South), 2 short ends (East/West), 4 corners
            Sections = new List<StandsSection>
            {
                // North stand (long side, positive Z)
                new StandsSection(width: PitchLength * 0.8f, depth: 12f, height: 10f, tierCount: 3, distanceFromPitch: standDistance, capacity: 3000, angleOffset: 0f),
                // South stand (long side, negative Z)
                new StandsSection(width: PitchLength * 0.8f, depth: 12f, height: 10f, tierCount: 3, distanceFromPitch: standDistance, capacity: 3000, angleOffset: 180f),
                // East stand (short end, positive X)
                new StandsSection(width: PitchWidth * 0.7f, depth: 10f, height: 8f, tierCount: 2, distanceFromPitch: standDistance, capacity: 1500, angleOffset: 90f),
                // West stand (short end, negative X)
                new StandsSection(width: PitchWidth * 0.7f, depth: 10f, height: 8f, tierCount: 2, distanceFromPitch: standDistance, capacity: 1500, angleOffset: 270f),
                // Corner NE
                new StandsSection(width: 12f, depth: 8f, height: 7f, tierCount: 2, distanceFromPitch: standDistance + 2f, capacity: 800, angleOffset: 45f),
                // Corner NW
                new StandsSection(width: 12f, depth: 8f, height: 7f, tierCount: 2, distanceFromPitch: standDistance + 2f, capacity: 800, angleOffset: 315f),
                // Corner SE
                new StandsSection(width: 12f, depth: 8f, height: 7f, tierCount: 2, distanceFromPitch: standDistance + 2f, capacity: 800, angleOffset: 135f),
                // Corner SW
                new StandsSection(width: 12f, depth: 8f, height: 7f, tierCount: 2, distanceFromPitch: standDistance + 2f, capacity: 800, angleOffset: 225f),
            };

            // 4 floodlight towers at the corners (outside the stands)
            float towerX = halfLength + 15f;
            float towerZ = halfWidth + 15f;
            float towerY = 25f;

            FloodlightTowers = new List<FloodlightTowerData>
            {
                new FloodlightTowerData(x: towerX,  y: towerY, z: towerZ,  intensity: 150f, coneAngle: 100f),
                new FloodlightTowerData(x: -towerX, y: towerY, z: towerZ,  intensity: 150f, coneAngle: 100f),
                new FloodlightTowerData(x: towerX,  y: towerY, z: -towerZ, intensity: 150f, coneAngle: 100f),
                new FloodlightTowerData(x: -towerX, y: towerY, z: -towerZ, intensity: 150f, coneAngle: 100f),
            };

            // Advertising boards along touchlines (north and south)
            AdvertisingBoards = new List<AdvertisingBoardData>();
            float boardY = 0.5f;
            float boardWidth = 6f;
            float boardHeight = 1f;
            float boardZ_North = halfWidth + 2f;
            float boardZ_South = -(halfWidth + 2f);

            for (int i = 0; i < 6; i++)
            {
                float boardX = -halfLength + 5f + i * (boardWidth + 2f);
                AdvertisingBoards.Add(new AdvertisingBoardData(x: boardX, y: boardY, z: boardZ_North, width: boardWidth, height: boardHeight, rotationY: 180f));
                AdvertisingBoards.Add(new AdvertisingBoardData(x: boardX, y: boardY, z: boardZ_South, width: boardWidth, height: boardHeight, rotationY: 0f));
            }

            // 4 corner flags
            CornerFlags = new List<CornerFlagData>
            {
                new CornerFlagData(x: halfLength,  y: 0f, z: halfWidth,  poleHeight: 1.5f, poleRadius: 0.02f),
                new CornerFlagData(x: -halfLength, y: 0f, z: halfWidth,  poleHeight: 1.5f, poleRadius: 0.02f),
                new CornerFlagData(x: halfLength,  y: 0f, z: -halfWidth, poleHeight: 1.5f, poleRadius: 0.02f),
                new CornerFlagData(x: -halfLength, y: 0f, z: -halfWidth, poleHeight: 1.5f, poleRadius: 0.02f),
            };

            // 2 dugouts on south touchline
            float dugoutZ = -(halfWidth + 4f);
            Dugouts = new List<DugoutData>
            {
                new DugoutData(x: -8f, y: 0f, z: dugoutZ, width: 6f, depth: 2f, height: 2.5f, teamLabel: "Home"),
                new DugoutData(x: 8f,  y: 0f, z: dugoutZ, width: 6f, depth: 2f, height: 2.5f, teamLabel: "Away"),
            };

            // Scoreboard above the north stand
            Scoreboard = new ScoreboardData(x: 0f, y: 12f, z: halfWidth + 18f, width: 8f, height: 3f);

            // Tunnel entrance behind south stand center
            Tunnel = new TunnelData(x: 0f, y: 0f, z: -(halfWidth + 8f), width: 4f, height: 3f, depth: 3f);

            // Crowd density zones (one per section)
            CrowdDensityZones = new List<CrowdDensityZone>();
            float[] densities = { 0.95f, 0.90f, 0.85f, 0.85f, 0.75f, 0.75f, 0.70f, 0.70f };
            for (int i = 0; i < Sections.Count; i++)
            {
                CrowdDensityZones.Add(new CrowdDensityZone(sectionIndex: i, density: i < densities.Length ? densities[i] : 0.8f));
            }
        }

        /// <summary>Total stadium capacity across all sections.</summary>
        public int TotalCapacity
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Sections.Count; i++)
                {
                    total += Sections[i].Capacity;
                }
                return total;
            }
        }

        /// <summary>Number of floodlight towers.</summary>
        public int FloodlightCount => FloodlightTowers.Count;
    }
}
