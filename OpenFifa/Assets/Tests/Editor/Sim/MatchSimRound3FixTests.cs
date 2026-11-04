using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>Regression tests for the third-round review findings on PR #5 (match sim).</summary>
    [TestFixture]
    [Category("FID_SIM")]
    public class MatchSimRound3FixTests
    {
        private const float HL = 52.5f;

        // ---- Sim note: lofted flight times in the lead-point search come from a cached table. ----

        [Test]
        public void PassFlightTime_LobAfterWarmUp_RunsNoFlightIntegrations()
        {
            var cfg = new MatchSimConfig();
            KickSolver.PassFlightTime(IntentAction.Lob, 0.5f, 30f, false, cfg.Actions, cfg.Ball); // builds the table
            KickSolver.PassFlightTime(IntentAction.Cross, 0.5f, 30f, false, cfg.Actions, cfg.Ball);
            KickSolver.PassFlightTime(IntentAction.Pass, 0.5f, 30f, true, cfg.Actions, cfg.Ball);
            KickSolver.CarrySimulations = 0;
            for (int i = 0; i < 40; i++)
            {
                float d = 5f + i * 1.37f;
                KickSolver.PassFlightTime(IntentAction.Lob, 0.5f, d, false, cfg.Actions, cfg.Ball);
                KickSolver.PassFlightTime(IntentAction.Cross, 0.5f, d, false, cfg.Actions, cfg.Ball);
                KickSolver.PassFlightTime(IntentAction.Pass, 0.5f, d, true, cfg.Actions, cfg.Ball);
            }

            Assert.AreEqual(0, KickSolver.CarrySimulations,
                "the lead-point search must read lofted times from the table, not re-solve LoftedSpeed by bisection");
        }

        [Test]
        public void PassFlightTime_LoftedTable_MatchesExactSolveWithinTolerance()
        {
            var cfg = new MatchSimConfig();
            var ac = cfg.Actions;
            var ball = cfg.Ball;
            for (int k = 0; k < 3; k++)
            {
                IntentAction act = k == 0 ? IntentAction.Lob : (k == 1 ? IntentAction.Cross : IntentAction.Pass);
                bool throwIn = k == 2;
                float e = (k == 0 ? ac.LobElevationDeg : (k == 1 ? ac.CrossElevationDeg : 25f)) * SimMath.Deg2Rad;
                float spin = k == 0 ? -8f : (k == 1 ? -5f : 0f);
                for (float d = 3f; d <= 70f; d += 0.73f)
                {
                    float exactSpeed = KickSolver.LoftedSpeed(throwIn ? SimMath.Max(d, 5f) : d, e, spin, ball);
                    if (throwIn) exactSpeed = SimMath.Min(exactSpeed, 15f);
                    float exact = KickSolver.LoftedTime(exactSpeed, e, spin, ball);
                    float cached = KickSolver.PassFlightTime(act, 0.5f, d, throwIn, ac, ball);
                    Assert.AreEqual(exact, cached, 1.5f / 120f, $"{act} throwIn={throwIn} d={d}");
                }
            }
        }

        [Test]
        public void LoftedSpeedCached_MatchesBisection()
        {
            var ball = new BallSimConfig();
            float e = 40f * SimMath.Deg2Rad;
            for (float d = 3f; d <= 70f; d += 0.91f)
            {
                float exact = KickSolver.LoftedSpeed(d, e, -8f, ball);
                float cached = KickSolver.LoftedSpeedCached(d, e, -8f, ball);
                Assert.AreEqual(exact, cached, 0.05f, $"d={d}");
                // Where it matters: the cached speed lands as close to the target as the bisection does.
                Assert.AreEqual(d, KickSolver.Carry(cached, e, -8f, ball), 0.15f, $"d={d}");
            }
        }

        [Test]
        public void LoftedTable_BallConfigChanged_Rebuilt()
        {
            var ball = new BallSimConfig();
            float e = 40f * SimMath.Deg2Rad;
            float before = KickSolver.LoftedSpeedCached(30f, e, -8f, ball);
            ball.DragCoefficientLowSpeed *= 2f;
            ball.DragCoefficientHighSpeed *= 2f;
            float after = KickSolver.LoftedSpeedCached(30f, e, -8f, ball);
            Assert.AreEqual(KickSolver.LoftedSpeed(30f, e, -8f, ball), after, 0.02f);
            Assert.Greater(after, before + 0.1f);
        }

        [Test]
        public void ResolveKick_LobToRunningReceiver_FewFlightIntegrations()
        {
            var cfg = new MatchSimConfig();
            var sim = SimTestHelpers.NewSim(41, cfg);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, HL - 1f);
            SimVec3 passer = new SimVec3(0f, 0f, -35f);
            SimTestHelpers.Place(sim, 5, passer.X, passer.Z);
            SimTestHelpers.Place(sim, 9, -6f, -5f);
            sim.State.Players[9].Velocity = new SimVec3(4f, 0f, 1f);
            SimVec3 aim = (sim.State.Players[9].Position - passer).Flat.Normalized;
            sim.State.Players[5].Facing = SimMath.Yaw(aim);
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, passer + aim * 0.35f + new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
            KickSolver.PassFlightTime(IntentAction.Lob, 0.5f, 30f, false, cfg.Actions, cfg.Ball); // warm the table

            var intents = SimTestHelpers.IdleIntents();
            intents[5] = PlayerIntent.Kick(IntentAction.Lob, 0.5f, aim, 9);
            int maxPerTick = 0;
            bool kicked = false;
            for (int t = 0; t < 120 && !kicked; t++)
            {
                sim.State.Players[9].Velocity = new SimVec3(4f, 0f, 1f);
                KickSolver.CarrySimulations = 0;
                foreach (var e in sim.Step(intents))
                    if (e.Type == MatchEventType.Pass && e.PlayerId == 5) kicked = true;
                intents[5] = PlayerIntent.Idle;
                if (KickSolver.CarrySimulations > maxPerTick) maxPerTick = KickSolver.CarrySimulations;
            }

            Assert.IsTrue(kicked);
            // Solve itself still runs one exact bisection (15 carries); the lead search must add none.
            Assert.LessOrEqual(maxPerTick, 15);
        }

        // ---- 9: kick variants (KickStyle) reach the solver. ----

        private static SimKickRequest Req(IntentAction act, KickStyle style, float power, float targetDistance)
        {
            return new SimKickRequest
            {
                Action = act,
                Style = style,
                Power = power,
                Aim = new SimVec3(0f, 0f, 1f),
                TargetPoint = new SimVec3(0f, 0f, targetDistance),
                HasTarget = targetDistance > 0f,
                KickerPosition = new SimVec3(0f, 0f, -0.4f),
                BallPosition = new SimVec3(0f, 0.11f, 0f),
            };
        }

        private static SimKickResult SolveNoError(SimKickRequest r)
        {
            var cfg = new MatchSimConfig();
            var ac = cfg.Actions;
            ac.PassErrorDeg = ac.ThroughErrorDeg = ac.LobErrorDeg = ac.CrossErrorDeg = 0f;
            ac.ShotErrorDeg = ac.FinesseErrorDeg = ac.DrivenErrorDeg = 0f;
            return KickSolver.Solve(r, new SimPlayerAttributes(), ac, cfg.Ball, new SimRng(3));
        }

        private static float ElevationDeg(SimVec3 v) =>
            (float)System.Math.Atan2(v.Y, v.Flat.Magnitude) / SimMath.Deg2Rad;

        [Test]
        public void Solve_ChipShot_HighArcWithBackspinLandingOnTarget()
        {
            var plain = SolveNoError(Req(IntentAction.Shoot, KickStyle.Default, 0.6f, 14f));
            var chip = SolveNoError(Req(IntentAction.Shoot, KickStyle.Chip, 0.6f, 14f));
            Assert.Less(ElevationDeg(plain.Velocity), 10f);
            Assert.Greater(ElevationDeg(chip.Velocity), 30f, "a chip is a high arc");
            // Backspin: spin about the right axis is negative (topspin positive).
            SimVec3 right = SimVec3.Cross(SimVec3.Up, chip.Velocity.Flat.Normalized);
            Assert.Less(SimVec3.Dot(chip.Spin, right), -1f, "a chip carries backspin");
            Assert.Less(chip.Velocity.Magnitude, plain.Velocity.Magnitude);
            var ball = new SimBall { Position = new SimVec3(0f, 0.11f, 0f), Velocity = chip.Velocity, Spin = chip.Spin };
            var landed = SimTestHelpers.FlyUntilFirstBounce(ball, new BallSimConfig(), SimTestHelpers.HugePitch);
            Assert.AreEqual(14f, landed.Position.Z, 0.6f, "drops onto the target (goal line) point");
        }

        [Test]
        public void Solve_LowShot_FlatterThanNormalShotAtSamePower()
        {
            var plain = SolveNoError(Req(IntentAction.Shoot, KickStyle.Default, 0.9f, 20f));
            var low = SolveNoError(Req(IntentAction.Shoot, KickStyle.Low, 0.9f, 20f));
            Assert.Less(ElevationDeg(low.Velocity), 1.5f);
            Assert.Less(ElevationDeg(low.Velocity), ElevationDeg(plain.Velocity) - 3f);
            Assert.Greater(low.Velocity.Magnitude, 15f, "still a driven strike");
        }

        [Test]
        public void Solve_DrivenPass_FasterThanPlainPassToSameTarget()
        {
            var plain = SolveNoError(Req(IntentAction.Pass, KickStyle.Default, 0.5f, 20f));
            var driven = SolveNoError(Req(IntentAction.Pass, KickStyle.Driven, 0.5f, 20f));
            Assert.AreEqual(0f, plain.Velocity.Y, 1e-4f);
            Assert.AreEqual(0f, driven.Velocity.Y, 1e-4f, "driven pass stays on the ground");
            Assert.Greater(driven.Velocity.Magnitude, plain.Velocity.Magnitude + 2f);
            var cfg = new MatchSimConfig();
            Assert.Less(KickSolver.PassFlightTime(IntentAction.Pass, KickStyle.Driven, 0.5f, 20f, false, cfg.Actions, cfg.Ball),
                KickSolver.PassFlightTime(IntentAction.Pass, KickStyle.Default, 0.5f, 20f, false, cfg.Actions, cfg.Ball) - 0.2f);
        }

        [Test]
        public void Solve_DrivenLob_FlatterThanLob()
        {
            var lob = SolveNoError(Req(IntentAction.Lob, KickStyle.Default, 0.5f, 30f));
            var driven = SolveNoError(Req(IntentAction.Lob, KickStyle.Driven, 0.5f, 30f));
            Assert.Less(ElevationDeg(driven.Velocity), ElevationDeg(lob.Velocity) - 10f);
        }

        /// <summary>Home 5 lobs to Home 9, who runs at a constant velocity; returns where the ball first lands.</summary>
        private static SimVec3 LobLanding(KickStyle style, SimVec3 run, out SimVec3 receiverAtLanding)
        {
            var cfg = new MatchSimConfig();
            cfg.Actions.LobErrorDeg = 0f;
            var sim = SimTestHelpers.NewSim(77, cfg);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, HL - 1f);
            SimVec3 passer = new SimVec3(0f, 0f, -35f);
            SimVec3 q0 = new SimVec3(-6f, 0f, -10f);
            SimTestHelpers.Place(sim, 5, passer.X, passer.Z);
            SimVec3 aim = (q0 - passer).Flat.Normalized;
            sim.State.Players[5].Facing = SimMath.Yaw(aim);
            sim.State.Owner = 5;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, passer + aim * 0.35f + new SimVec3(0f, 0.11f, 0f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            intents[5] = PlayerIntent.Kick(IntentAction.Lob, 0.5f, aim, 9, style);
            float dt = sim.Config.Dt;
            bool kicked = false;
            for (int t = 0; t < 600; t++)
            {
                SimVec3 q = q0 + run * (t * dt);
                sim.State.Players[9].Position = q;
                sim.State.Players[9].Velocity = run;
                bool airborneBefore = kicked && !sim.State.Ball.Grounded;
                foreach (var e in sim.Step(intents))
                    if (e.Type == MatchEventType.Pass && e.PlayerId == 5) kicked = true;
                intents[5] = PlayerIntent.Idle;
                if (airborneBefore && (sim.State.Ball.Position.Y < 0.2f || sim.State.Owner == 9))
                {
                    receiverAtLanding = q0 + run * ((t + 1) * dt);
                    return sim.State.Ball.Position.Flat;
                }
            }

            Assert.Fail("lob never landed");
            receiverAtLanding = default;
            return default;
        }

        [Test]
        public void LobbedThrough_ToRunningReceiver_LandsLeadDistanceInHisPath()
        {
            SimVec3 run = new SimVec3(0f, 0f, 6f);
            SimVec3 plain = LobLanding(KickStyle.Default, run, out SimVec3 q1);
            SimVec3 through = LobLanding(KickStyle.LobbedThrough, run, out SimVec3 q2);
            float lead = new MatchSimConfig().Actions.ThroughLeadDistance;
            Assert.AreEqual(0f, SimMath.FlatDistance(plain, q1), 2.0f, "plain lob lands at the receiver");
            float ahead = SimVec3.Dot((through - q2).Flat, run.Normalized);
            Assert.AreEqual(lead, ahead, 2.0f, $"lobbed through lands in his path ({through} vs runner {q2})");
        }

        [Test]
        public void PlayerIntent_KickWithoutStyle_DefaultsToPlain()
        {
            Assert.AreEqual(KickStyle.Default, PlayerIntent.Kick(IntentAction.Shoot, 1f, SimVec3.Zero).Style);
            Assert.AreEqual(KickStyle.Default, PlayerIntent.Idle.Style);
            Assert.AreEqual(KickStyle.Default, default(PlayerIntent).Style);
        }

        [Test]
        public void ChipShot_KeeperOffHisLine_BallPassesOverHisReach()
        {
            float heightOverKeeper = ShotOverKeeper(KickStyle.Chip, out bool saved);
            Assert.IsFalse(saved);
            Assert.Greater(heightOverKeeper, new MatchSimConfig().Actions.GkMaxReachHeight,
                "a chip from 14 m clears a keeper 4 m off his line");
            float plain = ShotOverKeeper(KickStyle.Default, out _);
            Assert.Less(plain, 2.0f, "the same shot without the chip goes at the keeper");
        }

        /// <summary>Home 9 shoots from 14 m at the Away goal (+Z); the Away keeper stands 4 m off his line.</summary>
        private static float ShotOverKeeper(KickStyle style, out bool saved)
        {
            var cfg = new MatchSimConfig();
            var ac = cfg.Actions;
            ac.ShotErrorDeg = 0f;
            var sim = SimTestHelpers.NewSim(5, cfg);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -HL + 1f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, -10f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 4f);
            sim.State.Players[11].Facing = SimMath.Pi;
            SimTestHelpers.Place(sim, 9, 0f, HL - 14.5f);
            sim.State.Players[9].Facing = 0f;
            sim.State.Owner = 9;
            sim.State.PossessionTeam = SimTeam.Home;
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, HL - 14f), SimVec3.Zero);
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = PlayerIntent.Kick(IntentAction.Shoot, 0.6f, new SimVec3(0f, 0f, 1f), -1, style);
            saved = false;
            float keeperZ = HL - 4f;
            float prevZ = sim.State.Ball.Position.Z;
            for (int t = 0; t < 300; t++)
            {
                sim.State.Players[11].Position = new SimVec3(0f, 0f, keeperZ);
                sim.State.Players[11].Velocity = SimVec3.Zero;
                foreach (var e in sim.Step(intents))
                    if (e.Type == MatchEventType.Save) saved = true;
                intents[9] = PlayerIntent.Idle;
                var b = sim.State.Ball;
                if (prevZ < keeperZ && b.Position.Z >= keeperZ) return b.Position.Y;
                if (saved || sim.State.Owner == 11) return 0f;
                prevZ = b.Position.Z;
            }

            return 0f;
        }
    }
}
