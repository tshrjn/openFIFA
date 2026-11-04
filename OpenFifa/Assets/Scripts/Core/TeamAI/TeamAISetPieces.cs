using System;

namespace OpenFifa.Core
{
    /// <summary>Which set-piece routine a team is running.</summary>
    public enum TeamAISetPieceKind
    {
        /// <summary>No routine: kick-offs, throw-ins, goal kicks and free kicks far from goal use the plain shape.</summary>
        None = 0,
        CornerFor = 1,
        CornerAgainst = 2,
        PenaltyFor = 3,
        PenaltyAgainst = 4,
        /// <summary>Free kick for us within shooting / crossing range of the opponent goal.</summary>
        FreeKickFor = 5,
        /// <summary>Free kick against us within range of our goal (wall + marking).</summary>
        FreeKickAgainst = 6
    }

    /// <summary>Job of one player inside a set-piece routine.</summary>
    public enum TeamAISetPieceRole
    {
        None = 0,
        Taker = 1,
        NearPost = 2,
        FarPost = 3,
        Central = 4,
        KeeperScreen = 5,
        EdgeOfBox = 6,
        ShortOption = 7,
        RestDefence = 8,
        Rebound = 9,
        ZonalPost = 10,
        ZonalSixYard = 11,
        ZonalEdge = 12,
        Marker = 13,
        Wall = 14,
        Outlet = 15,
        PenaltyEdge = 16,
        Goalkeeper = 17
    }

    /// <summary>
    /// Set-piece routines for one team: attacking and defending corners (near post, far post, central, keeper
    /// screen, edge of the box, short option; zonal plus man marking when defending), penalties (keeper on his
    /// line, everyone else outside the box and arc, taker picks a side from a noisy read of the keeper) and direct
    /// free kicks near the box (wall sized by distance, shot or cross chosen once per kick).
    /// Planned every decision tick while a restart is pending; all memory is preallocated.
    /// </summary>
    public sealed class TeamAISetPieces
    {
        /// <summary>Free kicks closer than this (m, ball to goal centre) get a routine.</summary>
        public const float FreeKickRange = 35f;
        public const float WallSpacing = 0.6f;
        /// <summary>Earliest the taker plays a routine set piece (s after it was awarded).</summary>
        public const float MinSettle = 1.5f;
        /// <summary>Latest the taker waits for the routine to set up (the sim auto-takes at 8 s).</summary>
        public const float MaxSettle = 7f;
        public const float PenaltySettle = 1.0f;

        public TeamAISetPieceKind Kind { get; private set; }
        /// <summary>Players in the wall when defending a free kick (0 otherwise).</summary>
        public int WallSize { get; private set; }
        /// <summary>For FreeKickFor: true = shoot directly, false = deliver into the box. Decided once per kick.</summary>
        public bool FreeKickShot { get; private set; }
        /// <summary>For PenaltyFor: -1 / +1 = side of the goal (world X) the taker aims at, 0 = down the middle.</summary>
        public int PenaltySide { get; private set; }
        /// <summary>Match time the current routine started.</summary>
        public float StartTime { get; private set; }

        public readonly TeamAISetPieceRole[] Roles = new TeamAISetPieceRole[TeamAIWorld.MaxPlayers];
        public readonly SimVec3[] Targets = new SimVec3[TeamAIWorld.MaxPlayers];
        /// <summary>World index of the opponent a Marker picks up, -1 otherwise.</summary>
        public readonly int[] MarkIndex = new int[TeamAIWorld.MaxPlayers];

        private readonly bool[] _used = new bool[TeamAIWorld.MaxPlayers];
        private readonly int[] _threats = new int[TeamAIWorld.MaxPlayers];
        private SimVec3 _spot;
        private TeamAISetPieceKind _lastKind = TeamAISetPieceKind.None;

        // Geometry of the goal under attack in this routine (same for both teams).
        private float _goalZ;
        private float _inward;
        private float _ns;
        private PitchSpec _pitch;

