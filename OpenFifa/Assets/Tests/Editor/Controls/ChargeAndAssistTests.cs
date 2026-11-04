using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-CONTROLS")]
    public class ChargeAndAssistTests
    {
        [Test]
        public void PowerFromHold_ZeroHold_ReturnsMinPower()
        {
            Assert.AreEqual(0.1f, ChargeMeter.PowerFromHold(0f, 1f, 0.1f), 1e-6f);
        }

        [Test]
        public void PowerFromHold_FullOrLongerHold_ReturnsOne()
        {
            Assert.AreEqual(1f, ChargeMeter.PowerFromHold(1f, 1f, 0.1f), 1e-6f);
            Assert.AreEqual(1f, ChargeMeter.PowerFromHold(5f, 1f, 0.1f), 1e-6f);
        }

        [Test]
        public void PowerFromHold_HalfHold_FollowsEaseOutCurve()
        {
            // x = 0.5 -> eased 0.75 -> 0.1 + 0.9 * 0.75 = 0.775
            Assert.AreEqual(0.775f, ChargeMeter.PowerFromHold(0.5f, 1f, 0.1f), 1e-5f);
        }

        [Test]
        public void PowerFromHold_IsMonotonic()
        {
            float prev = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float p = ChargeMeter.PowerFromHold(i * 0.03f, 1f, 0.1f);
                Assert.GreaterOrEqual(p, prev);
                prev = p;
            }
        }

        [Test]
        public void KickBuffer_ConsumesOnlyWhenCanStrike()
        {
            var b = new ControlKickBuffer();
            b.Push(ControlAction.Pass, 0.5f, ControlVec2.Up, 1f);
            Assert.IsFalse(b.TryConsume(ControlContext.Attack(1.1f, canStrike: false), 0.5f, out _, out _, out _));
            Assert.IsTrue(b.HasPending);
            Assert.IsTrue(b.TryConsume(ControlContext.Attack(1.2f, canStrike: true), 0.5f, out var a, out var p, out _));
            Assert.AreEqual(ControlAction.Pass, a);
            Assert.AreEqual(0.5f, p, 1e-6f);
            Assert.IsFalse(b.HasPending);
        }

        [Test]
        public void PassDirection_FullAssist_SnapsToTeammateInCone()
        {
            var mates = new[]
            {
                new ControlPlayerInfo(1, new SimVec3(10f, 0f, 3f)),
                new ControlPlayerInfo(2, new SimVec3(-10f, 0f, 0f)),
            };
            SimVec3 dir = AimAssist.PassDirection(SimVec3.Zero, 0, new SimVec3(1f, 0f, 0f), mates, 1f, out int target);
            Assert.AreEqual(1, target);
            SimVec3 expected = new SimVec3(10f, 0f, 3f).Normalized;
            Assert.AreEqual(expected.X, dir.X, 1e-4f);
            Assert.AreEqual(expected.Z, dir.Z, 1e-4f);
        }

        [Test]
        public void PassDirection_Manual_KeepsStickDirection()
        {
            var mates = new[] { new ControlPlayerInfo(1, new SimVec3(10f, 0f, 3f)) };
            SimVec3 dir = AimAssist.PassDirection(SimVec3.Zero, 0, new SimVec3(1f, 0f, 0f), mates, 0f, out int target);
            // Teammate is ~16.7 degrees off: outside the 10 degree manual cone.
            Assert.AreEqual(-1, target);
            Assert.AreEqual(1f, dir.X, 1e-5f);
            Assert.AreEqual(0f, dir.Z, 1e-5f);
        }

        [Test]
        public void PassDirection_PrefersSmallerAngle()
        {
            var mates = new[]
            {
                new ControlPlayerInfo(1, new SimVec3(8f, 0f, 6f)),
                new ControlPlayerInfo(2, new SimVec3(20f, 0f, 1f)),
            };
            AimAssist.PassDirection(SimVec3.Zero, 0, new SimVec3(1f, 0f, 0f), mates, 1f, out int target);
            Assert.AreEqual(2, target);
        }

        [Test]
        public void ShotTarget_FullAssist_StaysInsidePosts()
        {
            var goal = new SimVec3(0f, 0f, 52.5f);
            SimVec3 target = AimAssist.ShotTarget(new SimVec3(10f, 0f, 35f), new SimVec3(1f, 0f, 0.2f), goal, 3.66f, 1f);
            Assert.LessOrEqual(System.Math.Abs(target.X), 3.66f);
            Assert.AreEqual(52.5f, target.Z, 1e-5f);
        }

        [Test]
        public void ShotTarget_Manual_FollowsStickAndCanMiss()
        {
            var goal = new SimVec3(0f, 0f, 52.5f);
            SimVec3 target = AimAssist.ShotTarget(new SimVec3(10f, 0f, 35f), new SimVec3(1f, 0f, 1f), goal, 3.66f, 0f);
            Assert.AreEqual(27.5f, target.X, 1e-3f);
        }

        [Test]
        public void PassPower_FullAssist_MatchesDistance()
        {
            Assert.AreEqual(0.5f, AimAssist.PassPower(1f, 25f, 1f), 1e-5f);
            Assert.AreEqual(1f, AimAssist.PassPower(1f, 25f, 0f), 1e-5f);
        }

        [Test]
        public void SmartSwitch_PicksPlayerNearestBallPath_NotNearestBall()
        {
            // Ball at origin rolling +X at 10 m/s. Player 1 is closest to the ball but behind it;
            // player 2 is farther away but standing in its path.
            var players = new[]
            {
                new ControlPlayerInfo(0, new SimVec3(-20f, 0f, 0f)),
                new ControlPlayerInfo(1, new SimVec3(-4f, 0f, 2f)),
                new ControlPlayerInfo(2, new SimVec3(12f, 0f, 1f)),
            };
            int pick = SmartSwitchSelector.SelectNearestToBallPath(players, SimVec3.Zero, new SimVec3(10f, 0f, 0f), currentId: 0);
            Assert.AreEqual(2, pick);
        }

        [Test]
        public void SmartSwitch_StationaryBall_PicksNearestExcludingCurrent()
        {
            var players = new[]
            {
                new ControlPlayerInfo(0, new SimVec3(1f, 0f, 0f)),
                new ControlPlayerInfo(1, new SimVec3(5f, 0f, 0f)),
                new ControlPlayerInfo(2, new SimVec3(9f, 0f, 0f)),
            };
            Assert.AreEqual(1, SmartSwitchSelector.SelectNearestToBallPath(players, SimVec3.Zero, SimVec3.Zero, currentId: 0));
        }

        [Test]
        public void DirectionalSwitch_PicksPlayerInFlickDirection()
        {
            var players = new[]
            {
                new ControlPlayerInfo(0, SimVec3.Zero),
                new ControlPlayerInfo(1, new SimVec3(-6f, 0f, 0f)),
                new ControlPlayerInfo(2, new SimVec3(0f, 0f, 15f)),
                new ControlPlayerInfo(3, new SimVec3(4f, 0f, 20f)),
            };
            Assert.AreEqual(2, SmartSwitchSelector.SelectDirectional(players, SimVec3.Zero, 0, new SimVec3(0f, 0f, 1f)));
            Assert.AreEqual(1, SmartSwitchSelector.SelectDirectional(players, SimVec3.Zero, 0, new SimVec3(-1f, 0f, 0f)));
            Assert.AreEqual(-1, SmartSwitchSelector.SelectDirectional(players, SimVec3.Zero, 0, new SimVec3(0f, 0f, -1f)));
        }

        [Test]
        public void ToDirection8_MapsCardinalAndDiagonal()
        {
            Assert.AreEqual(Direction8.Up, ControlVec2.ToDirection8(new ControlVec2(0f, 1f)));
            Assert.AreEqual(Direction8.DownLeft, ControlVec2.ToDirection8(new ControlVec2(-1f, -1f)));
            Assert.AreEqual(Direction8.None, ControlVec2.ToDirection8(ControlVec2.Zero));
        }
    }
}
