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
            "#F1C9A5", "#E0AC88", "#C68863", "#A86E4B", "#8A5537", "#7C4B33", "#634031",
        };

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
