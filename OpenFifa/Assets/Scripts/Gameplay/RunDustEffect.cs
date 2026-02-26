using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Dust particle effect at player feet when running.
    /// Positioned as a child at foot level, emitting ground-aligned particles.
    /// </summary>
    public class RunDustEffect : MonoBehaviour
    {
        [SerializeField] private float _walkThreshold = 2f;
        [SerializeField] private float _particleLifetime = 0.4f;
        [SerializeField] private float _particleSize = 0.15f;
        [SerializeField] private Color _dustColor = new Color(0.76f, 0.7f, 0.5f, 0.6f);

        private ParticleSystem _ps;
        private Rigidbody _rb;
        private RunDustLogic _logic;

        /// <summary>The underlying dust logic.</summary>
        public RunDustLogic Logic => _logic;

        private void Awake()
        {
            _logic = new RunDustLogic(_walkThreshold, 20);
            _rb = GetComponentInParent<Rigidbody>();
            _ps = GetComponent<ParticleSystem>();

            if (_ps == null)
            {
                _ps = gameObject.AddComponent<ParticleSystem>();
            }

            ConfigureParticleSystem();
        }

        private void Update()
        {
            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            _logic.Update(speed);

            var emission = _ps.emission;
            emission.enabled = _logic.ShouldEmit;

            if (_logic.ShouldEmit)
            {
                emission.rateOverTime = _logic.EmissionRate;
            }
        }

        private void ConfigureParticleSystem()
        {
            var main = _ps.main;
            main.startLifetime = _particleLifetime;
            main.startSize = _particleSize;
            main.startSpeed = 0.5f;
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = _dustColor;
            main.playOnAwake = false;

            var emission = _ps.emission;
            emission.enabled = false;

            // Shape: hemisphere rotated to emit horizontally
            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.radius = 0.1f;

            // Color fade out
            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(_dustColor, 0f),
                    new GradientColorKey(_dustColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.6f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
        }
    }
}
