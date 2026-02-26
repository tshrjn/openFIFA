using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Manages the active formation for a team.
    /// Can swap formations at runtime.
    /// </summary>
    public class FormationManager : MonoBehaviour
    {
        [SerializeField] private FormationData _activeFormation;
        [SerializeField] private bool _isHomeTeam = true;
        [SerializeField] private float _pitchLength = 50f;

        private FormationLayoutData _currentLayout;

        /// <summary>Current formation name.</summary>
        public string CurrentFormationName => _activeFormation != null ? _activeFormation.FormationName : "None";

        /// <summary>Whether this is the home team.</summary>
        public bool IsHomeTeam => _isHomeTeam;

        private void Awake()
        {
            if (_activeFormation != null)
            {
                _currentLayout = _activeFormation.ToData();
            }
        }

        /// <summary>
        /// Initialize for tests or runtime.
        /// </summary>
        public void Initialize(FormationData formation, bool isHomeTeam, float pitchLength)
        {
            _activeFormation = formation;
            _isHomeTeam = isHomeTeam;
            _pitchLength = pitchLength;
            if (_activeFormation != null)
            {
                _currentLayout = _activeFormation.ToData();
            }
        }

        /// <summary>
        /// Initialize with pure data (for tests without ScriptableObject).
        /// </summary>
        public void Initialize(FormationLayoutData layout, bool isHomeTeam, float pitchLength)
        {
            _currentLayout = layout;
            _isHomeTeam = isHomeTeam;
            _pitchLength = pitchLength;
        }

        /// <summary>
        /// Swap the active formation at runtime.
        /// </summary>
        public void SwapFormation(FormationData newFormation)
        {
            _activeFormation = newFormation;
            _currentLayout = _activeFormation.ToData();
        }

        /// <summary>
        /// Swap with pure data.
        /// </summary>
        public void SwapFormation(FormationLayoutData layout)
        {
            _currentLayout = layout;
        }

        /// <summary>
        /// Get the current world positions for all formation slots.
        /// </summary>
        public Vector3[] GetWorldPositions()
        {
            if (_currentLayout == null) return new Vector3[0];

            var positions = _currentLayout.GetWorldPositions(_isHomeTeam, _pitchLength);
            var result = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                result[i] = new Vector3(positions[i].x, positions[i].y, positions[i].z);
            }
            return result;
        }

        /// <summary>
        /// Get the formation slot data.
        /// </summary>
        public FormationSlotData[] GetSlots()
        {
            return _currentLayout?.GetSlots() ?? new FormationSlotData[0];
        }
    }
}
