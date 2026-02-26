namespace OpenFifa.Core
{
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
        public int StandsSections = 4;

        // --- Screenshots ---
        /// <summary>Baseline screenshot width for visual regression.</summary>
        public int BaselineScreenshotWidth = 1920;

        /// <summary>Baseline screenshot height for visual regression.</summary>
        public int BaselineScreenshotHeight = 1080;
    }
}
