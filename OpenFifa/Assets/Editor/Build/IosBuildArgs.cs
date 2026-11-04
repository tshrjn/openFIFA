using System.Collections.Generic;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Command-line option lookup for <see cref="IosBuild"/>, kept free of engine types so it can be checked outside Unity.
    /// </summary>
    internal static class IosBuildArgs
    {
        /// <summary>
        /// True when <paramref name="token"/> is another option ("-batchmode", "-ipadExports"), not a value.
        /// A dash followed by a digit (e.g. "-1") still counts as a value.
        /// </summary>
        public static bool LooksLikeFlag(string token) =>
            token != null && token.Length > 1 && token[0] == '-' && !char.IsDigit(token[1]);

        /// <summary>
        /// Finds "-name value" in <paramref name="args"/>. Returns false when the option is absent.
        /// When the option is present but has no value (it is the last token, or the next token is another
        /// option, as happens when CI passes an unquoted empty variable like <c>-appleTeamId $APPLE_TEAM_ID</c>),
        /// it returns true with an empty value and does not consume the next option.
        /// </summary>
        public static bool TryGet(IReadOnlyList<string> args, string name, out string value)
        {
            value = string.Empty;
            if (args == null) return false;
            string flag = "-" + name;
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] != flag) continue;
                if (i + 1 < args.Count && !LooksLikeFlag(args[i + 1]))
                {
                    value = (args[i + 1] ?? string.Empty).Trim();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resolves an option: a non-empty command-line value wins, then a non-empty environment value,
        /// then <paramref name="fallback"/>. An empty value never swallows the following option.
        /// </summary>
        public static string Resolve(IReadOnlyList<string> args, string name, string envValue, string fallback)
        {
            if (TryGet(args, name, out var value) && value.Length > 0) return value;
            return string.IsNullOrEmpty(envValue) ? fallback : envValue.Trim();
        }
    }
}
