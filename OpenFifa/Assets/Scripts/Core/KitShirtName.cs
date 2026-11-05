using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Shirt lettering from roster names: upper case, diacritics removed and the few letters that do not decompose
    /// mapped by hand (Đ→D, Ø→O, Ł→L, ß→SS, Æ→AE, Œ→OE, Þ→TH, ı→I), so the kit print font always has a glyph.
    /// Pure C#.
    /// </summary>
    public static class KitShirtName
    {
        /// <summary>The roster's shirt name when given, else the known-as name; ASCII-folded and upper case.</summary>
        public static string For(string shirtName, string knownAs)
        {
            string source = !string.IsNullOrWhiteSpace(shirtName) ? shirtName : knownAs;
            return Fold(source);
        }

        /// <summary>ASCII upper-case fold of a name (letters, digits, space, hyphen, apostrophe and dot kept).</summary>
        public static string Fold(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var decomposed = name.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                switch (c)
                {
                    case 'Đ': case 'đ': sb.Append('D'); continue;
                    case 'Ø': case 'ø': sb.Append('O'); continue;
                    case 'Ł': case 'ł': sb.Append('L'); continue;
                    case 'ß': sb.Append("SS"); continue;
                    case 'Æ': case 'æ': sb.Append("AE"); continue;
                    case 'Œ': case 'œ': sb.Append("OE"); continue;
                    case 'Þ': case 'þ': sb.Append("TH"); continue;
                    case 'ı': sb.Append('I'); continue;
                }
                char u = char.ToUpperInvariant(c);
                if ((u >= 'A' && u <= 'Z') || (u >= '0' && u <= '9') || u == ' ' || u == '-' || u == '\'' || u == '.')
                    sb.Append(u);
            }
            return sb.ToString().Trim();
        }
    }
}
