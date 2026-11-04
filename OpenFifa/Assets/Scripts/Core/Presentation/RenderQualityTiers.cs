using System;

namespace OpenFifa.Core
{
    /// <summary>Rendering quality tier: the desktop/capture look, or the lighter iPad/mobile one.</summary>
    public enum RenderQualityTier
    {
        High = 0,
        Mobile = 1,
    }

    /// <summary>Post anti-aliasing of the broadcast camera.</summary>
    public enum PostAntialiasing
    {
        None = 0,
        Fxaa = 1,
        SmaaLow = 2,
        SmaaHigh = 3,
    }

    /// <summary>Everything a tier sets. Engine-free; the Gameplay side maps it onto URP and the turf textures.</summary>
    public struct RenderQualityProfile
    {
        public RenderQualityTier Tier;
        /// <summary>URP asset MSAA sample count (1 = off).</summary>
        public int MsaaSamples;
        public PostAntialiasing PostAa;
        /// <summary>Main-light shadow map size (px), cascade count and distance (m).</summary>
        public int ShadowResolution;
        public int ShadowCascades;
        public float ShadowDistance;
        /// <summary>Soft-shadow filter: 1 low, 2 medium, 3 high (URP SoftShadowQuality).</summary>
        public int SoftShadowQuality;
        /// <summary>Screen-space ambient occlusion renderer feature on.</summary>
        public bool Ssao;
        /// <summary>Turf texture sizes (px), anisotropic levels and whether to block-compress at runtime.</summary>
        public int TurfBaseWidth, TurfBaseHeight, TurfNormalWidth, TurfNormalHeight, TurfDetailSize;
        public int TurfBaseAniso, TurfDetailAniso;
        public bool CompressTurf;
        /// <summary>Live particle budget of the rain and snow emitters (caps maxParticles and the emission rate).</summary>
        public int MaxRainParticles, MaxSnowParticles;
    }

    /// <summary>
    /// Picks the rendering tier and its settings. iPad Pro renders 2732 x 2048: SMAA High on top of MSAA 4x, a 4096
    /// four-cascade shadow map with the high soft-shadow filter and full-resolution SSAO cost several milliseconds a
    /// frame there, so mobile gets FXAA, MSAA 2x, a 2048 two-cascade map, medium soft shadows, no SSAO and smaller turf
    /// textures. Desktop and captures keep the high tier. Override with <c>-spQuality low|high</c> (also mobile/ipad,
    /// desktop) so a desktop capture can preview the iPad tier.
    /// </summary>
    public static class RenderQualityTiers
    {
        public const string Argument = "-spQuality";

        /// <summary>Tier from the command line (<see cref="Argument"/>), else from the platform.</summary>
        public static RenderQualityTier Resolve(string[] args, bool isMobilePlatform)
        {
            if (args != null)
            {
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (!string.Equals(args[i], Argument, StringComparison.OrdinalIgnoreCase)) continue;
                    if (TryParse(args[i + 1], out var tier)) return tier;
                }
            }
            return isMobilePlatform ? RenderQualityTier.Mobile : RenderQualityTier.High;
        }

        public static bool TryParse(string value, out RenderQualityTier tier)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "low":
                case "mobile":
                case "ipad":
                    tier = RenderQualityTier.Mobile;
                    return true;
                case "high":
                case "desktop":
                    tier = RenderQualityTier.High;
                    return true;
                default:
                    tier = RenderQualityTier.High;
                    return false;
            }
        }

        public static RenderQualityProfile For(RenderQualityTier tier)
        {
            if (tier == RenderQualityTier.Mobile)
            {
                return new RenderQualityProfile
                {
                    Tier = tier,
                    MsaaSamples = 2,
                    PostAa = PostAntialiasing.Fxaa,
                    ShadowResolution = 2048,
                    ShadowCascades = 2,
                    ShadowDistance = 110f,
                    SoftShadowQuality = 2,
                    Ssao = false,
                    TurfBaseWidth = 512, TurfBaseHeight = 256,
                    TurfNormalWidth = 256, TurfNormalHeight = 128,
                    TurfDetailSize = 256,
                    TurfBaseAniso = 2, TurfDetailAniso = 2,
                    CompressTurf = false, // no runtime ASTC on iOS: stay small instead
                    // Thin alpha-blended streaks; iPad fill rate at 2732 x 2048 wants under half the desktop count.
                    MaxRainParticles = 2500, MaxSnowParticles = 2000,
                };
            }
            return new RenderQualityProfile
            {
                Tier = RenderQualityTier.High,
                MsaaSamples = 4,
                PostAa = PostAntialiasing.SmaaHigh,
                ShadowResolution = 4096,
                ShadowCascades = 4,
                ShadowDistance = 140f,
                SoftShadowQuality = 3,
                Ssao = true,
                TurfBaseWidth = 1024, TurfBaseHeight = 512,
                TurfNormalWidth = 512, TurfNormalHeight = 256,
                TurfDetailSize = 512,
                TurfBaseAniso = 4, TurfDetailAniso = 2,
                CompressTurf = true,
                MaxRainParticles = 6000, MaxSnowParticles = 5000,
            };
        }

        /// <summary>GPU bytes of a w x h texture at <paramref name="bitsPerPixel"/>, with or without its full mip chain (4x4 blocks for compressed formats).</summary>
        public static long TextureBytes(int width, int height, int bitsPerPixel, bool mips, bool blockCompressed = false)
        {
            long total = 0;
            int w = Math.Max(1, width), h = Math.Max(1, height);
            while (true)
            {
                long bw = blockCompressed ? (w + 3) / 4 * 4 : w;
                long bh = blockCompressed ? (h + 3) / 4 * 4 : h;
                total += bw * bh * bitsPerPixel / 8;
                if (!mips || (w == 1 && h == 1)) break;
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            return total;
        }

        /// <summary>
        /// GPU bytes of the four turf textures (base, base normal, detail albedo, detail normal) for a profile: RGBA32
        /// (32 bpp) with mips, or with the base and detail albedo block-compressed (DXT5, 8 bpp) when
        /// <see cref="RenderQualityProfile.CompressTurf"/> is set. Normal maps stay RGBA32 (block compression bands them).
        /// </summary>
        public static long TurfBytes(in RenderQualityProfile p)
        {
            int albedoBits = p.CompressTurf ? 8 : 32;
            return TextureBytes(p.TurfBaseWidth, p.TurfBaseHeight, albedoBits, true, p.CompressTurf)
                   + TextureBytes(p.TurfNormalWidth, p.TurfNormalHeight, 32, true)
                   + TextureBytes(p.TurfDetailSize, p.TurfDetailSize, albedoBits, true, p.CompressTurf)
                   + TextureBytes(p.TurfDetailSize, p.TurfDetailSize, 32, true);
        }

        /// <summary>The turf before tiers: all four textures RGBA32 at the desktop sizes.</summary>
        public static long LegacyTurfBytes()
        {
            var p = For(RenderQualityTier.High);
            return TextureBytes(p.TurfBaseWidth, p.TurfBaseHeight, 32, true)
                   + TextureBytes(p.TurfNormalWidth, p.TurfNormalHeight, 32, true)
                   + 2 * TextureBytes(p.TurfDetailSize, p.TurfDetailSize, 32, true);
        }
    }
}
