namespace OpenFifa.Core
{
    /// <summary>One player as the team AI sees it. Attributes are normalised 0..1.</summary>
    public struct TeamAIPlayer
    {
        public int Id;
        /// <summary>0 = home, 1 = away.</summary>
        public int Team;
        public TeamAIRole Role;
        /// <summary>Slot 0..10 in the team's formation (0 = GK).</summary>
        public int FormationSlot;
        public SimVec3 Position;
        public SimVec3 Velocity;
        /// <summary>Unit flat facing direction.</summary>
        public SimVec3 Facing;
        /// <summary>Top speed, m/s.</summary>
        public float MaxSpeed;
        public float Passing;
        public float Shooting;
        public float Dribbling;
        public float Defending;
        public float Reactions;
        public float Goalkeeping;
        /// <summary>Ball control (0..1): sets the fastest pass he can take cleanly on the first touch.</summary>
        public float BallControl;
        public bool Active;
        public bool HumanControlled;

        public static TeamAIPlayer Create(int id, int team, TeamAIRole role, int slot, SimVec3 position)
        {
            return new TeamAIPlayer
            {
                Id = id,
                Team = team,
                Role = role,
                FormationSlot = slot,
                Position = position,
                Velocity = SimVec3.Zero,
                Facing = new SimVec3(0f, 0f, team == 0 ? 1f : -1f),
                MaxSpeed = 7.5f,
                Passing = 0.7f,
                Shooting = 0.7f,
                Dribbling = 0.7f,
                Defending = 0.7f,
                Reactions = 0.7f,
                Goalkeeping = role == TeamAIRole.GK ? 0.75f : 0.1f,
                BallControl = 0.7f,
                Active = true,
                HumanControlled = false
            };
        }
    }

    /// <summary>Ball as the team AI sees it.</summary>
    public struct TeamAIBall
    {
        public SimVec3 Position;
        public SimVec3 Velocity;
        /// <summary>Ball spin (rad/s, world axes). Optional: zero is fine; used by the ball prediction.</summary>
        public SimVec3 Spin;
        /// <summary>Id of the player in control, -1 when loose or in flight.</summary>
        public int OwnerId;
        /// <summary>Team of the last touch, -1 when unknown.</summary>
        public int LastTouchTeam;
        /// <summary>Id of the last player to touch the ball, -1 when unknown.</summary>
        public int LastTouchId;
    }

    /// <summary>
    /// Input view for the team AI. The coordinator fills this from the match sim each tick.
    /// Reference type with preallocated arrays so filling it allocates nothing.
    /// </summary>
    public sealed class TeamAIWorld
    {
        public const int MaxPlayers = 22;

        public readonly TeamAIPlayer[] Players = new TeamAIPlayer[MaxPlayers];
        public int PlayerCount;
        public TeamAIBall Ball;
        public PitchSpec Pitch;
        /// <summary>+1 when the home team (0) attacks +Z, -1 otherwise.</summary>
        public int HomeAttackSign = 1;
        public TeamAIMatchPhase MatchPhase = TeamAIMatchPhase.InPlay;
        /// <summary>Team awarded the current restart, -1 when in play.</summary>
        public int RestartTeam = -1;
        /// <summary>The current free kick is indirect (no direct shot). Only read while MatchPhase is FreeKick.</summary>
        public bool IndirectFreeKick;
        /// <summary>Match clock in seconds (sim time, never wall time).</summary>
        public float Time;
        /// <summary>Human calls to AI teammates, per team (0 = home, 1 = away). See <see cref="TeamAIUserDirectives"/>.</summary>
        public readonly TeamAIUserDirectives[] Directives = { TeamAIUserDirectives.None, TeamAIUserDirectives.None };

        public TeamAIWorld(PitchSpec pitch)
        {
            Pitch = pitch;
            Ball.OwnerId = -1;
            Ball.LastTouchTeam = -1;
            Ball.LastTouchId = -1;
        }

        /// <summary>+1 or -1: the Z direction this team attacks.</summary>
        public int AttackSign(int team) => team == 0 ? HomeAttackSign : -HomeAttackSign;

        public int IndexOfId(int id)
        {
            if (id < 0) return -1;
            for (int i = 0; i < PlayerCount; i++)
                if (Players[i].Id == id) return i;
            return -1;
        }

        public int OwnerIndex => IndexOfId(Ball.OwnerId);

        public int OwnerTeam
        {
            get
            {
                int i = OwnerIndex;
                return i >= 0 ? Players[i].Team : -1;
            }
        }
    }
}
