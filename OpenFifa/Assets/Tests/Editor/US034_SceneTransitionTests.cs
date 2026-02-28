using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US034")]
    public class US034_SceneTransitionTests
    {
        [Test]
        public void TransitionLogic_InitialState_NotTransitioning()
        {
            var logic = new SceneTransitionLogic(0.5f);
            Assert.IsFalse(logic.IsTransitioning);
        }

        [Test]
        public void TransitionLogic_StartTransition_SetsTransitioning()
        {
            var logic = new SceneTransitionLogic(0.5f);
            bool started = logic.StartTransition("Match");
            Assert.IsTrue(started);
            Assert.IsTrue(logic.IsTransitioning);
        }

        [Test]
        public void TransitionLogic_DoubleTransition_Blocked()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("Match");
            bool second = logic.StartTransition("MainMenu");
            Assert.IsFalse(second, "Should block double transition");
        }

        [Test]
        public void TransitionLogic_FadeIn_AlphaIncreasesOverTime()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("Match");

            logic.UpdateFadeIn(0.25f);
            Assert.Greater(logic.CurrentAlpha, 0f);
            Assert.Less(logic.CurrentAlpha, 1f);
        }

        [Test]
        public void TransitionLogic_FadeIn_CompletesAtDuration()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("Match");

            logic.UpdateFadeIn(0.5f);
            Assert.AreEqual(1f, logic.CurrentAlpha, 0.01f);
            Assert.IsTrue(logic.FadeInComplete);
        }

        [Test]
        public void TransitionLogic_FadeOut_AlphaDecreases()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("Match");
            logic.UpdateFadeIn(0.5f);
            logic.StartFadeOut();

            logic.UpdateFadeOut(0.25f);
            Assert.Less(logic.CurrentAlpha, 1f);
        }

        [Test]
        public void TransitionLogic_FadeOut_CompletesAtDuration()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("Match");
            logic.UpdateFadeIn(0.5f);
            logic.StartFadeOut();
            logic.UpdateFadeOut(0.5f);

            Assert.AreEqual(0f, logic.CurrentAlpha, 0.01f);
            Assert.IsFalse(logic.IsTransitioning);
        }

        [Test]
        public void TransitionLogic_TargetScene_Stored()
        {
            var logic = new SceneTransitionLogic(0.5f);
            logic.StartTransition("TeamSelect");
            Assert.AreEqual("TeamSelect", logic.TargetScene);
        }

        [Test]
        public void TransitionLogic_FadeDuration_Configurable()
        {
            var logic = new SceneTransitionLogic(0.8f);
            logic.StartTransition("Match");
            logic.UpdateFadeIn(0.4f);
            Assert.Less(logic.CurrentAlpha, 1f, "Should not complete at half duration");
        }
    }
}
