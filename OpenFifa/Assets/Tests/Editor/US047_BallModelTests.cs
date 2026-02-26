using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-047")]
    [Category("Art")]
    public class US047_BallModelTests
    {
        [Test]
        public void BallModelConfig_MaxTriangles_Under1000()
        {
            var config = new BallModelConfig();
            Assert.Less(config.MaxTriangles, 1000);
        }

        [Test]
        public void BallModelConfig_TextureSettings_HasAlbedoTexture()
        {
            var config = new BallModelConfig();
            Assert.IsNotNull(config.AlbedoTextureName);
            Assert.IsNotEmpty(config.AlbedoTextureName);
        }

        [Test]
        public void BallModelConfig_TextureSettings_HasNormalMap()
        {
            var config = new BallModelConfig();
            Assert.IsNotNull(config.NormalMapName);
            Assert.IsNotEmpty(config.NormalMapName);
        }

        [Test]
        public void BallModelConfig_Smoothness_InRange()
        {
            var config = new BallModelConfig();
            Assert.GreaterOrEqual(config.Smoothness, 0f);
            Assert.LessOrEqual(config.Smoothness, 1f);
            // Slight sheen for soccer ball
            Assert.GreaterOrEqual(config.Smoothness, 0.3f);
            Assert.LessOrEqual(config.Smoothness, 0.6f);
        }

        [Test]
        public void BallModelConfig_Shader_IsURPLit()
        {
            var config = new BallModelConfig();
            Assert.AreEqual("Universal Render Pipeline/Lit", config.ShaderName);
        }

        [Test]
        public void BallModelConfig_MeshOrigin_PivotAtCenter()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.PivotAtCenter);
        }

        [Test]
        public void BallModelConfig_ColliderRadius_Positive()
        {
            var config = new BallModelConfig();
            Assert.Greater(config.SphereColliderRadius, 0f);
            Assert.Less(config.SphereColliderRadius, 0.5f);
        }

        [Test]
        public void BallModelConfig_VisualRotation_FromRigidbody()
        {
            var config = new BallModelConfig();
            Assert.IsTrue(config.RotationFromAngularVelocity);
        }
    }
}
