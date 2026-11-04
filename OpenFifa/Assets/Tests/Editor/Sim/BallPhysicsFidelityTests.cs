using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    /// <summary>
    /// Ball physics fidelity: spin-parameter Magnus lift, spin drag, knuckleballs, pitch surfaces, and how touches
    /// (first touch, body deflections, knuckle shots) set the ball moving.
    /// </summary>
    [TestFixture]
    [Category("FID_SIM")]
    public class BallPhysicsFidelityTests
    {
        private const float Dt = SimTestHelpers.BallDt;
        private const float HL = 52.5f;
        private static PitchSpec Huge => SimTestHelpers.HugePitch;

        private static SimBall Launch(float speed, float elevationDeg, SimVec3 spin, float x = 0f, float z = 0f)
        {
            float e = elevationDeg * SimMath.Deg2Rad;
            return new SimBall
            {
                Position = new SimVec3(x, 0.11f, z),
                Velocity = new SimVec3(0f, speed * (float)System.Math.Sin(e), speed * (float)System.Math.Cos(e)),
                Spin = spin,
            };
        }

        private static SimBall FlyForward(SimBall b, BallSimConfig cfg, float metres)
        {
            float z0 = b.Position.Z;
            for (int i = 0; i < 2400 && b.Position.Z - z0 < metres; i++) BallIntegrator.Step(ref b, cfg, Huge, Dt);
            return b;
        }

        private static BallSimConfig NoKnuckle() => new BallSimConfig();

        // ---- Aerodynamics. ----

        [Test]
        public void LiftCoefficient_GrowsWithSpinThenSaturates()
        {
            var c = new BallSimConfig();
            float low = BallIntegrator.LiftCoefficient(c, 0.1f);
            float mid = BallIntegrator.LiftCoefficient(c, 0.3f);
            float high = BallIntegrator.LiftCoefficient(c, 1.5f);
            Assert.AreEqual(0f, BallIntegrator.LiftCoefficient(c, 0f));
            Assert.Less(low, mid);
            Assert.Less(mid, high);
            Assert.Less(high, 0.6f, "lift saturates for heavily spun balls");
            Assert.Greater(mid / low, 2f, "near-linear for small spin");
            Assert.Less(high / mid, 3f, "but far from linear at large spin");
        }

        [Test]
        public void Curl_SlowHeavilySpunBall_BendsLessThanLinearMagnusWould()
        {
            var lift = NoKnuckle();
            var linear = new BallSimConfig { UseSpinParameterLift = false, DragSpinFactor = 0f, KnuckleForceCoefficient = 0f };
            var spin = new SimVec3(0f, 80f, 0f);
            float bendLift = FlyForward(Launch(15f, 10f, spin), lift, 25f).Position.X;
            float bendLinear = FlyForward(Launch(15f, 10f, spin), linear, 25f).Position.X;
            Assert.Greater(bendLift, 1f, "still curls");
            Assert.Less(bendLift, bendLinear - 1f, $"lift {bendLift:F2} m vs linear {bendLinear:F2} m");
        }

        [Test]
        public void Curl_FastFreeKick_StillBendsTwoToFourMetres()
        {
            float bend = FlyForward(Launch(25f, 10f, new SimVec3(0f, 60f, 0f)), new BallSimConfig(), 25f).Position.X;
            Assert.That(bend, Is.InRange(2f, 4.5f));
        }

        [Test]
        public void SpinDrag_SpinningBallSlowsFasterInTheAir()
        {
            var c = NoKnuckle();
            var plain = Launch(28f, 15f, SimVec3.Zero);
            var spun = Launch(28f, 15f, new SimVec3(0f, 70f, 0f));
            for (int i = 0; i < 60; i++)
            {
                BallIntegrator.Step(ref plain, c, Huge, Dt);
                BallIntegrator.Step(ref spun, c, Huge, Dt);
            }

            Assert.Less(spun.Velocity.Magnitude, plain.Velocity.Magnitude - 0.2f);
        }

        [Test]
        public void SpinDrag_NotAppliedToRollingBall()
        {
            var a = new BallSimConfig();
            var b = new BallSimConfig { DragSpinFactor = 0f };
            Assert.AreEqual(BallFlight.GroundRollDistance(b, 12f), BallFlight.GroundRollDistance(a, 12f), 1e-4f);
        }

        // ---- Knuckleball. ----

        private static SimBall KnuckleBall(float speed, float elevationDeg, SimVec3 dir, SimVec3 from, float seed)
        {
            float e = elevationDeg * SimMath.Deg2Rad;
            SimVec3 flat = dir.Flat.Normalized;
            var b = new SimBall
            {
                Position = new SimVec3(from.X, 0.11f, from.Z),
                Velocity = flat * (speed * (float)System.Math.Cos(e)) + new SimVec3(0f, speed * (float)System.Math.Sin(e), 0f),
                KnuckleSeed = seed,
            };
            b.KnuckleOrigin = b.Position;
            return b;
        }

        /// <summary>Signed sideways offset (m, right of travel) of a knuckle shot vs the same shot without knuckle, after 25 m.</summary>
        private static float KnuckleDeviation(SimVec3 dir, SimVec3 from, float seed)
        {
            var on = KnuckleBall(29f, 8f, dir, from, seed);
            var off = on;
            off.ClearKnuckle();
            var cfg = new BallSimConfig();
            SimVec3 f = dir.Flat.Normalized;
            for (int i = 0; i < 600; i++)
            {
                if (SimVec3.Dot(on.Position - from, f) >= 25f) break;
                BallIntegrator.Step(ref on, cfg, Huge, Dt);
                BallIntegrator.Step(ref off, cfg, Huge, Dt);
            }

            SimVec3 right = new SimVec3(f.Z, 0f, -f.X);
            return SimVec3.Dot(on.Position - off.Position, right);
        }

        [Test]
        public void Knuckle_ShotWobblesOffItsLine_EachSeedItsOwnWay()
        {
            float maxDev = 0f, min = float.MaxValue, max = float.MinValue;
            for (int i = 1; i <= 6; i++)
            {
                float dev = KnuckleDeviation(new SimVec3(0f, 0f, 1f), SimVec3.Zero, i / 7f);
                maxDev = SimMath.Max(maxDev, SimMath.Abs(dev));
                min = SimMath.Min(min, dev);
                max = SimMath.Max(max, dev);
                Assert.Less(SimMath.Abs(dev), 1.5f, "a wobble, not a curler");
            }

            Assert.Greater(maxDev, 0.2f, "knuckling shots move off their line");
            Assert.Greater(max - min, 0.2f, "each strike wobbles its own way");
        }

        [Test]
        public void Knuckle_SameSeed_SameWobbleInEveryDirectionAndPlace()
        {
            // Regression: the phase once came from absolute yaw/position (a +-pi jump for -Z shots and different drift
            // per direction). Now it depends only on the seed and the distance flown.
            float reference = KnuckleDeviation(new SimVec3(0f, 0f, 1f), SimVec3.Zero, 0.4f);
            foreach (var dir in new[] { new SimVec3(0f, 0f, -1f), new SimVec3(-0.02f, 0f, -1f), new SimVec3(0.02f, 0f, -1f),
                         new SimVec3(1f, 0f, 0.3f), new SimVec3(-1f, 0f, -0.7f) })
                Assert.AreEqual(reference, KnuckleDeviation(dir, new SimVec3(13f, 0f, -21f), 0.4f), 0.02f, $"dir {dir.X},{dir.Z}");
        }

        [Test]
        public void Knuckle_ForceIsContinuousAlongTheFlight()
        {
            var cfg = new BallSimConfig();
            var b = KnuckleBall(29f, 8f, new SimVec3(-0.01f, 0f, -1f), SimVec3.Zero, 0.3f);
            SimVec3 prev = BallIntegrator.KnuckleForce(cfg, b, b.Velocity, b.Spin, b.Velocity.Magnitude);
            for (int i = 0; i < 100; i++)
            {
                BallIntegrator.Step(ref b, cfg, Huge, Dt);
                SimVec3 f = BallIntegrator.KnuckleForce(cfg, b, b.Velocity, b.Spin, b.Velocity.Magnitude);
                Assert.Less(SimVec3.Distance(f, prev), 1.8f, $"step {i}: smooth, no phase jumps (a yaw wrap once jumped ~7 N)");
                prev = f;
            }
        }

        [Test]
        public void Knuckle_OnlyFlaggedShots_NotSpinlessHeadersOrLobs()
        {
            var cfg = new BallSimConfig();
            var plain = KnuckleBall(29f, 8f, new SimVec3(0f, 0f, 1f), SimVec3.Zero, 0f);
            Assert.IsFalse(plain.Knuckling);
            Assert.AreEqual(SimVec3.Zero, BallIntegrator.KnuckleForce(cfg, plain, plain.Velocity, plain.Spin, 29f));
        }

        [Test]
        public void Knuckle_EndsAtFirstBounce()
        {
            var b = KnuckleBall(25f, 12f, new SimVec3(0f, 0f, 1f), SimVec3.Zero, 0.6f);
            var landed = SimTestHelpers.FlyUntilFirstBounce(b, new BallSimConfig(), Huge);
            Assert.IsFalse(landed.Knuckling);
        }

        [Test]
        public void Knuckle_SpinningOrSlowBall_Unaffected()
        {
            var cfg = new BallSimConfig();
            var spun = KnuckleBall(29f, 8f, new SimVec3(0f, 0f, 1f), SimVec3.Zero, 0.5f);
            spun.Spin = new SimVec3(30f, 0f, 0f);
            Assert.AreEqual(SimVec3.Zero, BallIntegrator.KnuckleForce(cfg, spun, spun.Velocity, spun.Spin, 29f));
            var slow = KnuckleBall(15f, 8f, new SimVec3(0f, 0f, 1f), SimVec3.Zero, 0.5f);
            Assert.AreEqual(SimVec3.Zero, BallIntegrator.KnuckleForce(cfg, slow, slow.Velocity, slow.Spin, 15f));
        }

        [Test]
        public void SinDeterministic_CloseToMathSin()
        {
            for (float x = -40f; x <= 40f; x += 0.37f)
                Assert.AreEqual((float)System.Math.Sin(x), SimMath.SinDeterministic(x), 0.002f, $"x {x}");
        }

        [Test]
        public void KnuckleShot_InMatch_SeedsTheBallAndHeadersDoNot()
        {
            var sim = SimTestHelpers.NewSim(9);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
            SimTestHelpers.Place(sim, 9, 0f, 20f);
            SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 20.5f), SimVec3.Zero);
            sim.State.Owner = 9;
            sim.State.PossessionTeam = SimTeam.Home;
            var intents = SimTestHelpers.IdleIntents();
            intents[9] = new PlayerIntent { Action = IntentAction.Shoot, Power = 0.9f, Style = KickStyle.Knuckle,
                Aim = new SimVec3(0f, 0f, 1f) };
            bool kicked = false;
            for (int t = 0; t < 60 && !kicked; t++)
            {
                foreach (var e in sim.Step(intents)) if (e.Type == MatchEventType.Shot) kicked = true;
                intents = SimTestHelpers.IdleIntents();
            }

            Assert.IsTrue(kicked, "shot taken");
            Assert.IsTrue(sim.State.Ball.Knuckling, "a knuckle shot is seeded");
        }

        [Test]
        public void KnuckleShot_SolverStrikesWithAlmostNoSpinAndExtraPace()
        {
            var cfg = new SimActionConfig { ShotErrorDeg = 0f };
            var ball = new BallSimConfig();
            SimKickRequest req = new SimKickRequest
            {
                Action = IntentAction.Shoot, Power = 0.8f, Aim = new SimVec3(0f, 0f, 1f),
                KickerPosition = new SimVec3(0f, 0f, -0.4f), BallPosition = new SimVec3(0f, ball.Radius, 0f),
            };
            var plain = KickSolver.Solve(req, new SimPlayerAttributes(), cfg, ball, new SimRng(3));
            req.Style = KickStyle.Knuckle;
            var knuckle = KickSolver.Solve(req, new SimPlayerAttributes(), cfg, ball, new SimRng(3));
            Assert.Less(knuckle.Spin.Magnitude, 3f);
            Assert.Greater(plain.Spin.Magnitude, 10f);
            Assert.Greater(knuckle.Velocity.Magnitude, plain.Velocity.Magnitude);
        }

        // ---- Pitch surfaces. ----

        private static SimBall RollFrom(float speed)
        {
            var b = SimBall.AtRest(SimVec3.Zero, 0.11f);
            b.Velocity = new SimVec3(0f, 0f, speed);
            b.Spin = BallIntegrator.RollingSpin(b.Velocity, 0.11f, 0f);
            return b;
        }

        private static float FirstReboundApex(BallSimConfig c)
        {
            var b = new SimBall { Position = new SimVec3(0f, 2f, 0f), Velocity = new SimVec3(0f, 0f, 8f) };
            bool bounced = false;
            for (int i = 0; i < 1200; i++)
            {
                var f = BallIntegrator.Step(ref b, c, Huge, Dt);
                if ((f & BallStepFlags.Bounced) != 0) bounced = true;
                else if (bounced && b.Velocity.Y <= 0f) return b.Position.Y;
            }

            return 0f;
        }

        [Test]
        public void Surface_RollDistance_HeavyShortest_DryLongest()
        {
            float heavy = SimTestHelpers.RollUntilStopped(RollFrom(12f), new BallSimConfig().WithSurface(PitchSurface.Heavy), Huge).Position.Z;
            float normal = SimTestHelpers.RollUntilStopped(RollFrom(12f), new BallSimConfig(), Huge).Position.Z;
            float dry = SimTestHelpers.RollUntilStopped(RollFrom(12f), new BallSimConfig().WithSurface(PitchSurface.Dry), Huge).Position.Z;
            Assert.Less(heavy, normal - 2f);
            Assert.Greater(dry, normal + 1f);
        }

        [Test]
        public void Surface_Bounce_DryHighest_WetAndHeavyDead()
        {
            float normal = FirstReboundApex(new BallSimConfig());
            Assert.Greater(FirstReboundApex(new BallSimConfig().WithSurface(PitchSurface.Dry)), normal + 0.1f);
            Assert.Less(FirstReboundApex(new BallSimConfig().WithSurface(PitchSurface.Wet)), normal - 0.1f);
            Assert.Less(FirstReboundApex(new BallSimConfig().WithSurface(PitchSurface.Heavy)), normal - 0.1f);
        }

        [Test]
        public void Surface_Wet_BallSkidsOnAfterBounce()
        {
            float SpeedAfterBounce(BallSimConfig c)
            {
                var b = new SimBall { Position = new SimVec3(0f, 1f, 0f), Velocity = new SimVec3(0f, -3f, 12f) };
                for (int i = 0; i < 600; i++)
                    if ((BallIntegrator.Step(ref b, c, Huge, Dt) & BallStepFlags.Bounced) != 0) return b.Velocity.Flat.Magnitude;
                return 0f;
            }

            Assert.Greater(SpeedAfterBounce(new BallSimConfig().WithSurface(PitchSurface.Wet)),
                SpeedAfterBounce(new BallSimConfig()) + 0.5f);
        }

        [Test]
        public void Surface_WithSurface_ReturnsCopy_KeepsTuning_AndPredictionFollowsIt()
        {
            var c = new BallSimConfig { RollingResistance = 0.11f, LiftScale = 1.1f };
            float normal = BallFlight.GroundRollDistance(c, 12f);
            var heavy = c.WithSurface(PitchSurface.Heavy);
            Assert.AreEqual(PitchSurface.Heavy, heavy.Surface);
            Assert.AreEqual(PitchSurface.Normal, c.Surface, "the source config is untouched");
            Assert.AreEqual(0.11f, c.RollingResistance);
            Assert.AreEqual(0.11f * 1.3f, heavy.RollingResistance, 1e-5f, "custom tuning is scaled, not reset");
            Assert.AreEqual(1.1f, heavy.LiftScale, "air values kept");
            Assert.Less(BallFlight.GroundRollDistance(heavy, 12f), normal - 2f, "heavy roll table");
            Assert.AreEqual(normal, BallFlight.GroundRollDistance(c, 12f), 1e-4f, "source roll table unchanged");
            Assert.Throws<System.InvalidOperationException>(() => heavy.WithSurface(PitchSurface.Wet));
        }

        [Test]
        public void Surface_WetDribble_TouchStillReachesItsTarget()
        {
            // The dribble touch speed accounts for water drag, so touches do not fall short on a wet pitch.
            foreach (var surface in new[] { PitchSurface.Normal, PitchSurface.Wet })
            {
                var cfg = new MatchSimConfig();
                cfg.Ball = cfg.Ball.WithSurface(surface);
                var sim = SimTestHelpers.NewSim(5, cfg);
                SimTestHelpers.ForceOpenPlay(sim);
                SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
                SimTestHelpers.Place(sim, 9, 0f, 0f);
                SimTestHelpers.SetBall(sim, new SimVec3(0f, 0.11f, 0.5f), SimVec3.Zero);
                sim.State.Owner = 9;
                sim.State.PossessionTeam = SimTeam.Home;
                var intents = SimTestHelpers.IdleIntents();
                intents[9] = new PlayerIntent { Move = new SimVec3(0f, 0f, 1f) };
                int loose = 0;
                for (int t = 0; t < 240; t++)
                {
                    sim.Step(intents);
                    if (sim.State.Owner != 9) loose++;
                }

                Assert.AreEqual(0, loose, $"{surface}: dribble keeps the ball");
            }
        }

        [Test]
        public void Surface_ForWeather_RainIsWet_SnowIsHeavy()
        {
            Assert.AreEqual(PitchSurface.Normal, PitchSurfaces.ForWeather(WeatherType.Clear));
            Assert.AreEqual(PitchSurface.Wet, PitchSurfaces.ForWeather(WeatherType.LightRain));
            Assert.AreEqual(PitchSurface.Wet, PitchSurfaces.ForWeather(WeatherType.HeavyRain));
            Assert.AreEqual(PitchSurface.Heavy, PitchSurfaces.ForWeather(WeatherType.Snow));
        }

        // ---- Touches. ----

        private static MatchSim Receiver(SimVec3 ballPos, SimVec3 ballVel)
        {
            var sim = SimTestHelpers.NewSim(4);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, -45f);
            SimTestHelpers.Place(sim, 9, 0f, 10f);
            SimTestHelpers.SetBall(sim, ballPos, ballVel);
            return sim;
        }

        [Test]
        public void FirstTouch_AerialBall_CushionedDownNotSnappedToTurf()
        {
            var sim = Receiver(new SimVec3(0f, 1.2f, 9.2f), new SimVec3(0f, -0.5f, 5f));
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.FirstTouch, 30, out _));
            var b = sim.State.Ball;
            Assert.Greater(b.Position.Y, 0.7f, "the ball stays where it was met for the touch tick");
            Assert.IsFalse(b.Grounded);
            Assert.AreEqual(9, sim.State.Owner);

            SimVec3 prev = b.Position;
            for (int t = 0; t < 90; t++)
            {
                sim.Step(SimTestHelpers.IdleIntents());
                Assert.Less(SimVec3.Distance(prev, sim.State.Ball.Position), 0.25f, "drops smoothly");
                prev = sim.State.Ball.Position;
            }

            Assert.AreEqual(0.11f, sim.State.Ball.Position.Y, 0.05f, "lands at the feet");
            Assert.AreEqual(9, sim.State.Owner);
        }

        [Test]
        public void FirstTouch_AerialBall_PredictedDribbleTouchTickMatchesActual()
        {
            var sim = Receiver(new SimVec3(0f, 1.2f, 9.2f), new SimVec3(0f, -0.5f, 5f));
            var intents = SimTestHelpers.IdleIntents();
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, intents, MatchEventType.FirstTouch, 30, out _));
            int checks = 0;
            for (int t = 0; t < 40; t++)
            {
                bool predicted = sim.TryPredictDribbleTouch(9, 120, out int predictedTick, out _);
                int actual = -1;
                var probe = sim.State; // the sim steps forward; record when the touch really happens
                for (int k = 0; k < 120 && actual < 0; k++)
                    foreach (var e in sim.Step(intents))
                        if (e.Type == MatchEventType.DribbleTouch && e.PlayerId == 9) actual = sim.State.Tick;
                Assert.IsTrue(predicted, $"from tick {t}");
                Assert.AreEqual(predictedTick, actual, $"prediction made {t} ticks after the first touch");
                checks++;
                break;
            }

            Assert.AreEqual(1, checks);
        }

        [Test]
        public void FirstTouch_SoftGroundBall_KilledFlat_HeavyOneBobblesUp()
        {
            var soft = Receiver(new SimVec3(0f, 0.11f, 9.16f), new SimVec3(0f, 0f, 4f));
            SimTestHelpers.StepUntil(soft, SimTestHelpers.IdleIntents(), MatchEventType.FirstTouch, 10, out _);
            Assert.IsTrue(soft.State.Ball.Grounded);
            Assert.AreEqual(0f, soft.State.Ball.Velocity.Y, 1e-4f);

            bool popped = false;
            for (ulong seed = 1; seed <= 5 && !popped; seed++)
            {
                var hard = SimTestHelpers.NewSim(seed);
                SimTestHelpers.ForceOpenPlay(hard);
                SimTestHelpers.ParkTeam(hard, SimTeam.Away, -45f);
                SimTestHelpers.Place(hard, 9, 0f, 10f);
                SimTestHelpers.SetBall(hard, new SimVec3(0f, 0.11f, 9.0f), new SimVec3(0f, 0f, 30f));
                SimTestHelpers.StepUntil(hard, SimTestHelpers.IdleIntents(), MatchEventType.FirstTouch, 10, out var e);
                if (e.Value < 0.6f && hard.State.Ball.Velocity.Y > 0.1f) popped = true;
            }

            Assert.IsTrue(popped, "a heavy touch on a fast ball pops it up off the foot");
        }

        [Test]
        public void Deflection_GlancingBodyContact_SpinsTheBall()
        {
            var sim = SimTestHelpers.NewSim(41);
            SimTestHelpers.ForceOpenPlay(sim);
            SimTestHelpers.ParkTeam(sim, SimTeam.Home, -45f);
            SimTestHelpers.ParkTeam(sim, SimTeam.Away, 45f);
            SimTestHelpers.Place(sim, 11, 0f, HL - 1f);
            SimTestHelpers.Place(sim, 16, 0f, 20f);
            sim.State.Players[16].ControlBlockedUntilTick = 100000;
            sim.State.Ball = new SimBall
            {
                Position = new SimVec3(0.3f, 1.0f, 12f), Velocity = new SimVec3(0f, 0.6f, 16f), Spin = SimVec3.Zero,
            };
            sim.State.LastTouchPlayer = 5;
            sim.State.LastTouchTeam = SimTeam.Home;
            Assert.IsTrue(SimTestHelpers.StepUntil(sim, SimTestHelpers.IdleIntents(), MatchEventType.Deflection, 60, out var e));
            Assert.AreEqual(16, e.PlayerId);
            Assert.Greater(sim.State.Ball.Spin.Magnitude, 5f, "a glancing contact spins the ball");
        }
    }
}
