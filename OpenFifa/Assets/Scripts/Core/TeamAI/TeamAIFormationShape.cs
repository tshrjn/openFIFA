namespace OpenFifa.Core
{
    /// <summary>Block geometry for one phase, in depth units (fraction of pitch length).</summary>
    public readonly struct TeamAIBlockParams
    {
        /// <summary>Lowest allowed back-line depth.</summary>
        public readonly float LineMin;
        /// <summary>Highest allowed back-line depth.</summary>
        public readonly float LineMax;
        /// <summary>Preferred gap from the ball back to the back line.</summary>
        public readonly float BallGap;
        /// <summary>Back line to front line distance.</summary>
        public readonly float BlockLength;
        /// <summary>Fraction of the pitch half-width the block spans.</summary>
        public readonly float Width;
        /// <summary>How strongly the block slides sideways with the ball (0..1).</summary>
        public readonly float LateralShift;

        public TeamAIBlockParams(float lineMin, float lineMax, float ballGap, float blockLength, float width, float lateralShift)
        {
            LineMin = lineMin;
            LineMax = lineMax;
            BallGap = ballGap;
            BlockLength = blockLength;
            Width = width;
            LateralShift = lateralShift;
        }

        public static TeamAIBlockParams ForPhase(TeamAIPhase phase)
        {
            switch (phase)
            {
                case TeamAIPhase.BuildUp: return new TeamAIBlockParams(0.10f, 0.30f, 0.12f, 0.50f, 0.92f, 0.20f);
                case TeamAIPhase.Progression: return new TeamAIBlockParams(0.22f, 0.45f, 0.24f, 0.46f, 0.92f, 0.22f);
                case TeamAIPhase.FinalThird: return new TeamAIBlockParams(0.38f, 0.60f, 0.32f, 0.42f, 0.95f, 0.20f);
                case TeamAIPhase.HighPress: return new TeamAIBlockParams(0.38f, 0.62f, 0.26f, 0.34f, 0.72f, 0.45f);
                case TeamAIPhase.MidBlock: return new TeamAIBlockParams(0.20f, 0.42f, 0.24f, 0.31f, 0.66f, 0.50f);
                case TeamAIPhase.LowBlock: return new TeamAIBlockParams(0.07f, 0.20f, 0.16f, 0.27f, 0.60f, 0.50f);
                case TeamAIPhase.CounterPress: return new TeamAIBlockParams(0.28f, 0.58f, 0.24f, 0.34f, 0.72f, 0.45f);
                case TeamAIPhase.Recovery: return new TeamAIBlockParams(0.08f, 0.30f, 0.26f, 0.34f, 0.68f, 0.40f);
                case TeamAIPhase.SetPieceFor: return new TeamAIBlockParams(0.20f, 0.55f, 0.28f, 0.42f, 0.90f, 0.20f);
                default: return new TeamAIBlockParams(0.07f, 0.30f, 0.20f, 0.30f, 0.62f, 0.45f); // SetPieceAgainst
            }
        }
    }

    /// <summary>
    /// Formation anchors: where each slot should stand given phase, tactics and the ball.
    /// The block slides with the ball laterally and longitudinally and compresses out of possession.
    /// </summary>
    public static class TeamAIFormationShape
    {
        /// <summary>Centre-back lateral factor when the holder drops between them in build-up (1.35 otherwise).</summary>
        public const float HolderDropSplit = 1.7f;

        public static bool InPossession(TeamAIPhase phase) =>
            phase == TeamAIPhase.BuildUp || phase == TeamAIPhase.Progression ||
            phase == TeamAIPhase.FinalThird || phase == TeamAIPhase.SetPieceFor;

        /// <summary>Back-line depth (0..1 from own goal) for this phase, tactics and ball depth.</summary>
        public static float BackLineDepth(TeamAIPhase phase, TeamAITactics tactics, float ballDepth)
        {
            var bp = TeamAIBlockParams.ForPhase(phase);
            float depthShift = (tactics.Depth - 0.5f) * 0.14f;
            float lo = bp.LineMin + depthShift;
            float hi = bp.LineMax + depthShift;
            float line = TeamAIMath.Clamp(ballDepth - bp.BallGap, lo, hi);
            if (!InPossession(phase))
            {
                // Never stand behind ... the ball's own-goal side gap closes when the ball is deep in our half.
                float maxBehindBall = ballDepth - 0.03f;
                if (line > maxBehindBall) line = maxBehindBall;
            }
            return TeamAIMath.Clamp(line, 0.05f, 0.7f);
        }

        /// <summary>
        /// Fill anchors[0..10] (world positions, Y = 0) for every formation slot. Slot 0 (GK) gets a default
        /// spot in front of goal; the goalkeeper logic refines it.
        /// </summary>
        public static float ComputeAnchors(TeamAIPhase phase, TeamAITactics tactics, SimVec3 ball, int attackSign,
            PitchSpec pitch, SimVec3[] anchors)
        {
            var bp = TeamAIBlockParams.ForPhase(phase);
            bool inPossession = InPossession(phase);
            float ballDepth = TeamAIMath.Depth(ball.Z, attackSign, pitch);
            float line = BackLineDepth(phase, tactics, ballDepth);

            float compact = inPossession ? 1f : (1f - 0.25f * tactics.PressingIntensity);
            float blockLen = bp.BlockLength * compact;
            float widthFrac = bp.Width * (0.8f + 0.4f * tactics.Width);
            if (widthFrac > 0.97f) widthFrac = 0.97f;
            float halfW = pitch.HalfWidth;
            float shift = ball.X * bp.LateralShift;
            // Width is spent where the ball is: shrink the far side a little when shifted.
            float margin = 1.5f;

            bool backThree = TeamAIFormations.CentreBacks(tactics.Formation) >= 3;
            int holderSlot = SingleHolder(tactics.Formation);
            bool dropToBuild = phase == TeamAIPhase.BuildUp && tactics.Directness < 0.4f && !backThree && holderSlot >= 0;
            for (int s = 0; s < TeamAIFormations.SlotCount; s++)
            {
                var slot = TeamAIFormations.GetSlot(tactics.Formation, s);
                if (slot.Role == TeamAIRole.GK)
                {
                    float gkDepth = TeamAIMath.Clamp(line - 0.12f, 0.01f, 0.25f);
                    anchors[s] = new SimVec3(ball.X * 0.08f, 0f, TeamAIMath.DepthToZ(gkDepth, attackSign, pitch));
                    continue;
                }

                float blockDepth = slot.BlockDepth;
                float lateral = slot.Lateral;
                if (inPossession)
                {
                    // Full-backs push on, wingers hold the touchline, forwards stretch the last line.
                    if (slot.Role == TeamAIRole.FB) blockDepth += phase == TeamAIPhase.BuildUp ? 0.12f : 0.35f;
                    if (slot.Role == TeamAIRole.W) lateral = lateral < 0f ? -1f : 1f;
                    // Build-up: the centre-backs split; wider still when the holder drops between them.
                    if (slot.Role == TeamAIRole.CB && phase == TeamAIPhase.BuildUp) lateral *= dropToBuild ? HolderDropSplit : 1.35f;
                    if (dropToBuild)
                    {
                        // Patient build-up with a back two: the holding midfielder drops between the split centre-backs
                        // (a back three on the ball) and the full-backs go higher, so there is always a short outlet.
                        if (slot.Role == TeamAIRole.DM && s == holderSlot) { blockDepth = 0.02f; lateral = 0f; }
                        else if (slot.Role == TeamAIRole.FB) blockDepth += 0.18f;
                    }
                }
                else
                {
                    blockDepth = DefensiveBlockDepth(slot, phase, tactics, backThree);
                }

                float depth = line + blockDepth * blockLen;
                if (depth > 0.97f) depth = 0.97f;
                float x = lateral * widthFrac * halfW * attackSign + shift;
                var p = new SimVec3(x, 0f, TeamAIMath.DepthToZ(depth, attackSign, pitch));
                anchors[s] = TeamAIMath.ClampToPitch(p, pitch, margin);
            }
            return line;
        }

        /// <summary>The only DM slot of the formation, or -1 when it has none or two.</summary>
        public static int SingleHolder(TeamAIFormation f)
        {
            int found = -1;
            for (int s = 1; s < TeamAIFormations.SlotCount; s++)
            {
                if (TeamAIFormations.GetSlot(f, s).Role != TeamAIRole.DM) continue;
                if (found >= 0) return -1;
                found = s;
            }
            return found;
        }

        /// <summary>
        /// Out-of-possession block depth of a slot. Wide forwards (4-3-3, 3-4-3) track back into the midfield line in a
        /// mid or low block, so the side defends as a 4-5-1 / 5-4-1; how far depends on how many players the team
        /// commits forward. With a back three the wing-backs drop into a back five. Pressing phases keep the shape.
        /// </summary>
        public static float DefensiveBlockDepth(in TeamAISlot slot, TeamAIPhase phase, TeamAITactics tactics, bool backThree)
        {
            float d = slot.BlockDepth;
            float drop;
            switch (phase)
            {
                case TeamAIPhase.LowBlock:
                case TeamAIPhase.SetPieceAgainst:
                    drop = 1f;
                    break;
                case TeamAIPhase.MidBlock:
                case TeamAIPhase.Recovery:
                    // Half-way in a mid block (0.35 at neutral ForwardRuns): a full drop on every regain/loss turned the
                    // wide forwards into shuttles (HoldWidth running share up by a quarter in full-sim stats).
                    drop = 0.5f * TeamAIMath.Clamp01(1.2f - tactics.ForwardRuns);
                    break;
                default:
                    return d;
            }
            if (slot.Role == TeamAIRole.W && d > 0.8f) return TeamAIMath.Lerp(d, 0.55f, drop);
            if (backThree && slot.Role == TeamAIRole.FB && d > 0.05f) return TeamAIMath.Lerp(d, 0.02f, TeamAIMath.Clamp01(0.4f + 0.6f * drop));
            return d;
        }
    }
}
