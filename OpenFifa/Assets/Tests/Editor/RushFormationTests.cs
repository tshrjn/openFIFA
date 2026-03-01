using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Rush")]
    public class RushFormationTests
    {
        [Test]
        public void Rush4v4_Has4Slots()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var slots = formation.GetSlots();
            Assert.AreEqual(4, slots.Length,
                $"Rush 4v4 formation should have 4 slots but had {slots.Length}");
        }

        [Test]
        public void Rush4v4_HasNoGoalkeeper()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var slots = formation.GetSlots();
            foreach (var slot in slots)
            {
                Assert.AreNotEqual(PositionRole.Goalkeeper, slot.Role,
                    "Rush 4v4 formation should not contain a Goalkeeper slot");
            }
        }

        [Test]
        public void Rush4v4_Has2Defenders()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var slots = formation.GetSlots();
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Defender) count++;
            }
            Assert.AreEqual(2, count,
                $"Rush 4v4 should have 2 defenders but had {count}");
        }

        [Test]
        public void Rush4v4_Has1Midfielder()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var slots = formation.GetSlots();
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Midfielder) count++;
            }
            Assert.AreEqual(1, count,
                $"Rush 4v4 should have 1 midfielder but had {count}");
        }

        [Test]
        public void Rush4v4_Has1Forward()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var slots = formation.GetSlots();
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Forward) count++;
            }
            Assert.AreEqual(1, count,
                $"Rush 4v4 should have 1 forward but had {count}");
        }

        [Test]
        public void Rush4v4_Name_IsRush4v4()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            Assert.AreEqual("Rush-4v4", formation.Name,
                $"Formation name should be 'Rush-4v4' but was '{formation.Name}'");
        }

        [Test]
        public void Rush4v4_GetWorldPositions_Returns4Positions()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var positions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: 50f);
            Assert.AreEqual(4, positions.Length,
                $"GetWorldPositions should return 4 positions but returned {positions.Length}");
        }

        [Test]
        public void Rush4v4_HomePositions_AreOnNegativeXHalf()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var positions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: 50f);
            foreach (var pos in positions)
            {
                Assert.Less(pos.x, 0f,
                    $"Home team player should be on negative X half but was at x={pos.x}");
            }
        }

        [Test]
        public void Rush4v4_AwayPositions_AreOnPositiveXHalf()
        {
            var formation = FormationLayoutData.CreateRush4v4();
            var positions = formation.GetWorldPositions(isHomeTeam: false, pitchLength: 50f);
            foreach (var pos in positions)
            {
                Assert.Greater(pos.x, 0f,
                    $"Away team player should be on positive X half but was at x={pos.x}");
            }
        }
    }
}