        /// <summary>
        /// Work out the routine for <paramref name="team"/> and every player's job and target.
        /// Returns false (Kind = None) when the restart has no routine.
        /// </summary>
        public bool Plan(TeamAIWorld world, int team, int takerIndex, TeamAIRandom rng, in TeamAIDifficulty diff)
        {
            Kind = Classify(world, team);
            for (int i = 0; i < TeamAIWorld.MaxPlayers; i++)
            {
                Roles[i] = TeamAISetPieceRole.None;
                MarkIndex[i] = -1;
                _used[i] = false;
            }
            WallSize = 0;
            if (Kind == TeamAISetPieceKind.None)
            {
                _lastKind = Kind;
                return false;
            }

            var pitch = world.Pitch;
            _pitch = pitch;
            var ball = TeamAIMath.Flat(world.Ball.Position);
            int attackTeam = world.RestartTeam;
            int attackSign = world.AttackSign(attackTeam);
            _goalZ = attackSign * pitch.HalfLength;
            _inward = -attackSign;
            _ns = ball.X >= 0f ? 1f : -1f;

            bool fresh = Kind != _lastKind || TeamAIMath.FlatDistance(ball, _spot) > 0.5f;
            if (fresh)
            {
                _lastKind = Kind;
                _spot = ball;
                StartTime = world.Time;
                OnFreshRoutine(world, team, takerIndex, rng, diff, ball);
            }

            if (takerIndex >= 0) { Roles[takerIndex] = TeamAISetPieceRole.Taker; _used[takerIndex] = true; Targets[takerIndex] = ball; }
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (world.Players[i].Team != team || !world.Players[i].Active) { _used[i] = true; continue; }
                if (world.Players[i].Role == TeamAIRole.GK && i != takerIndex)
                {
                    _used[i] = true;
                    Roles[i] = TeamAISetPieceRole.Goalkeeper;
                    Targets[i] = GoalkeeperTarget(world, team, ball);
                }
            }

            switch (Kind)
            {
                case TeamAISetPieceKind.CornerFor: PlanCornerFor(world); break;
                case TeamAISetPieceKind.CornerAgainst: PlanCornerAgainst(world, team, ball); break;
                case TeamAISetPieceKind.PenaltyFor: PlanPenaltyFor(world); break;
                case TeamAISetPieceKind.PenaltyAgainst: PlanPenaltyAgainst(world); break;
                case TeamAISetPieceKind.FreeKickFor: PlanFreeKickFor(world, team, ball); break;
                case TeamAISetPieceKind.FreeKickAgainst: PlanFreeKickAgainst(world, team, ball); break;
            }

