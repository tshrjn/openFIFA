using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Applies team color to character models using MaterialPropertyBlock
    /// for GPU instancing compatibility. Configures humanoid avatar.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class HumanoidSetup : MonoBehaviour
    {
        [SerializeField] private int teamIndex;
        [SerializeField] private Renderer targetRenderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            _mpb = new MaterialPropertyBlock();
            ApplyTeamColor();
        }

        public void SetTeam(int team)
        {
            teamIndex = team;
            ApplyTeamColor();
        }

        private void ApplyTeamColor()
        {
            if (targetRenderer == null || _mpb == null) return;

            var config = new CharacterModelConfig();
            var assigner = new TeamColorAssigner();
            SimpleColor c = assigner.GetTeamColor(teamIndex, config);

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, new Color(c.R, c.G, c.B, c.A));
            targetRenderer.SetPropertyBlock(_mpb);
        }
    }
}
