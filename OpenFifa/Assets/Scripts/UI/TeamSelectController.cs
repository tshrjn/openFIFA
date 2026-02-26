using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using OpenFifa.Core;

namespace OpenFifa.UI
{
    /// <summary>
    /// Team selection screen controller.
    /// Displays at least 4 teams, allows selection, and assigns AI opponent.
    /// </summary>
    public class TeamSelectController : MonoBehaviour
    {
        [SerializeField] private Button[] _teamButtons;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _selectedTeamText;
        [SerializeField] private TeamData[] _teamDataAssets;

        private TeamSelectLogic _logic;

        /// <summary>Currently selected team index.</summary>
        public int SelectedTeamIndex => _logic != null ? _logic.SelectedTeamIndex : -1;

        /// <summary>Confirm button reference (for test verification).</summary>
        public Button ConfirmButton => _confirmButton;

        private void Awake()
        {
            int teamCount = _teamButtons != null ? _teamButtons.Length : 4;
            _logic = new TeamSelectLogic(teamCount);
        }

        private void Start()
        {
            // Wire up team buttons
            if (_teamButtons != null)
            {
                for (int i = 0; i < _teamButtons.Length; i++)
                {
                    int index = i; // Capture for closure
                    _teamButtons[i].onClick.AddListener(() => OnTeamSelected(index));
                }
            }

            // Wire up confirm button
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
                _confirmButton.interactable = false;
            }
        }

        private void OnTeamSelected(int index)
        {
            _logic.SelectTeam(index);

            // Update confirm button
            if (_confirmButton != null)
                _confirmButton.interactable = _logic.CanConfirm;

            // Update selection text
            if (_selectedTeamText != null && _teamDataAssets != null && index < _teamDataAssets.Length)
                _selectedTeamText.text = _teamDataAssets[index].TeamName;

            // Visual highlight: scale selected button, reset others
            for (int i = 0; i < _teamButtons.Length; i++)
            {
                _teamButtons[i].transform.localScale =
                    (i == index) ? Vector3.one * 1.1f : Vector3.one;
            }
        }

        private void OnConfirmClicked()
        {
            if (!_logic.CanConfirm) return;
            SceneManager.LoadScene("Match");
        }
    }

    /// <summary>
    /// TeamData ScriptableObject for team configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTeam", menuName = "OpenFifa/Team Data")]
    public class TeamData : ScriptableObject
    {
        [SerializeField] private string _teamName = "Team";
        [SerializeField] private Color _primaryColor = Color.blue;
        [SerializeField] private Color _secondaryColor = Color.white;

        public string TeamName => _teamName;
        public Color PrimaryColor => _primaryColor;
        public Color SecondaryColor => _secondaryColor;

        public TeamDataEntry ToEntry()
        {
            return new TeamDataEntry(
                _teamName,
                _primaryColor.r, _primaryColor.g, _primaryColor.b,
                _secondaryColor.r, _secondaryColor.g, _secondaryColor.b
            );
        }
    }
}
