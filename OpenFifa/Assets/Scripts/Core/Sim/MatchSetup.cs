namespace OpenFifa.Core
{
    /// <summary>
    /// Everything needed to start a match: seed, squads (11 each, index 0 = goalkeeper) and kick-off shapes.
    /// Formation spots are in the "attack frame": X = left(-)/right(+) as seen attacking, Z = metres from the
    /// halfway line toward the opponents' goal (own half is negative). The sim mirrors them per side and half.
    /// </summary>
    public sealed class MatchSetup
    {
        public const int PlayersPerTeam = 11;

        public ulong Seed = 1UL;
        public SimPlayerAttributes[] Home = new SimPlayerAttributes[PlayersPerTeam];
        public SimPlayerAttributes[] Away = new SimPlayerAttributes[PlayersPerTeam];
        public SimVec3[] HomeFormation = DefaultFormation();
        public SimVec3[] AwayFormation = DefaultFormation();
        public SimTeam FirstKickOff = SimTeam.Home;

        /// <summary>Slot of the kick-off taker in each squad (default: a striker).</summary>
        public int KickOffTakerSlot = 10;

        public static MatchSetup CreateDefault(ulong seed)
        {
            var s = new MatchSetup { Seed = seed };
            for (int i = 0; i < PlayersPerTeam; i++)
            {
                s.Home[i] = i == 0 ? SimPlayerAttributes.DefaultGoalkeeper() : new SimPlayerAttributes();
                s.Away[i] = i == 0 ? SimPlayerAttributes.DefaultGoalkeeper() : new SimPlayerAttributes();
            }

            return s;
        }

        /// <summary>A 4-4-2 kick-off shape for a 105 x 68 pitch, every spot in the own half and outside the centre circle.</summary>
        public static SimVec3[] DefaultFormation() => new[]
        {
            new SimVec3(0f, 0f, -50f),
            new SimVec3(-24f, 0f, -34f), new SimVec3(-8f, 0f, -37f), new SimVec3(8f, 0f, -37f), new SimVec3(24f, 0f, -34f),
            new SimVec3(-24f, 0f, -18f), new SimVec3(-8f, 0f, -20f), new SimVec3(8f, 0f, -20f), new SimVec3(24f, 0f, -18f),
            new SimVec3(-12f, 0f, -4f), new SimVec3(10f, 0f, -6f),
        };
    }
}
