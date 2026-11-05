namespace OpenFifa.Core
{
    /// <summary>
    /// Appearance of one player: skin tone, hair, facial hair, eyes and boots. Fields the roster gives
    /// (<see cref="RosterAppearance"/>) are used as given; anything missing is chosen deterministically from the
    /// roster id. Stylised procedural looks, no likenesses.
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

        /// <summary>
        /// Look for a roster player: roster appearance fields when present, the id hash for the rest (stable across runs).
        /// </summary>
        public static PlayerLook ForPlayer(string playerId, RosterAppearance appearance)
        {
            var look = ForPlayer(playerId);
            if (appearance == null) return look;

            if (appearance.HasSkinTone)
            {
                look.Skin = KitSkinTones.FromTone(appearance.SkinTone);
                look.SkinIndex = KitSkinTones.IndexForTone(appearance.SkinTone);
                look.EyeTone = look.SkinIndex <= 1 ? look.EyeTone : 0.05f * (ClubKitFactory.StableHash(playerId ?? "") % 3u);
            }
            if (appearance.HairStyle != null && TryHairStyle(appearance.HairStyle, out var hair)) look.Hair = hair;
            if (appearance.HasHairColour && KitColorUtility.TryParseHex(appearance.HairColour, out var hairColour))
                look.HairColour = hairColour;
            if (appearance.FacialHair != null && TryFacialHair(appearance.FacialHair, out var facial)) look.Facial = facial;
            if (appearance.HasBootColour && KitColorUtility.TryParseHex(appearance.BootColour, out var boot))
            {
                look.BootBase = boot;
                // Accent: dark trim on light boots, otherwise the seeded accent unless it is too close to the base.
                if (JerseyComparer.GetLuminance(boot) > 0.55f) look.BootAccent = new SimpleColor(0.07f, 0.07f, 0.08f);
                else if (JerseyComparer.ColorDistance(look.BootAccent, boot) < 0.35f) look.BootAccent = SimpleColor.White;
            }
            return look;
        }

        /// <summary>
        /// Roster hair token (RosterAppearance.HairStyles) to the nearest renderable style. "afro" uses the curly cap
        /// and "braids" the close-cropped paint until those looks get their own rendering.
        /// </summary>
        public static bool TryHairStyle(string token, out HairStyle style)
        {
            switch (token)
            {
                case "buzz": style = HairStyle.Buzz; return true;
                case "short": style = HairStyle.Short; return true;
                case "curly": style = HairStyle.Curly; return true;
                case "afro": style = HairStyle.Curly; return true;
                case "long_tied": style = HairStyle.Tied; return true;
                case "bald": style = HairStyle.Bald; return true;
                case "braids": style = HairStyle.Buzz; return true;
                default: style = HairStyle.Short; return false;
            }
        }

        /// <summary>
        /// Roster facial-hair token (RosterAppearance.FacialHairStyles) to the nearest renderable style
        /// ("moustache" uses the goatee paint, which includes a moustache, until it gets its own).
        /// </summary>
        public static bool TryFacialHair(string token, out FacialHair facial)
        {
            switch (token)
            {
                case "none": facial = FacialHair.None; return true;
                case "stubble": facial = FacialHair.Stubble; return true;
                case "beard": facial = FacialHair.Beard; return true;
                case "moustache": facial = FacialHair.Goatee; return true;
                default: facial = FacialHair.None; return false;
            }
        }

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
