using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US-031")]
    public class US031_PauseMenuTests
    {
        [Test]
        public void PauseLogic_InitialState_NotPaused()
        {
            var logic = new PauseLogic();
            Assert.IsFalse(logic.IsPaused);
        }

        [Test]
        public void PauseLogic_Pause_SetsPaused()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            Assert.IsTrue(logic.IsPaused);
        }

        [Test]
        public void PauseLogic_Pause_StoresTimeScale()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            Assert.AreEqual(1f, logic.PreviousTimeScale);
        }

        [Test]
        public void PauseLogic_Pause_TargetTimeScaleIsZero()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            Assert.AreEqual(0f, logic.TargetTimeScale);
        }

        [Test]
        public void PauseLogic_Resume_RestoresTimeScale()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            logic.Resume();
            Assert.IsFalse(logic.IsPaused);
            Assert.AreEqual(1f, logic.TargetTimeScale);
        }

        [Test]
        public void PauseLogic_Resume_PreservesOriginalTimeScale()
        {
            var logic = new PauseLogic();
            logic.Pause(0.5f); // Was at 0.5x (e.g., slow motion)
            logic.Resume();
            Assert.AreEqual(0.5f, logic.TargetTimeScale);
        }

        [Test]
        public void PauseLogic_Restart_SetsTimeScaleToOne()
        {
            var logic = new PauseLogic();
            logic.Pause(0.5f);
            logic.Restart();
            Assert.AreEqual(1f, logic.TargetTimeScale);
            Assert.IsFalse(logic.IsPaused);
        }

        [Test]
        public void PauseLogic_Quit_SetsTimeScaleToOne()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            logic.Quit();
            Assert.AreEqual(1f, logic.TargetTimeScale);
        }

        [Test]
        public void PauseLogic_DoublePause_Ignored()
        {
            var logic = new PauseLogic();
            logic.Pause(1f);
            logic.Pause(0.5f);
            Assert.AreEqual(1f, logic.PreviousTimeScale,
                "Should store the original time scale, not the paused one");
        }
    }
}
