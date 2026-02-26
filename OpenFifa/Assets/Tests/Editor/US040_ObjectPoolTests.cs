using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-040")]
    public class US040_ObjectPoolTests
    {
        [Test]
        public void ObjectPoolLogic_Get_ReturnsIndex()
        {
            var pool = new ObjectPoolLogic(10);
            int index = pool.Get();
            Assert.GreaterOrEqual(index, 0);
            Assert.Less(index, 10);
        }

        [Test]
        public void ObjectPoolLogic_Get_ReturnsUniqueIndices()
        {
            var pool = new ObjectPoolLogic(10);
            int a = pool.Get();
            int b = pool.Get();
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void ObjectPoolLogic_Return_MakesIndexAvailable()
        {
            var pool = new ObjectPoolLogic(2);
            int a = pool.Get();
            int b = pool.Get();

            pool.Return(a);
            int c = pool.Get();
            Assert.AreEqual(a, c, "Returned index should be reused");
        }

        [Test]
        public void ObjectPoolLogic_ExhaustedPool_ReturnsNegativeOne()
        {
            var pool = new ObjectPoolLogic(2);
            pool.Get();
            pool.Get();
            int overflow = pool.Get();
            Assert.AreEqual(-1, overflow, "Exhausted pool should return -1");
        }

        [Test]
        public void ObjectPoolLogic_PreWarm_AllAvailable()
        {
            var pool = new ObjectPoolLogic(5);
            Assert.AreEqual(5, pool.AvailableCount);
        }

        [Test]
        public void ObjectPoolLogic_ActiveCount_TracksUsage()
        {
            var pool = new ObjectPoolLogic(5);
            Assert.AreEqual(0, pool.ActiveCount);

            pool.Get();
            Assert.AreEqual(1, pool.ActiveCount);

            pool.Get();
            Assert.AreEqual(2, pool.ActiveCount);
        }

        [Test]
        public void StringBuilderCache_FormatTimer_NoAllocation()
        {
            var cache = new StringBuilderCache();
            string result1 = cache.FormatTimer(90.5f);
            string result2 = cache.FormatTimer(89.0f);
            Assert.AreEqual("01:30", result1);
            Assert.AreEqual("01:29", result2);
        }

        [Test]
        public void StringBuilderCache_FormatScore_NoAllocation()
        {
            var cache = new StringBuilderCache();
            string result = cache.FormatScore("Lions", 3, "Tigers", 1);
            Assert.AreEqual("Lions 3 - 1 Tigers", result);
        }
    }
}
