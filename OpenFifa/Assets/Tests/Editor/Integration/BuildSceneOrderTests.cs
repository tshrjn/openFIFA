using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor.Integration
{
    /// <summary>Player builds boot into the 11v11 flow: SinglePlayerMatch, then LocalMultiplayerMatch, then the rest.</summary>
    [TestFixture]
    [Category("FID_INTEGRATION")]
    public class BuildSceneOrderTests
    {
        [Test]
        public void For_CommittedSettings_PutsSinglePlayerThenLocalMultiplayerBeforeMatch()
        {
            var scenes = BuildSceneOrder.For(new[] { "Assets/Scenes/Match.unity" });
            CollectionAssert.AreEqual(new[]
            {
                "Assets/Scenes/SinglePlayerMatch.unity", "Assets/Scenes/LocalMultiplayerMatch.unity", "Assets/Scenes/Match.unity",
            }, scenes);
        }

        [Test]
        public void For_ScenesAlreadyListed_AreNotDuplicated_AndTheRestKeepTheirOrder()
        {
            var scenes = BuildSceneOrder.For(new[]
            {
                "Assets/Scenes/B.unity", BuildSceneOrder.LocalMultiplayerScene, "Assets/Scenes/A.unity", BuildSceneOrder.SinglePlayerScene,
            });
            CollectionAssert.AreEqual(new[]
            {
                BuildSceneOrder.SinglePlayerScene, BuildSceneOrder.LocalMultiplayerScene, "Assets/Scenes/B.unity", "Assets/Scenes/A.unity",
            }, scenes);
        }

        [Test]
        public void For_MissingLeadingScene_IsSkipped()
        {
            var scenes = BuildSceneOrder.For(new[] { "Assets/Scenes/Match.unity" }, p => p != BuildSceneOrder.LocalMultiplayerScene);
            CollectionAssert.AreEqual(new[] { BuildSceneOrder.SinglePlayerScene, "Assets/Scenes/Match.unity" }, scenes);
            Assert.AreEqual(0, BuildSceneOrder.For(null, p => false).Length);
        }

        [Test]
        public void LeadingScenes_ExistInTheProject()
        {
            // Tests run from the project root (OpenFifa/).
            foreach (var s in BuildSceneOrder.Leading)
                Assert.IsTrue(System.IO.File.Exists(s), s + " is missing");
        }
    
        [Test]
        public void LocalMultiplayerSceneName_IsTheLoadableNameOfTheSecondBuildScene()
        {
            Assert.AreEqual("LocalMultiplayerMatch", BuildSceneOrder.LocalMultiplayerSceneName);
            Assert.AreEqual(BuildSceneOrder.LocalMultiplayerScene, BuildSceneOrder.Leading[1]);
            Assert.AreEqual("Match", BuildSceneOrder.SceneName("Assets\\Scenes\\Match.unity"));
            Assert.AreEqual("Plain", BuildSceneOrder.SceneName("Plain"));
            Assert.AreEqual(string.Empty, BuildSceneOrder.SceneName(null));
        }
}
}
