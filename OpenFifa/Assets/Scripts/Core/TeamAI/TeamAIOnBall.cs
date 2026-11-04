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
        /// <summary>Risk (0..1) that the kick is blocked at the foot (see <see cref="TeamAIOnBall.BlockRisk"/>).</summary>
        public float Block;
    }

    /// <summary>Context flags for the on-ball decision.</summary>
    public struct TeamAIOnBallContext
    {
        public bool IsGoalkeeper;
        public bool SetPiece;
        public TeamAIMatchPhase MatchPhase;
        /// <summary>Offside line depth for our attackers (0..1 from our goal).</summary>
        public float OffsideDepth;
        /// <summary>A human teammate is calling for the ball (see <see cref="CalledReceiver"/>).</summary>
        public bool HasCall;
        /// <summary>World index of the teammate calling for the ball; only read when <see cref="HasCall"/>.</summary>
        public int CalledReceiver;
        /// <summary>The caller asked for a through ball rather than to feet.</summary>
        public bool CalledThrough;
    }

    /// <summary>
    /// On-ball utility AI: scores passes (ground, through, lob, cross) by pitch control at the receiver,
    /// progression, threat and interception risk; shots by an xG-style model; carries, shielding and
    /// clearances. Allocation-free.
    /// </summary>
    public sealed class TeamAIOnBall
    {
        public const float ShotWeight = 5.5f;
        public const float MaxGroundPass = 42f;
        public const float MaxLobPass = 60f;
        /// <summary>Score per unit of depth (0..1 of the pitch) a pass gains.</summary>
        public const float ProgressionWeight = 0.8f;
        /// <summary>Score per unit of depth a forward pass gains.</summary>
        public const float ForwardProgressionWeight = 1.3f;
        /// <summary>Weight of the receiving spot's threat (<see cref="Threat"/>) in a pass score.</summary>
        public const float ThreatWeight = 4f;
        /// <summary>Weight of interception risk in a pass score.</summary>
        public const float RiskWeight = 0.55f;
        /// <summary>
        /// Seconds from an on-ball decision to the ball leaving the foot (decision interval, latch and wind-up), used to
        /// predict where a closing opponent will be at contact.
        /// </summary>
        public const float PassLaunchDelay = 0.22f;
        public const float LobLaunchDelay = 0.3f;
        public const float ShotLaunchDelay = 0.36f;
        /// <summary>
        /// The sim gives the ball to any opponent within its control radius (0.85 m) as it leaves the foot, and body
        /// contact blocks anything struck through him. Opponents whose predicted position is within
        /// <see cref="BlockSafe"/> of the ball's first 1.5 m of flight make the kick risky, fully blocked inside
        /// <see cref="BlockSure"/>.
        /// </summary>
        public const float BlockSure = 0.75f;
        public const float BlockSafe = 1.6f;
        /// <summary>Same thresholds for an opponent behind the ball's line: the ball leaves him, so only one at the ball takes it.</summary>
        public const float BehindSure = 0.5f;
        /// <summary>Opponents this close to the passer are judged by <see cref="BlockRisk"/>, not the lane model.</summary>
        public const float NearPasserRadius = 2.5f;
        public const float BehindSafe = 0.95f;

        /// <summary>Best pass option of the last decision.</summary>
        public TeamAIOption LastBestPass;
        /// <summary>Shot option of the last decision (Score = -1 when not considered).</summary>
        public TeamAIOption LastShot;
        /// <summary>Best carry option of the last decision.</summary>
        public TeamAIOption LastCarry;
        /// <summary>Chosen option of the last decision.</summary>
        public TeamAIOption LastChoice;
        /// <summary>Least blocked pass of the last decision (Block = 999 when none was considered).</summary>
        public TeamAIOption LastSafestPass;
        /// <summary>
        /// Seconds the current carrier has been keeping the ball (shielding, or carrying because every kick was
        /// blocked) at the last decision; 0 when he is not keeping it.
        /// </summary>
        public float KeepSeconds { get; private set; }

        private int _keepOwnerId = -1;
        private float _keepSince = -1f;
        private float _keepLastSeen = -100f;

        /// <summary>Ball timing model (sim-matched). Set by the brain; defaults to the default ball config.</summary>
        public TeamAIBallModel Model = TeamAIBallModel.Default;
        /// <summary>
        /// Team build-up directness this tick (0..1, see <see cref="TeamAITactics.Directness"/>); the brain sets it.
        /// 0.5 is neutral. Higher values weigh a pass's forward progress more and its interception risk less, and make
        /// lofted balls forward more attractive.
        /// </summary>
        public float Directness = 0.5f;

        /// <summary>Extra score for a cross (0 = neutral); wing-play sides set it from their width.</summary>
        public float CrossBias;

        /// <summary>Multiplier on forward progress for a directness (1 at 0.5; 0.6 .. 1.4).</summary>
        public static float DirectProgress(float directness) => 0.6f + 0.8f * TeamAIMath.Clamp01(directness);
        /// <summary>Multiplier on interception risk for a directness (1 at 0.5; 1.35 .. 0.65).</summary>
        public static float DirectRisk(float directness) => 1.35f - 0.7f * TeamAIMath.Clamp01(directness);

        private int _calledReceiver = -1;
        private bool _calledThrough;

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

        // The static helpers below use the default ball config only; brain code uses its own Model (wet/dry pitch).
        /// <summary>Seconds for an AI ground pass of this length, from the sim's ball integrator (default config).</summary>
        [System.Obsolete("Use the brain's TeamAIBallModel")]
        public static float GroundPassTime(float dist) => TeamAIBallModel.Default.GroundPassTime(dist);

        /// <summary>Flight time of an AI lob of this length, from the sim's ball integrator (default config).</summary>
        [System.Obsolete("Use the brain's TeamAIBallModel")]
        public static float LobPassTime(float dist) => TeamAIBallModel.Default.LobPassTime(dist);

        /// <summary>Flight time of a cross of this length, from the sim's ball integrator (default config).</summary>
        [System.Obsolete("Use the brain's TeamAIBallModel")]
        public static float CrossTime(float dist) => TeamAIBallModel.Default.CrossTime(dist);

        /// <summary>
        /// Multiplier on the value of forward passes: 0.75 at theta 0 rising to 1.2 at theta 1, slowly at first, so
        /// the middle of the ladder stays measured and only the top levels play forward relentlessly.
        /// </summary>
        public static float Ambition(float theta)
        {
            float t = TeamAIMath.Clamp01(theta);
            return TeamAIMath.Lerp(0.75f, 1.2f, (float)Math.Pow(t, 1.25));
        }

        /// <summary>
        /// How much of a kick's block risk the side perceives (theta 0 = 0.4, theta 1 = 1): weaker sides play into a
        /// closing presser more often.
        /// </summary>
        public static float PressAwareness(float theta) => TeamAIMath.Lerp(0.4f, 1f, TeamAIMath.Clamp01(theta));

        /// <summary>
        /// Risk (0..1) that a kick from <paramref name="ball"/> along <paramref name="dir"/> is blocked or picked off at
        /// the foot by an opponent closing in during the <paramref name="launchDelay"/> before contact.
        /// </summary>
        public static float BlockRisk(TeamAIWorld world, int team, SimVec3 ball, SimVec3 dir, float launchDelay)
            => BlockRisk(world, team, ball, dir, launchDelay, false);

        /// <summary>
        /// <paramref name="fromHands"/>: the keeper releases from his hands, which opponents may not take (Law 12; the
        /// sim protects the release), so only a body square in the ball's path blocks it.
        /// </summary>
        public static float BlockRisk(TeamAIWorld world, int team, SimVec3 ball, SimVec3 dir, float launchDelay, bool fromHands)
        {
            float worst = 0f;
            var players = world.Players;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!players[i].Active || players[i].Team == team) continue;
                var pos = players[i].Position;
                float dist = TeamAIMath.FlatDistance(pos, ball);
                if (dist > 6f) continue;
                // Where he will be at contact: on his current velocity, but he stops at the ball.
                var move = players[i].Velocity * launchDelay;
                move = new SimVec3(move.X, 0f, move.Z);
                float m = move.Magnitude;
                float maxMove = Math.Max(0f, dist - 0.4f);
                if (m > maxMove && m > 1e-4f) move = move * (maxMove / m);
                var q = pos + move;
                float along = (q.X - ball.X) * dir.X + (q.Z - ball.Z) * dir.Z;
                if (fromHands && along < 0.2f) continue;
                float s = TeamAIMath.Clamp(along, 0f, 1.5f);
                var c = new SimVec3(ball.X + dir.X * s, 0f, ball.Z + dir.Z * s);
                float d = TeamAIMath.FlatDistance(q, c);
                float sure = BlockSure, safe = BlockSafe;
                if (fromHands) { sure = 0.45f; safe = 0.9f; }
                else if (along < 0f) { sure = BehindSure; safe = BehindSafe; } // the ball runs away from him
                float risk = 1f - TeamAIMath.Clamp01((d - sure) / (safe - sure));
                if (risk > worst) worst = risk;
            }
            return worst;
        }

        /// <summary>
        /// Probability-like risk (0..1) that an opponent intercepts a pass from `from` to `to` arriving after
        /// `travelTime`. Lobs ignore opponents under the flight path except near the passer and the landing zone.
        /// </summary>
        public static float InterceptionRisk(TeamAIWorld world, int team, SimVec3 from, SimVec3 to, float travelTime, bool lob)
            => InterceptionRisk(world, team, from, to, travelTime, lob, 0f);

        /// <summary>
        /// As above; opponents within <paramref name="ignoreNear"/> m of the passer are skipped (the caller scores them
        /// with <see cref="BlockRisk"/>, which knows which side of the ball they are on).
        /// </summary>
        public static float InterceptionRisk(TeamAIWorld world, int team, SimVec3 from, SimVec3 to, float travelTime, bool lob,
            float ignoreNear) => InterceptionRisk(world, team, from, to, travelTime, lob, ignoreNear, null, 0f);

        /// <summary>
        /// As above. With a <paramref name="rollModel"/> and the pass's <paramref name="launchSpeed"/>, the ball's time to
        /// each point of a ground pass comes from the sim's rolling curve (fast early, slow late) instead of a straight
        /// share of the travel time, which overstated the risk near the passer.
        /// </summary>
        public static float InterceptionRisk(TeamAIWorld world, int team, SimVec3 from, SimVec3 to, float travelTime, bool lob,
            float ignoreNear, TeamAIBallModel rollModel, float launchSpeed)
        {
            float length = TeamAIMath.FlatDistance(from, to);
            float worst = 0f;
            var players = world.Players;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!players[i].Active || players[i].Team == team) continue;
                var pos = players[i].Position;
                if (ignoreNear > 0f && TeamAIMath.FlatDistance(from, pos) < ignoreNear) continue;
                float s = TeamAIMath.SegmentParam(from, to, pos);
                if (lob && s > 0.08f && s < 0.8f) continue;
                if (s <= 0f && TeamAIMath.FlatDistance(from, pos) > 1.5f) continue; // behind the passer
                var point = new SimVec3(from.X + (to.X - from.X) * s, 0f, from.Z + (to.Z - from.Z) * s);
                float tb = travelTime * s;
                if (rollModel != null && !lob)
                {
                    float tr = rollModel.RollTime(launchSpeed, s * length);
                    if (tr < tb) tb = tr;
                }
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

            _calledReceiver = ctx.HasCall && ctx.CalledReceiver >= 0 && ctx.CalledReceiver != ownerIndex ? ctx.CalledReceiver : -1;
            _calledThrough = ctx.CalledThrough;
            LastBestPass = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            LastShot = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            LastCarry = new TeamAIOption { Score = -999f, TargetIndex = -1 };
            LastSafestPass = new TeamAIOption { Score = -999f, TargetIndex = -1, Block = 999f };

            // ---- Passes ----
            bool crossZone = ownerDepth > 0.76f && TeamAIMath.Abs(ballPos.X) > pitch.PenaltyAreaWidth * 0.5f - 2f;
            for (int j = 0; j < world.PlayerCount; j++)
            {
                if (j == ownerIndex || !players[j].Active || players[j].Team != team) continue;
                var rec = players[j];
                float recDepth = TeamAIMath.Depth(rec.Position.Z, sign, pitch);
                bool offside = recDepth > ctx.OffsideDepth + 0.004f && recDepth > 0.5f && !ctx.SetPiece;
                if (rec.Role == TeamAIRole.GK && ownerDepth > 0.45f) continue;

                // Passes are judged where the sim will send them: it leads the receiver along his run by the ball's real
                // flight time (MatchSim.PassLeadPoint / KickSolver.PassFlightTime), so a runner is found in space.
                if (!offside)
                {
                    var feet = LeadPoint(rec, ballPos, TeamAIAction.Pass, attackDir, pitch);
                    float dist = TeamAIMath.FlatDistance(ballPos, feet);
                    if (dist > 4f && dist < MaxGroundPass)
                        Consider(world, ownerIndex, j, feet, TeamAIAction.Pass, dist, false, owner, sign, ownerDepth, diff, rng);
                    var drop = LeadPoint(rec, ballPos, TeamAIAction.LobPass, attackDir, pitch);
                    float lobDist = TeamAIMath.FlatDistance(ballPos, drop);
                    if (lobDist > 16f && lobDist < MaxLobPass && !ctx.IsGoalkeeper)
                        Consider(world, ownerIndex, j, drop, TeamAIAction.LobPass, lobDist, true, owner, sign, ownerDepth, diff, rng);
                    if (ctx.IsGoalkeeper && lobDist > 25f && lobDist < MaxLobPass + 10f)
                        Consider(world, ownerIndex, j, drop, TeamAIAction.LobPass, lobDist, true, owner, sign, ownerDepth, diff, rng);
                }

                // Through ball into space for forward-minded players who are onside now.
                bool forwardRole = rec.Role == TeamAIRole.ST || rec.Role == TeamAIRole.W || rec.Role == TeamAIRole.AM ||
                                   (rec.Role == TeamAIRole.FB && phase != TeamAIPhase.BuildUp) ||
                                   (j == _calledReceiver && _calledThrough);
                if (!offside && forwardRole && recDepth > ownerDepth - 0.02f && !ctx.IsGoalkeeper && recDepth < 0.93f)
                {
                    var through = LeadPoint(rec, ballPos, TeamAIAction.ThroughPass, attackDir, pitch);
                    float dist = TeamAIMath.FlatDistance(ballPos, through);
                    if (dist > 8f && dist < MaxGroundPass)
                    {
                        float tBall = Model.GroundPassTimeTo(dist, rec.BallControl);
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
            bool shotAllowed = !ctx.IsGoalkeeper && ownerDepth > 0.64f &&
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
            // The clearance goes to whichever channel (own side, middle, far side) the nearest opponent cannot block;
            // when every channel is blocked, a hoof only hands him the ball in front of goal, so keep it instead.
            SimVec3 clearTo = default;
            bool canClear = false;
            // Composure scales with difficulty: a strong side plays out of pressure, a weak one hoofs it.
            if (!ctx.SetPiece && ownerDepth < 0.22f && pressure < 2.5f + 1.5f * (1f - diff.Theta) &&
                best.Score < 0.15f - 0.15f * diff.Theta)
                canClear = PickClearance(world, team, ballPos, sign, out clearTo);
            bool keeping = false;
            if (canClear)
            {
                best = new TeamAIOption
                {
                    Action = TeamAIAction.LobPass, Intent = TeamAIIntent.Clear, Target = clearTo, TargetIndex = -1,
                    Score = best.Score, Power = 1f
                };
            }
            // Every kick would be smothered at the foot: keep the ball (turn away, or shield) until a lane opens.
            else if (!ctx.SetPiece && best.Action != TeamAIAction.None && best.Block > 0.6f * PressAwareness(diff.Theta))
            {
                keeping = true;
                if (LastCarry.Score > -998f && LastCarry.Risk < 0.85f) best = LastCarry;
                else
                {
                    best = new TeamAIOption
                    {
                        Action = ctx.IsGoalkeeper ? TeamAIAction.None : TeamAIAction.Shield,
                        Intent = ctx.IsGoalkeeper ? TeamAIIntent.GKDistribute : TeamAIIntent.Shield,
                        Target = ballPos, TargetIndex = -1, Score = best.Score
                    };
                }
            }
            // Shield: tight pressure, nothing on, back to goal.
            else if (!ctx.SetPiece && !ctx.IsGoalkeeper && pressure < 1.8f && best.Score < 0.12f)
            {
                keeping = true;
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

            // Keeping the ball has a time limit (the keeper has his 6 s): against a presser who stays on him the
            // carrier would otherwise shield forever. Past the limit he turns into space if there is any, then plays
            // the least blocked pass (often back to the keeper) or a hopeful ball / clearance.
            if (keeping && !ctx.IsGoalkeeper)
            {
                if (owner.Id != _keepOwnerId || _keepSince < 0f || world.Time - _keepLastSeen > KeepGap)
                {
                    _keepOwnerId = owner.Id;
                    _keepSince = world.Time;
                }
                _keepLastSeen = world.Time;
                KeepSeconds = world.Time - _keepSince;
                float limit = KeepLimit(diff.Theta);
                if (KeepSeconds >= limit)
                    best = Escalate(world, team, ballPos, sign, ownerDepth, best, KeepSeconds - limit);
            }
            else
            {
                _keepSince = -1f;
                KeepSeconds = 0f;
            }

            LastChoice = best;
            ApplyOption(world, ownerIndex, best, diff, rng, ctx, ref cmd);
        }

        /// <summary>
        /// Where the sim's pass lead puts the ball for this receiver: his position plus his velocity times the ball's
        /// flight time (fixed point, 3 rounds), plus <see cref="TeamAIBallModel.ThroughLeadDistance"/> along his run
        /// (or the attack direction when he is standing) for a through ball.
        /// </summary>
        public SimVec3 LeadPoint(in TeamAIPlayer rec, SimVec3 from, TeamAIAction action, SimVec3 attackDir, PitchSpec pitch)
        {
            var vel = new SimVec3(rec.Velocity.X, 0f, rec.Velocity.Z);
            var extra = SimVec3.Zero;
            if (action == TeamAIAction.ThroughPass)
            {
                float sp = vel.Magnitude;
                extra = (sp > 1f ? vel / sp : attackDir) * Model.ThroughLeadDistance;
            }
            var p = rec.Position + extra;
            for (int k = 0; k < 3; k++)
            {
                float dist = TeamAIMath.FlatDistance(from, p);
                float t = action == TeamAIAction.LobPass ? Model.LobPassTime(dist)
                    : action == TeamAIAction.Cross ? Model.CrossTime(dist) : Model.GroundPassTimeTo(dist, rec.BallControl);
                p = rec.Position + vel * t + extra;
            }
            return TeamAIMath.ClampToPitch(new SimVec3(p.X, 0f, p.Z), pitch, 0.5f);
        }

        /// <summary>
        /// Seconds a carrier keeps the ball (shielding, or carrying because every kick is blocked) before he forces
        /// the issue: 2.5 s at theta 0 down to 1.5 s at theta 1, since a strong side moves the ball on sooner.
        /// </summary>
        public static float KeepLimit(float theta) => TeamAIMath.Lerp(2.5f, 1.5f, TeamAIMath.Clamp01(theta));

        /// <summary>Seconds past <see cref="KeepLimit"/> during which a turn into space is still tried before a kick.</summary>
        public const float KeepTurnWindow = 0.8f;
        /// <summary>A gap (s) between keeping decisions longer than this starts a new keep.</summary>
        private const float KeepGap = 0.5f;

        private TeamAIOption Escalate(TeamAIWorld world, int team, SimVec3 ballPos, int sign, float ownerDepth,
            in TeamAIOption kept, float pastLimit)
        {
            // 1. Turn and carry into space, if a lane is genuinely open (and he is not already carrying).
            if (pastLimit < KeepTurnWindow && kept.Intent != TeamAIIntent.Carry && LastCarry.Score > -998f &&
                LastCarry.Control > 0.6f)
                return LastCarry;
            // 2. The least blocked pass, back to the keeper included.
            if (LastSafestPass.TargetIndex >= 0 && LastSafestPass.Block < 0.9f) return LastSafestPass;
            // 3. A hopeful ball forward (a clearance from deep), into the least blocked channel.
            float depth = TeamAIMath.Clamp(ownerDepth + 0.25f, 0.62f, 0.9f);
            PickClearance(world, team, ballPos, sign, out var to, depth);
            float dist = TeamAIMath.FlatDistance(ballPos, to);
            return new TeamAIOption
            {
                Action = TeamAIAction.LobPass, Intent = TeamAIIntent.Clear, Target = to, TargetIndex = -1,
                Score = kept.Score, Power = ownerDepth < 0.3f ? 1f : TeamAIMath.Clamp(dist / MaxLobPass, 0.4f, 1f)
            };
        }

        private static bool PickClearance(TeamAIWorld world, int team, SimVec3 ballPos, int sign, out SimVec3 clearTo,
            float depth = 0.62f)
        {
            var pitch = world.Pitch;
            float side = ballPos.X >= 0f ? 1f : -1f;
            float z = TeamAIMath.DepthToZ(depth, sign, pitch);
            clearTo = new SimVec3(side * pitch.HalfWidth * 0.6f, 0f, z);
            float bestBlock = BlockRisk(world, team, ballPos, TeamAIMath.FlatDirection(ballPos, clearTo), LobLaunchDelay);
            for (int k = 0; k < 2; k++)
            {
                var c = new SimVec3(k == 0 ? 0f : -side * pitch.HalfWidth * 0.6f, 0f, z);
                float b = BlockRisk(world, team, ballPos, TeamAIMath.FlatDirection(ballPos, c), LobLaunchDelay);
                if (b < bestBlock - 0.05f) { bestBlock = b; clearTo = c; }
            }
            return bestBlock < 0.5f;
        }

        private void Consider(TeamAIWorld world, int ownerIndex, int recIndex, SimVec3 target, TeamAIAction action,
            float dist, bool lofted, in TeamAIPlayer owner, int sign, float ownerDepth, in TeamAIDifficulty diff, TeamAIRandom rng)
        {
            var pitch = world.Pitch;
            var from = TeamAIMath.Flat(world.Ball.Position);
            // Ground passes arrive no faster than the receiver can take cleanly (TeamAIBallModel.GroundPassPower).
            float recControl = world.Players[recIndex].BallControl;
            float groundPower = lofted ? 0f : Model.GroundPassPower(dist, recControl);
            float tBall = action == TeamAIAction.Cross ? Model.CrossTime(dist)
                : lofted ? Model.LobPassTime(dist) : Model.GroundPassTime(dist, Model.ArrivalSpeedForPower(groundPower));
            float control = TeamAIPitchControl.Evaluate(world, owner.Team, target, tBall, ownerIndex);
            float launch = lofted ? 0f : Model.GroundLaunchSpeed(dist, Model.ArrivalSpeedForPower(groundPower));
            float risk = InterceptionRisk(world, owner.Team, from, target, tBall, lofted, NearPasserRadius, lofted ? null : Model, launch);
            bool fromHands = owner.Role == TeamAIRole.GK && world.Ball.Position.Y > 0.5f;
            float block = BlockRisk(world, owner.Team, from, TeamAIMath.FlatDirection(from, target),
                lofted ? LobLaunchDelay : PassLaunchDelay, fromHands);
            // Reading the presser is a skill: a weak side half-ignores him and gets its kicks charged down.
            block *= PressAwareness(diff.Theta);
            if (block > risk) risk = block;
            float targetDepth = TeamAIMath.Depth(target.Z, sign, pitch);
            float progression = targetDepth - ownerDepth;
            float threat = Threat(target, sign, pitch);

            float technique = 0.6f + 0.4f * owner.Passing;
            float distPenalty = dist > 25f ? (dist - 25f) * 0.006f * (1.5f - owner.Passing) : 0f;
            // Gaining ground is worth more than losing it costs: a side that only weighs both the same recycles the
            // ball backwards against any screen in front of it.
            float prog = progression > 0f ? ForwardProgressionWeight * progression : ProgressionWeight * progression;
            // Ambition scales with difficulty: a strong side looks for the forward ball and accepts some lane risk to
            // play it; a weak one recycles sideways.
            float riskWeight = RiskWeight * DirectRisk(Directness);
            if (progression > 0f) prog *= Ambition(diff.Theta) * DirectProgress(Directness);
            float score = control * (0.35f + ThreatWeight * threat) + prog - riskWeight * risk - 0.6f * block - distPenalty;
            if (lofted) score -= 0.06f + 0.1f * (0.5f - TeamAIMath.Clamp01(Directness)); // harder to control; direct sides accept it
            // A lofted ball backwards or square gives the ball away for nothing; going back to the keeper is a reset.
            if (lofted && progression < 0.05f && action != TeamAIAction.Cross) score -= 0.25f;
            if (world.Players[recIndex].Role == TeamAIRole.GK && ownerDepth > 0.2f) score -= 0.12f;
            if (action == TeamAIAction.Cross) score += 0.05f + CrossBias;
            score *= technique;
            score += rng.NextSigned() * diff.DecisionNoise;
            if (recIndex == _calledReceiver)
            {
                // A human teammate is calling for it: strongly prefer the requested kind of pass.
                bool kindMatches = _calledThrough ? action == TeamAIAction.ThroughPass : action != TeamAIAction.ThroughPass;
                score += kindMatches ? TeamAIUserDirectives.PassCallBonus : TeamAIUserDirectives.PassCallBonus * 0.5f;
            }

            bool bestSoFar = score > LastBestPass.Score;
            // Least blocked pass, ties (within 0.05) broken by score: the way out for a carrier kept too long.
            bool safest = block < LastSafestPass.Block - 0.05f ||
                          (block < LastSafestPass.Block + 0.05f && score > LastSafestPass.Score);
            if (bestSoFar || safest)
            {
                float power = action == TeamAIAction.LobPass ? TeamAIMath.Clamp(dist / MaxLobPass, 0.25f, 1f)
                    : action == TeamAIAction.Cross ? TeamAIMath.Clamp(dist / 45f, 0.35f, 1f)
                    : groundPower;
                var opt = new TeamAIOption
                {
                    Action = action, Intent = TeamAIIntent.Pass, Target = target, TargetIndex = recIndex,
                    Score = score, Control = control, Risk = risk, Power = power, Block = block
                };
                if (bestSoFar) LastBestPass = opt;
                if (safest) LastSafestPass = opt;
            }
        }

        /// <summary>
        /// Shot composure for the low end of the ladder: 0.55 at theta 0 rising to 1 at theta 0.5 and above. It scales the
        /// shot's utility (a weak side passes up half-chances) and how far toward the post it aims (a weak side puts it
        /// nearer the keeper). Professional and above are unaffected.
        /// </summary>
        public static float ShotComposure(float theta) => TeamAIMath.Lerp(0.55f, 1f, TeamAIMath.Clamp01(theta / 0.5f));

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
                // A defender near the shooter covers the whole cone; one near the goal only part of the target.
                if (lateral < coneHalf) block += (0.25f * (1f - lateral / coneHalf) + 0.05f) * (s < 0.3f ? 1f : 0.7f);
            }
            if (block > 0.9f) block = 0.9f;
            // A defender closing on the ball blocks the strike at the foot.
            float atFoot = BlockRisk(world, owner.Team, from, TeamAIMath.FlatDirection(from, goal), ShotLaunchDelay);
            if (atFoot > block) block = atFoot;

            float composure = ShotComposure(diff.Theta);
            float score = xg * (0.6f + 0.6f * owner.Shooting) * (1f - block) * ShotWeight * composure;
            score += rng.NextSigned() * diff.DecisionNoise;

            // Aim at the side with the bigger gap to the keeper; a nervous side aims closer to him.
            float hw = (pitch.GoalWidth * 0.5f - 0.45f) * TeamAIMath.Lerp(0.45f, 1f, (composure - 0.55f) / 0.45f);
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
                Control = xg, Risk = block, Power = power, Block = atFoot
            };
        }

        private void EvaluateCarry(TeamAIWorld world, int ownerIndex, int sign, float ownerDepth, float pressure,
            in TeamAIDifficulty diff, TeamAIRandom rng)
        {
            var pitch = world.Pitch;
            var owner = world.Players[ownerIndex];
            var from = TeamAIMath.Flat(world.Ball.Position);
            var attackDir = new SimVec3(0f, 0f, sign);
            // Under tight pressure also look sideways and back: turning away from the presser keeps the ball.
            int span = pressure < 3f ? 5 : 2;
            for (int k = -span; k <= span; k++)
            {
                var dir = TeamAIMath.RotateFlat(attackDir, k * 0.55f);
                // Keep carries 3 m off the touchline: a dribble touch near the line runs the ball out.
                var target = TeamAIMath.ClampToPitch(from + dir * 8f, pitch, 3f);
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

        internal static void ApplyOption(TeamAIWorld world, int ownerIndex, in TeamAIOption opt, in TeamAIDifficulty diff,
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
                else if (ctx.IsGoalkeeper && opt.Intent == TeamAIIntent.GKDistribute)
                {
                    // Keeper waiting to release: step away from the opponent standing on him, inside the box.
                    var pitch = world.Pitch;
                    int sign = world.AttackSign(owner.Team);
                    int opp = NearestOpponentIndex(world, owner.Team, ballPos);
                    var away = opp >= 0 ? TeamAIMath.FlatDirection(world.Players[opp].Position, owner.Position) : SimVec3.Zero;
                    var t = owner.Position + away * 2f;
                    float hw = pitch.PenaltyAreaWidth * 0.5f - 1.5f;
                    float depthM = TeamAIMath.Clamp((t.Z + sign * pitch.HalfLength) * sign, 1f, pitch.PenaltyAreaDepth - 1.5f);
                    t = new SimVec3(TeamAIMath.Clamp(t.X, -hw, hw), 0f, sign * (depthM - pitch.HalfLength));
                    cmd.MoveTarget = t;
                    cmd.MoveDirection = TeamAIMath.FlatDistance(owner.Position, t) > 0.3f ? TeamAIMath.FlatDirection(owner.Position, t) : SimVec3.Zero;
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
