using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-044")]
    [Category("Art")]
    public class US044_CharacterModelTests
    {
        [Test]
        public void CharacterModelConfig_MaxTriangles_Under2000()
        {
            var config = new CharacterModelConfig();
            Assert.Less(config.MaxTrianglesPerModel, 2000);
        }

        [Test]
        public void CharacterModelConfig_HasTeamAColor()
        {
            var config = new CharacterModelConfig();
            Assert.IsNotNull(config.TeamAColor);
            // Blue: R < 0.5, B > 0.5
            Assert.Less(config.TeamAColor.R, 0.5f);
            Assert.Greater(config.TeamAColor.B, 0.5f);
        }

        [Test]
        public void CharacterModelConfig_HasTeamBColor()
        {
            var config = new CharacterModelConfig();
            Assert.IsNotNull(config.TeamBColor);
            // Red: R > 0.5, B < 0.5
            Assert.Greater(config.TeamBColor.R, 0.5f);
            Assert.Less(config.TeamBColor.B, 0.5f);
        }

        [Test]
        public void CharacterModelConfig_AvatarRig_IsHumanoid()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual("Humanoid", config.AvatarRigType);
        }

        [Test]
        public void CharacterModelConfig_RequiredAnimations_AllPresent()
        {
            var config = new CharacterModelConfig();
            Assert.Contains("Idle", config.RequiredAnimationStates);
            Assert.Contains("Run", config.RequiredAnimationStates);
            Assert.Contains("Sprint", config.RequiredAnimationStates);
            Assert.Contains("Kick", config.RequiredAnimationStates);
            Assert.Contains("Tackle", config.RequiredAnimationStates);
            Assert.Contains("Celebrate", config.RequiredAnimationStates);
        }

        [Test]
        public void CharacterModelConfig_UsesMaterialPropertyBlock()
        {
            var config = new CharacterModelConfig();
            Assert.IsTrue(config.UseMaterialPropertyBlock);
        }

        [Test]
        public void CharacterModelConfig_ShaderProperty_IsBaseColor()
        {
            var config = new CharacterModelConfig();
            Assert.AreEqual("_BaseColor", config.TeamColorShaderProperty);
        }

        [Test]
        public void TeamColorAssigner_AssignsTeamA()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(0, config);
            Assert.AreEqual(config.TeamAColor.R, color.R);
            Assert.AreEqual(config.TeamAColor.G, color.G);
            Assert.AreEqual(config.TeamAColor.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_AssignsTeamB()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(1, config);
            Assert.AreEqual(config.TeamBColor.R, color.R);
            Assert.AreEqual(config.TeamBColor.G, color.G);
            Assert.AreEqual(config.TeamBColor.B, color.B);
        }

        [Test]
        public void TeamColorAssigner_InvalidTeam_ReturnsWhite()
        {
            var assigner = new TeamColorAssigner();
            var config = new CharacterModelConfig();
            var color = assigner.GetTeamColor(5, config);
            Assert.AreEqual(1f, color.R);
            Assert.AreEqual(1f, color.G);
            Assert.AreEqual(1f, color.B);
        }
    }
}
