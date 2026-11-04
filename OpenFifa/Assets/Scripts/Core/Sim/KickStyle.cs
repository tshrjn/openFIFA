namespace OpenFifa.Core
{
    /// <summary>
    /// Variant of a kick action (<see cref="PlayerIntent.Style"/>). <see cref="Default"/> is the plain action; the others
    /// change how <see cref="KickSolver"/> launches the ball (and, for passes, how the lead point is chosen):
    /// <list type="bullet">
    /// <item><see cref="Chip"/> (Shoot): a high, soft arc with backspin that drops onto the goal line, over a keeper off it.</item>
    /// <item><see cref="Low"/> (Shoot): flat and driven along the ground, no power-dependent rise.</item>
    /// <item><see cref="Driven"/> (Pass, ThroughPass): a firmer ground pass that arrives faster.
    /// (Lob): a flatter, quicker lofted ball.</item>
    /// <item><see cref="LobbedThrough"/> (Lob): lofted into the receiver's run, led like a through ball.</item>
    /// <item><see cref="Knuckle"/> (Shoot): struck through the valve with almost no spin, so it wobbles and dips in
    /// flight (see <see cref="BallSimConfig.KnuckleForceCoefficient"/>).</item>
    /// </list>
    /// A style that does not apply to the action is ignored (the plain action is played).
    /// </summary>
    public enum KickStyle
    {
        Default = 0,
        Chip = 1,
        Low = 2,
        Driven = 3,
        LobbedThrough = 4,
        Knuckle = 5,
    }
}
