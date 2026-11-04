using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Goalkeeper positioning and actions: stands on the bisector of the angle the ball makes with the posts,
    /// sweeps balls played behind the line, claims crosses and closes down 1v1s. Distribution uses TeamAIOnBall.
    /// </summary>
    public static class TeamAIGoalkeeper
    {
        /// <summary>
        /// Point where the bisector of the ball-post angle meets the goal line (angle bisector theorem).
        /// </summary>
        public static SimVec3 BisectorOnGoalLine(SimVec3 ball, int attackSign, PitchSpec pitch)
        {
            var goal = TeamAIMath.OwnGoal(attackSign, pitch);
            float hw = pitch.GoalWidth * 0.5f;
            var left = new SimVec3(goal.X - hw, 0f, goal.Z);
            var right = new SimVec3(goal.X + hw, 0f, goal.Z);
            float dl = TeamAIMath.FlatDistance(ball, left);
            float dr = TeamAIMath.FlatDistance(ball, right);
            float t = dl + dr > 1e-4f ? dl / (dl + dr) : 0.5f;
            return new SimVec3(left.X + (right.X - left.X) * t, 0f, goal.Z);
        }

        /// <summary>
        /// Positional target on the angle bisector. Comes off the line as the ball approaches, narrows the angle
        /// in 1v1 range and becomes a sweeper when the ball is in the opponent's half.
        /// </summary>
        public static SimVec3 PositionTarget(SimVec3 ball, int attackSign, PitchSpec pitch, float backLineDepth)
        {
            var flatBall = TeamAIMath.Flat(ball);
            var p = BisectorOnGoalLine(flatBall, attackSign, pitch);
            var u = TeamAIMath.FlatDirection(p, flatBall);
            // Keep a component out of the goal even when the ball is near the goal line.
            float forward = u.Z * attackSign;
            if (forward < 0.15f)
            {
                u = TeamAIMath.FlatDirection(SimVec3.Zero, new SimVec3(u.X, 0f, 0.15f * attackSign));
            }
            float ballDist = TeamAIMath.FlatDistance(p, flatBall);
            float ballDepth = TeamAIMath.Depth(flatBall.Z, attackSign, pitch);
            float d;
            if (ballDepth > 0.5f)
            {
                // Sweeper-keeper: stay ~12 m behind the back line.
                float lineDist = backLineDepth * pitch.Length;
                d = TeamAIMath.Clamp(lineDist - 12f, 4f, 16f);
            }
            else
            {
                d = TeamAIMath.Clamp(1.2f + 0.1f * ballDist, 1.2f, 4.5f);
                if (ballDist < 12f) d = Math.Min(d, ballDist * 0.35f);
            }
            return p + u * d;
        }

        /// <summary>
        /// Out-of-possession keeper behaviour (positioning, sweeping, claiming, smothering).
        /// Returns true when the keeper should commit to the ball (sweep/claim).
        /// </summary>
        public static void Decide(TeamAIWorld world, int gkIndex, float backLineDepth, bool teamHasPossession,
            ref AIPlayerCommand cmd) => Decide(world, gkIndex, backLineDepth, teamHasPossession, null, ref cmd);

        /// <summary>Goal-line spot for a penalty against: on the line between the posts until the kick (Law 14).</summary>
        public static SimVec3 PenaltyLineTarget(int attackSign, PitchSpec pitch) =>
            new SimVec3(0f, 0f, -attackSign * (pitch.HalfLength - 0.15f));

        /// <summary>
        /// Out-of-possession keeper behaviour. <paramref name="trajectory"/> is this tick's sim-matched free-ball
        /// prediction (null falls back to <see cref="TeamAIMath.PredictBall"/>).
        /// </summary>
        public static void Decide(TeamAIWorld world, int gkIndex, float backLineDepth, bool teamHasPossession,
            TeamAIBallTrajectory trajectory, ref AIPlayerCommand cmd)
        {
            var gk = world.Players[gkIndex];
            int sign = world.AttackSign(gk.Team);
            var pitch = world.Pitch;
            var ball = world.Ball;
            var ownGoal = TeamAIMath.OwnGoal(sign, pitch);
            var flatBall = TeamAIMath.Flat(ball.Position);

            cmd.Action = TeamAIAction.None;
            cmd.Power = 0f;
            cmd.Aim = SimVec3.Zero;
            cmd.TargetPlayerId = -1;

            // 0) Penalty against: stay on the line until the kick.
            if (world.MatchPhase == TeamAIMatchPhase.Penalty && world.RestartTeam >= 0 && world.RestartTeam != gk.Team)
            {
                SetMove(ref cmd, gk, PenaltyLineTarget(sign, pitch), false, TeamAIIntent.GKPosition);
                return;
            }

            // 1) Claim a cross: ball in the air heading for our box.
            if (ball.OwnerId < 0 && ball.Position.Y > 1.2f && trajectory != null)
            {
                // First moment the (drag-affected) ball comes down through catchable height.
                for (int k = 3; k <= 60; k++)
                {
                    float t = k * TeamAIBallTrajectory.SampleDt;
                    float h = trajectory.HeightAt(t);
                    if (h > 2.2f || h >= trajectory.HeightAt(t - TeamAIBallTrajectory.SampleDt)) continue;
                    var land = trajectory.At(t);
                    float distToGoal = TeamAIMath.FlatDistance(land, ownGoal);
                    bool inZone = distToGoal < 13f && TeamAIMath.Abs(land.X - ownGoal.X) < 11f;
                    if (inZone && TeamAIMath.TimeToIntercept(gk, land, 0.25f) < t + 0.15f)
                    {
                        SetMove(ref cmd, gk, land, true, TeamAIIntent.GKClaim);
                        return;
                    }
                    break;
                }
            }
            else if (ball.OwnerId < 0 && ball.Position.Y > 1.2f)
            {
                float vy = ball.Velocity.Y, y = ball.Position.Y;
                // Time until the ball drops to 2.2 m (catchable): y + vy t - 4.905 t^2 = 2.2
                float a = -4.905f, b = vy, c = y - 2.2f;
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float tLand = (-b - (float)Math.Sqrt(disc)) / (2f * a);
                    if (tLand > 0.1f && tLand < 3f)
                    {
                        var land = new SimVec3(ball.Position.X + ball.Velocity.X * tLand, 0f,
                            ball.Position.Z + ball.Velocity.Z * tLand);
                        float distToGoal = TeamAIMath.FlatDistance(land, ownGoal);
                        bool inZone = distToGoal < 13f && TeamAIMath.Abs(land.X - ownGoal.X) < 11f;
                        if (inZone && TeamAIMath.TimeToIntercept(gk, land, 0.25f) < tLand + 0.15f)
                        {
                            SetMove(ref cmd, gk, land, true, TeamAIIntent.GKClaim);
                            return;
                        }
                    }
                }
            }

            // 2) Sweep a loose ball played in behind the back line.
            if (ball.OwnerId < 0 && !teamHasPossession)
            {
                float bestOpp = 999f, gkT = 999f;
                SimVec3 bestPoint = flatBall;
                for (int k = 0; k <= 12; k++)
                {
                    float t = k * 0.25f;
                    var bp = trajectory != null ? trajectory.At(t) : TeamAIMath.PredictBall(ball.Position, ball.Velocity, t);
                    float depth = TeamAIMath.Depth(bp.Z, sign, pitch);
                    if (depth > backLineDepth || TeamAIMath.FlatDistance(bp, ownGoal) > 32f) continue;
                    float tg = TeamAIMath.TimeToIntercept(gk, bp, 0.2f);
                    if (tg <= t && gkT > 900f) { gkT = t; bestPoint = bp; }
                    if (bestOpp > 900f)
                    {
                        for (int i = 0; i < world.PlayerCount; i++)
                        {
                            if (!world.Players[i].Active || world.Players[i].Team == gk.Team) continue;
                            if (TeamAIMath.TimeToIntercept(world.Players[i], bp, 0.2f) <= t) { bestOpp = t; break; }
                        }
                    }
                }
                if (gkT < 900f && gkT + 0.2f < bestOpp)
                {
                    SetMove(ref cmd, gk, bestPoint, true, TeamAIIntent.GKSweep);
                    return;
                }
            }

            // 3) Smother a 1v1: opponent carrying into the box with no defender goal-side.
            int owner = world.OwnerIndex;
            if (owner >= 0 && world.Players[owner].Team != gk.Team)
            {
                float d = TeamAIMath.FlatDistance(flatBall, ownGoal);
                if (d < 14f && !DefenderGoalSide(world, gk.Team, gkIndex, flatBall, ownGoal))
                {
                    var target = flatBall + TeamAIMath.FlatDirection(flatBall, ownGoal) * 1.0f;
                    SetMove(ref cmd, gk, target, true, TeamAIIntent.GKSweep);
                    return;
                }
            }

            // 4) Position on the angle bisector.
            var pos = PositionTarget(flatBall, sign, pitch, backLineDepth);
            SetMove(ref cmd, gk, pos, TeamAIMath.FlatDistance(gk.Position, pos) > 6f, TeamAIIntent.GKPosition);
        }

        private static bool DefenderGoalSide(TeamAIWorld world, int team, int gkIndex, SimVec3 ball, SimVec3 goal)
        {
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (i == gkIndex || !world.Players[i].Active || world.Players[i].Team != team) continue;
                var p = world.Players[i].Position;
                float s = TeamAIMath.SegmentParam(ball, goal, p);
                if (s > 0.02f && s < 0.95f && TeamAIMath.DistanceToLine(ball, goal, p) < 2.5f) return true;
            }
            return false;
        }

        private static void SetMove(ref AIPlayerCommand cmd, in TeamAIPlayer gk, SimVec3 target, bool sprint, TeamAIIntent intent)
        {
            cmd.MoveTarget = target;
            float dist = TeamAIMath.FlatDistance(gk.Position, target);
            cmd.MoveDirection = dist > 0.3f ? TeamAIMath.FlatDirection(gk.Position, target) : SimVec3.Zero;
            cmd.Sprint = sprint;
            cmd.Intent = intent;
            cmd.ActionTarget = target;
        }
    }
}
