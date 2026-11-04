using System;

namespace OpenFifa.Core
{
    /// <summary>Scored on-ball option (for debugging and tests).</summary>
    public struct TeamAIOption
    {
        public TeamAIAction Action;
        public TeamAIIntent Intent;
        public SimVec3 Target;
        public int TargetIndex;
        public float Score;
        public float Control;
        public float Risk;
        public float Power;
    }

    /// <summary>Context flags for the on-ball decision.</summary>
    public struct TeamAIOnBallContext
    {
        public bool IsGoalkeeper;
        public bool SetPiece;
        public TeamAIMatchPhase MatchPhase;
        /// <summary>Offside line depth for our attackers (0..1 from our goal).</summary>
        public float OffsideDepth;
    }

    /// <summary>
    /// On-ball utility AI: scores passes (ground, through, lob, cross) by pitch control at the receiver,
    /// progression, threat and interception risk; shots by an xG-style model; carries, shielding and
    /// clearances. Allocation-free.
    /// </summary>
    public sealed class TeamAIOnBall
    {
        public const float ShotWeight = 3.0f;
        public const float MaxGroundPass = 42f;
        public const float MaxLobPass = 60f;

        /// <summary>Best pass option of the last decision.</summary>
        public TeamAIOption LastBestPass;
        /// <summary>Shot option of the last decision (Score = -1 when not considered).</summary>
        public TeamAIOption LastShot;
        /// <summary>Best carry option of the last decision.</summary>
        public TeamAIOption LastCarry;
        /// <summary>Chosen option of the last decision.</summary>
        public TeamAIOption LastChoice;

        /// <summary>Approximate expected threat of having the ball at a point (0..~0.2).</summary>
        public static float Threat(SimVec3 p, int attackSign, PitchSpec pitch)
        {
            float d = TeamAIMath.Clamp01(TeamAIMath.Depth(p.Z, attackSign, pitch));
            float central = 1f - 0.6f * TeamAIMath.Clamp01(TeamAIMath.Abs(p.X) / pitch.HalfWidth);
            float d2 = d * d;
            return 0.01f + 0.15f * d2 * d2 * d2 * central;
        }

        /// <summary>xG-style shot value from distance and goal angle (logistic fit, open play).</summary>
        public static float ExpectedGoals(SimVec3 from, int attackSign, PitchSpec pitch)
        {
            var goal = TeamAIMath.OpponentGoal(attackSign, pitch);
            float dist = TeamAIMath.FlatDistance(from, goal);
            float angle = TeamAIMath.GoalAngle(from, goal, pitch.GoalWidth);
            return TeamAIMath.Sigmoid(-1.25f + 1.6f * angle - 0.08f * dist);
        }

        public static float GroundPassTime(float dist)
        {
            float v0 = TeamAIMath.Clamp(9f + 0.45f * dist, 10f, 25f);
            return dist / (0.8f * v0);
        }

        public static float LobPassTime(float dist) => 0.9f + dist / 20f;

