using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Shared fixtures for the FID-SIM tests: scripted bots and state surgery helpers.</summary>
    internal static class SimTestHelpers
    {
        public const float BallDt = 1f / 120f;

        public static MatchSim NewSim(ulong seed, MatchSimConfig cfg = null) =>
            new MatchSim(cfg ?? new MatchSimConfig(), MatchSetup.CreateDefault(seed));

        public static PlayerIntent[] IdleIntents()
        {
            var a = new PlayerIntent[MatchSimState.PlayerCount];
            for (int i = 0; i < a.Length; i++) a[i] = PlayerIntent.Idle;
            return a;
        }

        /// <summary>Puts the sim into open play with nobody on the ball (for hand-built scenarios).</summary>
        public static void ForceOpenPlay(MatchSim sim)
        {
            var s = sim.State;
            s.Phase = SimMatchPhase.InPlay;
            s.PendingRestart = RestartType.None;
            s.RestartTakerId = -1;
            s.Owner = -1;
            s.KeeperHolding = false;
            s.PassLive = false;
            s.ShotLive = false;
            s.OffsideMask = 0;
        }

        public static void Place(MatchSim sim, int id, float x, float z)
        {
            sim.State.Players[id].Position = new SimVec3(x, 0f, z);
            sim.State.Players[id].Velocity = SimVec3.Zero;
        }

        /// <summary>Moves every player of a team far out of the way (behind their own goal line area, wide).</summary>
        public static void ParkTeam(MatchSim sim, SimTeam team, float z)
        {
            int first = MatchSim.FirstId(team);
            for (int i = 0; i < MatchSetup.PlayersPerTeam; i++) Place(sim, first + i, -30f + i * 2f, z);
        }

        public static void SetBall(MatchSim sim, SimVec3 pos, SimVec3 vel)
        {
            var b = sim.State.Ball;
            b.Position = pos;
            b.Velocity = vel;
            b.Spin = SimVec3.Zero;
            b.InNet = 0;
            b.Grounded = pos.Y <= 0.111f && vel.Y <= 0f;
            sim.State.Ball = b;
        }

        public static bool StepUntil(MatchSim sim, PlayerIntent[] intents, MatchEventType type, int maxTicks, out MatchEvent found)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                var events = sim.Step(intents);
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Type == type)
                    {
                        found = events[i];
                        return true;
                    }
                }
            }

            found = default;
            return false;
        }

        /// <summary>Flies a ball with no players until it first lands; returns the landing point.</summary>
        public static SimBall FlyUntilFirstBounce(SimBall ball, BallSimConfig cfg, PitchSpec pitch, int maxSteps = 2400)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                var f = BallIntegrator.Step(ref ball, cfg, pitch, BallDt);
                if ((f & (BallStepFlags.Bounced | BallStepFlags.StartedRolling)) != 0) return ball;
            }

            return ball;
        }

        public static SimBall RollUntilStopped(SimBall ball, BallSimConfig cfg, PitchSpec pitch, int maxSteps = 12000)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                BallIntegrator.Step(ref ball, cfg, pitch, BallDt);
                if (ball.Grounded && ball.Velocity.SqrMagnitude == 0f) return ball;
            }

            return ball;
        }

        /// <summary>A big pitch so out-of-play geometry never interferes with free-flight tests.</summary>
        public static PitchSpec HugePitch => new PitchSpec(1000f, 1000f, 7.32f, 2.44f, 16.5f, 40.32f);

        /// <summary>
        /// Deterministic scripted bots (a pure function of sim state) used for soak and determinism runs.
        /// Not the real AI: just enough to move the ball around, shoot, tackle and take restarts.
        /// </summary>
        public static void BotIntents(MatchSim sim, PlayerIntent[] intents)
        {
            var s = sim.State;
            var pitch = sim.Config.Pitch;
            SimVec3 ball = s.Ball.Position;
            int chaserHome = Nearest(sim, SimTeam.Home, ball);
            int chaserAway = Nearest(sim, SimTeam.Away, ball);
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                SimPlayer p = s.Players[i];
                SimTeam team = p.Team;
                int sign = sim.AttackSign(team);
                SimVec3 goal = new SimVec3(0f, 0f, sign * pitch.HalfLength);
                var intent = PlayerIntent.Idle;

                if (s.Phase == SimMatchPhase.AwaitingRestart && i == s.RestartTakerId)
                {
                    if ((s.Tick + i) % 20 == 0)
                    {
                        int mate = NearestMate(sim, i);
                        SimVec3 aim = (s.Players[mate].Position - p.Position).Flat.Normalized;
                        intent = PlayerIntent.Kick(IntentAction.Pass, 0.5f, aim, mate);
                    }
                }
                else if (s.Owner == i)
                {
                    float ac = sim.AttackCoord(team, p.Position);
                    SimVec3 toGoal = (goal - p.Position).Flat.Normalized;
                    intent = PlayerIntent.MoveOnly(toGoal, (s.Tick / 90 + i) % 3 != 0);
                    if (ac > pitch.HalfLength - 22f && (s.Tick + i) % 15 == 0)
                    {
                        IntentAction shot = (s.Tick / 15) % 3 == 0 ? IntentAction.FinesseShot : ((s.Tick / 15) % 3 == 1 ? IntentAction.Shoot : IntentAction.DrivenShot);
                        intent.Action = shot;
                        intent.Power = 0.5f + ((s.Tick % 7) / 14f);
                        intent.Aim = toGoal;
                    }
                    else if ((s.Tick + i * 7) % 70 == 0)
                    {
                        IntentAction pass = (s.Tick / 70) % 4 == 0 ? IntentAction.Lob : ((s.Tick / 70) % 4 == 1 ? IntentAction.ThroughPass : IntentAction.Pass);
                        intent.Action = pass;
                        intent.Power = 0.5f;
                        intent.Aim = new SimVec3(((s.Tick % 11) - 5) * 0.15f, 0f, sign);
                    }
                }
                else if (i == chaserHome || i == chaserAway)
                {
                    SimVec3 to = (ball - p.Position).Flat;
                    intent = PlayerIntent.MoveOnly(to.Normalized, to.Magnitude > 4f);
                    if (s.Owner >= 0 && MatchSim.TeamOf(s.Owner) != team && to.Magnitude < 1.4f && (s.Tick + i) % 25 == 0)
                        intent.Action = (s.Tick % 50) < 25 ? IntentAction.Tackle : IntentAction.SlideTackle;
                    else if (s.Owner >= 0 && MatchSim.TeamOf(s.Owner) != team && to.Magnitude < 4f)
                        intent.Action = IntentAction.Jockey;
                }
                else
                {
                    // Drift toward a formation spot shifted with the ball.
                    int slot = i - MatchSim.FirstId(team);
                    SimVec3 f = sim.Setup.HomeFormation[slot];
                    SimVec3 home = new SimVec3(f.X * sign, 0f, f.Z * sign + ball.Z * 0.6f + sign * 12f);
                    SimVec3 to = (home - p.Position).Flat;
                    if (to.Magnitude > 1.5f) intent = PlayerIntent.MoveOnly(to.Normalized * SimMath.Min(1f, to.Magnitude / 5f));
                }

                intents[i] = intent;
            }
        }

        private static int NearestMate(MatchSim sim, int id)
        {
            int first = MatchSim.FirstId(MatchSim.TeamOf(id));
            int best = first;
            float bestD = float.MaxValue;
            for (int j = first + 1; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (j == id || sim.State.Players[j].SentOff) continue;
                float d = SimMath.FlatDistance(sim.State.Players[j].Position, sim.State.Players[id].Position);
                if (d < bestD) { bestD = d; best = j; }
            }

            return best;
        }

        private static int Nearest(MatchSim sim, SimTeam team, SimVec3 pos)
        {
            int first = MatchSim.FirstId(team);
            int best = -1;
            float bestD = float.MaxValue;
            for (int j = first + 1; j < first + MatchSetup.PlayersPerTeam; j++)
            {
                if (sim.State.Players[j].SentOff) continue;
                float d = SimMath.FlatDistance(sim.State.Players[j].Position, pos);
                if (d < bestD) { bestD = d; best = j; }
            }

            return best;
        }
    }
}
