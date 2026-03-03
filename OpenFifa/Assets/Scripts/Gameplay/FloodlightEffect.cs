using System.Collections;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Per-floodlight visual effects: lens flare, volumetric cone approximation,
    /// warm-up glow, and emergency lighting mode.
    /// Attach to each floodlight tower GameObject.
    /// </summary>
    public class FloodlightEffect : MonoBehaviour
    {
        [Header("Light Reference")]
        [SerializeField] private Light spotLight;

        [Header("Volumetric Cone")]
        [SerializeField] private MeshRenderer volumetricCone;
        [SerializeField] private float coneAlphaFullPower = 0.08f;
        [SerializeField] private float coneAlphaWarmUp = 0.03f;

        [Header("Warm-Up Glow")]
        [SerializeField] private MeshRenderer glowRenderer;
        [SerializeField] private float glowIntensityMax = 2f;

        [Header("Light Cookie")]
        [SerializeField] private Texture lightCookie;

        [Header("Flicker Settings")]
        [SerializeField] private float flickerMinIntensity = 0.3f;
        [SerializeField] private float flickerMaxIntensity = 1.0f;
        [SerializeField] private float flickerSpeed = 8f;

        [Header("Emergency Mode")]
        [SerializeField] private float emergencyPulseSpeed = 2f;

        // State
        private FloodlightState _currentState = FloodlightState.Off;
        private Coroutine _warmUpCoroutine;
        private Coroutine _emergencyCoroutine;
        private float _baseIntensity;
        private Material _coneMaterial;
        private Material _glowMaterial;

        /// <summary>Current floodlight state.</summary>
        public FloodlightState CurrentState => _currentState;

        private void Awake()
        {
            if (spotLight != null)
            {
                _baseIntensity = spotLight.intensity;

                // Apply light cookie if available
                if (lightCookie != null)
                {
                    spotLight.cookie = lightCookie;
                }
            }

            // Cache materials for runtime modification
            if (volumetricCone != null)
            {
                _coneMaterial = volumetricCone.material;
            }

            if (glowRenderer != null)
            {
                _glowMaterial = glowRenderer.material;
            }

            // Start off
            SetVisuals(FloodlightState.Off);
        }

        /// <summary>
        /// Called by DynamicLightingSystem when the floodlight state changes.
        /// </summary>
        public void OnStateChanged(FloodlightState newState)
        {
            var previousState = _currentState;
            _currentState = newState;

            // Stop any active coroutines
            if (_warmUpCoroutine != null)
            {
                StopCoroutine(_warmUpCoroutine);
                _warmUpCoroutine = null;
            }

            if (_emergencyCoroutine != null)
            {
                StopCoroutine(_emergencyCoroutine);
                _emergencyCoroutine = null;
            }

            // Start new visual behavior
            switch (newState)
            {
                case FloodlightState.Off:
                    SetVisuals(FloodlightState.Off);
                    break;

                case FloodlightState.WarmingUp:
                    _warmUpCoroutine = StartCoroutine(WarmUpFlickerCoroutine());
                    break;

                case FloodlightState.FullPower:
                    SetVisuals(FloodlightState.FullPower);
                    break;

                case FloodlightState.Dimmed:
                    SetVisuals(FloodlightState.Dimmed);
                    break;

                case FloodlightState.Emergency:
                    _emergencyCoroutine = StartCoroutine(EmergencyPulseCoroutine());
                    break;
            }
        }

        /// <summary>
        /// Warm-up flicker coroutine: simulates the characteristic flicker
        /// of stadium floodlights warming up (metal halide lamps).
        /// </summary>
        private IEnumerator WarmUpFlickerCoroutine()
        {
            float elapsed = 0f;
            float warmUpDuration = 3f; // Match FloodlightBehavior default

            // Set glow visible during warm-up
            SetGlowActive(true);
            SetConeAlpha(coneAlphaWarmUp);

            while (elapsed < warmUpDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / warmUpDuration;

                // Ramp up with flicker
                float baseRamp = progress * progress; // Ease-in
                float flicker = Mathf.Sin(elapsed * flickerSpeed * Mathf.PI * 2f)
                    * (1f - progress) * 0.4f; // Flicker decreases as warm-up progresses

                float intensity = Mathf.Clamp(
                    baseRamp + flicker,
                    flickerMinIntensity * baseRamp,
                    flickerMaxIntensity
                );

                if (spotLight != null)
                {
                    spotLight.intensity = _baseIntensity * intensity;
                    spotLight.enabled = true;

                    // Warm color temperature during start, shifting to daylight
                    float warmth = 1f - progress;
                    spotLight.color = new Color(1f, 0.85f + warmth * 0.05f, 0.7f + warmth * 0.1f);
                }

                // Update glow intensity
                SetGlowIntensity(glowIntensityMax * intensity * (1f - progress));

                yield return null;
            }

            // Warm-up complete - transition to full power visuals
            SetVisuals(FloodlightState.FullPower);
            SetGlowActive(false);
            _warmUpCoroutine = null;
        }

        /// <summary>
        /// Emergency mode: red-tinted pulsing light.
        /// </summary>
        private IEnumerator EmergencyPulseCoroutine()
        {
            SetGlowActive(false);

            while (true)
            {
                float pulse = (Mathf.Sin(Time.time * emergencyPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                float intensity = Mathf.Lerp(0.3f, 0.7f, pulse);

                if (spotLight != null)
                {
                    spotLight.intensity = _baseIntensity * intensity;
                    spotLight.color = new Color(1f, 0.2f, 0.1f); // Red tint
                    spotLight.enabled = true;
                }

                SetConeColor(new Color(1f, 0.1f, 0.05f, coneAlphaWarmUp * pulse));

                yield return null;
            }
        }

        // -----------------------------------------------------------------
        // Visual state helpers
        // -----------------------------------------------------------------

        private void SetVisuals(FloodlightState state)
        {
            if (spotLight == null) return;

            switch (state)
            {
                case FloodlightState.Off:
                    spotLight.enabled = false;
                    spotLight.intensity = 0f;
                    SetConeAlpha(0f);
                    SetGlowActive(false);
                    break;

                case FloodlightState.FullPower:
                    spotLight.enabled = true;
                    spotLight.intensity = _baseIntensity;
                    spotLight.color = new Color(1f, 0.96f, 0.88f); // Warm white
                    spotLight.shadows = LightShadows.Soft;
                    SetConeAlpha(coneAlphaFullPower);
                    SetGlowActive(false);
                    break;

                case FloodlightState.Dimmed:
                    spotLight.enabled = true;
                    spotLight.intensity = _baseIntensity * 0.4f;
                    spotLight.color = new Color(1f, 0.96f, 0.88f);
                    spotLight.shadows = LightShadows.Soft;
                    SetConeAlpha(coneAlphaFullPower * 0.5f);
                    SetGlowActive(false);
                    break;

                case FloodlightState.Emergency:
                    // Handled by coroutine
                    break;
            }
        }

        private void SetConeAlpha(float alpha)
        {
            if (_coneMaterial == null) return;

            var color = _coneMaterial.color;
            color.a = alpha;
            _coneMaterial.color = color;

            if (volumetricCone != null)
            {
                volumetricCone.enabled = alpha > 0.001f;
            }
        }

        private void SetConeColor(Color color)
        {
            if (_coneMaterial == null) return;
            _coneMaterial.color = color;

            if (volumetricCone != null)
            {
                volumetricCone.enabled = color.a > 0.001f;
            }
        }

        private void SetGlowActive(bool active)
        {
            if (glowRenderer != null)
            {
                glowRenderer.enabled = active;
            }
        }

        private void SetGlowIntensity(float intensity)
        {
            if (_glowMaterial == null) return;

            // Use emission for glow effect
            if (intensity > 0.01f)
            {
                _glowMaterial.EnableKeyword("_EMISSION");
                _glowMaterial.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.7f) * intensity);
            }
            else
            {
                _glowMaterial.DisableKeyword("_EMISSION");
            }
        }

        private void OnDestroy()
        {
            // Clean up runtime materials
            if (_coneMaterial != null)
            {
                Destroy(_coneMaterial);
            }

            if (_glowMaterial != null)
            {
                Destroy(_glowMaterial);
            }
        }
    }
}
