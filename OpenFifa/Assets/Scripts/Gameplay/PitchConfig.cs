using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject wrapper for PitchConfigData.
    /// Exposes tunable values in the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "PitchConfig", menuName = "OpenFifa/Config/Pitch Config")]
    public class PitchConfig : ScriptableObject
    {
        [Header("Pitch Dimensions")]
        [SerializeField] private float _pitchLength = 50f;
        [SerializeField] private float _pitchWidth = 30f;

        [Header("Goal")]
        [SerializeField] private float _goalWidth = 5f;
        [SerializeField] private float _goalHeight = 2.4f;
        [SerializeField] private float _goalAreaDepth = 4f;

        [Header("Markings")]
        [SerializeField] private float _centerCircleRadius = 3f;

        [Header("Boundaries")]
        [SerializeField] private float _boundaryWallHeight = 3f;
        [SerializeField] private float _boundaryWallThickness = 1f;

        public float PitchLength => _pitchLength;
        public float PitchWidth => _pitchWidth;
        public float GoalWidth => _goalWidth;
        public float GoalHeight => _goalHeight;
        public float GoalAreaDepth => _goalAreaDepth;
        public float CenterCircleRadius => _centerCircleRadius;
        public float BoundaryWallHeight => _boundaryWallHeight;
        public float BoundaryWallThickness => _boundaryWallThickness;

        /// <summary>
        /// Converts this ScriptableObject to a pure C# data object for core logic.
        /// </summary>
        public PitchConfigData ToData()
        {
            return new PitchConfigData(
                pitchLength: _pitchLength,
                pitchWidth: _pitchWidth,
                goalWidth: _goalWidth,
                centerCircleRadius: _centerCircleRadius,
                goalAreaDepth: _goalAreaDepth,
                goalHeight: _goalHeight,
                boundaryWallHeight: _boundaryWallHeight,
                boundaryWallThickness: _boundaryWallThickness
            );
        }
    }
}
