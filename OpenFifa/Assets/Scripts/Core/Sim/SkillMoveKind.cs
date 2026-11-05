namespace OpenFifa.Core
{
    /// <summary>
    /// Dribbling skill moves the sim can perform (<see cref="IntentAction.SkillMove"/>). Each one is a short scripted
    /// body-and-ball sequence (<see cref="SkillMoveCatalog"/>): the carrier keeps the ball on his foot through it, may
    /// sell a fake that wrong-foots nearby defenders, and releases the ball into a new direction at the end.
    /// Original OpenFootball move set; star requirements follow the common 1-5 skill-star convention.
    /// </summary>
    public enum SkillMoveKind
    {
        None = 0,

        /// <summary>1 star. Drop the shoulder one way, go the other at about 50 degrees.</summary>
        BodyFeint = 1,

        /// <summary>2 stars. Step over the ball toward one side, burst out on the other.</summary>
        Stepover = 2,

        /// <summary>2 stars. Roll the ball across the body with the sole and side-step with it (90 degrees).</summary>
        BallRoll = 3,

        /// <summary>2 stars. Stop the ball with the sole and pull it back, turning to go the other way.</summary>
        DragBack = 4,

        /// <summary>3 stars. Fake to go on, chop the ball behind the standing leg and cut back (about 120 degrees).</summary>
        HeelChop = 5,

        /// <summary>4 stars. 360 spin over the ball with the back to the defender, exiting to the side.</summary>
        Roulette = 6,

        /// <summary>2 stars. Shape to shoot or cross, stop the swing and push the ball past the blocker.</summary>
        FakeShot = 7,
    }
}
