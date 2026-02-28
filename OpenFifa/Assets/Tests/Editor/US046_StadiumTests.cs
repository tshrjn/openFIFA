using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US046")]
    [Category("Art")]
    public class US046_StadiumTests
    {
        [Test]
        public void StadiumConfig_SkyboxSettings_HasSkyboxHDRI()
        {
            var config = new StadiumConfig();
            Assert.IsNotNull(config.SkyboxHDRIName);
            Assert.AreEqual("kloppenheim_stadium", config.SkyboxHDRIName);
        }

        [Test]
        public void StadiumConfig_SkyboxShader_IsPanoramic()
        {
            var config = new StadiumConfig();
            Assert.AreEqual("Skybox/Panoramic", config.SkyboxShaderName);
        }

        [Test]
        public void StadiumConfig_PitchTexture_HasGrassBands()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.UsePitchGrassBands);
            Assert.Greater(config.GrassBandCount, 0);
        }

        [Test]
        public void GoalPostConfig_Dimensions_MatchPitch()
        {
            var config = new StadiumConfig();
            // Goal width from PitchConfig is typically ~3.66m (12ft)
            Assert.Greater(config.GoalPostWidth, 3f);
            Assert.Less(config.GoalPostWidth, 5f);
            Assert.Greater(config.GoalPostHeight, 2f);
            Assert.Less(config.GoalPostHeight, 3f);
        }

        [Test]
        public void GoalPostConfig_PostRadius_Reasonable()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.PostRadius, 0.03f);
            Assert.Less(config.PostRadius, 0.15f);
        }

        [Test]
        public void GoalNetConfig_NetAlpha_HasSemiTransparentMaterial()
        {
            var config = new StadiumConfig();
            Assert.Greater(config.NetAlpha, 0f);
            Assert.Less(config.NetAlpha, 1f);
        }

        [Test]
        public void GoalPostConfig_ColliderSettings_HasMeshCollider()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.PostsHaveMeshCollider);
            Assert.IsTrue(config.PostColliderConvex);
        }

        [Test]
        public void StadiumConfig_Geometry_HasStandsGeometry()
        {
            var config = new StadiumConfig();
            Assert.IsTrue(config.HasStandsGeometry);
            Assert.Greater(config.StandsSections, 0);
        }

        [Test]
        public void StadiumConfig_ScreenshotResolution_1920x1080()
        {
            var config = new StadiumConfig();
            Assert.AreEqual(1920, config.BaselineScreenshotWidth);
            Assert.AreEqual(1080, config.BaselineScreenshotHeight);
        }
    }
}
