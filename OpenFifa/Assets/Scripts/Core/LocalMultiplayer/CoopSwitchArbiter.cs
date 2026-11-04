using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Keeps two (or more) humans on the same team from controlling the same player. Each human's selector picks
    /// freely (follow the ball, manual switch); this runs right after and settles clashes:
    /// - Whoever already held the contested player keeps him. A pass to your partner leaves your partner on the
    ///   receiver and puts you back on your own player.
    /// - If neither held him before, the human who asked for him (manual switch) wins, then the lower slot.
    /// - The loser goes back to his previous player when that is still free and he did not ask to switch;
    ///   otherwise he gets the best free player for the ball (or in the flick direction he asked for).
    /// </summary>
    public static class CoopSwitchArbiter
    {
        /// <summary>
        /// Resolves clashes in place. Arrays are indexed by human (slot order); <paramref name="wanted"/> holds each
        /// selector's choice this tick and is rewritten with the settled ids.
        /// </summary>
        /// <param name="previous">Player each human controlled last tick.</param>
        /// <param name="wanted">Player each human's selector chose this tick (rewritten).</param>
        /// <param name="manual">The human pressed switch this tick.</param>
        /// <param name="flick">Pitch-space flick direction of a directional switch, zero when smart switching.</param>
        /// <param name="candidates">Switchable outfield players of the team.</param>
        /// <returns>Number of humans moved off their wanted player.</returns>
        public static int Resolve(int[] previous, int[] wanted, bool[] manual, SimVec3[] flick,
            IReadOnlyList<ControlPlayerInfo> candidates, SimVec3 ballPos, SimVec3 ballVel, int count)
        {
            int moved = 0;
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    if (wanted[a] < 0 || wanted[a] != wanted[b]) continue;
                    int winner = Winner(a, b, wanted[a], previous, manual);
                    int loser = winner == a ? b : a;
                    wanted[loser] = Fallback(loser, previous, wanted, manual, flick, candidates, ballPos, ballVel, count);
                    moved++;
                }
            }
            return moved;
        }

        private static int Winner(int a, int b, int contested, int[] previous, bool[] manual)
        {
            if (previous[a] == contested) return a;
            if (previous[b] == contested) return b;
            if (manual[a] != manual[b]) return manual[a] ? a : b;
            return a; // lower slot
        }

        private static int Fallback(int loser, int[] previous, int[] wanted, bool[] manual, SimVec3[] flick,
            IReadOnlyList<ControlPlayerInfo> candidates, SimVec3 ballPos, SimVec3 ballVel, int count)
        {
            int prev = previous[loser];
            if (!manual[loser] && prev >= 0 && !TakenByOther(prev, loser, wanted, count) && Contains(candidates, prev))
                return prev;

            var free = Scratch;
            free.Clear();
            for (int i = 0; i < candidates.Count; i++)
                if (!TakenByOther(candidates[i].Id, loser, wanted, count)) free.Add(candidates[i]);
            if (free.Count == 0) return prev;

            int pick = -1;
            if (manual[loser] && flick != null && flick[loser].SqrMagnitude > 1e-6f)
            {
                var from = PositionOf(candidates, prev, ballPos);
                pick = SmartSwitchSelector.SelectDirectional(free, from, prev, flick[loser]);
            }
            if (pick < 0) pick = SmartSwitchSelector.SelectNearestToBallPath(free, ballPos, ballVel, manual[loser] ? prev : -1);
            return pick;
        }

        [System.ThreadStatic] private static List<ControlPlayerInfo> _scratch;
        private static List<ControlPlayerInfo> Scratch => _scratch ?? (_scratch = new List<ControlPlayerInfo>(11));

        private static bool TakenByOther(int id, int self, int[] wanted, int count)
        {
            for (int i = 0; i < count; i++) if (i != self && wanted[i] == id) return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<ControlPlayerInfo> list, int id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return true;
            return false;
        }

        private static SimVec3 PositionOf(IReadOnlyList<ControlPlayerInfo> list, int id, SimVec3 fallback)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i].Position;
            return fallback;
        }
    }
}
