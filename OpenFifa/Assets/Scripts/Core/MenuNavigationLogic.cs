namespace OpenFifa.Core
{
    /// <summary>
    /// Menu button identifiers.
    /// </summary>
    public enum MenuButton
    {
        Play = 0,
        Settings = 1,
        Credits = 2
    }

    /// <summary>
    /// UI scaler configuration for canvas scaling across resolutions.
    /// </summary>
    public class UIScalerConfig
    {
        public int ReferenceWidth = 1920;
        public int ReferenceHeight = 1080;
        public float MatchWidthOrHeight = 0.5f;
    }

    /// <summary>
    /// Pure C# menu navigation logic.
    /// Maps menu buttons to target scenes.
    /// No Unity dependency — fully testable in EditMode.
    /// </summary>
    public class MenuNavigationLogic
    {
        /// <summary>Game title string.</summary>
        public string GameTitle => "OpenFifa";

        /// <summary>
        /// Get the target scene name for a menu button.
        /// </summary>
        public string GetTargetScene(MenuButton button)
        {
            switch (button)
            {
                case MenuButton.Play:
                    return "TeamSelect";
                case MenuButton.Settings:
                    return "Settings";
                case MenuButton.Credits:
                    return "Credits";
                default:
                    return "MainMenu";
            }
        }
    }
}
