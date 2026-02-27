using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Simple RGBA color struct for pure C# (no UnityEngine dependency).
    /// </summary>
    public struct SimpleColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public SimpleColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static SimpleColor White => new SimpleColor(1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// Configuration for character models: polygon budget, team colors, rig type.
    /// Maps to AAA-quality character models with MaterialPropertyBlock coloring.
    /// </summary>
    public class CharacterModelConfig
    {
        /// <summary>Max triangle count per character model for AAA quality.</summary>
        public int MaxTrianglesPerModel = 30000;

        public int MinTrianglesPerModel = 10000;

        public int TextureResolution = 2048;

        /// <summary>Team A primary color (blue).</summary>
        public SimpleColor TeamAColor = new SimpleColor(0.1f, 0.2f, 0.85f);

        /// <summary>Team B primary color (red).</summary>
        public SimpleColor TeamBColor = new SimpleColor(0.85f, 0.15f, 0.1f);

        /// <summary>Avatar rig type for Humanoid animation retargeting.</summary>
        public string AvatarRigType = "Humanoid";

        /// <summary>Whether to use MaterialPropertyBlock for team colors (GPU instancing safe).</summary>
        public bool UseMaterialPropertyBlock = true;

        /// <summary>Shader property name for team color tinting.</summary>
        public string TeamColorShaderProperty = "_BaseColor";

        /// <summary>Animation states the model must support.</summary>
        public List<string> RequiredAnimationStates = new List<string>
        {
            "Idle", "Run", "Sprint", "Kick", "Tackle", "Celebrate"
        };
    }

    /// <summary>
    /// Pure C# logic for assigning team colors to players.
    /// </summary>
    public class TeamColorAssigner
    {
        /// <summary>
        /// Returns the team color for the given team index.
        /// 0 = Team A, 1 = Team B. Any other value returns white.
        /// </summary>
        public SimpleColor GetTeamColor(int teamIndex, CharacterModelConfig config)
        {
            switch (teamIndex)
            {
                case 0: return config.TeamAColor;
                case 1: return config.TeamBColor;
                default: return SimpleColor.White;
            }
        }
    }
}
