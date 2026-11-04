namespace OpenFifa.Core
{
    /// <summary>
    /// Nature of a contact with the ball. Law 11 distinguishes a deliberate play
    /// by a defender (resets offside) from a deflection, rebound or save (does not).
    /// </summary>
    public enum BallTouchKind
    {
        /// <summary>Controlled or deliberately played (pass, shot, trap, dribble).</summary>
        Deliberate = 0,
        /// <summary>Unintended contact: deflection or rebound off a player.</summary>
        Deflection = 1,
        /// <summary>A deliberate save (any player stopping a ball going into or very close to goal).</summary>
        Save = 2
    }
}
