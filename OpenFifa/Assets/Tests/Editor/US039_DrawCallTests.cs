using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-039")]
    public class US039_DrawCallTests
    {
        [Test]
        public void BatchingConfig_MaxBatches_IsUnder100()
        {
            var config = new BatchingConfig();
            Assert.Less(config.MaxBatchTarget, 100);
        }

        [Test]
        public void BatchingConfig_GPUInstancing_Enabled()
        {
            var config = new BatchingConfig();
            Assert.IsTrue(config.EnableGPUInstancing);
        }

        [Test]
        public void BatchingConfig_StaticBatching_Enabled()
        {
            var config = new BatchingConfig();
            Assert.IsTrue(config.EnableStaticBatching);
        }

        [Test]
        public void BatchingConfig_DynamicBatching_Enabled()
        {
            var config = new BatchingConfig();
            Assert.IsTrue(config.EnableDynamicBatching);
        }

        [Test]
        public void BatchingConfig_SharedMaterials_TwoTeams()
        {
            var config = new BatchingConfig();
            Assert.AreEqual(2, config.TeamMaterialCount, "Should have 2 shared materials (one per team)");
        }

        [Test]
        public void MaterialPropertyConfig_PropertyBlock_SupportsInstancing()
        {
            var config = new MaterialPropertyConfig();
            Assert.IsTrue(config.UsePropertyBlock,
                "Should use MaterialPropertyBlock for per-instance colors without breaking batching");
        }
    }
}
