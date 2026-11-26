using System;
using System.Globalization;
using System.Text;

namespace OpenFifa.Core
{
    /// <summary>
    /// Builds the URL for a new GitHub issue from the gameplay feedback form
    /// (.github/ISSUE_TEMPLATE/gameplay_feedback.yml), with the title and the build-info textarea filled in.
    /// GitHub documents query-parameter prefill only for text fields, so the dropdowns are left for the player.
    /// Pure C#: the caller passes the result to Application.OpenURL.
    /// </summary>
    public static class FeedbackUrl
    {
        public const string DefaultRepo = "tshrjn/openFIFA";
        public const string Template = "gameplay_feedback.yml";
        public const string BuildInfoField = "build-info";

        /// <summary>Keep well under GitHub's URL limit (it answers 414 for long URLs).</summary>
        public const int MaxFieldLength = 1500;

        public static string Build(FeedbackContext ctx, string repo = DefaultRepo)
        {
            if (string.IsNullOrWhiteSpace(repo) || repo.IndexOf('/') <= 0)
                throw new ArgumentException("repo must look like owner/name", nameof(repo));

            var sb = new StringBuilder();
            sb.Append("https://github.com/").Append(repo.Trim()).Append("/issues/new");
            sb.Append("?template=").Append(Uri.EscapeDataString(Template));
            sb.Append("&title=").Append(Uri.EscapeDataString(Title(ctx)));
            sb.Append('&').Append(BuildInfoField).Append('=').Append(Uri.EscapeDataString(BuildInfo(ctx)));
            return sb.ToString();
        }

        public static string Title(FeedbackContext ctx)
        {
            string platform = Clean(ctx.Platform);
            string version = Clean(ctx.BuildVersion);
            return "Feedback: " + (platform ?? "unknown platform") + (version != null ? ", build " + version : "");
        }

        /// <summary>The text that lands in the build-info textarea, one "key: value" line per known fact.</summary>
        public static string BuildInfo(FeedbackContext ctx)
        {
            var sb = new StringBuilder();
            Line(sb, "build", Join(Clean(ctx.BuildVersion), Clean(ctx.GitSha)));
            Line(sb, "platform", Clean(ctx.Platform));
            Line(sb, "device", Clean(ctx.DeviceModel));
            Line(sb, "os", Clean(ctx.OsVersion));
            Line(sb, "mean fps", ctx.MeanFps > 0f ? ctx.MeanFps.ToString("0.#", CultureInfo.InvariantCulture) : null);
            Line(sb, "input", Clean(ctx.InputScheme));
            Line(sb, "league", Clean(ctx.League));
            Line(sb, "difficulty", Clean(ctx.Difficulty));
            Line(sb, "match length", ctx.MatchMinutes > 0 ? ctx.MatchMinutes.ToString(CultureInfo.InvariantCulture) + " min" : null);

            string text = sb.ToString().TrimEnd('\n');
            return text.Length <= MaxFieldLength ? text : text.Substring(0, MaxFieldLength);
        }

        private static void Line(StringBuilder sb, string key, string value)
        {
            if (value != null) sb.Append(key).Append(": ").Append(value).Append('\n');
        }

        private static string Join(string a, string b)
        {
            if (a == null) return b;
            return b == null ? a : a + " (" + b + ")";
        }

        /// <summary>Trims, flattens newlines so one value stays on one line, caps length; null when empty.</summary>
        private static string Clean(string s)
        {
            if (s == null) return null;
            s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (s.Length == 0) return null;
            return s.Length > 120 ? s.Substring(0, 120) : s;
        }
    }
}
