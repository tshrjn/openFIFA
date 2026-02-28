using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US045")]
    [Category("Art")]
    public class US045_AnimationIntegrationTests
    {
        [Test]
        public void AnimationClipConfig_IdleClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.IsNotNull(config.GetClipName("Idle"));
            Assert.AreEqual("Soccer_Idle", config.GetClipName("Idle"));
        }

        [Test]
        public void AnimationClipConfig_RunClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Running", config.GetClipName("Run"));
        }

        [Test]
        public void AnimationClipConfig_SprintClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Sprinting", config.GetClipName("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_KickClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Soccer_Kick", config.GetClipName("Kick"));
        }

        [Test]
        public void AnimationClipConfig_TackleClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Slide_Tackle", config.GetClipName("Tackle"));
        }

        [Test]
        public void AnimationClipConfig_GKDiveClip_Exists()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Goalkeeper_Dive", config.GetClipName("GKDive"));
        }

        [Test]
        public void AnimationClipConfig_LocomotionClips_LoopEnabled()
        {
            var config = new AnimationClipConfig();
            Assert.IsTrue(config.ShouldLoop("Idle"));
            Assert.IsTrue(config.ShouldLoop("Run"));
            Assert.IsTrue(config.ShouldLoop("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_ActionClips_NoLoop()
        {
            var config = new AnimationClipConfig();
            Assert.IsFalse(config.ShouldLoop("Kick"));
            Assert.IsFalse(config.ShouldLoop("Tackle"));
            Assert.IsFalse(config.ShouldLoop("GKDive"));
        }

        [Test]
        public void AnimationClipConfig_RootMotion_DisabledForLocomotion()
        {
            var config = new AnimationClipConfig();
            Assert.IsFalse(config.UseRootMotion("Idle"));
            Assert.IsFalse(config.UseRootMotion("Run"));
            Assert.IsFalse(config.UseRootMotion("Sprint"));
        }

        [Test]
        public void AnimationClipConfig_RetargetSource_IsQuaternius()
        {
            var config = new AnimationClipConfig();
            Assert.AreEqual("Quaternius_Humanoid", config.RetargetSourceAvatar);
        }

        [Test]
        public void AnimationClipConfig_AllRequiredStates_HaveClips()
        {
            var config = new AnimationClipConfig();
            var required = new[] { "Idle", "Run", "Sprint", "Kick", "Tackle", "GKDive" };
            foreach (var state in required)
            {
                Assert.IsNotNull(config.GetClipName(state),
                    $"Missing clip mapping for state: {state}");
            }
        }
    }
}
