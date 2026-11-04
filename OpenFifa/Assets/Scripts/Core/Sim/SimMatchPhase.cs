namespace OpenFifa.Core
{
    public enum SimMatchPhase
    {
        /// <summary>Ball live.</summary>
        InPlay = 0,

        /// <summary>Ball dead, short pause before the restart is set up (celebration, ball retrieval).</summary>
        Stoppage = 1,

        /// <summary>Ball placed, waiting for the taker to kick. Opponents are held 9.15 m away.</summary>
        AwaitingRestart = 2,

        HalfTime = 3,
        FullTime = 4,
    }
}
