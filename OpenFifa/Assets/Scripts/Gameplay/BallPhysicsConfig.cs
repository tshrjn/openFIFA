using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject holding ball physics configuration.
    /// Exposes tunable values in the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "BallPhysicsConfig", menuName = "OpenFifa/Config/Ball Physics")]
    public class BallPhysicsConfig : ScriptableObject
    {
        [Header("Rigidbody")]
        [SerializeField] private float _mass = 0.43f;
        [SerializeField] private float _drag = 0.1f;
        [SerializeField] private float _angularDrag = 0.5f;

        [Header("Physic Material")]
        [SerializeField] private float _bounciness = 0.6f;
        [SerializeField] private float _dynamicFriction = 0.5f;
        [SerializeField] private float _staticFriction = 0.5f;

        public float Mass => _mass;
        public float Drag => _drag;
        public float AngularDrag => _angularDrag;
        public float Bounciness => _bounciness;
        public float DynamicFriction => _dynamicFriction;
        public float StaticFriction => _staticFriction;

        /// <summary>
        /// Converts this ScriptableObject to a pure C# data object for core logic.
        /// </summary>
        public BallPhysicsData ToData()
        {
            return new BallPhysicsData(
                mass: _mass,
                drag: _drag,
                angularDrag: _angularDrag,
                bounciness: _bounciness,
                dynamicFriction: _dynamicFriction,
                staticFriction: _staticFriction
            );
        }
    }
}
