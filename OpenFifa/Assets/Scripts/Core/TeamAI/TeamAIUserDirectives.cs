namespace OpenFifa.Core
{
    /// <summary>
    /// Requests the human players of one team make to their AI teammates (call for a pass, send a run, put a
    /// second man on the ball). Written by the local-multiplayer layer (see <c>TeammateCallResolver</c>), read by
    /// <see cref="TeamAIBrain"/>. Every request carries an expiry on the match clock (<see cref="TeamAIWorld.Time"/>),
    /// so a stale request simply stops mattering. Player ids are world indices (== sim player ids).
    /// </summary>
    public struct TeamAIUserDirectives
    {
        /// <summary>Score added to a pass toward the caller, after technique scaling. Enough to beat a carry or a
        /// speculative shot, not enough to force a pass straight into an opponent (risk still costs up to 0.9).</summary>
        public const float PassCallBonus = 0.45f;

        /// <summary>Player calling for the ball, -1 when nobody is.</summary>
        public int PassCallerId;
        /// <summary>The caller wants it played into space ahead of them rather than to feet.</summary>
        public bool PassCallThrough;
        public float PassCallUntil;

        /// <summary>Player asked to make a run; -1 lets the brain pick the best runner.</summary>
        public int RunnerId;
        /// <summary>Where the run should end (flat). Zero length = a run in behind the line, brain picks the spot.</summary>
        public SimVec3 RunTarget;
        public bool RunHasTarget;
        public float RunUntil;

        /// <summary>The nearest AI teammate presses the ball carrier while active.</summary>
        public float PressUntil;

        public static TeamAIUserDirectives None => new TeamAIUserDirectives { PassCallerId = -1, RunnerId = -1, PassCallUntil = -1f, RunUntil = -1f, PressUntil = -1f };

        public bool PassCallActive(float time) => PassCallerId >= 0 && time <= PassCallUntil;
        public bool RunActive(float time) => time <= RunUntil;
        public bool PressActive(float time) => time <= PressUntil;

        public void CallForPass(int callerId, bool through, float until)
        {
            PassCallerId = callerId;
            PassCallThrough = through;
            PassCallUntil = until;
        }

        public void RequestRun(int runnerId, SimVec3 target, bool hasTarget, float until)
        {
            RunnerId = runnerId;
            RunTarget = new SimVec3(target.X, 0f, target.Z);
            RunHasTarget = hasTarget;
            RunUntil = until;
        }

        public void RequestPress(float until)
        {
            if (until > PressUntil) PressUntil = until;
        }

        public void ClearPassCall()
        {
            PassCallerId = -1;
            PassCallUntil = -1f;
        }

        public void ClearRun()
        {
            RunnerId = -1;
            RunUntil = -1f;
            RunHasTarget = false;
        }
    }
}
