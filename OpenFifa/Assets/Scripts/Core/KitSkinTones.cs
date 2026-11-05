namespace OpenFifa.Core
{
    /// <summary>
    /// Skin tones for player bodies rendered with the kit shader. A player's tone is picked from a stable hash of
    /// their roster id, so the same player always looks the same. sRGB, from light to deep.
    /// Pure C#.
    /// </summary>
    public static class KitSkinTones
    {
        public static readonly string[] Palette =
        {
            "#F1C9A5", "#E0AC88", "#C68863", "#A86E4B", "#8A5537", "#6B3F28", "#4E2C1C",
        };

        /// <summary>Palette index nearest to a roster skin tone (0 lightest .. 1 darkest).</summary>
        public static int IndexForTone(float tone)
        {
            float t = tone < 0f ? 0f : (tone > 1f ? 1f : tone);
            return (int)System.Math.Round(t * (Palette.Length - 1));
        }

        /// <summary>Colour for a roster skin tone (0 lightest .. 1 darkest), interpolated along the palette.</summary>
        public static SimpleColor FromTone(float tone)
        {
            float t = (tone < 0f ? 0f : (tone > 1f ? 1f : tone)) * (Palette.Length - 1);
            int i = (int)System.Math.Floor(t);
            if (i >= Palette.Length - 1) return KitColorUtility.ParseOrDefault(Palette[Palette.Length - 1], SimpleColor.Black);
            float f = t - i;
            var a = KitColorUtility.ParseOrDefault(Palette[i], SimpleColor.Black);
            var b = KitColorUtility.ParseOrDefault(Palette[i + 1], SimpleColor.Black);
            return new SimpleColor(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, 1f);
        }

        /// <summary>Tone for a player id (empty id = middle of the palette).</summary>
        public static SimpleColor ForPlayer(string playerId)
        {
            int index = string.IsNullOrEmpty(playerId)
                ? Palette.Length / 2
                : (int)(ClubKitFactory.StableHash(playerId) % (uint)Palette.Length);
            return KitColorUtility.ParseOrDefault(Palette[index], new SimpleColor(0.62f, 0.45f, 0.35f));
        }
    }
}
