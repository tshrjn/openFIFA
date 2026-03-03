using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Populates stand sections with crowd quads (flat colored sprites).
    /// Density and color variation are driven by CrowdPlacementConfig.
    /// Ready for shader animation in US-053.
    /// </summary>
    public class CrowdPlacementSystem : MonoBehaviour
    {
        [SerializeField] private bool _buildOnStart = true;

        private CrowdPlacementConfig _crowdConfig;
        private StadiumConfig _stadiumConfig;
        private int _totalCrowdGenerated;

        /// <summary>Total crowd members generated across all sections.</summary>
        public int TotalCrowdGenerated => _totalCrowdGenerated;

        /// <summary>The crowd placement config in use.</summary>
        public CrowdPlacementConfig CrowdConfig => _crowdConfig;

        private void Awake()
        {
            _crowdConfig = new CrowdPlacementConfig();
            _stadiumConfig = new StadiumConfig();
        }

        private void Start()
        {
            if (_buildOnStart)
            {
                BuildCrowd();
            }
        }

        /// <summary>
        /// Inject custom configs (for testing or editor usage).
        /// </summary>
        public void Initialize(CrowdPlacementConfig crowdConfig, StadiumConfig stadiumConfig)
        {
            _crowdConfig = crowdConfig;
            _stadiumConfig = stadiumConfig;
        }

        /// <summary>
        /// Build all crowd sections. Can be called externally.
        /// </summary>
        public void BuildCrowd()
        {
            if (_crowdConfig == null) _crowdConfig = new CrowdPlacementConfig();
            if (_stadiumConfig == null) _stadiumConfig = new StadiumConfig();

            _totalCrowdGenerated = 0;

            var crowdRoot = new GameObject("Crowd");
            crowdRoot.transform.SetParent(transform);
            crowdRoot.transform.localPosition = Vector3.zero;

            float halfLength = _stadiumConfig.PitchLength / 2f;
            float halfWidth = _stadiumConfig.PitchWidth / 2f;

            int sectionCount = Mathf.Min(
                _stadiumConfig.Sections.Count,
                _crowdConfig.SectionAngularOffsets.Count
            );

            var rng = new System.Random(_crowdConfig.RandomSeed);

            for (int s = 0; s < sectionCount; s++)
            {
                var section = _stadiumConfig.Sections[s];
                int crowdCount = _crowdConfig.CrowdCountForSection(s);
                if (crowdCount <= 0) continue;

                var sectionRoot = new GameObject($"CrowdSection_{s}");
                sectionRoot.transform.SetParent(crowdRoot.transform);

                // Position the crowd section to match the stand section
                Vector3 sectionPos = GetStandSectionPosition(s, section, halfLength, halfWidth);
                sectionRoot.transform.localPosition = sectionPos;

                float rotY = GetStandSectionRotation(s);
                sectionRoot.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

                // Populate crowd members row by row
                int generated = PopulateSectionCrowd(sectionRoot.transform, section, crowdCount, rng);
                _totalCrowdGenerated += generated;
            }
        }

        private int PopulateSectionCrowd(Transform parent, StandsSection section, int targetCount, System.Random rng)
        {
            int generated = 0;
            int rowCount = _crowdConfig.RowsPerSection;
            int seatsPerRow = _crowdConfig.SeatsPerRow;

            // Material shared across all crowd quads in this section for batching
            Material crowdMat = CreateCrowdBaseMaterial();

            for (int row = 0; row < rowCount && generated < targetCount; row++)
            {
                float depthOffset = _crowdConfig.RowDepthOffset(row);
                float heightOffset = _crowdConfig.RowVerticalOffset(row);

                for (int seat = 0; seat < seatsPerRow && generated < targetCount; seat++)
                {
                    float horizontalOffset = _crowdConfig.SeatHorizontalOffset(seat);

                    // Density-based skip: randomly omit some seats for sparse areas
                    if (rng.NextDouble() > 0.95) continue;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = $"Crowd_R{row}_S{seat}";
                    quad.transform.SetParent(parent);

                    // Position within the stand section's local space
                    float yPos = section.TierHeight * 0.5f + heightOffset + _crowdConfig.QuadHeight / 2f;
                    quad.transform.localPosition = new Vector3(
                        horizontalOffset,
                        yPos,
                        depthOffset
                    );

                    quad.transform.localScale = new Vector3(
                        _crowdConfig.QuadWidth,
                        _crowdConfig.QuadHeight,
                        1f
                    );

                    // Randomize color for crowd variety
                    var renderer = quad.GetComponent<MeshRenderer>();
                    var instanceMat = new Material(crowdMat);
                    instanceMat.color = RandomCrowdColor(rng);
                    renderer.sharedMaterial = instanceMat;

                    // Remove collider
                    var col = quad.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    generated++;
                }
            }

            return generated;
        }

        private Color RandomCrowdColor(System.Random rng)
        {
            float r = Lerp(_crowdConfig.ColorMinR, _crowdConfig.ColorMaxR, (float)rng.NextDouble());
            float g = Lerp(_crowdConfig.ColorMinG, _crowdConfig.ColorMaxG, (float)rng.NextDouble());
            float b = Lerp(_crowdConfig.ColorMinB, _crowdConfig.ColorMaxB, (float)rng.NextDouble());
            return new Color(r, g, b);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static Material CreateCrowdBaseMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            return mat;
        }

        private Vector3 GetStandSectionPosition(int index, StandsSection section, float halfLength, float halfWidth)
        {
            float dist = section.DistanceFromPitch;

            switch (index)
            {
                case 0: return new Vector3(0f, 0f, halfWidth + dist + section.Depth / 2f);
                case 1: return new Vector3(0f, 0f, -(halfWidth + dist + section.Depth / 2f));
                case 2: return new Vector3(halfLength + dist + section.Depth / 2f, 0f, 0f);
                case 3: return new Vector3(-(halfLength + dist + section.Depth / 2f), 0f, 0f);
                case 4: return new Vector3(halfLength + dist, 0f, halfWidth + dist);
                case 5: return new Vector3(-(halfLength + dist), 0f, halfWidth + dist);
                case 6: return new Vector3(halfLength + dist, 0f, -(halfWidth + dist));
                case 7: return new Vector3(-(halfLength + dist), 0f, -(halfWidth + dist));
                default: return Vector3.zero;
            }
        }

        private float GetStandSectionRotation(int index)
        {
            switch (index)
            {
                case 0: return 0f;
                case 1: return 180f;
                case 2: return 270f;
                case 3: return 90f;
                case 4: return 315f;
                case 5: return 45f;
                case 6: return 225f;
                case 7: return 135f;
                default: return 0f;
            }
        }
    }
}
