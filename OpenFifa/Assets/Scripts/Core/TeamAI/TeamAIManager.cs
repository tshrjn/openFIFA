namespace OpenFifa.Core
{
    /// <summary>
    /// The AI manager on the touchline: reads the score, the clock and the numbers on the pitch and picks a mentality,
    /// the way a real coach sends a side forward when chasing a game and sits deep to see out a lead.
    /// Changes go in at a stoppage (or after the wish has held for <see cref="ChangeDelay"/> seconds of open play), so
    /// the shape does not flicker with every goal-line scramble. Deterministic and allocation free.
    /// </summary>
    public sealed class TeamAIManager
    {
        /// <summary>Seconds of open play a new mentality must be wanted before it goes in without a stoppage.</summary>
        public const float ChangeDelay = 20f;

        /// <summary>Off by default; the session turns it on for AI-run sides.</summary>
        public bool Enabled;
        /// <summary>Most attacking mentality this manager will pick, 0..2 (see <see cref="ForDifficulty"/>).</summary>
        public int MaxAttack = 2;
        /// <summary>Most defensive mentality this manager will pick, as a positive step count 0..2.</summary>
        public int MaxDefend = 2;
        /// <summary>
        /// How fully the side carries out its style, 0..1: each style slider is pulled toward neutral (0.5) by
        /// 1 - StyleExecution before mentality is added. A weak AI side presses and breaks like its style only half-way,
        /// so a pressing club on Beginner still plays like a Beginner side. 1 = as written.
        /// </summary>
        public float StyleExecution = 1f;
        /// <summary>Mentality currently in force (added to the base tactics' own mentality).</summary>
        public TeamAIMentality Current { get; private set; } = TeamAIMentality.Balanced;
        /// <summary>What the manager would play now, before the change delay.</summary>
        public TeamAIMentality Wanted { get; private set; } = TeamAIMentality.Balanced;
        /// <summary>Number of mentality changes so far (tests, commentary).</summary>
        public int Changes { get; private set; }

        private float _wantedSince = -1f;

        public void Reset()
        {
            Current = TeamAIMentality.Balanced;
            Wanted = TeamAIMentality.Balanced;
            _wantedSince = -1f;
            Changes = 0;
        }

        /// <summary>
        /// Mentality for a goal difference (ours minus theirs), match progress (0..1) and player advantage (our active
        /// players minus theirs). Pure function.
        /// </summary>
        public static TeamAIMentality Choose(int goalDiff, float progress, int playerDiff)
        {
            int m = 0;
            if (goalDiff < 0)
            {
                if (progress > 0.85f || (goalDiff <= -2 && progress > 0.6f)) m = 2;
                else if (progress > 0.55f || goalDiff <= -2) m = 1;
            }
            else if (goalDiff > 0)
            {
                if (goalDiff == 1)
                {
                    if (progress > 0.85f) m = -2;
                    else if (progress > 0.65f) m = -1;
                }
                else if (progress > 0.75f)
                {
                    m = -1; // comfortable: keep the ball, don't throw bodies forward
                }
            }
            if (playerDiff < 0) m -= 1;
            else if (playerDiff > 0 && goalDiff <= 0) m += 1;
            if (m < -2) m = -2;
            if (m > 2) m = 2;
            return (TeamAIMentality)m;
        }

        /// <summary>
        /// Settings for an AI side of difficulty theta (0..1). Chasing a game: no change on Beginner and Amateur
        /// (theta under 0.3: a weak side that pushes on when behind takes the user's comeback away), one step
        /// (Attacking) up to World Class, all-out attack at Legendary and Ultimate. Protecting a lead: one step (Defensive) below World Class
        /// (theta 0.75), none above it, so a top side that leads late keeps threatening instead of sitting deep (which
        /// compressed the ladder: Ultimate goals fell by about a third). Style execution 0.25 at theta 0 to 1 at theta 1.
        /// </summary>
        public void ForDifficulty(float theta)
        {
            Enabled = true;
            MaxAttack = theta >= 0.9f ? 2 : (theta >= 0.3f ? 1 : 0);
            MaxDefend = theta >= 0.75f ? 0 : 1;
            StyleExecution = TeamAIMath.Lerp(0.25f, 1f, TeamAIMath.Clamp01(theta));
        }

        /// <summary>Advance: decide the wanted mentality and apply it when the moment allows.</summary>
        public void Update(TeamAIWorld world, int team)
        {
            if (!Enabled) return;
            int us = world.Score[team], them = world.Score[1 - team];
            int ours = 0, theirs = 0;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!world.Players[i].Active) continue;
                if (world.Players[i].Team == team) ours++; else theirs++;
            }
            var want = Choose(us - them, world.MatchProgress, ours - theirs);
            if ((int)want > MaxAttack) want = (TeamAIMentality)MaxAttack;
            if ((int)want < -MaxDefend) want = (TeamAIMentality)(-MaxDefend);
            if (want != Wanted)
            {
                Wanted = want;
                _wantedSince = world.Time;
            }
            if (Wanted == Current) return;
            bool dead = world.MatchPhase != TeamAIMatchPhase.InPlay;
            if (dead || (_wantedSince >= 0f && world.Time - _wantedSince >= ChangeDelay))
            {
                Current = Wanted;
                Changes++;
            }
        }

        /// <summary>
        /// Effective tactics: <paramref name="baseTactics"/> shifted by its own mentality plus <paramref name="extra"/>.
        /// One step = +0.12 line height and pressing, +0.15 forward runs; all-out attack also goes long, ultra
        /// defensive clears long and narrows.
        /// </summary>
        public static void Apply(TeamAITactics baseTactics, TeamAIMentality extra, TeamAITactics output) =>
            Apply(baseTactics, extra, 1f, output);

        /// <summary>As above, with each style slider first pulled toward 0.5 by 1 - <paramref name="execution"/>.</summary>
        public static void Apply(TeamAITactics baseTactics, TeamAIMentality extra, float execution, TeamAITactics output)
        {
            output.CopyFrom(baseTactics);
            if (execution < 1f)
            {
                float k = TeamAIMath.Clamp01(execution);
                output.Width = 0.5f + (output.Width - 0.5f) * k;
                output.Depth = 0.5f + (output.Depth - 0.5f) * k;
                output.PressingIntensity = 0.5f + (output.PressingIntensity - 0.5f) * k;
                output.Directness = 0.5f + (output.Directness - 0.5f) * k;
                output.ForwardRuns = 0.5f + (output.ForwardRuns - 0.5f) * k;
                output.CounterAttack = 0.5f + (output.CounterAttack - 0.5f) * k;
            }
            int m = (int)baseTactics.Mentality + (int)extra;
            if (m < -2) m = -2;
            if (m > 2) m = 2;
            output.Mentality = (TeamAIMentality)m;
            if (m == 0) return;
            output.Depth = TeamAIMath.Clamp01(output.Depth + 0.12f * m);
            output.PressingIntensity = TeamAIMath.Clamp01(output.PressingIntensity + 0.12f * m);
            output.ForwardRuns = TeamAIMath.Clamp01(output.ForwardRuns + 0.15f * m);
            output.Width = TeamAIMath.Clamp01(output.Width + 0.05f * m);
            if (m >= 2) output.Directness = TeamAIMath.Clamp01(output.Directness + 0.25f);
            if (m <= -2) output.Directness = TeamAIMath.Clamp01(output.Directness + 0.2f);
        }
    }
}
