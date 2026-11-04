namespace OpenFifa.Core
{
    /// <summary>Tactical role of a player within a formation (team AI layer).</summary>
    public enum TeamAIRole
    {
        GK = 0,
        CB = 1,
        FB = 2,
        DM = 3,
        CM = 4,
        AM = 5,
        W = 6,
        ST = 7
    }

    /// <summary>Team phase state machine states.</summary>
    public enum TeamAIPhase
    {
        // In possession
        BuildUp = 0,
        Progression = 1,
        FinalThird = 2,
        // Out of possession
        HighPress = 3,
        MidBlock = 4,
        LowBlock = 5,
        // Transitions
        CounterPress = 6,
        Recovery = 7,
        // Dead ball
        SetPieceFor = 8,
        SetPieceAgainst = 9
    }

    /// <summary>Match flow phase as seen by the team AI.</summary>
    public enum TeamAIMatchPhase
    {
        InPlay = 0,
        Kickoff = 1,
        GoalKick = 2,
        Corner = 3,
        FreeKick = 4,
        ThrowIn = 5,
        Penalty = 6,
        Stopped = 7
    }

    /// <summary>Formation presets.</summary>
    public enum TeamAIFormation
    {
        F433 = 0,
        F4231 = 1,
        F442 = 2,
        F352 = 3
    }

    /// <summary>
    /// Ball action requested by the AI. Close to the sim's PlayerIntent action set.
    /// Dribbling/carrying is None plus a move direction; clearances are LobPass at full power.
    /// </summary>
    public enum TeamAIAction
    {
        None = 0,
        Pass = 1,
        ThroughPass = 2,
        LobPass = 3,
        Cross = 4,
        Shoot = 5,
        FinesseShot = 6,
        DrivenShot = 7,
        Tackle = 8,
        SlideTackle = 9,
        Jockey = 10,
        Shield = 11
    }

    /// <summary>Why the AI chose a command. For debugging, tests, commentary and animation urgency.</summary>
    public enum TeamAIIntent
    {
        Idle = 0,
        HoldShape = 1,
        Support = 2,
        RunInBehind = 3,
        Overlap = 4,
        HoldWidth = 5,
        ChaseBall = 6,
        Receive = 7,
        Carry = 8,
        Pass = 9,
        Shoot = 10,
        Clear = 11,
        Shield = 12,
        Press = 13,
        CoverShadow = 14,
        Mark = 15,
        Jockey = 16,
        Tackle = 17,
        KeepLine = 18,
        Recover = 19,
        GKPosition = 20,
        GKSweep = 21,
        GKClaim = 22,
        GKDistribute = 23,
        SetPieceTaker = 24
    }

    /// <summary>
    /// Output of the team AI for one player for one decision tick.
    /// The coordinator adapts this to the sim's PlayerIntent.
    /// </summary>
    public struct AIPlayerCommand
    {
        public int PlayerId;
        /// <summary>Where the player wants to be (flat, Y = 0).</summary>
        public SimVec3 MoveTarget;
        /// <summary>Unit flat direction toward MoveTarget, or zero when arrived.</summary>
        public SimVec3 MoveDirection;
        public bool Sprint;
        public TeamAIAction Action;
        /// <summary>Point the action is aimed at (pass target, shot aim point, tackle point).</summary>
        public SimVec3 ActionTarget;
        /// <summary>Id of the target player for passes, tackles and marking; -1 when none.</summary>
        public int TargetPlayerId;
        /// <summary>0..1 kick power.</summary>
        public float Power;
        /// <summary>Unit flat aim direction from the ball (includes difficulty error).</summary>
        public SimVec3 Aim;
        public TeamAIIntent Intent;

        public static AIPlayerCommand Idle(int id, SimVec3 pos)
        {
            return new AIPlayerCommand
            {
                PlayerId = id,
                MoveTarget = pos,
                MoveDirection = SimVec3.Zero,
                Sprint = false,
                Action = TeamAIAction.None,
                ActionTarget = pos,
                TargetPlayerId = -1,
                Power = 0f,
                Aim = SimVec3.Zero,
                Intent = TeamAIIntent.Idle
            };
        }
    }
}
