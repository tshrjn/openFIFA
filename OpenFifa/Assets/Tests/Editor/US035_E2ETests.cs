using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US035")]
    [Category("E2E")]
    public class US035_E2ETests
    {
        [Test]
        public void E2EConfig_SceneSequence_CorrectOrder()
        {
            var config = new E2EJourneyConfig();
            Assert.AreEqual(5, config.SceneSequence.Count);
            Assert.AreEqual("MainMenu", config.SceneSequence[0]);
            Assert.AreEqual("TeamSelect", config.SceneSequence[1]);
            Assert.AreEqual("Match", config.SceneSequence[2]);
            Assert.AreEqual("Results", config.SceneSequence[3]);
            Assert.AreEqual("MainMenu", config.SceneSequence[4]);
        }

        [Test]
        public void E2EConfig_TimeScale_100x()
        {
            var config = new E2EJourneyConfig();
            Assert.AreEqual(100f, config.MatchTimeScale);
        }

        [Test]
        public void E2EConfig_Timeout_5Minutes()
        {
            var config = new E2EJourneyConfig();
            Assert.AreEqual(300000, config.TimeoutMs);
        }

        [Test]
        public void SceneValidator_MainMenuScene_HasExpectedObjects()
        {
            var validator = new SceneObjectValidator();
            // MainMenu expected objects
            var mainMenuObjects = validator.GetExpectedObjects("MainMenu");
            Assert.Contains("PlayButton", mainMenuObjects);
            Assert.Contains("SettingsButton", mainMenuObjects);
        }

        [Test]
        public void SceneValidator_TeamSelect_ExpectedObjects()
        {
            var validator = new SceneObjectValidator();
            var objects = validator.GetExpectedObjects("TeamSelect");
            Assert.Contains("TeamGrid", objects);
            Assert.Contains("ConfirmButton", objects);
        }

        [Test]
        public void SceneValidator_Match_ExpectedObjects()
        {
            var validator = new SceneObjectValidator();
            var objects = validator.GetExpectedObjects("Match");
            Assert.Contains("Ball", objects);
            Assert.Contains("MatchHUD", objects);
        }

        [Test]
        public void SceneValidator_Results_ExpectedObjects()
        {
            var validator = new SceneObjectValidator();
            var objects = validator.GetExpectedObjects("Results");
            Assert.Contains("FinalScore", objects);
            Assert.Contains("PlayAgainButton", objects);
            Assert.Contains("MainMenuButton", objects);
        }

        [Test]
        public void JourneyStateMachine_InitialState_MainMenu()
        {
            var journey = new E2EJourneyStateMachine();
            Assert.AreEqual(E2EJourneyState.MainMenu, journey.CurrentState);
        }

        [Test]
        public void JourneyStateMachine_SequentialAdvance_ProgressesThroughAllStates()
        {
            var journey = new E2EJourneyStateMachine();
            Assert.AreEqual(E2EJourneyState.MainMenu, journey.CurrentState);

            journey.Advance();
            Assert.AreEqual(E2EJourneyState.TeamSelect, journey.CurrentState);

            journey.Advance();
            Assert.AreEqual(E2EJourneyState.Match, journey.CurrentState);

            journey.Advance();
            Assert.AreEqual(E2EJourneyState.Results, journey.CurrentState);

            journey.Advance();
            Assert.AreEqual(E2EJourneyState.ReturnToMainMenu, journey.CurrentState);

            journey.Advance();
            Assert.AreEqual(E2EJourneyState.Complete, journey.CurrentState);
        }

        [Test]
        public void JourneyStateMachine_AtCompleteState_CannotAdvancePastComplete()
        {
            var journey = new E2EJourneyStateMachine();
            // Advance through all states
            for (int i = 0; i < 5; i++) journey.Advance();
            Assert.AreEqual(E2EJourneyState.Complete, journey.CurrentState);

            // Should stay at Complete
            journey.Advance();
            Assert.AreEqual(E2EJourneyState.Complete, journey.CurrentState);
        }

        [Test]
        public void JourneyStateMachine_IsComplete_TrueAtEnd()
        {
            var journey = new E2EJourneyStateMachine();
            Assert.IsFalse(journey.IsComplete);

            for (int i = 0; i < 5; i++) journey.Advance();
            Assert.IsTrue(journey.IsComplete);
        }

        [Test]
        public void ErrorLog_WhenCreated_InitiallyEmpty()
        {
            var errorLog = new E2EErrorLog();
            Assert.AreEqual(0, errorLog.ErrorCount);
            Assert.IsTrue(errorLog.IsClean);
        }

        [Test]
        public void ErrorLog_AfterLogError_RecordsErrors()
        {
            var errorLog = new E2EErrorLog();
            errorLog.LogError("NullReferenceException in Scene X");
            Assert.AreEqual(1, errorLog.ErrorCount);
            Assert.IsFalse(errorLog.IsClean);
        }
    }
}
