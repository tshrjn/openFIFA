using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Stadium-specific LOD handling for stands, structural elements, and crowd sections.
    ///   LOD0: full geometry + crowd individuals
    ///   LOD1: simplified geometry + crowd batches (merged for draw call reduction)
    ///   LOD2: impostors for distant stands
    ///   Culled: disabled
    /// Manages per-section crowd mesh merging at LOD1 for draw call reduction.
    /// </summary>
    public class StadiumLODController : MonoBehaviour
    {
        [SerializeField] private Renderer _lod0FullGeometry;
        [SerializeField] private Renderer _lod1SimplifiedGeometry;
        [SerializeField] private Renderer _lod2ImpostorRenderer;
        [SerializeField] private Transform _crowdIndividualsRoot;
        [SerializeField] private Renderer _crowdBatchRenderer;
        [SerializeField] private float _sectionHeight = 10f;
        [SerializeField] private string _sectionId = "StadiumSection";

        private LODManager _lodManager;
        private LODRegistration _registration;
        private LODLevel _currentLevel = LODLevel.LOD0_High;
        private StadiumLODProfile _stadiumProfile;
        private CrowdLODProfile _crowdProfile;
        private bool _isCrowdMerged;

        /// <summary>Current LOD level for this stadium section.</summary>
        public LODLevel CurrentLevel => _currentLevel;

        /// <summary>Section identifier for this stadium LOD controller.</summary>
        public string SectionId => _sectionId;

        /// <summary>Whether crowd meshes are currently merged (LOD1 batch mode).</summary>
        public bool IsCrowdMerged => _isCrowdMerged;

        /// <summary>Event fired when LOD level changes.</summary>
        public event System.Action<LODLevel, LODLevel> OnLODChanged;

        private void Start()
        {
            _lodManager = FindFirstObjectByType<LODManager>();
            _stadiumProfile = new StadiumLODProfile();
            _crowdProfile = new CrowdLODProfile();

            if (_lodManager != null)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                var trisPerLevel = new int[]
                {
                    _stadiumProfile.LOD0Triangles,
                    _stadiumProfile.LOD1Triangles,
                    _stadiumProfile.LOD2Triangles,
                    _stadiumProfile.LOD2Triangles,
                    0
                };

                _registration = _lodManager.Register(
                    transform, renderers, _stadiumProfile, isCharacter: false,
                    _sectionHeight, trisPerLevel);
            }

            ApplyLOD(LODLevel.LOD0_High);
        }

        /// <summary>
        /// Initializes with explicit profiles and manager reference for testing.
        /// </summary>
        public void Initialize(LODManager manager, StadiumLODProfile stadiumProfile, CrowdLODProfile crowdProfile)
        {
            _lodManager = manager;
            _stadiumProfile = stadiumProfile ?? new StadiumLODProfile();
            _crowdProfile = crowdProfile ?? new CrowdLODProfile();
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
        }

        /// <summary>
        /// Applies the given LOD level, toggling stadium geometry and crowd rendering mode.
        /// </summary>
        public void ApplyLOD(LODLevel level)
        {
            _currentLevel = level;

            // Toggle stadium geometry renderers
            SetRendererActive(_lod0FullGeometry, level == LODLevel.LOD0_High);
            SetRendererActive(_lod1SimplifiedGeometry, level == LODLevel.LOD1_Medium);
            SetRendererActive(_lod2ImpostorRenderer,
                level == LODLevel.LOD2_Low || level == LODLevel.LOD3_Billboard);

            if (level == LODLevel.Culled)
            {
                SetRendererActive(_lod0FullGeometry, false);
                SetRendererActive(_lod1SimplifiedGeometry, false);
                SetRendererActive(_lod2ImpostorRenderer, false);
            }

            // Apply crowd LOD
            ApplyCrowdLOD(level);
        }

        /// <summary>
        /// Manages crowd rendering mode based on LOD level.
        /// LOD0: show individual crowd meshes, hide batch.
        /// LOD1: hide individuals, show merged batch (draw call reduction).
        /// LOD2+: hide all crowd or show billboard crowd sheet.
        /// </summary>
        private void ApplyCrowdLOD(LODLevel level)
        {
            bool showIndividuals = (level == LODLevel.LOD0_High);
            bool showBatch = (level == LODLevel.LOD1_Medium);

            // Toggle individual crowd members
            if (_crowdIndividualsRoot != null)
            {
                _crowdIndividualsRoot.gameObject.SetActive(showIndividuals);
            }

            // Toggle merged batch renderer
            SetRendererActive(_crowdBatchRenderer, showBatch);

            _isCrowdMerged = showBatch;
        }

        /// <summary>
        /// Performs crowd mesh merging for LOD1 draw call reduction.
        /// Combines individual crowd meshes into a single batch mesh.
        /// Should be called once when the section first transitions to LOD1.
        /// </summary>
        public void MergeCrowdMeshes()
        {
            if (_crowdIndividualsRoot == null || _crowdBatchRenderer == null) return;

            var meshFilters = _crowdIndividualsRoot.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0) return;

            var combineInstances = new CombineInstance[meshFilters.Length];
            for (int i = 0; i < meshFilters.Length; i++)
            {
                combineInstances[i].mesh = meshFilters[i].sharedMesh;
                combineInstances[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            var meshFilter = _crowdBatchRenderer.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                if (meshFilter.sharedMesh == null)
                {
                    meshFilter.sharedMesh = new Mesh();
                }
                meshFilter.sharedMesh.CombineMeshes(combineInstances, true, true);
            }

            _isCrowdMerged = true;
        }

        /// <summary>
        /// Returns the estimated draw call count for the current LOD level.
        /// LOD0: one per individual crowd member + stadium geometry.
        /// LOD1: one merged batch + simplified geometry.
        /// LOD2: single impostor draw call.
        /// </summary>
        public int EstimatedDrawCalls
        {
            get
            {
                int crowdCount = 0;
                if (_crowdIndividualsRoot != null)
                {
                    crowdCount = _crowdIndividualsRoot.childCount;
                }

                switch (_currentLevel)
                {
                    case LODLevel.LOD0_High:
                        return crowdCount + 1; // individuals + full geometry
                    case LODLevel.LOD1_Medium:
                        return 2; // merged batch + simplified geometry
                    case LODLevel.LOD2_Low:
                    case LODLevel.LOD3_Billboard:
                        return 1; // impostor only
                    case LODLevel.Culled:
                        return 0;
                    default:
                        return 0;
                }
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
