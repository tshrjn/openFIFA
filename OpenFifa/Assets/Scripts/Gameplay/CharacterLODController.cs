using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Per-character LOD switching controller.
    /// Manages transitions between:
    ///   LOD0: full mesh + skeleton + IK
    ///   LOD1: reduced mesh, simplified skeleton
    ///   LOD2: billboard quad facing camera
    ///   Culled: disabled renderer
    /// Reduces Animator quality at lower LODs for performance.
    /// </summary>
    public class CharacterLODController : MonoBehaviour
    {
        [SerializeField] private Renderer _lod0Renderer;
        [SerializeField] private Renderer _lod1Renderer;
        [SerializeField] private Renderer _lod2BillboardRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _objectHeight = 1.8f;
        [SerializeField] private bool _useIKAtLOD0 = true;

        private LODManager _lodManager;
        private LODRegistration _registration;
        private LODLevel _currentLevel = LODLevel.LOD0_High;
        private CharacterLODProfile _profile;

        /// <summary>Current LOD level this character is rendered at.</summary>
        public LODLevel CurrentLevel => _currentLevel;

        /// <summary>The character LOD profile used by this controller.</summary>
        public CharacterLODProfile Profile => _profile;

        /// <summary>Whether IK is currently active (LOD0 only).</summary>
        public bool IsIKActive => _useIKAtLOD0 && _currentLevel == LODLevel.LOD0_High;

        /// <summary>Event fired when LOD level changes.</summary>
        public event System.Action<LODLevel, LODLevel> OnLODChanged;

        private void Start()
        {
            _lodManager = FindFirstObjectByType<LODManager>();
            _profile = new CharacterLODProfile();

            if (_lodManager != null)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                var trisPerLevel = new int[]
                {
                    _profile.LOD0Triangles,
                    _profile.LOD1Triangles,
                    _profile.LOD2Triangles,
                    _profile.LOD2Triangles,
                    0
                };

                _registration = _lodManager.Register(
                    transform, renderers, _profile, isCharacter: true,
                    _objectHeight, trisPerLevel);
            }

            ApplyLOD(LODLevel.LOD0_High);
        }

        /// <summary>
        /// Initializes the controller with an explicit profile and manager reference.
        /// Useful for testing or manual setup.
        /// </summary>
        public void Initialize(LODManager manager, CharacterLODProfile profile)
        {
            _lodManager = manager;
            _profile = profile ?? new CharacterLODProfile();
        }

        private void LateUpdate()
        {
            if (_registration == null) return;

            LODLevel newLevel = _registration.CurrentLevel;
            if (newLevel != _currentLevel)
            {
                var oldLevel = _currentLevel;
                ApplyLOD(newLevel);
                OnLODChanged?.Invoke(oldLevel, newLevel);
            }

            // Billboard always faces camera
            if (_currentLevel == LODLevel.LOD2_Low || _currentLevel == LODLevel.LOD3_Billboard)
            {
                FaceBillboardToCamera();
            }
        }

        /// <summary>
        /// Applies the given LOD level, toggling renderers and Animator quality.
        /// </summary>
        public void ApplyLOD(LODLevel level)
        {
            _currentLevel = level;

            // Toggle renderers
            SetRendererActive(_lod0Renderer, level == LODLevel.LOD0_High);
            SetRendererActive(_lod1Renderer, level == LODLevel.LOD1_Medium);
            SetRendererActive(_lod2BillboardRenderer,
                level == LODLevel.LOD2_Low || level == LODLevel.LOD3_Billboard);

            // If culled, disable all renderers
            if (level == LODLevel.Culled)
            {
                SetRendererActive(_lod0Renderer, false);
                SetRendererActive(_lod1Renderer, false);
                SetRendererActive(_lod2BillboardRenderer, false);
            }

            // Adjust Animator quality
            ApplyAnimatorQuality(level);
        }

        /// <summary>
        /// Adjusts Animator update mode and culling based on LOD level.
        /// LOD0: normal update, all bones evaluated, IK enabled.
        /// LOD1: normal update, reduced bones evaluated.
        /// LOD2+: culled animator — no bone evaluation.
        /// </summary>
        private void ApplyAnimatorQuality(LODLevel level)
        {
            if (_animator == null) return;

            switch (level)
            {
                case LODLevel.LOD0_High:
                    _animator.enabled = true;
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    break;

                case LODLevel.LOD1_Medium:
                    _animator.enabled = true;
                    _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    break;

                case LODLevel.LOD2_Low:
                case LODLevel.LOD3_Billboard:
                    _animator.enabled = true;
                    _animator.cullingMode = AnimatorCullingMode.CullCompletely;
                    break;

                case LODLevel.Culled:
                    _animator.enabled = false;
                    break;
            }
        }

        /// <summary>
        /// Rotates the billboard LOD2 renderer to face the main camera.
        /// </summary>
        private void FaceBillboardToCamera()
        {
            if (_lod2BillboardRenderer == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            var billboardTransform = _lod2BillboardRenderer.transform;
            Vector3 dirToCamera = cam.transform.position - billboardTransform.position;
            dirToCamera.y = 0f; // Keep upright
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                billboardTransform.rotation = Quaternion.LookRotation(dirToCamera);
            }
        }

        private static void SetRendererActive(Renderer renderer, bool active)
        {
            if (renderer != null)
            {
                renderer.enabled = active;
            }
        }

        private void OnDestroy()
        {
            if (_lodManager != null && _registration != null)
            {
                _lodManager.Unregister(_registration);
            }
        }
    }
}
