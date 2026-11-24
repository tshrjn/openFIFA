namespace OpenFifa.Core
{
    /// <summary>
    /// Auto-fade for the bottom-centre radar: when the ball or the controlled player projects under it (near-side play),
    /// it fades down to <see cref="CoveredAlpha"/> so it no longer hides the action, and fades back when play moves away.
    /// Screen coordinates in pixels, origin bottom-left (Unity screen space).
    /// </summary>
    public static class HudRadarFade
    {
        /// <summary>Radar opacity multiplier while play is under it.</summary>
        public const float CoveredAlpha = 0.15f;
        /// <summary>Seconds for a full fade out or in.</summary>
        public const float FadeSeconds = 0.25f;
        /// <summary>Extra margin around the radar (pixels at 720p, scaled by the screen height).</summary>
        public const float MarginPx720 = 24f;

        /// <summary>True when the point (pixels) lies inside the rectangle grown by <paramref name="margin"/>.</summary>
        public static bool Covers(float minX, float minY, float maxX, float maxY, float px, float py, float margin) =>
            px >= minX - margin && px <= maxX + margin && py >= minY - margin && py <= maxY + margin;

        /// <summary>
        /// True when the vertical segment from (px, footY) to (px, headY) (a player, or the ball with zero height)
        /// overlaps the grown rectangle.
        /// </summary>
        public static bool CoversSpan(float minX, float minY, float maxX, float maxY, float px, float footY, float headY, float margin)
        {
            if (px < minX - margin || px > maxX + margin) return false;
            float lo = footY < headY ? footY : headY, hi = footY < headY ? headY : footY;
            return hi >= minY - margin && lo <= maxY + margin;
        }

        /// <summary>Alpha after <paramref name="dt"/> seconds, moving linearly towards the covered or clear target.</summary>
        public static float Step(float alpha, bool covered, float dt)
        {
            float target = covered ? CoveredAlpha : 1f;
            float step = dt <= 0f ? 0f : dt * (1f - CoveredAlpha) / FadeSeconds;
            if (alpha < target) return alpha + step >= target ? target : alpha + step;
            if (alpha > target) return alpha - step <= target ? target : alpha - step;
            return alpha;
        }
    }
}
