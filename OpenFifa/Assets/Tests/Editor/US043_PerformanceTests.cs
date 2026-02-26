using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-043")]
    [Category("Performance")]
    public class US043_PerformanceTests
    {
        [Test]
        public void PerformanceBudget_MinAvgFPS_Is28()
        {
            var budget = new PerformanceBudget();
            Assert.AreEqual(28f, budget.MinAverageFPS);
        }

        [Test]
        public void PerformanceBudget_MaxFrameTime_Is100ms()
        {
            var budget = new PerformanceBudget();
            Assert.AreEqual(100f, budget.MaxFrameTimeMs);
        }

        [Test]
        public void PerformanceBudget_MaxBatches_Under100()
        {
            var budget = new PerformanceBudget();
            Assert.Less(budget.MaxBatches, 100);
        }

        [Test]
        public void PerformanceBudget_GCAlloc_Zero()
        {
            var budget = new PerformanceBudget();
            Assert.AreEqual(0, budget.MaxGCAllocBytes);
        }

        [Test]
        public void PerformanceBudget_95thPercentile_Under33ms()
        {
            var budget = new PerformanceBudget();
            Assert.Less(budget.Max95thPercentileMs, 33f);
        }

        [Test]
        public void PerformanceBudget_MeasurementWindow_5Seconds()
        {
            var budget = new PerformanceBudget();
            Assert.AreEqual(5f, budget.MeasurementWindowSeconds);
        }

        [Test]
        public void FrameTimeAnalyzer_MultipleSamples_CalculatesAverage()
        {
            var analyzer = new FrameTimeAnalyzer();
            analyzer.AddSample(16.6f);
            analyzer.AddSample(17.0f);
            analyzer.AddSample(16.0f);
            float avg = analyzer.GetAverageMs();
            Assert.AreEqual(16.53f, avg, 0.1f);
        }

        [Test]
        public void FrameTimeAnalyzer_MultipleSamples_CalculatesPercentile()
        {
            var analyzer = new FrameTimeAnalyzer();
            for (int i = 0; i < 100; i++)
            {
                analyzer.AddSample(i < 95 ? 16f : 40f);
            }
            float p95 = analyzer.GetPercentileMs(95);
            Assert.GreaterOrEqual(p95, 16f);
        }
    }
}
