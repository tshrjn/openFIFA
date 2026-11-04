using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Maps a <see cref="MatchSetup"/> kick-off shape (11 spots in the attack frame, slot 0 = GK) to team AI formation
    /// slots and roles. The sim's squad order and the team AI's template order differ (the sim's default 4-4-2 lists
    /// LB, CB, CB, RB, LM...; the AI's lists CB, CB, FB, FB...), so each sim slot is matched to the template slot whose
    /// normalised (lateral, depth) is closest. Greedy global-minimum matching with index tie-breaks: deterministic.
    /// </summary>
    public static class SimFormationMapper
    {
        private static readonly TeamAIFormation[] All = TeamAIFormations.All;

        /// <summary>
        /// Fills <paramref name="aiSlotOfSimSlot"/> (length 11) with the template slot of each sim slot and returns the
        /// total matching cost (lower = better fit).
        /// </summary>
        public static float Assign(SimVec3[] spots, TeamAIFormation formation, int[] aiSlotOfSimSlot)
        {
            int n = MatchSetup.PlayersPerTeam;
            if (spots == null || spots.Length != n) throw new ArgumentException("need 11 formation spots");
            if (aiSlotOfSimSlot == null || aiSlotOfSimSlot.Length != n) throw new ArgumentException("need an 11-slot output");

            // Normalise the outfield spots: lateral by the widest player, depth 0 (back line) .. 1 (front line).
            float maxAbsX = 1e-3f, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int s = 1; s < n; s++)
            {
                maxAbsX = Math.Max(maxAbsX, Math.Abs(spots[s].X));
                minZ = Math.Min(minZ, spots[s].Z);
                maxZ = Math.Max(maxZ, spots[s].Z);
            }
            float zRange = Math.Max(maxZ - minZ, 1e-3f);

            var cost = new float[n, n];
            for (int s = 1; s < n; s++)
            {
                float lat = spots[s].X / maxAbsX;
                float dep = (spots[s].Z - minZ) / zRange;
                for (int t = 1; t < n; t++)
                {
                    var slot = TeamAIFormations.GetSlot(formation, t);
                    float dl = lat - slot.Lateral;
                    float dd = (dep - slot.BlockDepth) * 1.5f; // lines matter more than width
                    cost[s, t] = dl * dl + dd * dd;
                }
            }

            var simUsed = new bool[n];
            var aiUsed = new bool[n];
            aiSlotOfSimSlot[0] = 0;
            float total = 0f;
            for (int k = 1; k < n; k++)
            {
                int bs = -1, bt = -1;
                float best = float.MaxValue;
                for (int s = 1; s < n; s++)
                {
                    if (simUsed[s]) continue;
                    for (int t = 1; t < n; t++)
                    {
                        if (aiUsed[t]) continue;
                        if (cost[s, t] < best) { best = cost[s, t]; bs = s; bt = t; }
                    }
                }
                simUsed[bs] = true;
                aiUsed[bt] = true;
                aiSlotOfSimSlot[bs] = bt;
                total += best;
            }
            return total;
        }

        /// <summary>The team AI formation template that best fits the kick-off shape.</summary>
        public static TeamAIFormation BestFit(SimVec3[] spots)
        {
            var tmp = new int[MatchSetup.PlayersPerTeam];
            TeamAIFormation best = TeamAIFormation.F433;
            float bestCost = float.MaxValue;
            foreach (var f in All)
            {
                float c = Assign(spots, f, tmp);
                if (c < bestCost - 1e-5f) { bestCost = c; best = f; }
            }
            return best;
        }
    }
}
