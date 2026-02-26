using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// States for the E2E user journey test state machine.
    /// </summary>
    public enum E2EJourneyState
    {
        MainMenu,
        TeamSelect,
        Match,
        Results,
        ReturnToMainMenu,
        Complete
    }

    /// <summary>
    /// Configuration for the end-to-end user journey test.
    /// Defines scene sequence, time scale, and timeout.
    /// </summary>
    public class E2EJourneyConfig
    {
        /// <summary>Ordered scene names the journey passes through.</summary>
        public List<string> SceneSequence = new List<string>
        {
            "MainMenu",
            "TeamSelect",
            "Match",
            "Results",
            "MainMenu"
        };

        /// <summary>Time scale during match phase for faster completion.</summary>
        public float MatchTimeScale = 100f;

        /// <summary>Maximum wall-clock timeout in milliseconds (5 minutes).</summary>
        public int TimeoutMs = 300000;

        /// <summary>Normal time scale to restore after match.</summary>
        public float NormalTimeScale = 1f;
    }

    /// <summary>
    /// Validates expected GameObjects in each scene for the E2E test.
    /// </summary>
    public class SceneObjectValidator
    {
        private readonly Dictionary<string, List<string>> _expectedObjects;

        public SceneObjectValidator()
        {
            _expectedObjects = new Dictionary<string, List<string>>
            {
                ["MainMenu"] = new List<string>
                {
                    "PlayButton", "SettingsButton", "QuitButton", "TitleText"
                },
                ["TeamSelect"] = new List<string>
                {
                    "TeamGrid", "ConfirmButton", "BackButton"
                },
                ["Match"] = new List<string>
                {
                    "Ball", "MatchHUD", "Pitch", "MainCamera"
                },
                ["Results"] = new List<string>
                {
                    "FinalScore", "PlayAgainButton", "MainMenuButton", "MOTM"
                }
            };
        }

        /// <summary>
        /// Returns the list of expected GameObject names for a scene.
        /// Returns empty list for unknown scenes.
        /// </summary>
        public List<string> GetExpectedObjects(string sceneName)
        {
            if (_expectedObjects.TryGetValue(sceneName, out var objects))
                return objects;
            return new List<string>();
        }
    }

    /// <summary>
    /// Simple state machine that tracks E2E journey progress.
    /// Advances linearly through states.
    /// </summary>
    public class E2EJourneyStateMachine
    {
        public E2EJourneyState CurrentState { get; private set; }
        public bool IsComplete => CurrentState == E2EJourneyState.Complete;

        public E2EJourneyStateMachine()
        {
            CurrentState = E2EJourneyState.MainMenu;
        }

        /// <summary>
        /// Advances to the next state in the journey.
        /// Does nothing if already complete.
        /// </summary>
        public void Advance()
        {
            switch (CurrentState)
            {
                case E2EJourneyState.MainMenu:
                    CurrentState = E2EJourneyState.TeamSelect;
                    break;
                case E2EJourneyState.TeamSelect:
                    CurrentState = E2EJourneyState.Match;
                    break;
                case E2EJourneyState.Match:
                    CurrentState = E2EJourneyState.Results;
                    break;
                case E2EJourneyState.Results:
                    CurrentState = E2EJourneyState.ReturnToMainMenu;
                    break;
                case E2EJourneyState.ReturnToMainMenu:
                    CurrentState = E2EJourneyState.Complete;
                    break;
                case E2EJourneyState.Complete:
                    // Stay at Complete
                    break;
            }
        }
    }

    /// <summary>
    /// Collects errors during the E2E journey for assertion at the end.
    /// </summary>
    public class E2EErrorLog
    {
        private readonly List<string> _errors;

        public E2EErrorLog()
        {
            _errors = new List<string>();
        }

        public int ErrorCount => _errors.Count;
        public bool IsClean => _errors.Count == 0;

        public void LogError(string message)
        {
            _errors.Add(message);
        }

        public IReadOnlyList<string> GetErrors()
        {
            return _errors;
        }

        public void Clear()
        {
            _errors.Clear();
        }
    }
}
