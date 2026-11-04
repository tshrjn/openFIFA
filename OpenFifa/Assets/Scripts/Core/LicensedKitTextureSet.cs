using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Optional texture set for officially licensed kits. Paths are Unity Resources
    /// paths (no extension), e.g. "Kits/Licensed/club_home_albedo". When
    /// <see cref="AlbedoPath"/> is set, the kit shader uses the textures instead of the
    /// procedural pattern. Name/number printing still layers on top.
    /// Mask channel convention (matches OpenFifaKit.shader):
    /// R = ambient occlusion, G = smoothness, B = fabric (1) vs print/badge (0), A = sweat/wear weight.
    /// </summary>
    [Serializable]
    public class LicensedKitTextureSet
    {
        /// <summary>Base colour (sRGB) texture laid out in the jersey mesh UVs.</summary>
        public string AlbedoPath = "";

        /// <summary>Tangent-space normal map. Optional.</summary>
        public string NormalPath = "";

        /// <summary>Packed mask (see class summary). Optional.</summary>
        public string MaskPath = "";

        /// <summary>Crest decal (RGBA). Optional; usually baked into the albedo for licensed kits.</summary>
        public string CrestPath = "";

        /// <summary>Sponsor decal (RGBA). Optional.</summary>
        public string SponsorPath = "";

        /// <summary>True when the set provides an albedo, i.e. it should replace the procedural pattern.</summary>
        public bool HasAlbedo => !string.IsNullOrEmpty(AlbedoPath);

        /// <summary>True when any path is set.</summary>
        public bool HasAny =>
            HasAlbedo
            || !string.IsNullOrEmpty(NormalPath)
            || !string.IsNullOrEmpty(MaskPath)
            || !string.IsNullOrEmpty(CrestPath)
            || !string.IsNullOrEmpty(SponsorPath);
    }
}
