using System;

namespace OpenFifa.Core
{
    /// <summary>Contacts that happened during one ball integration step.</summary>
    [Flags]
    public enum BallStepFlags
    {
        None = 0,
        Bounced = 1 << 0,
        HitPost = 1 << 1,
        HitCrossbar = 1 << 2,
        EnteredNet = 1 << 3,
        StartedRolling = 1 << 4,
    }
}
