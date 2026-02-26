using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject wrapper for formation data.
    /// Defines a team formation with 5 position slots.
    /// </summary>
    [CreateAssetMenu(fileName = "FormationData", menuName = "OpenFifa/Config/Formation Data")]
    public class FormationData : ScriptableObject
    {
        [System.Serializable]
        public struct FormationSlot
        {
            public PositionRole role;
            public Vector3 offset;
        }

        [SerializeField] private string _formationName = "2-1-2";
        [SerializeField] private FormationSlot[] _slots = new FormationSlot[]
        {
            new FormationSlot { role = PositionRole.Goalkeeper, offset = new Vector3(0f, 0f, -12f) },
            new FormationSlot { role = PositionRole.Defender,   offset = new Vector3(-6f, 0f, -6f) },
            new FormationSlot { role = PositionRole.Defender,   offset = new Vector3(6f, 0f, -6f) },
            new FormationSlot { role = PositionRole.Midfielder, offset = new Vector3(0f, 0f, 0f) },
            new FormationSlot { role = PositionRole.Forward,    offset = new Vector3(-6f, 0f, 8f) },
            new FormationSlot { role = PositionRole.Forward,    offset = new Vector3(6f, 0f, 8f) }
        };

        public string FormationName => _formationName;
        public int SlotCount => _slots.Length;

        /// <summary>
        /// Convert to pure C# data object.
        /// </summary>
        public FormationLayoutData ToData()
        {
            var slotData = new FormationSlotData[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                slotData[i] = new FormationSlotData(
                    _slots[i].role,
                    _slots[i].offset.x,
                    _slots[i].offset.y,
                    _slots[i].offset.z
                );
            }
            return new FormationLayoutData(_formationName, slotData);
        }

        /// <summary>
        /// Get world positions for this formation.
        /// </summary>
        public Vector3[] GetPositions(bool isHomeTeam, float pitchLength)
        {
            var data = ToData();
            var formationPositions = data.GetWorldPositions(isHomeTeam, pitchLength);
            var positions = new Vector3[formationPositions.Length];
            for (int i = 0; i < formationPositions.Length; i++)
            {
                positions[i] = new Vector3(
                    formationPositions[i].x,
                    formationPositions[i].y,
                    formationPositions[i].z
                );
            }
            return positions;
        }
    }
}
