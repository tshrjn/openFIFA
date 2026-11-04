namespace OpenFifa.Core
{
    /// <summary>What a player's body is committed to. Anything but Free blocks new actions.</summary>
    public enum SimPlayerActionState
    {
        Free = 0,

        /// <summary>Kick or tackle wind-up; the action resolves at <see cref="SimPlayer.ContactTick"/>.</summary>
        WindUp = 1,

        /// <summary>Short recovery after a kick or a missed standing tackle.</summary>
        Recovery = 2,

        /// <summary>Sliding along the ground; resolves contact during the slide.</summary>
        Sliding = 3,

        /// <summary>On the ground after a slide or a foul; cannot move.</summary>
        Grounded = 4,
    }
}
