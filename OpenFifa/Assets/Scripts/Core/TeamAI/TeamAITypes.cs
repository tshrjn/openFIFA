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
        F352 = 3,
        /// <summary>4-1-4-1: a single holding midfielder behind a flat four.</summary>
        F4141 = 4,
        /// <summary>4-1-2-1-2: the narrow diamond (no wingers; full-backs give the width).</summary>
        F41212 = 5,
        /// <summary>3-4-3: three centre-backs, wing-backs, two wide forwards.</summary>
        F343 = 6,
        /// <summary>5-3-2: a back five whose wing-backs push on in possession.</summary>
        F532 = 7
    }

    /// <summary>
    /// Team mentality, set by the manager (<see cref="TeamAIManager"/>) from the score and the clock, or fixed by the
    /// user. Shifts line height, pressing and how many players join the attack on top of the team's style.
    /// </summary>
    public enum TeamAIMentality
    {
        UltraDefensive = -2,
        Defensive = -1,
        Balanced = 0,
        Attacking = 1,
        AllOutAttack = 2
    }

    /// <summary>Named team styles (see <see cref="TeamAITactics.FromStyle"/>).</summary>
    public enum TeamAIStyle
    {
        Balanced = 0,
        /// <summary>Patient short passing, high line, keeper and centre-backs split to build up.</summary>
        Possession = 1,
        /// <summary>Gegenpress: very high line, relentless press and counter-press.</summary>
        HighPress = 2,
        /// <summary>Deep mid block, then fast vertical breaks with runners going early.</summary>
        CounterAttack = 3,
        /// <summary>Direct: long balls into the forwards, few players committed in build-up.</summary>
        LongBall = 4,
        /// <summary>Wide shape, full-backs overlap, crosses into the box.</summary>
        WingPlay = 5,
        /// <summary>Low block, narrow and compact, little pressing, clear it long.</summary>
        ParkTheBus = 6
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
        SetPieceTaker = 24,
        /// <summary>A marked forward comes short toward the ball to receive (then spins in behind if followed).</summary>
        CheckRun = 25
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
        /// <summary>
        /// Flat velocity of whatever <see cref="MoveTarget"/> follows (the ball carrier for a press, the ball for a chase,
        /// the offside line for a run in behind, a marked runner); zero for a fixed spot. The adapter brakes on the
        /// closing speed relative to it, so a player chasing a moving target does not pull up short of it.
        /// </summary>
        public SimVec3 MoveTargetVelocity;
        /// <summary>Where a player holding his spot looks (the ball); only used when <see cref="FaceLookAt"/>.</summary>
        public SimVec3 LookAt;
        public bool FaceLookAt;
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
        /// <summary>
        /// Tempo: the fastest the player should move on a non-sprint run (m/s), 0 = no limit (a full jog). Off-ball shape
        /// moves walk or jog (see <see cref="TeamAITempo"/>); presses, chases and runs in behind leave it at 0.
        /// </summary>
        public float SpeedLimit;
        /// <summary>The player's current top (sprint) speed, m/s, so the adapter can turn <see cref="SpeedLimit"/> into a throttle.</summary>
        public float SprintSpeed;

        public static AIPlayerCommand Idle(int id, SimVec3 pos)
        {
            return new AIPlayerCommand
            {
                PlayerId = id,
                MoveTarget = pos,
                MoveDirection = SimVec3.Zero,
                MoveTargetVelocity = SimVec3.Zero,
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
