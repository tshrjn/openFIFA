namespace OpenFifa.Core
{
    /// <summary>
    /// World axis the pitch length (goal to goal) runs along. The 4v4 MatchOrchestrator scene lays the length
    /// along X; the 11v11 match sim lays it along Z (goals at +/-HalfLength on Z).
    /// </summary>
    public enum PitchLengthAxis
    {
        X = 0,
        Z = 1
    }
}
