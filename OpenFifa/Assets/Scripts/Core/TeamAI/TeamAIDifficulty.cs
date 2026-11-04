namespace OpenFifa.Core
{
    /// <summary>
    /// Difficulty scaling for the team AI from a single 0..1 value (theta).
    /// 0 = slow, noisy, error-prone; 1 = fast, sharp, accurate.
    /// </summary>
    public readonly struct TeamAIDifficulty
    {
        public readonly float Theta;
        /// <summary>Seconds before a player reacts to a new situation (ball won, press trigger).</summary>
        public readonly float ReactionDelay;
        /// <summary>Half-width of uniform noise added to every utility score.</summary>
        public readonly float DecisionNoise;
        /// <summary>Standard deviation (radians) of aim error on passes and shots.</summary>
        public readonly float AimError;
        /// <summary>Standard deviation of power error (fraction of power).</summary>
        public readonly float PowerError;
        /// <summary>Multiplier on pressing distance and willingness.</summary>
        public readonly float PressAggression;
        /// <summary>Probability that an off-ball player misses a run or support cue on a tick.</summary>
        public readonly float LapseChance;

        /// <summary>Extra sharpness (s) already subtracted from <see cref="ReactionDelay"/>.</summary>
        public readonly float ReactionBonus;

        /// <summary>Shortest reaction delay any bonus can produce (s).</summary>
        public const float MinReactionDelay = 0.03f;

        public TeamAIDifficulty(float theta) : this(theta, 0f) { }

        /// <summary>
        /// Difficulty from theta plus a reaction bonus in seconds (the ladder's OpponentReactionBonus), subtracted from
        /// the reaction delay. The bonus moves press triggers and on-ball reactions earlier.
        /// </summary>
        public TeamAIDifficulty(float theta, float reactionBonus)
        {
            float t = TeamAIMath.Clamp01(theta);
            Theta = t;
            ReactionBonus = reactionBonus > 0f ? reactionBonus : 0f;
            ReactionDelay = TeamAIMath.Lerp(0.55f, 0.08f, t) - ReactionBonus;
            if (ReactionDelay < MinReactionDelay) ReactionDelay = MinReactionDelay;
            DecisionNoise = TeamAIMath.Lerp(0.30f, 0.02f, t);
            AimError = TeamAIMath.Lerp(0.14f, 0.015f, t);
            PowerError = TeamAIMath.Lerp(0.18f, 0.02f, t);
            PressAggression = TeamAIMath.Lerp(0.7f, 1.15f, t);
            LapseChance = TeamAIMath.Lerp(0.25f, 0.0f, t);
        }
    }
}
