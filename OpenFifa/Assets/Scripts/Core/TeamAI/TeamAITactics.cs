namespace OpenFifa.Core
{
    /// <summary>One slot of a formation template.</summary>
    public readonly struct TeamAISlot
    {
        public readonly TeamAIRole Role;
        /// <summary>Lateral position -1 (left) .. +1 (right), from the team's attacking point of view.</summary>
        public readonly float Lateral;
        /// <summary>Position inside the block: 0 = back line, 1 = front line.</summary>
        public readonly float BlockDepth;

        public TeamAISlot(TeamAIRole role, float lateral, float blockDepth)
        {
            Role = role;
            Lateral = lateral;
            BlockDepth = blockDepth;
        }
    }

    /// <summary>Static formation templates (11 slots, slot 0 is always the goalkeeper).</summary>
    public static class TeamAIFormations
    {
        public const int SlotCount = 11;

        private static readonly TeamAISlot[] F433 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.3f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.3f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.FB, 0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.DM, 0f, 0.3f),
            new TeamAISlot(TeamAIRole.CM, -0.42f, 0.52f),
            new TeamAISlot(TeamAIRole.CM, 0.42f, 0.52f),
            new TeamAISlot(TeamAIRole.W, -0.85f, 0.92f),
            new TeamAISlot(TeamAIRole.W, 0.85f, 0.92f),
            new TeamAISlot(TeamAIRole.ST, 0f, 1f),
        };

        private static readonly TeamAISlot[] F4231 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.3f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.3f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.FB, 0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.DM, -0.25f, 0.3f),
            new TeamAISlot(TeamAIRole.DM, 0.25f, 0.3f),
            new TeamAISlot(TeamAIRole.AM, 0f, 0.66f),
            new TeamAISlot(TeamAIRole.W, -0.8f, 0.72f),
            new TeamAISlot(TeamAIRole.W, 0.8f, 0.72f),
            new TeamAISlot(TeamAIRole.ST, 0f, 1f),
        };

        private static readonly TeamAISlot[] F442 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.3f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.3f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.FB, 0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.CM, -0.25f, 0.45f),
            new TeamAISlot(TeamAIRole.CM, 0.25f, 0.45f),
            new TeamAISlot(TeamAIRole.W, -0.85f, 0.48f),
            new TeamAISlot(TeamAIRole.W, 0.85f, 0.48f),
            new TeamAISlot(TeamAIRole.ST, -0.2f, 1f),
            new TeamAISlot(TeamAIRole.ST, 0.2f, 1f),
        };

        private static readonly TeamAISlot[] F352 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.45f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.45f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.92f, 0.38f),
            new TeamAISlot(TeamAIRole.FB, 0.92f, 0.38f),
            new TeamAISlot(TeamAIRole.DM, 0f, 0.3f),
            new TeamAISlot(TeamAIRole.CM, -0.4f, 0.55f),
            new TeamAISlot(TeamAIRole.CM, 0.4f, 0.55f),
            new TeamAISlot(TeamAIRole.ST, -0.2f, 1f),
            new TeamAISlot(TeamAIRole.ST, 0.2f, 1f),
        };

        public static TeamAISlot GetSlot(TeamAIFormation f, int slot)
        {
            switch (f)
            {
                case TeamAIFormation.F4231: return F4231[slot];
                case TeamAIFormation.F442: return F442[slot];
                case TeamAIFormation.F352: return F352[slot];
                default: return F433[slot];
            }
        }

        public static TeamAIRole RoleOf(TeamAIFormation f, int slot) => GetSlot(f, slot).Role;
    }

    /// <summary>Team tactic sliders (all 0..1) plus formation.</summary>
    public sealed class TeamAITactics
    {
        public TeamAIFormation Formation = TeamAIFormation.F433;
        /// <summary>0 = narrow, 1 = very wide.</summary>
        public float Width = 0.5f;
        /// <summary>0 = deep defensive line, 1 = very high line.</summary>
        public float Depth = 0.5f;
        /// <summary>0 = never press, 1 = relentless press and counter-press.</summary>
        public float PressingIntensity = 0.5f;

        public TeamAITactics() { }

        public TeamAITactics(TeamAIFormation formation, float width, float depth, float pressing)
        {
            Formation = formation;
            Width = TeamAIMath.Clamp01(width);
            Depth = TeamAIMath.Clamp01(depth);
            PressingIntensity = TeamAIMath.Clamp01(pressing);
        }
    }
}
