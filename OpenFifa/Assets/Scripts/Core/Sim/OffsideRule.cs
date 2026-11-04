namespace OpenFifa.Core
{
    /// <summary>
    /// Offside decision logic (Law 11), evaluated at the moment a teammate plays the ball. Positions are given as
    /// "attack coordinates": distance along the attacking direction, with the halfway line at 0 and the
    /// opponents' goal line at +HalfLength. The sim treats each player as a point (no body parts).
    /// Presentation of the call is owned elsewhere; this class only decides.
    /// </summary>
    public static class OffsideRule
    {
        /// <summary>
        /// True if an attacker is in an offside position: in the opponents' half (the halfway line itself is
        /// not), and strictly nearer the goal line than both the ball and the second-last opponent. Level is onside.
        /// </summary>
        public static bool IsOffsidePosition(float attackerCoord, float ballCoord, float secondLastDefenderCoord)
        {
            if (attackerCoord <= 0f) return false;
            if (attackerCoord <= ballCoord) return false;
            return attackerCoord > secondLastDefenderCoord;
        }

        /// <summary>
        /// Attack coordinate of the second-last defender (the goalkeeper usually being the last).
        /// With fewer than two defenders on the pitch, the line falls back to the halfway line (coordinate 0).
        /// </summary>
        public static float SecondLastDefender(float[] defenderCoords, int count)
        {
            float first = float.NegativeInfinity;
            float second = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                float c = defenderCoords[i];
                if (c > first)
                {
                    second = first;
                    first = c;
                }
                else if (c > second)
                {
                    second = c;
                }
            }

            return float.IsNegativeInfinity(second) ? 0f : second;
        }

        /// <summary>Offside does not apply directly from a throw-in, goal kick or corner (Law 11.3).</summary>
        public static bool AppliesToRestart(RestartType restart) =>
            restart != RestartType.ThrowIn && restart != RestartType.GoalKick && restart != RestartType.Corner;
    }
}
