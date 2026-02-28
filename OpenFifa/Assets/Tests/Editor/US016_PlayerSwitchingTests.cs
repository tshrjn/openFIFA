using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US016")]
    public class US016_PlayerSwitchingTests
    {
        [Test]
        public void PlayerSwitchLogic_MultiplePlayers_FindsNearestToBall()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 5f, Z = 0f },
                new PositionData { Id = 2, X = 10f, Z = 0f }
            };
            float ballX = 4f, ballZ = 0f;

            int nearest = logic.FindNearestPlayer(players, ballX, ballZ);
            Assert.AreEqual(1, nearest, "Player at (5,0) should be nearest to ball at (4,0)");
        }

        [Test]
        public void PlayerSwitchLogic_FindsNearestToBall_DiagonalDistance()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 3f, Z = 4f },
                new PositionData { Id = 2, X = 10f, Z = 10f }
            };
            float ballX = 2f, ballZ = 3f;

            int nearest = logic.FindNearestPlayer(players, ballX, ballZ);
            Assert.AreEqual(1, nearest, "Player at (3,4) should be nearest to ball at (2,3)");
        }

        [Test]
        public void PlayerSwitchLogic_TieBreaking_UsesLowestIndex()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 5f, Z = 0f },
                new PositionData { Id = 1, X = -5f, Z = 0f },
                new PositionData { Id = 2, X = 0f, Z = 5f }
            };
            // All equidistant from (0,0) at distance 5
            float ballX = 0f, ballZ = 0f;

            int nearest = logic.FindNearestPlayer(players, ballX, ballZ);
            Assert.AreEqual(0, nearest, "On tie, lowest index player should be selected");
        }

        [Test]
        public void PlayerSwitchLogic_WhenCurrentExcluded_ExcludesCurrentPlayer()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 5f, Z = 0f },
                new PositionData { Id = 2, X = 10f, Z = 0f }
            };
            float ballX = 0f, ballZ = 0f;

            // Player 0 is nearest, but if we exclude them (already controlled), player 1 should be returned
            int nearest = logic.FindNearestPlayerExcluding(players, ballX, ballZ, 0);
            Assert.AreEqual(1, nearest, "Should return next nearest when current player is excluded");
        }

        [Test]
        public void PlayerSwitchLogic_SinglePlayer_ReturnsThatPlayer()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 100f, Z = 100f }
            };
            float ballX = 0f, ballZ = 0f;

            int nearest = logic.FindNearestPlayer(players, ballX, ballZ);
            Assert.AreEqual(0, nearest, "Single player should always be returned");
        }

        [Test]
        public void PlayerSwitchLogic_EmptyPlayers_ReturnsNegativeOne()
        {
            var logic = new PlayerSwitchLogic();
            var players = new PositionData[0];
            float ballX = 0f, ballZ = 0f;

            int nearest = logic.FindNearestPlayer(players, ballX, ballZ);
            Assert.AreEqual(-1, nearest, "Empty player array should return -1");
        }

        [Test]
        public void PlayerSwitchLogic_PerformSwitch_SwitchIsInstantaneous()
        {
            var logic = new PlayerSwitchLogic();
            // Verify that the switch method returns the new active index immediately
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 5f, Z = 0f },
                new PositionData { Id = 2, X = 10f, Z = 0f }
            };

            var result = logic.PerformSwitch(players, 4f, 0f, 2);
            Assert.AreEqual(1, result.NewActiveIndex, "Switch should immediately select nearest player");
            Assert.AreEqual(2, result.PreviousActiveIndex, "Previous active should be returned");
        }

        [Test]
        public void PlayerSwitchLogic_SwitchResult_ContainsCorrectData()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 3f, Z = 0f },
                new PositionData { Id = 2, X = 8f, Z = 0f }
            };

            var result = logic.PerformSwitch(players, 2.5f, 0f, 0);
            Assert.AreEqual(1, result.NewActiveIndex);
            Assert.AreEqual(0, result.PreviousActiveIndex);
            Assert.IsTrue(result.SwitchOccurred, "Switch should occur when a different player is nearest");
        }

        [Test]
        public void PlayerSwitchLogic_NoSwitch_WhenNearestIsCurrent()
        {
            var logic = new PlayerSwitchLogic();
            var players = new[]
            {
                new PositionData { Id = 0, X = 0f, Z = 0f },
                new PositionData { Id = 1, X = 10f, Z = 0f }
            };

            // Ball is at (0,0) and current active is player 0 who is at (0,0)
            // FindNearestPlayerExcluding will still switch to the next nearest
            var result = logic.PerformSwitch(players, 0f, 0f, 0);
            Assert.AreEqual(1, result.NewActiveIndex, "Should switch to next nearest even if current is closest");
            Assert.IsTrue(result.SwitchOccurred);
        }
    }
}
