using System;
using System.Globalization;

namespace OpenFifa.Core
{
    /// <summary>
    /// Hex colour parsing/formatting for kit data. Pure C#.
    /// </summary>
    public static class KitColorUtility
    {
        /// <summary>
        /// Parses "#RGB", "#RRGGBB" or "#RRGGBBAA" (leading '#' optional) into a SimpleColor.
        /// </summary>
        public static bool TryParseHex(string hex, out SimpleColor color)
        {
            color = SimpleColor.White;
            if (string.IsNullOrEmpty(hex)) return false;

            var s = hex.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);

            if (s.Length == 3)
            {
                s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            }

            if (s.Length != 6 && s.Length != 8) return false;

            uint value;
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return false;

            if (s.Length == 6)
            {
                value = (value << 8) | 0xFF;
            }

            color = new SimpleColor(
                ((value >> 24) & 0xFF) / 255f,
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f);
            return true;
        }

        /// <summary>
        /// Parses a hex colour, returning <paramref name="fallback"/> when invalid.
        /// </summary>
        public static SimpleColor ParseOrDefault(string hex, SimpleColor fallback)
        {
            SimpleColor c;
            return TryParseHex(hex, out c) ? c : fallback;
        }

        /// <summary>
        /// Formats a colour as "#RRGGBB" (or "#RRGGBBAA" when alpha is below 1).
        /// </summary>
        public static string ToHex(SimpleColor color)
        {
            int r = ToByte(color.R);
            int g = ToByte(color.G);
            int b = ToByte(color.B);
            int a = ToByte(color.A);
            return a == 255
                ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", r, g, b)
                : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", r, g, b, a);
        }

        /// <summary>
        /// Converts a 0-1 channel to a 0-255 byte with rounding and clamping.
        /// </summary>
        public static byte ToByte(float channel)
        {
            if (channel <= 0f) return 0;
            if (channel >= 1f) return 255;
            return (byte)(channel * 255f + 0.5f);
        }
    }
}
