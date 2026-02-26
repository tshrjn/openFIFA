using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Ball trail particle effect that activates at high velocity.
    /// Emission and intensity scale with ball speed.
    /// </summary>
    public class BallTrailEffect : MonoBehaviour
    {
        [SerializeField] private float _velocityThreshold = 10f;
        [SerializeField] private float _particleLifetime = 0.3f;
        [SerializeField] private float _particleSize = 0.1f;
        [SerializeField] private Color _minColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color _maxColor = new Color(1f, 0.9f, 0.5f, 1f);

        private ParticleSystem _ps;
        private Rigidbody _rb;
        private BallTrailLogic _logic;

        /// <summary>The underlying trail logic.</summary>
        public BallTrailLogic Logic => _logic;

        private void Awake()
        {
            _logic = new BallTrailLogic(_velocityThreshold, 50);
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

                // Scale color alpha with speed
                var main = _ps.main;
                Color color = Color.Lerp(_minColor, _maxColor, _logic.TrailAlpha);
                main.startColor = color;
            }
        }

        private void ConfigureParticleSystem()
        {
            var main = _ps.main;
            main.startLifetime = _particleLifetime;
            main.startSize = _particleSize;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.playOnAwake = false;

            var emission = _ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Disable sub-emitters and most modules for performance
            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;
        }
    }
}