        /// <summary>
        /// Probability-like risk (0..1) that an opponent intercepts a pass from `from` to `to` arriving after
        /// `travelTime`. Lobs ignore opponents under the flight path except near the passer and the landing zone.
        /// </summary>
        public static float InterceptionRisk(TeamAIWorld world, int team, SimVec3 from, SimVec3 to, float travelTime, bool lob)
        {
            float worst = 0f;
            var players = world.Players;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!players[i].Active || players[i].Team == team) continue;
                var pos = players[i].Position;
                float s = TeamAIMath.SegmentParam(from, to, pos);
                if (lob && s > 0.08f && s < 0.8f) continue;
                if (s <= 0f && TeamAIMath.FlatDistance(from, pos) > 1.5f) continue; // behind the passer
                var point = new SimVec3(from.X + (to.X - from.X) * s, 0f, from.Z + (to.Z - from.Z) * s);
                float tb = travelTime * s;
                // 0.25 s reaction (watching the ball) and ~1 m reach.
                float tOpp = TeamAIMath.TimeToIntercept(players[i], point, 0.25f) - 1.0f / Math.Max(players[i].MaxSpeed, 0.5f);
                float risk = TeamAIMath.Sigmoid((tb - tOpp) * 5f);
                if (risk > worst) worst = risk;
            }
            return worst;
        }

        /// <summary>
        /// Decide what the ball owner does. Writes the action fields of `cmd` (and a carry move when dribbling).
        /// </summary>
        public void Decide(TeamAIWorld world, int ownerIndex, TeamAIPhase phase, in TeamAIDifficulty diff,
            TeamAIRandom rng, in TeamAIOnBallContext ctx, ref AIPlayerCommand cmd)
        {
            var players = world.Players;
            var owner = players[ownerIndex];
            int team = owner.Team;
            int sign = world.AttackSign(team);
            var pitch = world.Pitch;
            var ballPos = TeamAIMath.Flat(world.Ball.Position);
            var attackDir = new SimVec3(0f, 0f, sign);
            float ownerDepth = TeamAIMath.Depth(ballPos.Z, sign, pitch);
            float pressure = NearestOpponentDistance(world, team, ballPos);

            LastBestPass = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            LastShot = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            LastCarry = new TeamAIOption { Score = -999f, TargetIndex = -1 };

            // ---- Passes ----
            bool crossZone = ownerDepth > 0.76f && TeamAIMath.Abs(ballPos.X) > pitch.PenaltyAreaWidth * 0.5f - 2f;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                if (j == ownerIndex || !players[j].Active || players[j].Team != team) continue;
                var rec = players[j];
                float recDepth = TeamAIMath.Depth(rec.Position.Z, sign, pitch);
                bool offside = recDepth > ctx.OffsideDepth + 0.004f && recDepth > 0.5f && !ctx.SetPiece;
                if (rec.Role == TeamAIRole.GK && ownerDepth > 0.45f) continue;

                // Ground pass to feet (slightly led).
                var feet = TeamAIMath.ClampToPitch(rec.Position + rec.Velocity * 0.4f, pitch, 0.5f);
                if (!offside)
                {
                    float dist = TeamAIMath.FlatDistance(ballPos, feet);
                    if (dist > 4f && dist < MaxGroundPass)
                        Consider(world, ownerIndex, j, feet, TeamAIAction.Pass, dist, false, owner, sign, ownerDepth, diff, rng);
                    if (dist > 16f && dist < MaxLobPass && !ctx.IsGoalkeeper)
                        Consider(world, ownerIndex, j, feet, TeamAIAction.LobPass, dist, true, owner, sign, ownerDepth, diff, rng);
                    if (ctx.IsGoalkeeper && dist > 25f && dist < MaxLobPass + 10f)
                        Consider(world, ownerIndex, j, feet, TeamAIAction.LobPass, dist, true, owner, sign, ownerDepth, diff, rng);
                }

                // Through ball into space for forward-minded players who are onside now.
                bool forwardRole = rec.Role == TeamAIRole.ST || rec.Role == TeamAIRole.W || rec.Role == TeamAIRole.AM ||
                                   (rec.Role == TeamAIRole.FB && phase != TeamAIPhase.BuildUp);
                if (!offside && forwardRole && recDepth > ownerDepth - 0.02f && !ctx.IsGoalkeeper && recDepth < 0.93f)
                {
                    float lead = 7f + 0.6f * TeamAIMath.Clamp(SimVec3.Dot(rec.Velocity, attackDir), 0f, 8f);
                    var through = TeamAIMath.ClampToPitch(rec.Position + attackDir * lead + rec.Velocity * 0.3f, pitch, 1.5f);
                    float dist = TeamAIMath.FlatDistance(ballPos, through);
                    if (dist > 8f && dist < MaxGroundPass)
                    {
                        float tBall = GroundPassTime(dist);
                        float tRec = TeamAIMath.TimeToIntercept(rec, through, 0.15f);
                        if (tRec <= tBall + 0.5f)
                            Consider(world, ownerIndex, j, through, TeamAIAction.ThroughPass, dist, false, owner, sign, ownerDepth, diff, rng);
                    }
                }

                // Cross from wide areas to a runner in the box.
                if (crossZone && !offside && recDepth > 0.83f &&
                    TeamAIMath.Abs(rec.Position.X) < pitch.PenaltyAreaWidth * 0.5f)
                {
                    var goal = TeamAIMath.OpponentGoal(sign, pitch);
                    var spot = rec.Position + TeamAIMath.FlatDirection(rec.Position, goal) * 1.5f;
                    float dist = TeamAIMath.FlatDistance(ballPos, spot);
                    if (dist < 45f)
                        Consider(world, ownerIndex, j, spot, TeamAIAction.Cross, dist, true, owner, sign, ownerDepth, diff, rng);
                }
            }

            // ---- Shot ----
            bool shotAllowed = !ctx.IsGoalkeeper && ownerDepth > 0.68f &&
                               (!ctx.SetPiece || ctx.MatchPhase == TeamAIMatchPhase.Penalty || ctx.MatchPhase == TeamAIMatchPhase.FreeKick);
            if (shotAllowed) EvaluateShot(world, ownerIndex, sign, diff, rng);

            // ---- Carry ----
            bool carryAllowed = !ctx.IsGoalkeeper && !ctx.SetPiece;
            if (carryAllowed) EvaluateCarry(world, ownerIndex, sign, ownerDepth, pressure, diff, rng);

            // ---- Choose ----
            TeamAIOption best = LastBestPass;
            if (LastShot.Score > best.Score) best = LastShot;
            if (LastCarry.Score > best.Score) best = LastCarry;

            // Clearance: deep, pressed and no good option.
            if (!ctx.SetPiece && ownerDepth < 0.25f && pressure < 4f && best.Score < 0.18f)
            {
                float side = ballPos.X >= 0f ? 1f : -1f;
                var clearTo = new SimVec3(side * pitch.HalfWidth * 0.6f, 0f, TeamAIMath.DepthToZ(0.62f, sign, pitch));
                best = new TeamAIOption
                {
                    Action = TeamAIAction.LobPass, Intent = TeamAIIntent.Clear, Target = clearTo, TargetIndex = -1,
                    Score = best.Score, Power = 1f
                };
            }
            // Shield: tight pressure, nothing on, back to goal.
            else if (!ctx.SetPiece && !ctx.IsGoalkeeper && pressure < 1.8f && best.Score < 0.12f)
            {
                best = new TeamAIOption
                {
                    Action = TeamAIAction.Shield, Intent = TeamAIIntent.Shield, Target = ballPos, TargetIndex = -1,
                    Score = best.Score
                };
            }
            if (best.Score <= -998f)
            {
                // Nothing evaluated (e.g. set piece with no receiver): hold the ball.
                best = new TeamAIOption { Action = TeamAIAction.None, Intent = TeamAIIntent.Carry, Target = ballPos, TargetIndex = -1 };
            }

            LastChoice = best;
            ApplyOption(world, ownerIndex, best, diff, rng, ctx, ref cmd);
        }

        private void Consider(TeamAIWorld world, int ownerIndex, int recIndex, SimVec3 target, TeamAIAction action,
            float dist, bool lofted, in TeamAIPlayer owner, int sign, float ownerDepth, in TeamAIDifficulty diff, TeamAIRandom rng)
        {
            var pitch = world.Pitch;
            var from = TeamAIMath.Flat(world.Ball.Position);
            float tBall = lofted ? LobPassTime(dist) : GroundPassTime(dist);
            float control = TeamAIPitchControl.Evaluate(world, owner.Team, target, tBall, ownerIndex);
            float risk = InterceptionRisk(world, owner.Team, from, target, tBall, lofted);
            float targetDepth = TeamAIMath.Depth(target.Z, sign, pitch);
            float progression = targetDepth - ownerDepth;
            float threat = Threat(target, sign, pitch);

            float technique = 0.6f + 0.4f * owner.Passing;
            float distPenalty = dist > 25f ? (dist - 25f) * 0.006f * (1.5f - owner.Passing) : 0f;
            float score = control * (0.35f + 2.5f * threat) + 0.35f * progression - 0.9f * risk - distPenalty;
            if (lofted) score -= 0.06f;          // lofted balls are harder to control
            if (action == TeamAIAction.Cross) score += 0.05f;
            score *= technique;
            score += rng.NextSigned() * diff.DecisionNoise;

            if (score > LastBestPass.Score)
            {
                float power = action == TeamAIAction.LobPass ? TeamAIMath.Clamp(dist / MaxLobPass, 0.25f, 1f)
                    : action == TeamAIAction.Cross ? TeamAIMath.Clamp(dist / 45f, 0.35f, 1f)
                    : TeamAIMath.Clamp(dist / MaxGroundPass, 0.12f, 1f);
                LastBestPass = new TeamAIOption
                {
                    Action = action, Intent = TeamAIIntent.Pass, Target = target, TargetIndex = recIndex,
                    Score = score, Control = control, Risk = risk, Power = power
                };
            }
        }

        private void EvaluateShot(TeamAIWorld world, int ownerIndex, int sign, in TeamAIDifficulty diff, TeamAIRandom rng)
        {
            var pitch = world.Pitch;
            var owner = world.Players[ownerIndex];
            var from = TeamAIMath.Flat(world.Ball.Position);
            var goal = TeamAIMath.OpponentGoal(sign, pitch);
            float dist = TeamAIMath.FlatDistance(from, goal);
            if (dist > 35f) return;
            float xg = ExpectedGoals(from, sign, pitch);

            // Blockers in the shooting cone, and the keeper's position to pick the aim side.
            float block = 0f;
            float gkX = goal.X;
            var players = world.Players;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!players[i].Active || players[i].Team == owner.Team) continue;
                if (players[i].Role == TeamAIRole.GK) { gkX = players[i].Position.X; continue; }
                float s = TeamAIMath.SegmentParam(from, goal, players[i].Position);
                if (s <= 0.02f || s >= 0.98f) continue;
                float lateral = TeamAIMath.DistanceToLine(from, goal, players[i].Position);
                float coneHalf = 0.6f + s * pitch.GoalWidth * 0.5f;
                if (lateral < coneHalf) block += 0.3f * (1f - lateral / coneHalf) + 0.1f;
            }
            if (block > 0.9f) block = 0.9f;

            float score = xg * (0.6f + 0.6f * owner.Shooting) * (1f - block) * ShotWeight;
            score += rng.NextSigned() * diff.DecisionNoise;

            // Aim at the side with the bigger gap to the keeper.
            float hw = pitch.GoalWidth * 0.5f - 0.45f;
            float side = (gkX - goal.X) > 0f ? -1f : 1f;
            if (TeamAIMath.Abs(gkX - goal.X) < 0.2f) side = from.X >= goal.X ? -1f : 1f; // far post
            var aim = new SimVec3(goal.X + side * hw, 0f, goal.Z);

            TeamAIAction action = TeamAIAction.Shoot;
            float power = 0.85f;
            bool angled = TeamAIMath.Abs(from.X - goal.X) > 4f;
            if (dist > 22f || block > 0.4f) { action = TeamAIAction.DrivenShot; power = 1f; }
            else if (dist > 10f && angled && owner.Shooting > 0.6f)
            {
                action = TeamAIAction.FinesseShot;
                power = 0.72f;
                aim = new SimVec3(goal.X + (from.X > goal.X ? -hw : hw), 0f, goal.Z); // far post
            }

            LastShot = new TeamAIOption
            {
                Action = action, Intent = TeamAIIntent.Shoot, Target = aim, TargetIndex = -1, Score = score,
                Control = xg, Risk = block, Power = power
            };
        }

        private void EvaluateCarry(TeamAIWorld world, int ownerIndex, int sign, float ownerDepth, float pressure,
            in TeamAIDifficulty diff, TeamAIRandom rng)
        {
            var pitch = world.Pitch;
            var owner = world.Players[ownerIndex];
            var from = TeamAIMath.Flat(world.Ball.Position);
            var attackDir = new SimVec3(0f, 0f, sign);
            for (int k = -2; k <= 2; k++)
            {
                var dir = TeamAIMath.RotateFlat(attackDir, k * 0.55f);
                var target = TeamAIMath.ClampToPitch(from + dir * 8f, pitch, 1.5f);
                // Dribbling is slower than running: the carrier arrives later than a free runner would.
                float tCarrier = 8f / Math.Max(owner.MaxSpeed * (0.6f + 0.25f * owner.Dribbling), 0.5f);
                float tOpp = 999f;
                var players = world.Players;
                for (int i = 0; i < world.PlayerCount; i++)
                {
                    if (!players[i].Active || players[i].Team == owner.Team) continue;
                    float t = TeamAIMath.TimeToIntercept(players[i], target, 0.3f);
                    if (t < tOpp) tOpp = t;
                    // Opponents right in the carry lane.
                    float s = TeamAIMath.SegmentParam(from, target, players[i].Position);
                    if (s > 0.05f && TeamAIMath.DistanceToLine(from, target, players[i].Position) < 1.5f && s < 0.99f)
                        tOpp = Math.Min(tOpp, 0.2f);
                }
                float control = TeamAIMath.Sigmoid(2.5f * (tOpp - tCarrier));
                float targetDepth = TeamAIMath.Depth(target.Z, sign, pitch);
                float dispossess = TeamAIMath.Sigmoid((2.5f - pressure) * 1.5f) * (1f - 0.5f * owner.Dribbling);
                float score = control * (0.3f + 2.5f * Threat(target, sign, pitch)) + 0.35f * (targetDepth - ownerDepth)
                              - 0.6f * dispossess;
                score += rng.NextSigned() * diff.DecisionNoise;
                if (score > LastCarry.Score)
                {
                    LastCarry = new TeamAIOption
                    {
                        Action = TeamAIAction.None, Intent = TeamAIIntent.Carry, Target = target, TargetIndex = -1,
                        Score = score, Control = control, Risk = dispossess, Power = 0f
                    };
                }
            }
        }

        private static void ApplyOption(TeamAIWorld world, int ownerIndex, in TeamAIOption opt, in TeamAIDifficulty diff,
            TeamAIRandom rng, in TeamAIOnBallContext ctx, ref AIPlayerCommand cmd)
        {
            var owner = world.Players[ownerIndex];
            var ballPos = TeamAIMath.Flat(world.Ball.Position);
            cmd.Action = opt.Action;
            cmd.ActionTarget = opt.Target;
            cmd.TargetPlayerId = opt.TargetIndex >= 0 ? world.Players[opt.TargetIndex].Id : -1;
            cmd.Intent = ctx.IsGoalkeeper && opt.Intent == TeamAIIntent.Pass ? TeamAIIntent.GKDistribute : opt.Intent;

            if (opt.Action == TeamAIAction.None || opt.Action == TeamAIAction.Shield)
            {
                cmd.Power = 0f;
                cmd.Aim = SimVec3.Zero;
                if (opt.Intent == TeamAIIntent.Carry && !ctx.SetPiece && !ctx.IsGoalkeeper)
                {
                    cmd.MoveTarget = opt.Target;
                    cmd.MoveDirection = TeamAIMath.FlatDirection(owner.Position, opt.Target);
                    cmd.Sprint = opt.Control > 0.75f && opt.Risk < 0.3f;
                }
                else if (opt.Action == TeamAIAction.Shield)
                {
                    // Turn the back to the nearest opponent and hold the ball.
                    int opp = NearestOpponentIndex(world, owner.Team, ballPos);
                    var away = opp >= 0 ? TeamAIMath.FlatDirection(world.Players[opp].Position, owner.Position) : SimVec3.Zero;
                    cmd.MoveTarget = owner.Position + away * 1.0f;
                    cmd.MoveDirection = away;
                    cmd.Sprint = false;
                }
                else
                {
                    cmd.MoveTarget = owner.Position;
                    cmd.MoveDirection = SimVec3.Zero;
                    cmd.Sprint = false;
                }
                return;
            }

            // Kicks: aim and power with skill- and difficulty-scaled error.
            bool shot = opt.Action == TeamAIAction.Shoot || opt.Action == TeamAIAction.FinesseShot ||
                        opt.Action == TeamAIAction.DrivenShot;
            float skill = shot ? owner.Shooting : owner.Passing;
            float aimSd = diff.AimError * (1.4f - 0.8f * skill);
            var dir = TeamAIMath.FlatDirection(ballPos, opt.Target);
            dir = TeamAIMath.RotateFlat(dir, rng.NextGaussian() * aimSd);
            float power = opt.Power * (1f + rng.NextGaussian() * diff.PowerError * (1.3f - 0.6f * skill));
            cmd.Aim = dir;
            cmd.Power = TeamAIMath.Clamp01(power);
            cmd.MoveTarget = owner.Position;
            cmd.MoveDirection = SimVec3.Zero;
            cmd.Sprint = false;
        }

        public static float NearestOpponentDistance(TeamAIWorld world, int team, SimVec3 p)
        {
            int i = NearestOpponentIndex(world, team, p);
            return i >= 0 ? TeamAIMath.FlatDistance(world.Players[i].Position, p) : 999f;
        }

        public static int NearestOpponentIndex(TeamAIWorld world, int team, SimVec3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!world.Players[i].Active || world.Players[i].Team == team) continue;
                float d = TeamAIMath.FlatSqrDistance(world.Players[i].Position, p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
