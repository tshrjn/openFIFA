namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# logic for finding the nearest teammate to the ball
    /// and performing player switching. No Unity dependency.
    /// </summary>
    public class PlayerSwitchLogic
    {
        /// <summary>
        /// Find the player nearest to the ball position.
        /// Returns the index into the players array, or -1 if empty.
        /// Tie-breaking: lowest index wins.
        /// </summary>
        public int FindNearestPlayer(PositionData[] players, float ballX, float ballZ)
        {
            if (players == null || players.Length == 0)
                return -1;

            int nearestIndex = 0;
            float nearestDistSq = DistanceSquared(players[0].X, players[0].Z, ballX, ballZ);

            for (int i = 1; i < players.Length; i++)
            {
                float distSq = DistanceSquared(players[i].X, players[i].Z, ballX, ballZ);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// Find the nearest player excluding the specified index.
        /// Used for switching away from the currently controlled player.
        /// Returns -1 if no other players available.
        /// </summary>
        public int FindNearestPlayerExcluding(PositionData[] players, float ballX, float ballZ, int excludeIndex)
        {
            if (players == null || players.Length == 0)
                return -1;

            int nearestIndex = -1;
            float nearestDistSq = float.MaxValue;

            for (int i = 0; i < players.Length; i++)
            {
                if (i == excludeIndex) continue;

                float distSq = DistanceSquared(players[i].X, players[i].Z, ballX, ballZ);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// Perform a player switch. Finds the nearest player to the ball excluding
        /// the current active player, and returns the switch result.
        /// </summary>
        public SwitchResult PerformSwitch(PositionData[] players, float ballX, float ballZ, int currentActiveIndex)
        {
            int newIndex = FindNearestPlayerExcluding(players, ballX, ballZ, currentActiveIndex);

            if (newIndex < 0)
            {
                return new SwitchResult
                {
                    PreviousActiveIndex = currentActiveIndex,
                    NewActiveIndex = currentActiveIndex,
                    SwitchOccurred = false
                };
            }

            return new SwitchResult
            {
                PreviousActiveIndex = currentActiveIndex,
                NewActiveIndex = newIndex,
                SwitchOccurred = newIndex != currentActiveIndex
            };
        }

        private static float DistanceSquared(float x1, float z1, float x2, float z2)
        {
            float dx = x1 - x2;
            float dz = z1 - z2;
            return dx * dx + dz * dz;
        }
    }

    /// <summary>
    /// Result of a player switch operation.
    /// </summary>
    public struct SwitchResult
    {
        public int PreviousActiveIndex;
        public int NewActiveIndex;
        public bool SwitchOccurred;
    }
}
