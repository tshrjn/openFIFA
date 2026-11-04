using System.Collections.Generic;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Turns pure-C# print layouts and crest rasters into GPU textures. No font assets or
    /// TextMeshPro are involved: glyphs come from <see cref="KitGlyphFont"/>.
    /// Name/number textures are linear data (see <see cref="KitPrintRasterizer"/>);
    /// crests are sRGB colour and shared per team via a static cache.
    /// </summary>
    public static class KitPrintTextureBuilder
    {
        private static readonly Dictionary<string, Texture2D> CrestCache = new Dictionary<string, Texture2D>();

        /// <summary>Creates an empty print atlas texture (RGBA32, mipmapped, linear).</summary>
        public static Texture2D CreatePrintTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, true)
            {
                name = "KitNameNumber",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
            return texture;
        }

        /// <summary>
        /// Rasterizes <paramref name="layout"/> into <paramref name="texture"/> (sizes must match).
        /// <paramref name="scratch"/> is reused between calls to avoid garbage.
        /// </summary>
        public static void Write(Texture2D texture, KitPrintLayout layout, ref byte[] scratch)
        {
            int size = layout.Width * layout.Height * 4;
            if (scratch == null || scratch.Length != size) scratch = new byte[size];

            KitPrintRasterizer.RasterizeInto(layout, scratch);
            texture.SetPixelData(scratch, 0);
            texture.Apply(true, false);
        }

        /// <summary>
        /// Placeholder crest for a team (original shield design with initials), cached by
        /// team and colours so every player of a side shares one texture.
        /// </summary>
        public static Texture2D GetPlaceholderCrest(string initials, SimpleColor field, SimpleColor band,
            SimpleColor trim, KitFontStyle style, int size)
        {
            string key = string.Concat(initials, "|", KitColorUtility.ToHex(field), KitColorUtility.ToHex(band),
                KitColorUtility.ToHex(trim), "|", ((int)style).ToString(), "|", size.ToString());

            Texture2D texture;
            if (CrestCache.TryGetValue(key, out texture) && texture != null)
                return texture;

            var pixels = KitCrestRasterizer.Rasterize(size, field, band, trim, initials, style);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "KitCrest_" + initials,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(true, true);
            CrestCache[key] = texture;
            return texture;
        }
    }
}
