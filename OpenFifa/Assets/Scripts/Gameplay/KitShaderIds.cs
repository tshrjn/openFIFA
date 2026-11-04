using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Cached property IDs for OpenFifaKit.shader ("OpenFifa/Kit").
    /// </summary>
    public static class KitShaderIds
    {
        public const string ShaderName = "OpenFifa/Kit";

        /// <summary>Resources path of the template material (keeps the shader in player builds).</summary>
        public const string MaterialResourcePath = "Kits/OpenFifaKit";

        public static readonly int PrimaryColor = Shader.PropertyToID("_PrimaryColor");
        public static readonly int SecondaryColor = Shader.PropertyToID("_SecondaryColor");
        public static readonly int TertiaryColor = Shader.PropertyToID("_TertiaryColor");
        public static readonly int ShortsColor = Shader.PropertyToID("_ShortsColor");
        public static readonly int SocksColor = Shader.PropertyToID("_SocksColor");
        public static readonly int PatternType = Shader.PropertyToID("_PatternType");
        public static readonly int PatternScale = Shader.PropertyToID("_PatternScale");
        public static readonly int CollarStyle = Shader.PropertyToID("_CollarStyle");

        public static readonly int MappingMode = Shader.PropertyToID("_MappingMode");
        public static readonly int FacingAngle = Shader.PropertyToID("_FacingAngle");
        public static readonly int BackPrintRect = Shader.PropertyToID("_BackPrintRect");
        public static readonly int FrontPrintRect = Shader.PropertyToID("_FrontPrintRect");

        public static readonly int NameNumberTex = Shader.PropertyToID("_NameNumberTex");
        public static readonly int UseNameNumber = Shader.PropertyToID("_UseNameNumber");
        public static readonly int NumberColor = Shader.PropertyToID("_NumberColor");
        public static readonly int NumberOutlineColor = Shader.PropertyToID("_NumberOutlineColor");

        public static readonly int CrestTex = Shader.PropertyToID("_CrestTex");
        public static readonly int UseCrest = Shader.PropertyToID("_UseCrest");
        public static readonly int SponsorTex = Shader.PropertyToID("_SponsorTex");
        public static readonly int UseSponsor = Shader.PropertyToID("_UseSponsor");

        public static readonly int SheenStrength = Shader.PropertyToID("_SheenStrength");

        public static readonly int UseLicensedTextures = Shader.PropertyToID("_UseLicensedTextures");
        public static readonly int KitAlbedo = Shader.PropertyToID("_KitAlbedo");
        public static readonly int KitNormal = Shader.PropertyToID("_KitNormal");
        public static readonly int KitMask = Shader.PropertyToID("_KitMask");
        public static readonly int LicensedHasNormal = Shader.PropertyToID("_LicensedHasNormal");
        public static readonly int LicensedHasMask = Shader.PropertyToID("_LicensedHasMask");

        public static readonly int Sweat = Shader.PropertyToID("_Sweat");
        public static readonly int Dirt = Shader.PropertyToID("_Dirt");
    }
}
