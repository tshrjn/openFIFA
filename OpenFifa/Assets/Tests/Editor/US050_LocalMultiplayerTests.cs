using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-050")]
    [Category("Input")]
    public class US050_LocalMultiplayerTests
    {
        [Test]
        public void SplitTouchZone_Player1_LeftHalf()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            Assert.IsTrue(zone.IsPlayer1Zone(200, 500));
            Assert.IsTrue(zone.IsPlayer1Zone(539, 1000));
        }

        [Test]
        public void SplitTouchZone_Player2_RightHalf()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            Assert.IsTrue(zone.IsPlayer2Zone(540, 500));
            Assert.IsTrue(zone.IsPlayer2Zone(1000, 1000));
        }

        [Test]
        public void SplitTouchZone_Boundary_Player2()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            // Exactly at midpoint belongs to Player 2
            Assert.IsTrue(zone.IsPlayer2Zone(540, 500));
            Assert.IsFalse(zone.IsPlayer1Zone(540, 500));
        }

        [Test]
        public void SplitTouchZone_NoCrossTalk()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            // A point in P1 zone should not be P2
            Assert.IsTrue(zone.IsPlayer1Zone(100, 100));
            Assert.IsFalse(zone.IsPlayer2Zone(100, 100));
            // A point in P2 zone should not be P1
            Assert.IsTrue(zone.IsPlayer2Zone(800, 100));
            Assert.IsFalse(zone.IsPlayer1Zone(800, 100));
        }

        [Test]
        public void LocalMultiplayerConfig_TwoPlayers()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(2, config.HumanPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_AIFillsRemaining()
        {
            var config = new LocalMultiplayerConfig();
            // 5v5 = 10 players total, 2 human, 8 AI
            Assert.AreEqual(8, config.AIPlayerCount);
            Assert.AreEqual(10, config.TotalPlayerCount);
        }

        [Test]
        public void LocalMultiplayerConfig_Player1ControlsTeamA()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(0, config.Player1TeamIndex);
        }

        [Test]
        public void LocalMultiplayerConfig_Player2ControlsTeamB()
        {
            var config = new LocalMultiplayerConfig();
            Assert.AreEqual(1, config.Player2TeamIndex);
        }

        [Test]
        public void InputRouter_RoutesP1Touch_ToPlayer1()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            var router = new InputRouter(zone);

            router.ProcessTouch(0, 200, 500);
            Assert.AreEqual(0, router.GetOwningPlayer(0));
        }

        [Test]
        public void InputRouter_RoutesP2Touch_ToPlayer2()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            var router = new InputRouter(zone);

            router.ProcessTouch(1, 800, 500);
            Assert.AreEqual(1, router.GetOwningPlayer(1));
        }

        [Test]
        public void InputRouter_SimultaneousTouches_Independent()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            var router = new InputRouter(zone);

            router.ProcessTouch(0, 200, 500);  // P1
            router.ProcessTouch(1, 800, 500);  // P2

            Assert.AreEqual(0, router.GetOwningPlayer(0));
            Assert.AreEqual(1, router.GetOwningPlayer(1));
        }

        [Test]
        public void InputRouter_ReleasedTouch_ReturnsNegOne()
        {
            var zone = new SplitTouchZoneLogic(1080, 1920);
            var router = new InputRouter(zone);

            router.ProcessTouch(0, 200, 500);
            router.ReleaseTouch(0);
            Assert.AreEqual(-1, router.GetOwningPlayer(0));
        }

        [Test]
        public void ActionButtonLayout_P1_CenterLeft()
        {
            var layout = new ActionButtonLayout(1080, 1920);
            float p1X = layout.GetActionButtonCenterX(0);
            // P1 buttons should be in center-left area (around 25% of screen width)
            Assert.Greater(p1X, 100f);
            Assert.Less(p1X, 540f);
        }

        [Test]
        public void ActionButtonLayout_P2_CenterRight()
        {
            var layout = new ActionButtonLayout(1080, 1920);
            float p2X = layout.GetActionButtonCenterX(1);
            // P2 buttons should be in center-right area (around 75% of screen width)
            Assert.Greater(p2X, 540f);
            Assert.Less(p2X, 1080f);
        }
    }
}
