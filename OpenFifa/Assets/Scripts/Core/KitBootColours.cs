namespace OpenFifa.Core
{
    /// <summary>
    /// Boot colours per player: a dark base with a contrasting accent (side stripe and sole), picked from a stable
    /// hash of the roster id. Pure C#.
    /// </summary>
    public static class KitBootColours
    {
        public static readonly string[] Bases = { "#121316", "#1A1C22", "#0F1A2E", "#22201E", "#EDEDED" };
        public static readonly string[] Accents = { "#FF5A1F", "#C6F432", "#E8E8E8", "#1FB6FF", "#FFC21A", "#E0245E", "#00C2A8" };

        /// <summary>Base and accent colour for a player id.</summary>
        public static void ForPlayer(string playerId, out SimpleColor baseColour, out SimpleColor accent)
        {
            uint h = ClubKitFactory.StableHash((playerId ?? "") + "#boots");
            // Mostly dark boots; a white pair now and then.
            int b = (h % 10u) < 8u ? (int)(h % 4u) : 4;
            int a = (int)((h >> 8) % (uint)Accents.Length);
            baseColour = KitColorUtility.ParseOrDefault(Bases[b], new SimpleColor(0.07f, 0.07f, 0.08f));
            accent = KitColorUtility.ParseOrDefault(Accents[a], SimpleColor.White);
            if (b == 4 && a == 2) accent = KitColorUtility.ParseOrDefault("#121316", SimpleColor.Black);
        }
    }
}
