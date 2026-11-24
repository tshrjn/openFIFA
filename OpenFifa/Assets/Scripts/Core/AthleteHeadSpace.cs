namespace OpenFifa.Core
{
    /// <summary>
    /// Kit space of the Blender athlete body's head (Assets/Art/Characters/Athlete, tools/blender/athlete_body.py), as
    /// measured by KitBodyMapPostprocessor (HeadProfile log, LOD0) on import. The athlete counterpart of
    /// <see cref="MannequinHeadSpace"/>: the generator puts the Neck joint 0.226 m below the modelled eyes, so the
    /// painted face lands on the same kit-space heights, but the head sits 3.6 cm further forward of the torso centre.
    /// Re-measure (OPENFIFA_KITBODY_DUMP=1) when the generator's head or neck changes. Pure C#.
    /// </summary>
    public static class AthleteHeadSpace
    {
        /// <summary>Mesh-name prefix of the athlete body LODs (Athlete_LOD0..2).</summary>
        public const string MeshPrefix = "Athlete_";

        /// <summary>Head centre in kit space (y, z); x is 0.</summary>
        public const float CentreKitY = 0.200f, CentreKitZ = 0.024f;

        /// <summary>Head half width (the skull; the measured box also takes in the trapezius) and half depth (kit space).</summary>
        public const float HalfWidth = 0.090f, HalfDepth = 0.114f;

        /// <summary>The head centre in head-bone space (x, y, z).</summary>
        public const float CentreBoneX = 0f, CentreBoneY = 0.0152f, CentreBoneZ = 0.0142f;

        /// <summary>Kit +Y in head-bone space (x is 0).</summary>
        public const float UpBoneY = 0.998f, UpBoneZ = 0.058f;

        /// <summary>Kit +Z (front) in head-bone space (x is 0).</summary>
        public const float FrontBoneY = -0.058f, FrontBoneZ = 0.998f;
    }
}
