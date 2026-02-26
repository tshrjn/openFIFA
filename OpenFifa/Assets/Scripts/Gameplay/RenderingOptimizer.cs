using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Applies rendering optimizations: GPU instancing on materials,
    /// static batching flags, and shared team materials with MaterialPropertyBlock.
    /// </summary>
    public class RenderingOptimizer : MonoBehaviour
    {
        [SerializeField] private Material _teamAMaterial;
        [SerializeField] private Material _teamBMaterial;
        [SerializeField] private Color _teamAColor = Color.blue;
        [SerializeField] private Color _teamBColor = Color.red;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            EnableGPUInstancing();
        }

        /// <summary>
        /// Enable GPU instancing on all provided materials.
        /// </summary>
        public void EnableGPUInstancing()
        {
            if (_teamAMaterial != null)
                _teamAMaterial.enableInstancing = true;

            if (_teamBMaterial != null)
                _teamBMaterial.enableInstancing = true;
        }

        /// <summary>
        /// Apply team color to a renderer using MaterialPropertyBlock (preserves batching).
        /// </summary>
        public void ApplyTeamColor(Renderer renderer, TeamIdentifier team)
        {
            if (renderer == null) return;

            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);

            Color color = team == TeamIdentifier.TeamA ? _teamAColor : _teamBColor;
            mpb.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Apply shared team material to a renderer.
        /// </summary>
        public void ApplyTeamMaterial(Renderer renderer, TeamIdentifier team)
        {
            if (renderer == null) return;

            Material mat = team == TeamIdentifier.TeamA ? _teamAMaterial : _teamBMaterial;
            if (mat != null)
                renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Mark a GameObject and children as static for batching.
        /// </summary>
        public static void MarkAsStatic(GameObject go)
        {
            if (go == null) return;
            go.isStatic = true;

            foreach (Transform child in go.transform)
            {
                MarkAsStatic(child.gameObject);
            }
        }
    }
}