            // Legal distances and the pitch.
            bool against = Kind == TeamAISetPieceKind.CornerAgainst || Kind == TeamAISetPieceKind.PenaltyAgainst ||
                           Kind == TeamAISetPieceKind.FreeKickAgainst;
            bool penalty = Kind == TeamAISetPieceKind.PenaltyFor || Kind == TeamAISetPieceKind.PenaltyAgainst;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (Roles[i] == TeamAISetPieceRole.None || Roles[i] == TeamAISetPieceRole.Taker) continue;
                var t = Targets[i];
                if ((against || penalty) && Roles[i] != TeamAISetPieceRole.Goalkeeper)
                {
                    float d = TeamAIMath.FlatDistance(t, ball);
                    float min = TeamAIBrain.FreeKickDistance + 0.4f;
                    if (d < min)
                    {
                        var away = d > 1e-3f ? TeamAIMath.FlatDirection(ball, t) : new SimVec3(0f, 0f, _inward);
                        t = ball + away * min;
                    }
                }
                Targets[i] = TeamAIMath.ClampToPitch(t, pitch, Roles[i] == TeamAISetPieceRole.Goalkeeper ? 0.05f : 0.5f);
            }
            return true;
        }

        /// <summary>True when the routine is set and the taker should play the ball.</summary>
        public bool ReadyToTake(TeamAIWorld world)
        {
            float t = world.Time - StartTime;
            if (Kind == TeamAISetPieceKind.PenaltyFor) return t >= PenaltySettle;
            if (t < MinSettle) return false;
            if (t >= MaxSettle) return true;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                var r = Roles[i];
                if (r == TeamAISetPieceRole.None || r == TeamAISetPieceRole.Taker || r == TeamAISetPieceRole.Goalkeeper ||
                    r == TeamAISetPieceRole.RestDefence) continue;
                if (TeamAIMath.FlatDistance(world.Players[i].Position, Targets[i]) > 2.5f) return false;
            }
            return true;
        }

        /// <summary>Classify the current restart for this team.</summary>
        public static TeamAISetPieceKind Classify(TeamAIWorld world, int team)
        {
            if (world.RestartTeam < 0) return TeamAISetPieceKind.None;
            bool ours = world.RestartTeam == team;
            switch (world.MatchPhase)
            {
                case TeamAIMatchPhase.Corner:
                    return ours ? TeamAISetPieceKind.CornerFor : TeamAISetPieceKind.CornerAgainst;
                case TeamAIMatchPhase.Penalty:
                    return ours ? TeamAISetPieceKind.PenaltyFor : TeamAISetPieceKind.PenaltyAgainst;
                case TeamAIMatchPhase.FreeKick:
                {
                    int sign = world.AttackSign(world.RestartTeam);
                    var goal = TeamAIMath.OpponentGoal(sign, world.Pitch);
                    var ball = TeamAIMath.Flat(world.Ball.Position);
                    if (TeamAIMath.FlatDistance(ball, goal) > FreeKickRange) return TeamAISetPieceKind.None;
                    if (TeamAIMath.Depth(ball.Z, sign, world.Pitch) < 0.6f) return TeamAISetPieceKind.None;
                    return ours ? TeamAISetPieceKind.FreeKickFor : TeamAISetPieceKind.FreeKickAgainst;
                }
                default:
                    return TeamAISetPieceKind.None;
            }
        }

        /// <summary>Wall size for a direct free kick at <paramref name="distance"/> m from goal, <paramref name="wide"/> = outside the box width.</summary>
        public static int WallSizeFor(float distance, bool wide)
        {
            int n = distance <= 18f ? 5 : distance <= 22f ? 4 : distance <= 26f ? 3 : distance <= 30f ? 2 : 1;
            if (wide && n > 2) n = 2;
            return n;
        }

        // ------------------------------------------------------------------ routine start

        private void OnFreshRoutine(TeamAIWorld world, int team, int takerIndex, TeamAIRandom rng, in TeamAIDifficulty diff,
            SimVec3 ball)
        {
            FreeKickShot = false;
            PenaltySide = 0;
            var goal = new SimVec3(0f, 0f, _goalZ);
            if (Kind == TeamAISetPieceKind.FreeKickFor)
            {
                float dist = TeamAIMath.FlatDistance(ball, goal);
                bool wide = TeamAIMath.Abs(ball.X) > world.Pitch.PenaltyAreaWidth * 0.5f - 2f;
                float angle = TeamAIMath.GoalAngle(ball, goal, world.Pitch.GoalWidth);
                if (!world.IndirectFreeKick && dist <= 30f && !wide && angle > 0.16f)
                {
                    float shooting = takerIndex >= 0 ? world.Players[takerIndex].Shooting : 0.6f;
                    // Closer, more central and a better striker of the ball: more likely to go for goal.
                    float p = TeamAIMath.Clamp(1.45f - dist / 30f + 0.3f * (shooting - 0.6f) + 0.4f * (angle - 0.25f), 0.1f, 0.95f);
                    FreeKickShot = rng.NextFloat() < p;
                }
            }
            else if (Kind == TeamAISetPieceKind.PenaltyFor)
            {
                // Read the keeper's lean (where he stands, which way he has shuffled), with noise that shrinks
                // with difficulty, then go the other way. A small chance of going down the middle.
                float gkX = 0f;
                for (int i = 0; i < world.PlayerCount; i++)
                    if (world.Players[i].Team != team && world.Players[i].Active && world.Players[i].Role == TeamAIRole.GK)
                        gkX = world.Players[i].Position.X + world.Players[i].Velocity.X * 0.3f;
                float lean = TeamAIMath.Clamp(gkX / 0.8f, -1f, 1f) * 0.6f;
                float noise = rng.NextSigned() * (0.45f + diff.DecisionNoise);
                float read = lean + noise;
                if (rng.NextFloat() < 0.07f) PenaltySide = 0;
                else PenaltySide = read > 0f ? -1 : 1;
            }
        }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Point at lateral offset x (world X) and dz metres in front of the goal under attack.</summary>
        private SimVec3 P(float x, float dz) => new SimVec3(x, 0f, _goalZ + _inward * dz);

        private float Dz(SimVec3 p) => (p.Z - _goalZ) * _inward;

        private SimVec3 GoalkeeperTarget(TeamAIWorld world, int team, SimVec3 ball)
        {
            bool defending = world.RestartTeam != team;
            if (!defending)
            {
                // Attacking set piece: our keeper stays home, edge of his area.
                int sign = world.AttackSign(team);
                return new SimVec3(0f, 0f, -sign * (world.Pitch.HalfLength - 6f));
            }
            switch (Kind)
            {
                case TeamAISetPieceKind.PenaltyAgainst:
                    // Laws 14: on the goal line between the posts until the kick.
                    return P(0f, 0.15f);
                case TeamAISetPieceKind.CornerAgainst:
                    return P(_ns * 0.4f, 0.8f);
                case TeamAISetPieceKind.FreeKickAgainst:
                {
                    // The wall covers the near post; the keeper favours the far half.
                    var onLine = TeamAIGoalkeeper.BisectorOnGoalLine(ball, -world.AttackSign(world.RestartTeam), world.Pitch);
                    float x = TeamAIMath.Clamp(onLine.X - _ns * world.Pitch.GoalWidth * 0.12f,
                        -world.Pitch.GoalWidth * 0.3f, world.Pitch.GoalWidth * 0.3f);
                    return P(x, 1.0f);
                }
                default:
                    return P(0f, 1f);
            }
        }

        /// <summary>How well a role fits a job (0..3).</summary>
        private static float Pref(TeamAIRole r, TeamAISetPieceRole job)
        {
            switch (job)
            {
                case TeamAISetPieceRole.NearPost:
                case TeamAISetPieceRole.FarPost:
                    return r == TeamAIRole.CB || r == TeamAIRole.ST ? 3f : r == TeamAIRole.DM ? 1.5f : 1f;
                case TeamAISetPieceRole.Central:
                    return r == TeamAIRole.ST ? 3f : r == TeamAIRole.CB ? 2.6f : r == TeamAIRole.AM ? 1.5f : 1f;
                case TeamAISetPieceRole.KeeperScreen:
                    return r == TeamAIRole.W || r == TeamAIRole.AM ? 2f : r == TeamAIRole.CM ? 1.5f : 0.8f;
                case TeamAISetPieceRole.EdgeOfBox:
                    return r == TeamAIRole.CM ? 3f : r == TeamAIRole.AM ? 2.5f : r == TeamAIRole.DM ? 2f : 0.8f;
                case TeamAISetPieceRole.ShortOption:
                    return r == TeamAIRole.W ? 3f : r == TeamAIRole.FB ? 2.5f : r == TeamAIRole.AM ? 2f : 0.8f;
                case TeamAISetPieceRole.RestDefence:
                    return r == TeamAIRole.FB ? 3f : r == TeamAIRole.DM ? 2.5f : r == TeamAIRole.CM ? 1.5f : r == TeamAIRole.CB ? 1f : 0.3f;
                case TeamAISetPieceRole.Rebound:
                    return r == TeamAIRole.ST ? 3f : r == TeamAIRole.W || r == TeamAIRole.AM ? 2.5f : r == TeamAIRole.CM ? 2f : 0.5f;
                case TeamAISetPieceRole.ZonalSixYard:
                    return r == TeamAIRole.CB ? 3f : r == TeamAIRole.DM ? 2.5f : r == TeamAIRole.ST ? 2f : 1f;
                case TeamAISetPieceRole.ZonalPost:
                    return r == TeamAIRole.FB ? 3f : r == TeamAIRole.W ? 2.5f : 1f;
                case TeamAISetPieceRole.ZonalEdge:
                    return r == TeamAIRole.CM || r == TeamAIRole.AM ? 3f : r == TeamAIRole.DM ? 2f : 1f;
                case TeamAISetPieceRole.Wall:
                    return r == TeamAIRole.W || r == TeamAIRole.AM ? 3f : r == TeamAIRole.ST || r == TeamAIRole.CM ? 2.5f :
                        r == TeamAIRole.CB ? 0.2f : 1.5f;
                case TeamAISetPieceRole.Outlet:
                    return r == TeamAIRole.ST ? 3f : r == TeamAIRole.W ? 2.5f : r == TeamAIRole.AM ? 1.5f : 0.2f;
                case TeamAISetPieceRole.Marker:
                    return r == TeamAIRole.CB ? 3f : r == TeamAIRole.FB || r == TeamAIRole.DM ? 2.5f : r == TeamAIRole.CM ? 2f : 1f;
                case TeamAISetPieceRole.PenaltyEdge:
                    return r == TeamAIRole.CB || r == TeamAIRole.FB ? 3f : r == TeamAIRole.DM ? 2.5f : r == TeamAIRole.CM ? 2f : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>Give the job at <paramref name="spot"/> to the best free player. Returns the index or -1.</summary>
        private int Assign(TeamAIWorld world, TeamAISetPieceRole job, SimVec3 spot)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (_used[i]) continue;
                float score = Pref(world.Players[i].Role, job) - 0.015f * TeamAIMath.FlatDistance(world.Players[i].Position, spot);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) return -1;
            _used[best] = true;
            Roles[best] = job;
            Targets[best] = spot;
            return best;
        }

        private int FreeCount(TeamAIWorld world)
        {
            int n = 0;
            for (int i = 0; i < world.PlayerCount; i++) if (!_used[i]) n++;
            return n;
        }

        private void FillRestDefence(TeamAIWorld world, float dz)
        {
            int k = 0;
            while (FreeCount(world) > 0)
            {
                float x = k == 0 ? -10f : k == 1 ? 10f : k == 2 ? 0f : (k % 2 == 0 ? -20f : 20f);
                Assign(world, TeamAISetPieceRole.RestDefence, P(x, dz + (k >= 2 ? 6f : 0f)));
                k++;
            }
        }

        /// <summary>
        /// Opponents (of <paramref name="team"/>) inside the danger zone (dz &lt; maxDz, |x| &lt; maxX), most dangerous first.
        /// Returns the count written to _threats.
        /// </summary>
        private int CollectThreats(TeamAIWorld world, int team, float maxDz, float maxX)
        {
            int n = 0;
            int taker = world.OwnerIndex;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team == team || o.Role == TeamAIRole.GK || j == taker) continue;
                float dz = Dz(o.Position);
                if (dz > maxDz || dz < -1f || TeamAIMath.Abs(o.Position.X) > maxX) continue;
                // Insertion sort by danger (distance to goal centre).
                float d = Danger(o.Position);
                int k = n++;
                while (k > 0 && Danger(world.Players[_threats[k - 1]].Position) > d) { _threats[k] = _threats[k - 1]; k--; }
                _threats[k] = j;
            }
            return n;
        }

        private float Danger(SimVec3 p) => TeamAIMath.FlatDistance(p, new SimVec3(0f, 0f, _goalZ));

        /// <summary>Man-mark the threats goal-side, nearest free defender first.</summary>
        private void AssignMarkers(TeamAIWorld world, int threatCount, int maxMarkers)
        {
            var goal = new SimVec3(0f, 0f, _goalZ);
            int made = 0;
            for (int k = 0; k < threatCount && made < maxMarkers; k++)
            {
                int opp = _threats[k];
                var op = world.Players[opp].Position;
                var spot = op + TeamAIMath.FlatDirection(op, goal) * 1.0f;
                int m = Assign(world, TeamAISetPieceRole.Marker, spot);
                if (m < 0) return;
                MarkIndex[m] = opp;
                made++;
            }
        }

        // ------------------------------------------------------------------ routines

        private void PlanCornerFor(TeamAIWorld world)
        {
            float hw = _pitch.HalfWidth;
            Assign(world, TeamAISetPieceRole.NearPost, P(_ns * 2.5f, 5.5f));
            Assign(world, TeamAISetPieceRole.FarPost, P(-_ns * 3.5f, 6.5f));
            Assign(world, TeamAISetPieceRole.Central, P(-_ns * 0.5f, 10.5f));
            Assign(world, TeamAISetPieceRole.KeeperScreen, P(_ns * 0.8f, 2.0f));
            Assign(world, TeamAISetPieceRole.EdgeOfBox, P(-_ns * 1.5f, 18.5f));
            Assign(world, TeamAISetPieceRole.ShortOption, P(_ns * (hw - 5.5f), 4.5f));
            FillRestDefence(world, _pitch.HalfLength - 6f);
        }

        private void PlanCornerAgainst(TeamAIWorld world, int team, SimVec3 ball)
        {
            float hgw = _pitch.GoalWidth * 0.5f;
            // Zonal: near post, three across the six-yard line.
            Assign(world, TeamAISetPieceRole.ZonalPost, P(_ns * (hgw - 0.3f), 0.8f));
            Assign(world, TeamAISetPieceRole.ZonalSixYard, P(_ns * 2.5f, 5.0f));
            Assign(world, TeamAISetPieceRole.ZonalSixYard, P(0f, 5.5f));
            Assign(world, TeamAISetPieceRole.ZonalSixYard, P(-_ns * 2.5f, 5.0f));
            // One outlet for the counter.
            Assign(world, TeamAISetPieceRole.Outlet, P(-_ns * 8f, _pitch.HalfLength - 12f));
            // Man: the rest pick up the most dangerous attackers.
            int threats = CollectThreats(world, team, 22f, _pitch.PenaltyAreaWidth * 0.5f + 6f);
            AssignMarkers(world, threats, FreeCount(world));
            // Nobody left to mark: hold the edge of the box.
            int k = 0;
            while (FreeCount(world) > 0)
            {
                Assign(world, TeamAISetPieceRole.ZonalEdge, P(k % 2 == 0 ? -6f + k : 6f - k, 15f));
                k++;
            }
        }

        private void PlanPenaltyFor(TeamAIWorld world)
        {
            float edge = _pitch.PenaltyAreaDepth + 1.0f;
            Assign(world, TeamAISetPieceRole.Rebound, P(-7.5f, edge));
            Assign(world, TeamAISetPieceRole.Rebound, P(7.5f, edge));
            Assign(world, TeamAISetPieceRole.Rebound, P(-11f, edge));
            Assign(world, TeamAISetPieceRole.Rebound, P(11f, edge));
            FillRestDefence(world, _pitch.HalfLength - 4f);
        }

        private void PlanPenaltyAgainst(TeamAIWorld world)
        {
            float edge = _pitch.PenaltyAreaDepth + 1.0f;
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(-9f, edge));
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(9f, edge));
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(-13f, edge));
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(13f, edge));
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(-16.5f, edge - 1f));
            Assign(world, TeamAISetPieceRole.PenaltyEdge, P(16.5f, edge - 1f));
            Assign(world, TeamAISetPieceRole.Outlet, P(0f, _pitch.HalfLength - 12f));
            int k = 0;
            while (FreeCount(world) > 0)
            {
                Assign(world, TeamAISetPieceRole.PenaltyEdge, P(k % 2 == 0 ? -4f - k : 4f + k, edge + 4f));
                k++;
            }
        }

        private float OffsideDz(TeamAIWorld world, int attackTeam)
        {
            // Second-last defender of the defending team, as dz from the goal under attack.
            float d1 = 999f, d2 = 999f;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                var o = world.Players[j];
                if (!o.Active || o.Team == attackTeam) continue;
                float dz = Dz(o.Position);
                if (dz < d1) { d2 = d1; d1 = dz; }
                else if (dz < d2) d2 = dz;
            }
            return d2 > 900f ? 0f : d2;
        }

        private void PlanFreeKickFor(TeamAIWorld world, int team, SimVec3 ball)
        {
            float line = OffsideDz(world, team) + 0.6f;
            float ballDz = Dz(ball);
            if (FreeKickShot)
            {
                // Follow up for rebounds, level with the last line so nobody is offside.
                float dz = Math.Max(line, 7f);
                Assign(world, TeamAISetPieceRole.Rebound, P(_ns * 4f, dz));
                Assign(world, TeamAISetPieceRole.Rebound, P(-_ns * 4f, dz));
                Assign(world, TeamAISetPieceRole.Rebound, P(0f, dz + 1.5f));
                Assign(world, TeamAISetPieceRole.ShortOption, ball + new SimVec3(-_ns * 4f, 0f, -_inward * 1.5f));
            }
            else
            {
                Assign(world, TeamAISetPieceRole.NearPost, P(_ns * 2.5f, Math.Max(line, 6f)));
                Assign(world, TeamAISetPieceRole.FarPost, P(-_ns * 3.5f, Math.Max(line, 7f)));
                Assign(world, TeamAISetPieceRole.Central, P(0f, Math.Max(line, 10f)));
                Assign(world, TeamAISetPieceRole.EdgeOfBox, P(-_ns * 2f, Math.Max(18.5f, Math.Min(ballDz, 22f))));
                Assign(world, TeamAISetPieceRole.ShortOption, ball + new SimVec3(-_ns * 5f, 0f, -_inward * 1f));
            }
            FillRestDefence(world, _pitch.HalfLength - 6f);
        }

        private void PlanFreeKickAgainst(TeamAIWorld world, int team, SimVec3 ball)
        {
            var goal = new SimVec3(0f, 0f, _goalZ);
            float dist = TeamAIMath.FlatDistance(ball, goal);
            bool wide = TeamAIMath.Abs(ball.X) > _pitch.PenaltyAreaWidth * 0.5f - 2f;
            WallSize = WallSizeFor(dist, wide);

            // Wall: first man just outside the line to the near post, the rest step across toward the far post.
            float hgw = _pitch.GoalWidth * 0.5f;
            var nearPost = new SimVec3(_ns * hgw, 0f, _goalZ);
            var farPost = new SimVec3(-_ns * hgw, 0f, _goalZ);
            var u = TeamAIMath.FlatDirection(ball, nearPost);
            var perp = new SimVec3(-u.Z, 0f, u.X);
            if (SimVec3.Dot(perp, farPost - nearPost) < 0f) perp = perp * -1f;
            var basePoint = ball + u * (TeamAIBrain.FreeKickDistance + 0.15f);
            for (int k = 0; k < WallSize; k++)
                Assign(world, TeamAISetPieceRole.Wall, basePoint + perp * (k * WallSpacing - 0.35f));

            Assign(world, TeamAISetPieceRole.Outlet, P(-_ns * 6f, _pitch.HalfLength - 14f));
            float ballDz = Dz(ball);
            int threats = CollectThreats(world, team, Math.Max(ballDz + 4f, 20f), _pitch.PenaltyAreaWidth * 0.5f + 4f);
            AssignMarkers(world, threats, FreeCount(world));
            float lineDz = TeamAIMath.Clamp(ballDz - 8f, 6f, 13f);
            int j = 0;
            while (FreeCount(world) > 0)
            {
                Assign(world, TeamAISetPieceRole.ZonalEdge, P(j % 2 == 0 ? -3f - 3f * j : 3f + 3f * j, lineDz));
                j++;
            }
        }

        // ------------------------------------------------------------------ taker

        /// <summary>
        /// The taker's choice for a routine set piece. Writes <paramref name="best"/> and returns true when the routine
        /// owns the decision (corner, penalty, free kick near the box).
        /// </summary>
        public bool ChooseTakerOption(TeamAIWorld world, int ownerIndex, in TeamAIDifficulty diff, TeamAIRandom rng,
            out TeamAIOption best)
        {
            best = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            var owner = world.Players[ownerIndex];
            var ball = TeamAIMath.Flat(world.Ball.Position);
            var goal = new SimVec3(0f, 0f, _goalZ);
            float hgw = _pitch.GoalWidth * 0.5f;
            switch (Kind)
            {
                case TeamAISetPieceKind.PenaltyFor:
                {
                    float x = PenaltySide * (hgw - 0.75f);
                    best = new TeamAIOption
                    {
                        Action = TeamAIAction.Shoot, Intent = TeamAIIntent.Shoot, Target = new SimVec3(x, 0f, _goalZ),
                        TargetIndex = -1, Score = 1f, Power = PenaltySide == 0 ? 0.6f : 0.78f
                    };
                    return true;
                }
                case TeamAISetPieceKind.FreeKickFor when FreeKickShot:
                {
                    float dist = TeamAIMath.FlatDistance(ball, goal);
                    // Over and around the wall into the far side, or driven from range.
                    var aim = new SimVec3(-_ns * (hgw - 0.6f), 0f, _goalZ);
                    bool finesse = dist <= 25f;
                    best = new TeamAIOption
                    {
                        Action = finesse ? TeamAIAction.FinesseShot : TeamAIAction.DrivenShot, Intent = TeamAIIntent.Shoot,
                        Target = aim, TargetIndex = -1, Score = 1f, Power = finesse ? 0.75f : 1f
                    };
                    return true;
                }
                case TeamAISetPieceKind.CornerFor:
                case TeamAISetPieceKind.FreeKickFor:
                {
                    for (int i = 0; i < world.PlayerCount; i++)
                    {
                        var r = Roles[i];
                        float baseScore;
                        TeamAIAction action = TeamAIAction.Cross;
                        switch (r)
                        {
                            case TeamAISetPieceRole.NearPost: baseScore = 0.5f; break;
                            case TeamAISetPieceRole.FarPost: baseScore = 0.45f; break;
                            case TeamAISetPieceRole.Central: baseScore = 0.5f; break;
                            case TeamAISetPieceRole.EdgeOfBox: baseScore = 0.3f; break;
                            case TeamAISetPieceRole.ShortOption: baseScore = 0.22f; action = TeamAIAction.Pass; break;
                            default: continue;
                        }
                        var rec = world.Players[i];
                        var target = TeamAIMath.Flat(rec.Position);
                        float dist = TeamAIMath.FlatDistance(ball, target);
                        if (dist < 3f) continue;
                        float tBall = action == TeamAIAction.Pass
                            ? TeamAIBallModel.Default.GroundPassTime(dist)
                            : TeamAIBallModel.Default.CrossTime(dist);
                        float control = TeamAIPitchControl.Evaluate(world, owner.Team, target, tBall, ownerIndex);
                        float tight = TeamAIOnBall.NearestOpponentDistance(world, owner.Team, target);
                        float score = baseScore + 0.5f * control - (tight < 1.5f ? 0.15f * (1.5f - tight) : 0f);
                        // Some variety at every level so routines cannot be read.
                        score += rng.NextSigned() * (diff.DecisionNoise + 0.15f);
                        if (score > best.Score)
                        {
                            float power = action == TeamAIAction.Pass
                                ? Math.Min(TeamAIMath.Clamp(dist / TeamAIOnBall.MaxGroundPass, 0.12f, 1f),
                                    TeamAIBallModel.Default.GroundPassPower(dist, rec.BallControl))
                                : TeamAIMath.Clamp(dist / 45f, 0.35f, 1f);
                            best = new TeamAIOption
                            {
                                Action = action, Intent = TeamAIIntent.Pass, Target = target, TargetIndex = i,
                                Score = score, Control = control, Power = power
                            };
                        }
                    }
                    return best.Score > -998f;
                }
                default:
                    return false;
            }
        }
    }
}
