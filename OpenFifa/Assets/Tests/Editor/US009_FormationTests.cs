using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US009")]
    public class US009_FormationTests
    {
        [Test]
        public void PositionRole_EnumValues_HasAllExpectedValues()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(PositionRole), PositionRole.Goalkeeper));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PositionRole), PositionRole.Defender));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PositionRole), PositionRole.Midfielder));
            Assert.IsTrue(System.Enum.IsDefined(typeof(PositionRole), PositionRole.Forward));
        }

        [Test]
        public void FormationSlotData_WhenConstructed_StoresRoleAndOffset()
        {
            var slot = new FormationSlotData(PositionRole.Goalkeeper, 0f, 0f, -12f);
            Assert.AreEqual(PositionRole.Goalkeeper, slot.Role,
                "Slot role should be Goalkeeper");
            Assert.AreEqual(0f, slot.OffsetX, 0.01f, "Offset X should be 0");
            Assert.AreEqual(0f, slot.OffsetY, 0.01f, "Offset Y should be 0");
            Assert.AreEqual(-12f, slot.OffsetZ, 0.01f, "Offset Z should be -12");
        }

        [Test]
        public void FormationLayoutData_Default212_Has6Positions()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var positions = formation.GetSlots();
            Assert.AreEqual(6, positions.Length,
                $"2-1-2 formation (GK + 2D + 1M + 2F) should have 6 positions but had {positions.Length}");
        }

        [Test]
        public void FormationLayoutData_Default212_HasGoalkeeper()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var slots = formation.GetSlots();
            bool hasGK = false;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Goalkeeper)
                {
                    hasGK = true;
                    break;
                }
            }
            Assert.IsTrue(hasGK, "2-1-2 formation should have a Goalkeeper");
        }

        [Test]
        public void FormationLayoutData_Default212_HasDefenders()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var slots = formation.GetSlots();
            int defenderCount = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Defender) defenderCount++;
            }
            Assert.AreEqual(2, defenderCount,
                $"2-1-2 formation should have 2 defenders but had {defenderCount}");
        }

        [Test]
        public void FormationLayoutData_Default212_HasMidfielder()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var slots = formation.GetSlots();
            int midCount = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Midfielder) midCount++;
            }
            Assert.AreEqual(1, midCount,
                $"2-1-2 formation should have 1 midfielder but had {midCount}");
        }

        [Test]
        public void FormationLayoutData_Default212_HasForwards()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var slots = formation.GetSlots();
            int fwdCount = 0;
            foreach (var slot in slots)
            {
                if (slot.Role == PositionRole.Forward) fwdCount++;
            }
            Assert.AreEqual(2, fwdCount,
                $"2-1-2 formation should have 2 forwards but had {fwdCount}");
        }

        [Test]
        public void FormationLayoutData_GetPositions_ReturnsLength6()
        {
            var formation = FormationLayoutData.CreateDefault212();
            float pitchLength = 50f;
            var positions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: pitchLength);
            Assert.AreEqual(6, positions.Length,
                $"GetWorldPositions should return 6 positions but returned {positions.Length}");
        }

        [Test]
        public void FormationLayoutData_HomeTeamPositions_AreOnNegativeXHalf()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var positions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: 50f);
            // Goalkeeper should be on the negative X side (home team defends negative X)
            var gkSlots = formation.GetSlots();
            for (int i = 0; i < gkSlots.Length; i++)
            {
                if (gkSlots[i].Role == PositionRole.Goalkeeper)
                {
                    Assert.Less(positions[i].x, 0f,
                        $"Home team GK should be on negative X side but was at x={positions[i].x}");
                }
            }
        }

        [Test]
        public void FormationLayoutData_AwayTeamPositions_AreMirroredOnPositiveXHalf()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var homePositions = formation.GetWorldPositions(isHomeTeam: true, pitchLength: 50f);
            var awayPositions = formation.GetWorldPositions(isHomeTeam: false, pitchLength: 50f);

            // Away positions should be mirrored (negated X)
            for (int i = 0; i < homePositions.Length; i++)
            {
                Assert.AreEqual(-homePositions[i].x, awayPositions[i].x, 0.01f,
                    $"Away position[{i}].x should be mirror of home. " +
                    $"Home: {homePositions[i].x}, Away: {awayPositions[i].x}");
            }
        }

        [Test]
        public void FormationLayoutData_AwayTeamGK_IsOnPositiveXSide()
        {
            var formation = FormationLayoutData.CreateDefault212();
            var positions = formation.GetWorldPositions(isHomeTeam: false, pitchLength: 50f);
            var slots = formation.GetSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Role == PositionRole.Goalkeeper)
                {
                    Assert.Greater(positions[i].x, 0f,
                        $"Away team GK should be on positive X side but was at x={positions[i].x}");
                }
            }
        }
    }
}
