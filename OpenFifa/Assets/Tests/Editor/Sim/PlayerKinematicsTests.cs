using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Sim
{
    [TestFixture]
    [Category("FID-SIM")]
    public class PlayerKinematicsTests
    {
        private static readonly PlayerKinematicsConfig Cfg = new PlayerKinematicsConfig();
        private const float Dt = 1f / 60f;

        private static SimPlayer Fresh() => new SimPlayer { Stamina = 1f };

        private static float TimeToSpeed(SimPlayerAttributes a, float speed, int maxTicks = 600)
        {
            var p = Fresh();
            for (int t = 1; t <= maxTicks; t++)
            {
                PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(0f, 0f, 1f), true, float.MaxValue, SimVec3.Zero, Dt);
                if (p.Speed >= speed) return t * Dt;
            }

            return float.MaxValue;
        }

        [Test]
        public void Step_SprintFromRest_ReachesTopSpeedWithin6Seconds()
        {
            var a = new SimPlayerAttributes();
            float top = PlayerKinematics.SprintSpeed(a, Cfg, 1f);
            Assert.Less(TimeToSpeed(a, top * 0.99f), 6f);
            Assert.That(top, Is.InRange(7.5f, 10f));
        }

        [Test]
        public void SprintSpeed_HigherPace_IsFaster()
        {
            var slow = new SimPlayerAttributes { Pace = 50 };
            var fast = new SimPlayerAttributes { Pace = 95 };
            Assert.Greater(PlayerKinematics.SprintSpeed(fast, Cfg, 1f), PlayerKinematics.SprintSpeed(slow, Cfg, 1f) + 1f);
        }

        [Test]
        public void Step_ExplosiveVsLengthy_ExplosiveFirstToFiveMetresPerSecond()
        {
            var ex = new SimPlayerAttributes { AccelerationTypeOverride = SimAccelerationType.Explosive };
            var le = new SimPlayerAttributes { AccelerationTypeOverride = SimAccelerationType.Lengthy };
            Assert.Less(TimeToSpeed(ex, 5f), TimeToSpeed(le, 5f) - 0.2f);
        }

        [Test]
        public void AccelerationCurve_Lengthy_StrongerThanExplosiveNearTopSpeed()
        {
            Assert.Greater(PlayerKinematics.AccelerationCurve(SimAccelerationType.Lengthy, 0.95f),
                PlayerKinematics.AccelerationCurve(SimAccelerationType.Explosive, 0.95f));
            Assert.Greater(PlayerKinematics.AccelerationCurve(SimAccelerationType.Explosive, 0f),
                PlayerKinematics.AccelerationCurve(SimAccelerationType.Controlled, 0f));
        }

        [Test]
        public void DeriveAccelerationType_ShortAgileVsTallStrong_ExplosiveAndLengthy()
        {
            var small = new SimPlayerAttributes { Acceleration = 90, Agility = 90, Strength = 55, Height = 1.70f };
            var big = new SimPlayerAttributes { Acceleration = 70, Agility = 60, Strength = 85, Height = 1.92f };
            Assert.AreEqual(SimAccelerationType.Explosive, small.AccelerationType);
            Assert.AreEqual(SimAccelerationType.Lengthy, big.AccelerationType);
            Assert.AreEqual(SimAccelerationType.Controlled, new SimPlayerAttributes().AccelerationType);
        }

        [Test]
        public void Step_ReverseAtSprint_SlowsDownBeforeTurning()
        {
            var a = new SimPlayerAttributes();
            var p = Fresh();
            for (int t = 0; t < 300; t++) PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(0f, 0f, 1f), true, float.MaxValue, SimVec3.Zero, Dt);
            float before = p.Speed;
            float minSpeed = before;
            float startZ = p.Position.Z;
            float maxZ = startZ;
            for (int t = 0; t < 120; t++)
            {
                PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(0f, 0f, -1f), true, float.MaxValue, SimVec3.Zero, Dt);
                if (p.Speed < minSpeed) minSpeed = p.Speed;
                if (p.Position.Z > maxZ) maxZ = p.Position.Z;
            }

            Assert.Less(minSpeed, before * 0.4f, "a sprinting player must plant and shed speed to reverse");
            Assert.Greater(maxZ, startZ + 1f, "momentum carries the player on before the turn");
            Assert.Less(p.Velocity.Z, 0f, "and he is heading back after two seconds");
        }

        [Test]
        public void TurnRate_AtSprint_LowerThanAtWalk()
        {
            var a = new SimPlayerAttributes();
            Assert.Less(PlayerKinematics.TurnRate(a, Cfg, 9f), PlayerKinematics.TurnRate(a, Cfg, 1f) * 0.5f);
        }

        [Test]
        public void TurnRate_HighAgility_TurnsFaster()
        {
            Assert.Greater(PlayerKinematics.TurnRate(new SimPlayerAttributes { Agility = 95 }, Cfg, 5f),
                PlayerKinematics.TurnRate(new SimPlayerAttributes { Agility = 40 }, Cfg, 5f));
        }

        [Test]
        public void Step_LongSprint_DrainsStaminaThenWalkingRecovers()
        {
            var a = new SimPlayerAttributes();
            var p = Fresh();
            for (int t = 0; t < 60 * 20; t++) PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(0f, 0f, 1f), true, float.MaxValue, SimVec3.Zero, Dt);
            float drained = p.Stamina;
            Assert.Less(drained, 0.95f);
            for (int t = 0; t < 60 * 20; t++) PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(0f, 0f, 0.3f), false, float.MaxValue, SimVec3.Zero, Dt);
            Assert.Greater(p.Stamina, drained);
        }

        [Test]
        public void Step_SpeedCap_IsRespected()
        {
            var a = new SimPlayerAttributes { Pace = 99 };
            var p = Fresh();
            for (int t = 0; t < 600; t++) PlayerKinematics.Step(ref p, a, Cfg, new SimVec3(1f, 0f, 0f), true, 3.6f, SimVec3.Zero, Dt);
            Assert.LessOrEqual(p.Speed, 3.6f + 1e-4f);
        }
    }
}
