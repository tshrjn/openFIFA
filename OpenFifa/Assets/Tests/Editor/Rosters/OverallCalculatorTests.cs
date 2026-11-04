using System;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID-DATA")]
    public class OverallCalculatorTests
    {
        [Test]
        public void WeightsFor_EveryPosition_SumTo100()
        {
            foreach (RosterPosition pos in Enum.GetValues(typeof(RosterPosition)))
            {
                int sum = 0;
                foreach (var w in OverallCalculator.WeightsFor(pos)) sum += w.Value;
                Assert.AreEqual(100, sum, pos.ToString());
            }
        }

        [Test]
        public void OverallTenths_UniformAttributes_EqualsThatValue()
        {
            foreach (RosterPosition pos in Enum.GetValues(typeof(RosterPosition)))
            {
                Assert.AreEqual(10, OverallCalculator.OverallTenths(RosterTestData.Uniform(1), pos));
                Assert.AreEqual(130, OverallCalculator.OverallTenths(RosterTestData.Uniform(13), pos));
                Assert.AreEqual(200, OverallCalculator.OverallTenths(RosterTestData.Uniform(20), pos));
            }
        }

        [Test]
        public void OverallTenths_KeeperProfile_HighInGoalLowUpFront()
        {
            var a = RosterTestData.Uniform(1);
            a[(int)RosterAttribute.GkDiving] = 20;
            a[(int)RosterAttribute.GkHandling] = 20;
            a[(int)RosterAttribute.GkDistribution] = 20;
            a[(int)RosterAttribute.GkReflexes] = 20;
            a[(int)RosterAttribute.GkPositioning] = 20;
            // GK weights on gk* attributes total 88: (88*20 + 12*1) / 10 = 177.2 -> 177
            Assert.AreEqual(177, OverallCalculator.OverallTenths(a, RosterPosition.GK));
            Assert.AreEqual(10, OverallCalculator.OverallTenths(a, RosterPosition.ST));
        }

        [Test]
        public void OverallTenths_StrikerFinishing_WeighsTwentyPercent()
        {
            var a = RosterTestData.Uniform(10);
            a[(int)RosterAttribute.Finishing] = 20;
            // 100 + 20 * (20 - 10) / 10 = 120
            Assert.AreEqual(120, OverallCalculator.OverallTenths(a, RosterPosition.ST));
            Assert.AreEqual(100, OverallCalculator.OverallTenths(a, RosterPosition.CB));
        }

        [Test]
        public void OverallTenths_RoundsHalfUp()
        {
            var a = RosterTestData.Uniform(10);
            a[(int)RosterAttribute.Volleys] = 15; // not weighted for ST
            a[(int)RosterAttribute.Composure] = 11; // ST weight 8 -> sum 1008 -> 100.8 -> 101
            Assert.AreEqual(101, OverallCalculator.OverallTenths(a, RosterPosition.ST));
        }

        [Test]
        public void SharedFamilies_SameFormula()
        {
            var a = RosterTestData.Uniform(9);
            a[(int)RosterAttribute.Crossing] = 18;
            Assert.AreEqual(OverallCalculator.OverallTenths(a, RosterPosition.RB), OverallCalculator.OverallTenths(a, RosterPosition.LWB));
            Assert.AreEqual(OverallCalculator.OverallTenths(a, RosterPosition.RW), OverallCalculator.OverallTenths(a, RosterPosition.LW));
            Assert.AreEqual(OverallCalculator.OverallTenths(a, RosterPosition.RM), OverallCalculator.OverallTenths(a, RosterPosition.LM));
        }

        [Test]
        public void GroupTenths_IsMeanOfGroup()
        {
            var a = RosterTestData.Uniform(10);
            a[(int)RosterAttribute.Acceleration] = 15;
            a[(int)RosterAttribute.TopSpeed] = 16;
            Assert.AreEqual(155, OverallCalculator.GroupTenths(a, AttributeGroup.Pace));
            Assert.AreEqual(100, OverallCalculator.GroupTenths(a, AttributeGroup.Physical));
        }

        [Test]
        public void Format_Tenths_OneDecimal()
        {
            Assert.AreEqual("15.4", OverallCalculator.Format(154));
            Assert.AreEqual("20.0", OverallCalculator.Format(200));
        }

        [Test]
        public void Schema_AttributeTables_Consistent()
        {
            Assert.AreEqual(Enum.GetValues(typeof(RosterAttribute)).Length, RosterSchema.AttributeCount);
            Assert.AreEqual(34, RosterSchema.AttributeCount);
            Assert.IsTrue(RosterSchema.TryParseAttribute("offTheBall", out var a));
            Assert.AreEqual(RosterAttribute.OffTheBall, a);
            Assert.AreEqual(AttributeGroup.Goalkeeping, RosterSchema.GroupOf(RosterAttribute.GkPositioning));
            Assert.IsFalse(RosterSchema.TryParsePosition("3", out _));
            Assert.AreEqual("dele_okonkwo_hart", RosterSchema.Slug("Dele Okonkwo-Hart"));
        }
    }
}
