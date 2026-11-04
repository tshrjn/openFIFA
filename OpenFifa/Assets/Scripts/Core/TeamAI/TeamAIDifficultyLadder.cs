namespace OpenFifa.Core
{
    /// <summary>
    /// Single-player difficulty levels, a seven-step ladder like modern football games.
    /// </summary>
    public enum TeamAIDifficultyLevel
    {
        Beginner = 0,
        Amateur = 1,
        SemiPro = 2,
        Professional = 3,
        WorldClass = 4,
        Legendary = 5,
        Ultimate = 6
    }

    /// <summary>
    /// What a difficulty level means for a single-player match against the AI.
    /// OpponentTheta drives the opposing team AI; TeammateTheta drives the human's AI teammates, which stay competent
    /// (0.64-0.78) and ease off slightly up the ladder, so a higher level never hands the user better teammates.
    /// OpponentReactionBonus is extra sharpness for the top levels (subtracted from reaction delay).
    /// Every value moves one way with the level (see SinglePlayerSessionTests for the measured 7-level ordering).
    /// </summary>
    public readonly struct TeamAIDifficultyProfile
    {
        public readonly TeamAIDifficultyLevel Level;
        public readonly float OpponentTheta;
        public readonly float TeammateTheta;
        public readonly float OpponentReactionBonus;

        public TeamAIDifficultyProfile(TeamAIDifficultyLevel level, float opponentTheta, float teammateTheta,
            float opponentReactionBonus)
        {
            Level = level;
            OpponentTheta = opponentTheta;
            TeammateTheta = teammateTheta;
            OpponentReactionBonus = opponentReactionBonus;
        }

        /// <summary>Opponent AI difficulty, with <see cref="OpponentReactionBonus"/> applied to the reaction delay.</summary>
        public TeamAIDifficulty OpponentDifficulty => new TeamAIDifficulty(OpponentTheta, OpponentReactionBonus);
        public TeamAIDifficulty TeammateDifficulty => new TeamAIDifficulty(TeammateTheta);
    }

    /// <summary>
    /// Maps each difficulty level to team AI parameters. Values are starting points for playtest tuning.
    /// </summary>
    public static class TeamAIDifficultyLadder
    {
        public const int LevelCount = 7;

        // Theta's effect on results is flat below about 0.4 and saturates near 0.75 (theta 0.75 and 1.0 played
        // alike in Nov 2026 measurements), so the spacing is tightest in the 0.45-0.75 band; the ends of the ladder
        // are separated by SinglePlayerDifficulty's opponent outfield ratings, keepers and assists as well.
        private static readonly float[] OpponentThetas = { 0.08f, 0.28f, 0.45f, 0.58f, 0.72f, 0.87f, 1.0f };
        private static readonly float[] TeammateThetas = { 0.78f, 0.75f, 0.72f, 0.70f, 0.68f, 0.66f, 0.64f };
        private static readonly float[] ReactionBonuses = { 0f, 0f, 0f, 0f, 0.01f, 0.03f, 0.05f };

        public static TeamAIDifficultyProfile For(TeamAIDifficultyLevel level)
        {
            int i = (int)level;
            if (i < 0) i = 0;
            if (i >= LevelCount) i = LevelCount - 1;
            return new TeamAIDifficultyProfile((TeamAIDifficultyLevel)i, OpponentThetas[i], TeammateThetas[i],
                ReactionBonuses[i]);
        }

        public static string DisplayName(TeamAIDifficultyLevel level)
        {
            switch (level)
            {
                case TeamAIDifficultyLevel.Beginner: return "Beginner";
                case TeamAIDifficultyLevel.Amateur: return "Amateur";
                case TeamAIDifficultyLevel.SemiPro: return "Semi-Pro";
                case TeamAIDifficultyLevel.Professional: return "Professional";
                case TeamAIDifficultyLevel.WorldClass: return "World Class";
                case TeamAIDifficultyLevel.Legendary: return "Legendary";
                default: return "Ultimate";
            }
        }
    }
}
