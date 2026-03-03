using System;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// UI controller for the jersey customization panel.
    /// Provides a 3D model preview with rotation, color pickers, pattern selection,
    /// name/number input fields, crest position selector, and apply/reset buttons.
    /// </summary>
    public class JerseyCustomizationUI : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform previewModelRoot;
        [SerializeField] private float previewRotationSpeed = 30f;

        [Header("Renderers")]
        [SerializeField] private JerseyRenderer previewJerseyRenderer;

        [Header("State")]
        [SerializeField] private bool isRotating;

        private FullJerseyConfig _workingConfig;
        private FullJerseyConfig _originalConfig;
        private bool _isDirty;

        /// <summary>
        /// Whether the user has unsaved changes.
        /// </summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// The current working jersey configuration being edited.
        /// </summary>
        public FullJerseyConfig WorkingConfig => _workingConfig;

        /// <summary>
        /// Fired when the user applies jersey customization.
        /// </summary>
        public event Action<FullJerseyConfig> OnApply;

        /// <summary>
        /// Fired when the user resets to original configuration.
        /// </summary>
        public event Action OnReset;

        private void Awake()
        {
            _workingConfig = new FullJerseyConfig();
            _originalConfig = new FullJerseyConfig();
        }

        private void Update()
        {
            if (isRotating && previewModelRoot != null)
            {
                previewModelRoot.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
            }
        }

        /// <summary>
        /// Initializes the customization UI with an existing jersey config.
        /// </summary>
        public void Initialize(FullJerseyConfig config)
        {
            if (config == null)
            {
                _workingConfig = new FullJerseyConfig();
                _originalConfig = new FullJerseyConfig();
            }
            else
            {
                _workingConfig = config;
                _originalConfig = new FullJerseyConfig
                {
                    Design = config.Design.Clone(),
                    NameConfig = new PlayerNameConfig(config.NameConfig.Name),
                    NumberConfig = new PlayerNumberConfig(config.NumberConfig.Number),
                    Crest = new CrestConfig(config.Crest.TeamName)
                };
            }
            _isDirty = false;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the primary color and updates the preview.
        /// </summary>
        public void SetPrimaryColor(SimpleColor color)
        {
            _workingConfig.Design.PrimaryColor = color;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the secondary color and updates the preview.
        /// </summary>
        public void SetSecondaryColor(SimpleColor color)
        {
            _workingConfig.Design.SecondaryColor = color;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the tertiary color and updates the preview.
        /// </summary>
        public void SetTertiaryColor(SimpleColor color)
        {
            _workingConfig.Design.TertiaryColor = color;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the jersey pattern and updates the preview.
        /// </summary>
        public void SetPattern(JerseyPattern pattern)
        {
            _workingConfig.Design.Pattern = pattern;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the collar style and updates the preview.
        /// </summary>
        public void SetCollarStyle(CollarStyle collar)
        {
            _workingConfig.Design.Collar = collar;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the player name on the jersey.
        /// </summary>
        public void SetPlayerName(string name)
        {
            _workingConfig.NameConfig.Name = NameFormatter.FormatAndSanitize(name);
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Sets the player number on the jersey.
        /// </summary>
        public void SetPlayerNumber(int number)
        {
            if (JerseyValidation.IsNumberValid(number))
            {
                _workingConfig.NumberConfig.Number = number;
                _isDirty = true;
                RefreshPreview();
            }
        }

        /// <summary>
        /// Sets the crest position on the jersey.
        /// </summary>
        public void SetCrestPosition(CrestPosition position)
        {
            _workingConfig.Crest.Position = position;
            _isDirty = true;
            RefreshPreview();
        }

        /// <summary>
        /// Toggles the preview model rotation.
        /// </summary>
        public void ToggleRotation()
        {
            isRotating = !isRotating;
        }

        /// <summary>
        /// Sets the preview rotation state.
        /// </summary>
        public void SetRotating(bool rotating)
        {
            isRotating = rotating;
        }

        /// <summary>
        /// Applies the current working configuration and fires the OnApply event.
        /// Only applies if the design passes validation.
        /// </summary>
        public bool Apply()
        {
            if (!JerseyValidation.IsDesignComplete(_workingConfig))
                return false;

            _originalConfig = new FullJerseyConfig
            {
                Design = _workingConfig.Design.Clone(),
                NameConfig = new PlayerNameConfig(_workingConfig.NameConfig.Name),
                NumberConfig = new PlayerNumberConfig(_workingConfig.NumberConfig.Number),
                Crest = new CrestConfig(_workingConfig.Crest.TeamName)
            };

            _isDirty = false;
            OnApply?.Invoke(_workingConfig);
            return true;
        }

        /// <summary>
        /// Resets the working configuration to the original state.
        /// </summary>
        public void Reset()
        {
            _workingConfig = new FullJerseyConfig
            {
                Design = _originalConfig.Design.Clone(),
                NameConfig = new PlayerNameConfig(_originalConfig.NameConfig.Name),
                NumberConfig = new PlayerNumberConfig(_originalConfig.NumberConfig.Number),
                Crest = new CrestConfig(_originalConfig.Crest.TeamName)
            };

            _isDirty = false;
            RefreshPreview();
            OnReset?.Invoke();
        }

        private void RefreshPreview()
        {
            if (previewJerseyRenderer != null)
            {
                previewJerseyRenderer.ApplyJersey(_workingConfig);
            }
        }
    }
}
