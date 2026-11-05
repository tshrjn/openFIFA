namespace OpenFifa.Core
{
    /// <summary>
    /// Kit space of the Quaternius mannequin's head, as measured by KitBodyMapPostprocessor (HeadProfile log) on
    /// UAL2_Standard. Shared by HeadSculpt (import-time head geometry), HairCapBuilder and EarBuilder (meshes on the
    /// head bone). KitPaintHead in OpenFifaKitSurface.hlsl paints in the same kit space with the same numbers:
    /// change them together. Kit space: metres, +Y up from the neck base, +Z the character's front.
    /// Pure C#.
    /// </summary>
    public static class MannequinHeadSpace
    {
        /// <summary>Head centre in kit space (y, z); x is 0.</summary>
        public const float CentreKitY = 0.201f, CentreKitZ = -0.012f;

        /// <summary>The same point in head-bone space (x, y, z).</summary>
        public const float CentreBoneX = 0f, CentreBoneY = 0.1194f, CentreBoneZ = -0.0068f;

        /// <summary>Kit +Y in head-bone space (x is 0).</summary>
        public const float UpBoneY = 0.999f, UpBoneZ = 0.037f;

        /// <summary>Kit +Z (front) in head-bone space (x is 0).</summary>
        public const float FrontBoneY = -0.037f, FrontBoneZ = 0.999f;

        /// <summary>Head half depth used to normalise kit z (front of the face ~ +1).</summary>
        public const float HalfDepth = 0.118f;

        /// <summary>Normalised front coordinate: 0 at the head centre, about 1 at the face.</summary>
        public static float Frontness(float kitZ) => (kitZ - CentreKitZ) / HalfDepth;
    }
}
