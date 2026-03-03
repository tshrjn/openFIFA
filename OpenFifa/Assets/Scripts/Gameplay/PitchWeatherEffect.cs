using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Pitch surface weather effects: puddle placement, snow accumulation overlay,
    /// mud splatter near goal areas, and ball splash on wet pitch contact.
    /// Works in tandem with WeatherSystem to reflect pitch conditions visually.
    /// </summary>
    public class PitchWeatherEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeatherSystem _weatherSystem;
        [SerializeField] private Renderer _pitchRenderer;
        [SerializeField] private Transform _ballTransform;

        [Header("Puddle Settings")]
        [SerializeField] private int _maxPuddles = 8;
        [SerializeField] private float _puddleSize = 1.5f;
        [SerializeField] private Material _puddleMaterial;

        [Header("Snow Overlay")]
        [SerializeField] private Color _snowTintColor = new Color(0.95f, 0.95f, 1f, 1f);

        [Header("Mud Splatter")]
        [SerializeField] private float _goalAreaZ = 22f;
        [SerializeField] private float _mudSplatterRadius = 5f;

        [Header("Ball Splash")]
        [SerializeField] private ParticleSystem _ballSplashEffect;
        [SerializeField] private float _ballSplashThreshold = 0.3f;
        [SerializeField] private float _ballGroundHeight = 0.2f;

        private MaterialPropertyBlock _pitchPropertyBlock;
        private GameObject[] _puddleObjects;
        private bool _puddlesVisible;
        private float _previousBallY;
        private bool _ballWasAboveGround;

        // Shader property IDs
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
        private static readonly int SnowCoverageId = Shader.PropertyToID("_SnowCoverage");
        private static readonly int SnowTintId = Shader.PropertyToID("_SnowTint");
        private static readonly int MudLevelId = Shader.PropertyToID("_MudLevel");
        private static readonly int MudCenterZId = Shader.PropertyToID("_MudCenterZ");
        private static readonly int MudRadiusId = Shader.PropertyToID("_MudRadius");

        /// <summary>Whether puddles are currently visible.</summary>
        public bool PuddlesVisible => _puddlesVisible;

        /// <summary>Number of puddle objects allocated.</summary>
        public int PuddleCount => _puddleObjects != null ? _puddleObjects.Length : 0;

        private void Awake()
        {
            _pitchPropertyBlock = new MaterialPropertyBlock();
            _previousBallY = float.MaxValue;
            _ballWasAboveGround = true;

            CreatePuddleObjects();
            EnsureBallSplashEffect();
        }

        private void Update()
        {
            if (_weatherSystem == null || _weatherSystem.Logic == null) return;

            var pitchCondition = _weatherSystem.Logic.PitchCondition;
            var logic = _weatherSystem.Logic;

            UpdatePitchSurfaceProperties(pitchCondition);
            UpdatePuddleVisibility(logic);
            UpdateSnowOverlay(pitchCondition);
            UpdateMudSplatter(pitchCondition);
            CheckBallSplash(pitchCondition);
        }

        private void UpdatePitchSurfaceProperties(PitchConditionTracker pitchCondition)
        {
            if (_pitchRenderer == null) return;

            _pitchRenderer.GetPropertyBlock(_pitchPropertyBlock);
            _pitchPropertyBlock.SetFloat(WetnessId, pitchCondition.Wetness);
            _pitchPropertyBlock.SetFloat(SnowCoverageId, pitchCondition.SnowCoverage);
            _pitchPropertyBlock.SetFloat(MudLevelId, pitchCondition.MudLevel);
            _pitchRenderer.SetPropertyBlock(_pitchPropertyBlock);
        }

        private void UpdatePuddleVisibility(WeatherLogic logic)
        {
            bool shouldShow = logic.ShouldShowPuddles && logic.PitchCondition.Wetness > 0.2f;

            if (shouldShow != _puddlesVisible)
            {
                _puddlesVisible = shouldShow;
                SetPuddlesActive(shouldShow);
            }

            if (_puddlesVisible && _puddleObjects != null)
            {
                // Scale puddle alpha with wetness
                float alpha = logic.PitchCondition.Wetness;
                foreach (var puddle in _puddleObjects)
                {
                    if (puddle == null) continue;
                    var renderer = puddle.GetComponent<Renderer>();
                    if (renderer == null) continue;

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetFloat("_Alpha", alpha);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        private void UpdateSnowOverlay(PitchConditionTracker pitchCondition)
        {
            if (_pitchRenderer == null || pitchCondition.SnowCoverage <= 0f) return;

            _pitchRenderer.GetPropertyBlock(_pitchPropertyBlock);
            _pitchPropertyBlock.SetColor(SnowTintId, Color.Lerp(Color.white, _snowTintColor, pitchCondition.SnowCoverage));
            _pitchPropertyBlock.SetFloat(SnowCoverageId, pitchCondition.SnowCoverage);
            _pitchRenderer.SetPropertyBlock(_pitchPropertyBlock);
        }

        private void UpdateMudSplatter(PitchConditionTracker pitchCondition)
        {
            if (_pitchRenderer == null || pitchCondition.MudLevel <= 0f) return;

            _pitchRenderer.GetPropertyBlock(_pitchPropertyBlock);
            _pitchPropertyBlock.SetFloat(MudLevelId, pitchCondition.MudLevel);
            _pitchPropertyBlock.SetFloat(MudCenterZId, _goalAreaZ);
            _pitchPropertyBlock.SetFloat(MudRadiusId, _mudSplatterRadius);
            _pitchRenderer.SetPropertyBlock(_pitchPropertyBlock);
        }

        private void CheckBallSplash(PitchConditionTracker pitchCondition)
        {
            if (_ballTransform == null || _ballSplashEffect == null) return;
            if (pitchCondition.Wetness < _ballSplashThreshold) return;

            float currentY = _ballTransform.position.y;
            bool isNearGround = currentY <= _ballGroundHeight;
            bool wasAboveGround = _ballWasAboveGround;

            _ballWasAboveGround = !isNearGround;

            // Trigger splash when ball touches wet ground
            if (isNearGround && wasAboveGround)
            {
                _ballSplashEffect.transform.position = new Vector3(
                    _ballTransform.position.x,
                    0.01f,
                    _ballTransform.position.z
                );

                // Scale splash intensity with wetness
                var emission = _ballSplashEffect.emission;
                var main = _ballSplashEffect.main;
                float intensity = pitchCondition.Wetness;
                main.startSize = 0.1f + 0.15f * intensity;

                _ballSplashEffect.Emit((int)(5 + 10 * intensity));
            }

            _previousBallY = currentY;
        }

        private void CreatePuddleObjects()
        {
            _puddleObjects = new GameObject[_maxPuddles];

            // Predefined puddle positions spread across the pitch
            var positions = GetPuddlePositions();

            for (int i = 0; i < _maxPuddles && i < positions.Length; i++)
            {
                var puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puddle.name = $"Puddle_{i}";
                puddle.transform.SetParent(transform);
                puddle.transform.localPosition = positions[i];
                puddle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                puddle.transform.localScale = new Vector3(_puddleSize, _puddleSize, 1f);

                // Remove collider from puddle quad
                var collider = puddle.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                // Apply puddle material if provided
                if (_puddleMaterial != null)
                {
                    var renderer = puddle.GetComponent<Renderer>();
                    renderer.material = _puddleMaterial;
                }

                puddle.SetActive(false);
                _puddleObjects[i] = puddle;
            }

            _puddlesVisible = false;
        }

        private Vector3[] GetPuddlePositions()
        {
            // Low points on the pitch where water would collect
            return new Vector3[]
            {
                new Vector3(-8f, 0.01f, -5f),
                new Vector3(5f, 0.01f, 3f),
                new Vector3(-12f, 0.01f, 8f),
                new Vector3(10f, 0.01f, -10f),
                new Vector3(0f, 0.01f, -12f),
                new Vector3(-15f, 0.01f, -2f),
                new Vector3(18f, 0.01f, 7f),
                new Vector3(-3f, 0.01f, 14f)
            };
        }

        private void SetPuddlesActive(bool active)
        {
            if (_puddleObjects == null) return;
            foreach (var puddle in _puddleObjects)
            {
                if (puddle != null)
                    puddle.SetActive(active);
            }
        }

        private void EnsureBallSplashEffect()
        {
            if (_ballSplashEffect != null) return;

            var splashGO = new GameObject("BallSplash");
            splashGO.transform.SetParent(transform);
            splashGO.transform.localPosition = Vector3.zero;
            _ballSplashEffect = splashGO.AddComponent<ParticleSystem>();

            var main = _ballSplashEffect.main;
            main.startLifetime = 0.3f;
            main.startSize = 0.1f;
            main.startSpeed = 2f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.5f);
            main.playOnAwake = false;

            var emission = _ballSplashEffect.emission;
            emission.enabled = false;

            var shape = _ballSplashEffect.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var colorOverLifetime = _ballSplashEffect.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.7f, 0.75f, 0.85f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.75f, 0.85f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
        }

        private void OnDestroy()
        {
            // Clean up dynamically created puddle objects
            if (_puddleObjects != null)
            {
                foreach (var puddle in _puddleObjects)
                {
                    if (puddle != null)
                        Destroy(puddle);
                }
            }
        }
    }
}
