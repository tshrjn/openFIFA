using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.TeamAI
{
    /// <summary>Review item 3: the AI's ball timing must match the sim's integrator.</summary>
    [TestFixture]
    [Category("FID-TEAMAI")]
    public class TeamAIBallTimingTests
    {
        private static readonly PitchSpec Free = new PitchSpec(10000f, 10000f, 7.32f, 2.44f, 16.5f, 40.32f);
        private const float Dt = 1f / 120f;

        /// <summary>Integrate a launch along +Z until it has moved <paramref name="dist"/> m flat (ground) or first lands (lofted).</summary>
        private static float IntegrateGround(BallSimConfig cfg, float speed, float dist)
        {
            var v = new SimVec3(0f, 0f, speed);
            var b = new SimBall { Position = new SimVec3(0f, cfg.Radius, 0f), Velocity = v, Grounded = true,
                Spin = BallIntegrator.RollingSpin(v, cfg.Radius, 0f) };
            for (int i = 1; i < 120 * 30; i++)
            {
                BallIntegrator.Step(ref b, cfg, Free, Dt);
                if (b.Position.Z >= dist) return i * Dt;
            }
            return float.PositiveInfinity;
        }

        private static float IntegrateFlight(BallSimConfig cfg, float speed, float elevDeg, float topSpin, out float carry)
        {
            float e = elevDeg * SimMath.Deg2Rad;
            var b = new SimBall
            {
                Position = new SimVec3(0f, cfg.Radius, 0f),
                Velocity = new SimVec3(0f, speed * (float)Math.Sin(e), speed * (float)Math.Cos(e)),
                Spin = new SimVec3(topSpin, 0f, 0f),
            };
            for (int i = 1; i < 2400; i++)
            {
                var f = BallIntegrator.Step(ref b, cfg, Free, Dt);
                if ((f & (BallStepFlags.Bounced | BallStepFlags.StartedRolling)) != 0) { carry = b.Position.Z; return i * Dt; }
            }
            carry = b.Position.Z;
            return float.PositiveInfinity;
        }

        [Test]
        public void GroundPassTime_10To40m_WithinTenPercentOfIntegrator()
        {
            var cfg = new BallSimConfig();
            var act = new SimActionConfig();
            var model = new TeamAIBallModel(cfg, act);
            for (float d = 10f; d <= 40f; d += 5f)
            {
                // The launch the sim's kick solver would use for the AI's pass of this length.
                float arrive = act.PassArrivalSpeedMin + act.PassArrivalSpeedRange * TeamAIBallModel.GroundPassPower(d);
                float v0 = Math.Min(BallFlight.GroundPassSpeedFor(cfg, d, arrive), act.PassMaxSpeed);
                float truth = IntegrateGround(cfg, v0, d);
                float ai = model.GroundPassTime(d);
                Assert.AreEqual(truth, ai, truth * 0.1f, $"ground pass {d} m");
                Assert.AreEqual(truth, TeamAIOnBall.GroundPassTime(d), truth * 0.1f, $"static ground pass {d} m");
            }
        }

        [Test]
        public void GroundPassTime_OldHeuristicWasOffByMoreThanTenPercent()
        {
            // Documents why item 3 mattered: v0 = 9 + 0.45 d, t = d / (0.8 v0) vs the integrator at 40 m.
            var cfg = new BallSimConfig();
            var act = new SimActionConfig();
            float d = 40f;
            float arrive = act.PassArrivalSpeedMin + act.PassArrivalSpeedRange * TeamAIBallModel.GroundPassPower(d);
            float truth = IntegrateGround(cfg, Math.Min(BallFlight.GroundPassSpeedFor(cfg, d, arrive), act.PassMaxSpeed), d);
            float old = d / (0.8f * Math.Min(Math.Max(9f + 0.45f * d, 10f), 25f));
            Assert.Greater(Math.Abs(old - truth) / truth, 0.1f);
        }

        [Test]
        public void LobPassTime_10To40m_WithinTenPercentOfIntegrator()
        {
            var cfg = new BallSimConfig();
            var act = new SimActionConfig();
            var model = new TeamAIBallModel(cfg, act);
            for (float d = 10f; d <= 40f; d += 5f)
            {
                float speed = KickSolver.LoftedSpeed(d, act.LobElevationDeg * SimMath.Deg2Rad, -8f, cfg);
                float truth = IntegrateFlight(cfg, speed, act.LobElevationDeg, -8f, out float carry);
                Assert.AreEqual(d, carry, 0.6f, "solver sanity");
                Assert.AreEqual(truth, model.LobPassTime(d), truth * 0.1f, $"lob {d} m");
                Assert.AreEqual(truth, TeamAIOnBall.LobPassTime(d), truth * 0.1f, $"static lob {d} m");
            }
        }

        [Test]
        public void CrossTime_10To40m_WithinTenPercentOfIntegrator()
        {
            var cfg = new BallSimConfig();
            var act = new SimActionConfig();
            var model = new TeamAIBallModel(cfg, act);
            for (float d = 10f; d <= 40f; d += 5f)
            {
                float speed = KickSolver.LoftedSpeed(d, act.CrossElevationDeg * SimMath.Deg2Rad, -5f, cfg);
                float truth = IntegrateFlight(cfg, speed, act.CrossElevationDeg, -5f, out _);
                Assert.AreEqual(truth, model.CrossTime(d), truth * 0.1f, $"cross {d} m");
            }
        }

        [Test]
        public void Trajectory_LoftedBall_MatchesIntegratorAndKnowsItIsAirborne()
        {
            var cfg = new BallSimConfig();
            var w = new TeamAIWorld(PitchSpec.Standard);
            w.Ball.Position = new SimVec3(0f, cfg.Radius, -20f);
            w.Ball.Velocity = new SimVec3(2f, 12f, 16f);
            w.Ball.Spin = new SimVec3(-8f, 0f, 0f);
            var traj = new TeamAIBallTrajectory();
            traj.Build(w, TeamAIBallModel.Default);
            var b = new SimBall { Position = w.Ball.Position, Velocity = w.Ball.Velocity, Spin = w.Ball.Spin };
            foreach (float t in new[] { 0.5f, 1f, 1.5f, 2.5f })
            {
                var truth = BallIntegrator.PredictBall(b, cfg, w.Pitch, t);
                var p = traj.At(t);
                Assert.AreEqual(truth.Position.X, p.X, 0.3f, $"x at {t}");
                Assert.AreEqual(truth.Position.Z, p.Z, 0.3f, $"z at {t}");
                Assert.AreEqual(truth.Position.Y, traj.HeightAt(t), 0.3f, $"height at {t}");
            }
            Assert.Greater(traj.HeightAt(1f), 3f, "a lofted ball is over head height mid-flight");
            // The old flat model treated a lofted ball as rolling and decayed it; the static helper now flies it.
            var flat = TeamAIMath.PredictBall(w.Ball.Position, w.Ball.Velocity, 1.5f);
            Assert.AreEqual(BallIntegrator.PredictBall(b, cfg, w.Pitch, 1.5f).Position.Z, flat.Z, 0.3f);
        }

        [Test]
        public void Trajectory_RollingBall_MatchesIntegrator()
        {
            var cfg = new BallSimConfig();
            var w = new TeamAIWorld(PitchSpec.Standard);
            var v = new SimVec3(3f, 0f, 14f);
            w.Ball.Position = new SimVec3(0f, cfg.Radius, -30f);
            w.Ball.Velocity = v;
            var traj = new TeamAIBallTrajectory();
            traj.Build(w, TeamAIBallModel.Default);
            var b = new SimBall { Position = w.Ball.Position, Velocity = v, Grounded = true,
                Spin = BallIntegrator.RollingSpin(v, cfg.Radius, 0f) };
            foreach (float t in new[] { 0.5f, 1f, 2f, 3.5f })
            {
                var truth = BallIntegrator.PredictBall(b, cfg, w.Pitch, t).Position;
                var p = traj.At(t);
                Assert.AreEqual(truth.X, p.X, 0.15f, $"x at {t}");
                Assert.AreEqual(truth.Z, p.Z, 0.15f, $"z at {t}");
            }
        }

        [Test]
        public void Brain_LoftedLooseBall_ChaserGoesToLandingNotUnderTheFlight()
        {
            // A lob over a home midfielder standing under its path; a home striker is near where it lands.
            var w = TeamAITestWorlds.Empty();
            var cfg = new BallSimConfig();
            float speed = KickSolver.LoftedSpeed(32f, 40f * SimMath.Deg2Rad, -8f, cfg);
            float e = 40f * SimMath.Deg2Rad;
            int under = TeamAITestWorlds.Add(w, 0, TeamAIRole.CM, 6, 0f, -12f);
            int target = TeamAITestWorlds.Add(w, 0, TeamAIRole.ST, 10, 1.5f, 11f);
            TeamAITestWorlds.Add(w, 0, TeamAIRole.GK, 0, 0f, -50f);
            TeamAITestWorlds.Add(w, 1, TeamAIRole.GK, 0, 0f, 50f);
            w.Ball.Position = new SimVec3(0f, cfg.Radius, -20f);
            w.Ball.Velocity = new SimVec3(0f, speed * (float)Math.Sin(e), speed * (float)Math.Cos(e));
            w.Ball.Spin = new SimVec3(-8f, 0f, 0f);
            w.Ball.OwnerId = -1;
            w.Ball.LastTouchTeam = 0;
            var dir = new TeamAIDirector(PitchSpec.Standard, new TeamAITactics(), new TeamAITactics(), 1f, 1f, 3);
            dir.SetPossession(0);
            dir.DecideNow(w, 0.1f);
            Assert.AreEqual(target, dir.Brains[0].ChaserIndex, "the player under the flight cannot reach it in the air");
            Assert.AreNotEqual(under, dir.Brains[0].ChaserIndex);
        }
    }
}
