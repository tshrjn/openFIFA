namespace OpenFifa.Core
{
    /// <summary>
    /// Appearance of one (fictional) player: skin tone, hair, facial hair, eyes and boots, chosen deterministically
    /// from the roster id. Original procedural looks, no real likenesses.
    /// </summary>
    public sealed class PlayerLook
    {
        public SimpleColor Skin;
        public int SkinIndex;
        public HairStyle Hair;
        public SimpleColor HairColour;
        public FacialHair Facial;
        /// <summary>0 = dark brown eyes .. 1 = light blue-grey.</summary>
        public float EyeTone;
        public SimpleColor BootBase;
        public SimpleColor BootAccent;

        public static readonly string[] HairColours =
        {
            "#120E0C", "#2A1B12", "#4A2E1C", "#6B4A2E", "#A27A4A", "#C9A66B", "#7A3B1E",
        };

        /// <summary>Look for a roster player id (stable across runs).</summary>
        public static PlayerLook ForPlayer(string playerId)
        {
            string id = playerId ?? "";
            uint h = ClubKitFactory.StableHash(id + "#look");
            var look = new PlayerLook();
            look.SkinIndex = string.IsNullOrEmpty(id)
                ? KitSkinTones.Palette.Length / 2
                : (int)(ClubKitFactory.StableHash(id) % (uint)KitSkinTones.Palette.Length);
            look.Skin = KitSkinTones.ForPlayer(playerId);

            uint r = h % 100u;
            look.Hair = r < 6u ? HairStyle.Bald : r < 30u ? HairStyle.Buzz : r < 68u ? HairStyle.Short : r < 88u ? HairStyle.Curly : HairStyle.Tied;
            uint f = (h >> 8) % 100u;
            look.Facial = f < 50u ? FacialHair.None : f < 75u ? FacialHair.Stubble : f < 90u ? FacialHair.Beard : FacialHair.Goatee;

            // Darker skin tones mostly pair with black / dark brown hair; lighter ones use the whole range.
            uint c = (h >> 16) % 100u;
            int hairIndex;
            if (look.SkinIndex >= 3) hairIndex = c < 70u ? 0 : 1;
            else hairIndex = (int)((h >> 16) % (uint)HairColours.Length);
            look.HairColour = KitColorUtility.ParseOrDefault(HairColours[hairIndex], new SimpleColor(0.1f, 0.07f, 0.05f));
            look.EyeTone = look.SkinIndex <= 1 ? ((h >> 24) % 4u) / 3f : 0.05f * ((h >> 24) % 3u);

            KitBootColours.ForPlayer(playerId, out look.BootBase, out look.BootAccent);
            return look;
        }
    }
}
