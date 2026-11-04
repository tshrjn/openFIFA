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
    /// OpponentTheta drives the opposing team AI; TeammateTheta drives the human's AI teammates,
    /// which stay competent but never out-think the human's opponent on high levels.
    /// OpponentReactionBonus is extra sharpness for the top levels (subtracted from reaction delay).
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

        public TeamAIDifficulty OpponentDifficulty => new TeamAIDifficulty(OpponentTheta);
        public TeamAIDifficulty TeammateDifficulty => new TeamAIDifficulty(TeammateTheta);
    }

    /// <summary>
    /// Maps each difficulty level to team AI parameters. Values are starting points for playtest tuning.
    /// </summary>
    public static class TeamAIDifficultyLadder
    {
        public const int LevelCount = 7;

        private static readonly float[] OpponentThetas = { 0.05f, 0.22f, 0.40f, 0.58f, 0.75f, 0.90f, 1.0f };
        private static readonly float[] TeammateThetas = { 0.55f, 0.60f, 0.65f, 0.70f, 0.72f, 0.75f, 0.75f };
        private static readonly float[] ReactionBonuses = { 0f, 0f, 0f, 0f, 0f, 0.02f, 0.04f };

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
