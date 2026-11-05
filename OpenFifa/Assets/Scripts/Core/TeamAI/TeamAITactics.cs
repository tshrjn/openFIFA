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

        private static readonly TeamAISlot[] F4141 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.3f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.3f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.FB, 0.85f, 0.03f),
            new TeamAISlot(TeamAIRole.DM, 0f, 0.28f),
            new TeamAISlot(TeamAIRole.CM, -0.32f, 0.56f),
            new TeamAISlot(TeamAIRole.CM, 0.32f, 0.56f),
            new TeamAISlot(TeamAIRole.W, -0.85f, 0.6f),
            new TeamAISlot(TeamAIRole.W, 0.85f, 0.6f),
            new TeamAISlot(TeamAIRole.ST, 0f, 1f),
        };

        private static readonly TeamAISlot[] F41212 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.3f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.3f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.88f, 0.05f),
            new TeamAISlot(TeamAIRole.FB, 0.88f, 0.05f),
            new TeamAISlot(TeamAIRole.DM, 0f, 0.28f),
            new TeamAISlot(TeamAIRole.CM, -0.45f, 0.48f),
            new TeamAISlot(TeamAIRole.CM, 0.45f, 0.48f),
            new TeamAISlot(TeamAIRole.AM, 0f, 0.7f),
            new TeamAISlot(TeamAIRole.ST, -0.22f, 1f),
            new TeamAISlot(TeamAIRole.ST, 0.22f, 1f),
        };

        private static readonly TeamAISlot[] F343 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.45f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.45f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.92f, 0.4f),
            new TeamAISlot(TeamAIRole.FB, 0.92f, 0.4f),
            new TeamAISlot(TeamAIRole.CM, -0.25f, 0.42f),
            new TeamAISlot(TeamAIRole.CM, 0.25f, 0.42f),
            new TeamAISlot(TeamAIRole.W, -0.72f, 0.92f),
            new TeamAISlot(TeamAIRole.W, 0.72f, 0.92f),
            new TeamAISlot(TeamAIRole.ST, 0f, 1f),
        };

        private static readonly TeamAISlot[] F532 =
        {
            new TeamAISlot(TeamAIRole.GK, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, -0.42f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0f, 0f),
            new TeamAISlot(TeamAIRole.CB, 0.42f, 0f),
            new TeamAISlot(TeamAIRole.FB, -0.9f, 0.1f),
            new TeamAISlot(TeamAIRole.FB, 0.9f, 0.1f),
            new TeamAISlot(TeamAIRole.DM, 0f, 0.38f),
            new TeamAISlot(TeamAIRole.CM, -0.4f, 0.52f),
            new TeamAISlot(TeamAIRole.CM, 0.4f, 0.52f),
            new TeamAISlot(TeamAIRole.ST, -0.2f, 1f),
            new TeamAISlot(TeamAIRole.ST, 0.2f, 1f),
        };

        /// <summary>Every formation template, in enum order.</summary>
        public static readonly TeamAIFormation[] All =
        {
            TeamAIFormation.F433, TeamAIFormation.F4231, TeamAIFormation.F442, TeamAIFormation.F352,
            TeamAIFormation.F4141, TeamAIFormation.F41212, TeamAIFormation.F343, TeamAIFormation.F532
        };

        /// <summary>Number of centre-backs in the template (2 = back four, 3 = back three or five).</summary>
        public static int CentreBacks(TeamAIFormation f)
        {
            int n = 0;
            for (int s = 1; s < SlotCount; s++) if (GetSlot(f, s).Role == TeamAIRole.CB) n++;
            return n;
        }

        /// <summary>Display name, e.g. "4-3-3".</summary>
        public static string Name(TeamAIFormation f)
        {
            switch (f)
            {
                case TeamAIFormation.F4231: return "4-2-3-1";
                case TeamAIFormation.F442: return "4-4-2";
                case TeamAIFormation.F352: return "3-5-2";
                case TeamAIFormation.F4141: return "4-1-4-1";
                case TeamAIFormation.F41212: return "4-1-2-1-2";
                case TeamAIFormation.F343: return "3-4-3";
                case TeamAIFormation.F532: return "5-3-2";
                default: return "4-3-3";
            }
        }

        public static TeamAISlot GetSlot(TeamAIFormation f, int slot)
        {
            switch (f)
            {
                case TeamAIFormation.F4141: return F4141[slot];
                case TeamAIFormation.F41212: return F41212[slot];
                case TeamAIFormation.F343: return F343[slot];
                case TeamAIFormation.F532: return F532[slot];
                case TeamAIFormation.F4231: return F4231[slot];
                case TeamAIFormation.F442: return F442[slot];
                case TeamAIFormation.F352: return F352[slot];
                default: return F433[slot];
            }
        }

        public static TeamAIRole RoleOf(TeamAIFormation f, int slot) => GetSlot(f, slot).Role;
    }

    /// <summary>
    /// Team instructions: formation, shape sliders, style sliders and mentality. All sliders are 0..1 and 0.5 is the
    /// neutral (balanced) value, so a default-constructed instance plays exactly as the AI did before styles existed.
    /// </summary>
    public sealed class TeamAITactics
    {
        public TeamAIFormation Formation = TeamAIFormation.F433;
        /// <summary>0 = narrow, 1 = very wide.</summary>
        public float Width = 0.5f;
        /// <summary>0 = deep defensive line, 1 = very high line.</summary>
        public float Depth = 0.5f;
        /// <summary>0 = never press, 1 = relentless press and counter-press.</summary>
        public float PressingIntensity = 0.5f;
        /// <summary>
        /// Build-up: 0 = patient short passing (the keeper and centre-backs split, the holding midfielder drops in),
        /// 1 = long balls forward. Scales how much a pass's forward progress is worth against its interception risk.
        /// </summary>
        public float Directness = 0.5f;
        /// <summary>
        /// Chance creation: how many players break beyond the ball (0 = one runner and no overlaps, 0.5 = two,
        /// 1 = three, midfielders arriving late in the box, full-backs always overlapping).
        /// </summary>
        public float ForwardRuns = 0.5f;
        /// <summary>
        /// Transition after winning the ball: above 0.5 the team breaks at once (runners go immediately, the first
        /// passes are vertical) for a few seconds; 0.5 and below is the plain possession game.
        /// </summary>
        public float CounterAttack = 0.5f;
        /// <summary>Mentality on top of the sliders (the manager changes it during a match, see <see cref="TeamAIManager"/>).</summary>
        public TeamAIMentality Mentality = TeamAIMentality.Balanced;
        /// <summary>The named style these sliders came from (informational: HUD, commentary).</summary>
        public TeamAIStyle Style = TeamAIStyle.Balanced;

        public TeamAITactics() { }

        public TeamAITactics(TeamAIFormation formation, float width, float depth, float pressing)
        {
            Formation = formation;
            Width = TeamAIMath.Clamp01(width);
            Depth = TeamAIMath.Clamp01(depth);
            PressingIntensity = TeamAIMath.Clamp01(pressing);
        }

        /// <summary>Copy every field of <paramref name="o"/> into this instance (no allocation).</summary>
        public void CopyFrom(TeamAITactics o)
        {
            Formation = o.Formation;
            Width = o.Width;
            Depth = o.Depth;
            PressingIntensity = o.PressingIntensity;
            Directness = o.Directness;
            ForwardRuns = o.ForwardRuns;
            CounterAttack = o.CounterAttack;
            Mentality = o.Mentality;
            Style = o.Style;
        }

        public TeamAITactics Clone()
        {
            var t = new TeamAITactics();
            t.CopyFrom(this);
            return t;
        }

        /// <summary>Tactics for a named style in the given formation.</summary>
        public static TeamAITactics FromStyle(TeamAIStyle style, TeamAIFormation formation = TeamAIFormation.F433)
        {
            var t = new TeamAITactics { Formation = formation, Style = style };
            switch (style)
            {
                case TeamAIStyle.Possession:
                    t.Set(width: 0.6f, depth: 0.65f, press: 0.65f, direct: 0.25f, runs: 0.8f, counter: 0.3f);
                    break;
                case TeamAIStyle.HighPress:
                    t.Set(width: 0.55f, depth: 0.85f, press: 0.95f, direct: 0.55f, runs: 0.6f, counter: 0.75f);
                    break;
                case TeamAIStyle.CounterAttack:
                    t.Set(width: 0.45f, depth: 0.3f, press: 0.3f, direct: 0.5f, runs: 0.5f, counter: 0.8f);
                    break;
                case TeamAIStyle.LongBall:
                    t.Set(width: 0.5f, depth: 0.4f, press: 0.4f, direct: 0.7f, runs: 0.45f, counter: 0.5f);
                    break;
                case TeamAIStyle.WingPlay:
                    t.Set(width: 0.95f, depth: 0.5f, press: 0.5f, direct: 0.55f, runs: 0.75f, counter: 0.5f);
                    break;
                case TeamAIStyle.ParkTheBus:
                    t.Set(width: 0.4f, depth: 0.1f, press: 0.3f, direct: 0.5f, runs: 0.1f, counter: 0.5f);
                    // Built-in defensive mentality: the block stays deep in possession too and few players go forward,
                    // so the side scores and concedes least (the manager's mentality adds on top).
                    t.Mentality = TeamAIMentality.Defensive;
                    break;
            }
            return t;
        }

        /// <summary>Parse a style name ("possession", "high-press", "Park The Bus"...); false when unknown.</summary>
        public static bool TryParseStyle(string name, out TeamAIStyle style)
        {
            style = TeamAIStyle.Balanced;
            if (string.IsNullOrEmpty(name)) return false;
            var k = name.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
            switch (k)
            {
                case "balanced": style = TeamAIStyle.Balanced; return true;
                case "possession": case "tikitaka": style = TeamAIStyle.Possession; return true;
                case "highpress": case "gegenpress": case "pressing": style = TeamAIStyle.HighPress; return true;
                case "counter": case "counterattack": style = TeamAIStyle.CounterAttack; return true;
                case "longball": case "direct": style = TeamAIStyle.LongBall; return true;
                case "wingplay": case "wide": style = TeamAIStyle.WingPlay; return true;
                case "parkthebus": case "lowblock": style = TeamAIStyle.ParkTheBus; return true;
                default: return false;
            }
        }

        private void Set(float width, float depth, float press, float direct, float runs, float counter)
        {
            Width = width;
            Depth = depth;
            PressingIntensity = press;
            Directness = direct;
            ForwardRuns = runs;
            CounterAttack = counter;
        }

        /// <summary>Most players who may run beyond the ball at once (1..3).</summary>
        public int MaxRunners => ForwardRuns < 0.25f ? 1 : (ForwardRuns < 0.75f ? 2 : 3);

        /// <summary>0..1: how hard the team breaks after winning the ball (0 at CounterAttack 0.5 or below).</summary>
        public float CounterBoost => TeamAIMath.Clamp01((CounterAttack - 0.5f) * 2f);
    }
}
