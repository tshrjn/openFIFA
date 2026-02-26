namespace OpenFifa.Core
{
    /// <summary>
    /// Lightweight position data for AI calculations.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public struct PositionData
    {
        public int Id;
        public float X;
        public float Z;

        public PositionData(int id, float x, float z)
        {
            Id = id;
            X = x;
            Z = z;
        }
    }
}
