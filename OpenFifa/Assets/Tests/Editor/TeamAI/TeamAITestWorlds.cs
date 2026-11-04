using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Scenario builders and a tiny kinematic loop for team AI tests (no NUnit dependency).</summary>
    public static class TeamAITestWorlds
    {
        /// <summary>22 players in their formation anchors (team 0 attacks +Z), ball at centre with no owner.</summary>
        public static TeamAIWorld FullMatch(TeamAITactics home, TeamAITactics away)
        {
            var pitch = PitchSpec.Standard;
            var world = new TeamAIWorld(pitch);
            var anchors = new SimVec3[TeamAIFormations.SlotCount];
            int n = 0;
            for (int team = 0; team < 2; team++)
            {
                var tac = team == 0 ? home : away;
                int sign = world.AttackSign(team);
                TeamAIFormationShape.ComputeAnchors(TeamAIPhase.MidBlock, tac, SimVec3.Zero, sign, pitch, anchors);
                // Keep everyone in their own half at the start.
                for (int s = 0; s < TeamAIFormations.SlotCount; s++)
                {
                    var a = anchors[s];
                    float d = TeamAIMath.Depth(a.Z, sign, pitch);
                    if (d > 0.48f) a = new SimVec3(a.X, 0f, TeamAIMath.DepthToZ(0.48f, sign, pitch));
                    if (s == 0) a = new SimVec3(0f, 0f, TeamAIMath.DepthToZ(0.02f, sign, pitch));
                    world.Players[n] = TeamAIPlayer.Create(n, team, TeamAIFormations.RoleOf(tac.Formation, s), s, a);
                    n++;
                }
            }
            world.PlayerCount = n;
            world.Ball.Position = SimVec3.Zero;
            return world;
        }

        public static TeamAIWorld Empty()
        {
            var w = new TeamAIWorld(PitchSpec.Standard);
            w.PlayerCount = 0;
            return w;
        }

        public static int Add(TeamAIWorld w, int team, TeamAIRole role, int slot, float x, float z)
        {
            int i = w.PlayerCount;
            w.Players[i] = TeamAIPlayer.Create(i, team, role, slot, new SimVec3(x, 0f, z));
            w.PlayerCount++;
            return i;
        }

        public static void GiveBall(TeamAIWorld w, int index, float offsetZ = 0.6f)
        {
            var p = w.Players[index];
            float dir = w.AttackSign(p.Team);
            w.Ball.Position = new SimVec3(p.Position.X, 0.11f, p.Position.Z + offsetZ * dir);
            w.Ball.Velocity = SimVec3.Zero;
            w.Ball.OwnerId = p.Id;
            w.Ball.LastTouchId = p.Id;
            w.Ball.LastTouchTeam = p.Team;
        }

        /// <summary>
        /// Crude kinematic stand-in for the match sim: players move toward their command, the owner kicks
        /// on pass/shot actions, the closest player within 1 m of a slow ball takes it. Deterministic.
        /// </summary>
        public static void Step(TeamAIWorld w, AIPlayerCommand[] cmds, float dt)
        {
            w.Time += dt;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                ref var p = ref w.Players[i];
                var c = cmds[i];
                float speed = p.MaxSpeed * (c.Sprint ? 1f : 0.6f);
                p.Velocity = c.MoveDirection * speed;
                p.Position = TeamAIMath.ClampToPitch(p.Position + p.Velocity * dt, w.Pitch, 0.2f);
                if (c.MoveDirection.SqrMagnitude > 0.01f) p.Facing = c.MoveDirection;
            }

            int owner = w.OwnerIndex;
            if (owner >= 0)
            {
                var c = cmds[owner];
                var p = w.Players[owner];
                bool kick = c.Action == TeamAIAction.Pass || c.Action == TeamAIAction.ThroughPass ||
                            c.Action == TeamAIAction.LobPass || c.Action == TeamAIAction.Cross ||
                            c.Action == TeamAIAction.Shoot || c.Action == TeamAIAction.FinesseShot ||
                            c.Action == TeamAIAction.DrivenShot;
                if (kick && c.Aim.SqrMagnitude > 0.5f)
                {
                    w.Ball.Velocity = c.Aim * (8f + 20f * c.Power);
                    w.Ball.OwnerId = -1;
                    w.Ball.Position = new SimVec3(w.Ball.Position.X, 0.11f, w.Ball.Position.Z) + c.Aim * 1.2f;
                    return;
                }
                w.Ball.Position = new SimVec3(p.Position.X + p.Facing.X * 0.6f, 0.11f, p.Position.Z + p.Facing.Z * 0.6f);
                w.Ball.Velocity = p.Velocity;
                // Simple tackle resolution.
                for (int i = 0; i < w.PlayerCount; i++)
                {
                    if (w.Players[i].Team == p.Team) continue;
                    if (cmds[i].Action == TeamAIAction.Tackle &&
                        TeamAIMath.FlatDistance(w.Players[i].Position, w.Ball.Position) < 1.2f)
                    {
                        TakeBall(w, i);
                        return;
                    }
                }
                return;
            }

            // Loose ball rolls.
            float decay = 1f - 0.45f * dt;
            w.Ball.Position = w.Ball.Position + w.Ball.Velocity * dt;
            w.Ball.Velocity = w.Ball.Velocity * decay;
            float hx = w.Pitch.HalfWidth, hz = w.Pitch.HalfLength;
            if (w.Ball.Position.X < -hx || w.Ball.Position.X > hx || w.Ball.Position.Z < -hz || w.Ball.Position.Z > hz)
            {
                // Out: give it to the nearest player of the team that did not touch it last.
                w.Ball.Position = TeamAIMath.ClampToPitch(w.Ball.Position, w.Pitch, 1f);
                w.Ball.Velocity = SimVec3.Zero;
            }
            int best = -1;
            float bd = 1.0f;
            bool fast = w.Ball.Velocity.Flat.Magnitude > 4f;
            for (int i = 0; i < w.PlayerCount; i++)
            {
                if (fast && w.Players[i].Id == w.Ball.LastTouchId) continue;
                float d = TeamAIMath.FlatDistance(w.Players[i].Position, w.Ball.Position);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0 && w.Ball.Velocity.Flat.Magnitude < 18f) TakeBall(w, best);
        }

        private static void TakeBall(TeamAIWorld w, int i)
        {
            w.Ball.OwnerId = w.Players[i].Id;
            w.Ball.LastTouchId = w.Players[i].Id;
            w.Ball.LastTouchTeam = w.Players[i].Team;
            w.Ball.Velocity = SimVec3.Zero;
        }
    }
}
