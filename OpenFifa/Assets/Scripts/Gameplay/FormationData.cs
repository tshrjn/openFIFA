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
            [SerializeField] private PositionRole _role;
            [SerializeField] private Vector3 _offset;

            public PositionRole Role => _role;
            public Vector3 Offset => _offset;

            public FormationSlot(PositionRole role, Vector3 offset)
            {
                _role = role;
                _offset = offset;
            }
        }

        [SerializeField] private string _formationName = "2-1-2";
        [SerializeField] private FormationSlot[] _slots = new FormationSlot[]
        {
            new FormationSlot(PositionRole.Goalkeeper, new Vector3(0f, 0f, -12f)),
            new FormationSlot(PositionRole.Defender,   new Vector3(-6f, 0f, -6f)),
            new FormationSlot(PositionRole.Defender,   new Vector3(6f, 0f, -6f)),
            new FormationSlot(PositionRole.Midfielder, new Vector3(0f, 0f, 0f)),
            new FormationSlot(PositionRole.Forward,    new Vector3(-6f, 0f, 8f)),
            new FormationSlot(PositionRole.Forward,    new Vector3(6f, 0f, 8f))
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
                    _slots[i].Role,
                    _slots[i].Offset.x,
                    _slots[i].Offset.y,
                    _slots[i].Offset.z
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
