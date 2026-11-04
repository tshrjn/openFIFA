namespace OpenFifa.Core
{
    /// <summary>
    /// Acceleration profile, after the three types EA restored in FC 26.
    /// Explosive: quick first steps, flatter top end. Lengthy: slow first steps, keeps building near top speed.
    /// Controlled: in between.
    /// </summary>
    public enum SimAccelerationType
    {
        Controlled = 0,
        Explosive = 1,
        Lengthy = 2,
    }
}
