using System;
using System.Collections.Generic;
using System.Linq;

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
        public static SimpleColor Black => new SimpleColor(0f, 0f, 0f, 1f);
    }

    /// <summary>
    /// Identifies which part of the uniform a renderer corresponds to.
    /// </summary>
    public enum UniformSlot
    {
        Jersey = 0,
        Shorts = 1,
        Socks = 2
    }

    /// <summary>
    /// LOD configuration for character models — pure C# with no engine dependencies.
    /// LOD0 = full detail for close-up, LOD1 = reduced for mid-distance.
    /// </summary>
    public class LODConfig
    {
        /// <summary>LOD0 triangle budget — full detail (close-up broadcast view).</summary>
        public int LOD0MaxTriangles = 30000;

        /// <summary>LOD1 triangle budget — reduced for mid-distance.</summary>
        public int LOD1MaxTriangles = 5000;

        /// <summary>Screen-relative height threshold where LOD0 transitions to LOD1 (0-1 range).</summary>
        public float LOD0ScreenHeight = 0.4f;

        /// <summary>Screen-relative height threshold where LOD1 transitions to culled (0-1 range).</summary>
        public float LOD1ScreenHeight = 0.1f;

        /// <summary>Whether cross-fade dithering is enabled to reduce LOD popping.</summary>
        public bool CrossFadeEnabled = true;

        /// <summary>Duration in seconds for the cross-fade transition between LOD levels.</summary>
        public float CrossFadeDuration = 0.5f;

        /// <summary>
        /// Validates that a triangle count is within budget for the specified LOD level.
        /// </summary>
        /// <param name="triangleCount">Actual triangle count of the mesh.</param>
        /// <param name="lodLevel">0 = LOD0, 1 = LOD1.</param>
        /// <returns>True if within budget.</returns>
        public bool IsWithinBudget(int triangleCount, int lodLevel)
        {
            if (triangleCount < 0) return false;

            switch (lodLevel)
            {
                case 0: return triangleCount <= LOD0MaxTriangles;
                case 1: return triangleCount <= LOD1MaxTriangles;
                default: return false;
            }
        }

        /// <summary>
        /// Returns the max triangle budget for the given LOD level.
        /// </summary>
        public int GetMaxTriangles(int lodLevel)
        {
            switch (lodLevel)
            {
                case 0: return LOD0MaxTriangles;
                case 1: return LOD1MaxTriangles;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// Per-uniform-slot color configuration for a team. Each slot (Jersey, Shorts, Socks)
    /// can have an independent color for authentic kit design.
    /// </summary>
    public class TeamUniformConfig
    {
        /// <summary>Per-slot colors keyed by UniformSlot.</summary>
        public readonly Dictionary<UniformSlot, SimpleColor> SlotColors;

        /// <summary>Team display name.</summary>
        public string TeamName;

        public TeamUniformConfig(string teamName, SimpleColor jerseyColor, SimpleColor shortsColor, SimpleColor socksColor)
        {
            TeamName = teamName;
            SlotColors = new Dictionary<UniformSlot, SimpleColor>
            {
                { UniformSlot.Jersey, jerseyColor },
                { UniformSlot.Shorts, shortsColor },
                { UniformSlot.Socks, socksColor }
            };
        }

        /// <summary>
        /// Returns the color for the given uniform slot.
        /// Falls back to White if the slot is not configured.
        /// </summary>
        public SimpleColor GetSlotColor(UniformSlot slot)
        {
            SimpleColor color;
            if (SlotColors.TryGetValue(slot, out color))
                return color;
            return SimpleColor.White;
        }

        /// <summary>
        /// Sets the color for a specific uniform slot.
        /// </summary>
        public void SetSlotColor(UniformSlot slot, SimpleColor color)
        {
            SlotColors[slot] = color;
        }
    }

    /// <summary>
    /// Result of a full character model validation pass.
    /// </summary>
    public class CharacterValidationResult
    {
        /// <summary>Whether the model passed all validation checks.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Human-readable list of validation errors (empty if valid).</summary>
        public readonly List<string> Errors;

        /// <summary>Human-readable list of non-blocking warnings.</summary>
        public readonly List<string> Warnings;

        /// <summary>Total triangle count found during validation.</summary>
        public int TriangleCount { get; set; }

        /// <summary>Number of bones found in the rig.</summary>
        public int BoneCount { get; set; }

        /// <summary>Texture resolution found during validation.</summary>
        public int TextureResolution { get; set; }

        public CharacterValidationResult()
        {
            IsValid = true;
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        /// <summary>
        /// Adds an error and marks the result as invalid.
        /// </summary>
        public void AddError(string message)
        {
            IsValid = false;
            Errors.Add(message);
        }

        /// <summary>
        /// Adds a non-blocking warning without failing validation.
        /// </summary>
        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }
    }

    /// <summary>
    /// Bone structure requirements for humanoid character rigs.
    /// Ensures imported models have the skeleton needed for animation retargeting.
    /// </summary>
    public class BoneStructureRequirements
    {
        /// <summary>Minimum number of bones required for a humanoid rig.</summary>
        public int MinBoneCount = 55;

        /// <summary>
        /// Bones that must exist in the skeleton for humanoid animation retargeting.
        /// These map to Unity's required HumanBodyBones.
        /// </summary>
        public readonly List<string> RequiredBones = new List<string>
        {
            "Hips",
            "Spine",
            "Chest",
            "UpperChest",
            "Neck",
            "Head",
            "LeftShoulder",
            "LeftUpperArm",
            "LeftLowerArm",
            "LeftHand",
            "RightShoulder",
            "RightUpperArm",
            "RightLowerArm",
            "RightHand",
            "LeftUpperLeg",
            "LeftLowerLeg",
            "LeftFoot",
            "LeftToes",
            "RightUpperLeg",
            "RightLowerLeg",
            "RightFoot",
            "RightToes"
        };

        /// <summary>
        /// Validates that all required bones are present in the provided bone list.
        /// </summary>
        /// <param name="actualBones">List of bone names found in the model.</param>
        /// <returns>List of missing bone names (empty if all present).</returns>
        public List<string> FindMissingBones(IEnumerable<string> actualBones)
        {
            var boneSet = new HashSet<string>(actualBones, StringComparer.OrdinalIgnoreCase);
            return RequiredBones.Where(b => !boneSet.Contains(b)).ToList();
        }

        /// <summary>
        /// Checks whether the bone count meets the minimum requirement.
        /// </summary>
        public bool IsBoneCountValid(int boneCount)
        {
            return boneCount >= MinBoneCount;
        }
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

        /// <summary>Minimum texture resolution considered acceptable.</summary>
        public int MinTextureResolution = 512;

        /// <summary>Maximum texture resolution (memory budget constraint).</summary>
        public int MaxTextureResolution = 4096;

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

        /// <summary>LOD configuration for character meshes.</summary>
        public readonly LODConfig LOD = new LODConfig();

        /// <summary>Bone structure requirements for humanoid rigs.</summary>
        public readonly BoneStructureRequirements BoneRequirements = new BoneStructureRequirements();

        /// <summary>
        /// Team A full uniform config (jersey/shorts/socks).
        /// </summary>
        public TeamUniformConfig TeamAUniform = new TeamUniformConfig(
            "Team A",
            jerseyColor: new SimpleColor(0.1f, 0.2f, 0.85f),   // blue jersey
            shortsColor: new SimpleColor(0.1f, 0.1f, 0.6f),    // dark blue shorts
            socksColor: new SimpleColor(1f, 1f, 1f)             // white socks
        );

        /// <summary>
        /// Team B full uniform config (jersey/shorts/socks).
        /// </summary>
        public TeamUniformConfig TeamBUniform = new TeamUniformConfig(
            "Team B",
            jerseyColor: new SimpleColor(0.85f, 0.15f, 0.1f),  // red jersey
            shortsColor: new SimpleColor(0.6f, 0.1f, 0.1f),    // dark red shorts
            socksColor: new SimpleColor(1f, 1f, 1f)             // white socks
        );

        /// <summary>
        /// Validates that a triangle count is within the allowed range for the model.
        /// </summary>
        /// <param name="triangleCount">Actual triangle count.</param>
        /// <returns>True if within [MinTrianglesPerModel, MaxTrianglesPerModel].</returns>
        public bool IsTriangleCountValid(int triangleCount)
        {
            return triangleCount >= MinTrianglesPerModel && triangleCount <= MaxTrianglesPerModel;
        }

        /// <summary>
        /// Validates that a texture resolution is within the allowed range.
        /// </summary>
        /// <param name="resolution">Texture width or height in pixels.</param>
        /// <returns>True if within [MinTextureResolution, MaxTextureResolution].</returns>
        public bool IsTextureResolutionValid(int resolution)
        {
            return resolution >= MinTextureResolution && resolution <= MaxTextureResolution;
        }

        /// <summary>
        /// Validates that a texture resolution is a power of two (required for GPU mipmapping).
        /// </summary>
        public bool IsTextureResolutionPowerOfTwo(int resolution)
        {
            if (resolution <= 0) return false;
            return (resolution & (resolution - 1)) == 0;
        }

        /// <summary>
        /// Runs a full validation pass on a character model's metrics.
        /// </summary>
        /// <param name="triangleCount">Total triangle count of the model.</param>
        /// <param name="textureResolution">Texture resolution (width or height).</param>
        /// <param name="boneCount">Number of bones in the rig.</param>
        /// <param name="boneNames">Names of bones in the rig.</param>
        /// <returns>Validation result with errors and warnings.</returns>
        public CharacterValidationResult ValidateModel(
            int triangleCount,
            int textureResolution,
            int boneCount,
            IEnumerable<string> boneNames)
        {
            var result = new CharacterValidationResult
            {
                TriangleCount = triangleCount,
                BoneCount = boneCount,
                TextureResolution = textureResolution
            };

            // Triangle count validation
            if (triangleCount < MinTrianglesPerModel)
            {
                result.AddError(
                    $"Triangle count {triangleCount} is below minimum {MinTrianglesPerModel}. " +
                    "Model may lack sufficient detail for AAA quality.");
            }
            else if (triangleCount > MaxTrianglesPerModel)
            {
                result.AddError(
                    $"Triangle count {triangleCount} exceeds maximum {MaxTrianglesPerModel}. " +
                    "Model needs optimization for performance budget.");
            }

            // LOD0 budget check (should match MaxTrianglesPerModel)
            if (!LOD.IsWithinBudget(triangleCount, 0))
            {
                result.AddWarning(
                    $"Triangle count {triangleCount} exceeds LOD0 budget of {LOD.LOD0MaxTriangles}.");
            }

            // Texture resolution validation
            if (!IsTextureResolutionValid(textureResolution))
            {
                result.AddError(
                    $"Texture resolution {textureResolution} is outside valid range " +
                    $"[{MinTextureResolution}, {MaxTextureResolution}].");
            }

            if (!IsTextureResolutionPowerOfTwo(textureResolution))
            {
                result.AddWarning(
                    $"Texture resolution {textureResolution} is not a power of two. " +
                    "This may cause issues with mipmapping and GPU compression.");
            }

            // Bone count validation
            if (!BoneRequirements.IsBoneCountValid(boneCount))
            {
                result.AddError(
                    $"Bone count {boneCount} is below minimum {BoneRequirements.MinBoneCount}. " +
                    "Humanoid animation retargeting requires more bones.");
            }

            // Required bone validation
            var boneList = boneNames != null ? boneNames : new List<string>();
            var missingBones = BoneRequirements.FindMissingBones(boneList);
            if (missingBones.Count > 0)
            {
                result.AddError(
                    $"Missing {missingBones.Count} required bone(s): {string.Join(", ", missingBones)}. " +
                    "These are needed for humanoid animation retargeting.");
            }

            return result;
        }
    }

    /// <summary>
    /// Pure C# logic for assigning team colors to players.
    /// Supports both simple (single color) and per-slot (uniform) assignments.
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

        /// <summary>
        /// Returns the team uniform config for the given team index.
        /// 0 = Team A, 1 = Team B. Any other value returns null.
        /// </summary>
        public TeamUniformConfig GetTeamUniform(int teamIndex, CharacterModelConfig config)
        {
            switch (teamIndex)
            {
                case 0: return config.TeamAUniform;
                case 1: return config.TeamBUniform;
                default: return null;
            }
        }

        /// <summary>
        /// Returns the color for a specific uniform slot on the given team.
        /// Falls back to the team's primary color if uniform config is null.
        /// </summary>
        public SimpleColor GetSlotColor(int teamIndex, UniformSlot slot, CharacterModelConfig config)
        {
            var uniform = GetTeamUniform(teamIndex, config);
            if (uniform != null)
                return uniform.GetSlotColor(slot);

            // Fallback to simple team color
            return GetTeamColor(teamIndex, config);
        }
    }
}
