using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Defines a complete set of PBR texture paths for a soccer ball material.
    /// All paths are relative to the Unity Assets folder.
    /// </summary>
    public class PBRTextureSet
    {
        /// <summary>Albedo (base color) texture path — classic pentagon/hexagon pattern.</summary>
        public string AlbedoPath = "Textures/Ball/SoccerBall_Albedo";

        /// <summary>Normal map texture path for panel seam detail.</summary>
        public string NormalPath = "Textures/Ball/SoccerBall_Normal";

        /// <summary>Metallic map texture path (should be near-zero for synthetic leather).</summary>
        public string MetallicPath = "Textures/Ball/SoccerBall_Metallic";

        /// <summary>Roughness map texture path (inverted smoothness).</summary>
        public string RoughnessPath = "Textures/Ball/SoccerBall_Roughness";

        /// <summary>Ambient occlusion map texture path for panel crevice shadows.</summary>
        public string AOPath = "Textures/Ball/SoccerBall_AO";

        /// <summary>
        /// Returns true if all five PBR texture paths are assigned and non-empty.
        /// </summary>
        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(AlbedoPath)
                && !string.IsNullOrEmpty(NormalPath)
                && !string.IsNullOrEmpty(MetallicPath)
                && !string.IsNullOrEmpty(RoughnessPath)
                && !string.IsNullOrEmpty(AOPath);
        }

        /// <summary>
        /// Returns a list of any missing texture paths for diagnostic reporting.
        /// </summary>
        public List<string> GetMissingTextures()
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(AlbedoPath)) missing.Add("Albedo");
            if (string.IsNullOrEmpty(NormalPath)) missing.Add("Normal");
            if (string.IsNullOrEmpty(MetallicPath)) missing.Add("Metallic");
            if (string.IsNullOrEmpty(RoughnessPath)) missing.Add("Roughness");
            if (string.IsNullOrEmpty(AOPath)) missing.Add("AO");
            return missing;
        }
    }

    /// <summary>
    /// Mesh configuration for the ball model: vertex budget, UV mapping, panel count.
    /// </summary>
    public class BallMeshConfig
    {
        /// <summary>Maximum vertex count budget for LOD0.</summary>
        public int MaxVertexCount = 2500;

        /// <summary>Maximum triangle count budget for LOD0.</summary>
        public int MaxTriangleCount = 5000;

        /// <summary>UV mapping type (Spherical for soccer balls).</summary>
        public string UVMappingType = "Spherical";

        /// <summary>Number of panels on the ball surface (32 for classic, 6 for modern).</summary>
        public int PanelCount = 32;

        /// <summary>Supported UV mapping types.</summary>
        public static readonly string[] ValidUVMappingTypes = { "Spherical", "Cylindrical", "Planar", "Box" };

        /// <summary>
        /// Returns true if the given vertex count is within budget.
        /// </summary>
        public bool IsVertexCountValid(int vertexCount)
        {
            return vertexCount > 0 && vertexCount <= MaxVertexCount;
        }

        /// <summary>
        /// Returns true if the given triangle count is within budget.
        /// </summary>
        public bool IsTriangleCountValid(int triangleCount)
        {
            return triangleCount > 0 && triangleCount <= MaxTriangleCount;
        }

        /// <summary>
        /// Returns true if the UV mapping type is in the valid set.
        /// </summary>
        public bool IsUVMappingValid()
        {
            foreach (var valid in ValidUVMappingTypes)
            {
                if (string.Equals(UVMappingType, valid, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Collider configuration for the ball: radius, physics material parameters.
    /// </summary>
    public class BallColliderConfig
    {
        /// <summary>SphereCollider radius in meters (FIFA regulation ball ~0.11m).</summary>
        public float Radius = 0.11f;

        /// <summary>Bounciness of the ball physics material (0 = no bounce, 1 = perfect).</summary>
        public float Bounciness = 0.6f;

        /// <summary>Static friction coefficient.</summary>
        public float StaticFriction = 0.4f;

        /// <summary>Dynamic friction coefficient.</summary>
        public float DynamicFriction = 0.35f;

        /// <summary>Friction combine mode name (Average, Minimum, Maximum, Multiply).</summary>
        public string FrictionCombine = "Average";

        /// <summary>Bounce combine mode name.</summary>
        public string BounceCombine = "Average";

        /// <summary>
        /// Returns true if the radius is a positive value within realistic bounds.
        /// FIFA regulation ball radius: 0.11m +/- 0.005m. Allow 0.05 - 0.5 range for flexibility.
        /// </summary>
        public bool IsRadiusValid()
        {
            return Radius > 0.05f && Radius < 0.5f;
        }

        /// <summary>
        /// Returns true if bounciness is within 0..1 range.
        /// </summary>
        public bool IsBouncinessValid()
        {
            return Bounciness >= 0f && Bounciness <= 1f;
        }

        /// <summary>
        /// Returns true if friction values are non-negative.
        /// </summary>
        public bool IsFrictionValid()
        {
            return StaticFriction >= 0f && DynamicFriction >= 0f;
        }
    }

    /// <summary>
    /// LOD (Level of Detail) configuration for the ball model.
    /// </summary>
    public class BallLODConfig
    {
        /// <summary>Vertex count for LOD0 (highest detail, close-up).</summary>
        public int LOD0VertexCount = 2500;

        /// <summary>Vertex count for LOD1 (medium detail, broadcast view).</summary>
        public int LOD1VertexCount = 500;

        /// <summary>Vertex count for LOD2 (lowest detail, far away).</summary>
        public int LOD2VertexCount = 128;

        /// <summary>Camera distance at which LOD0 transitions to LOD1 (meters).</summary>
        public float LOD0TransitionDistance = 15f;

        /// <summary>Camera distance at which LOD1 transitions to LOD2 (meters).</summary>
        public float LOD1TransitionDistance = 40f;

        /// <summary>Camera distance beyond which the ball is culled (meters).</summary>
        public float CullDistance = 100f;

        /// <summary>
        /// Returns true if LOD vertex counts descend properly (LOD0 > LOD1 > LOD2 > 0).
        /// </summary>
        public bool AreVertexCountsValid()
        {
            return LOD0VertexCount > LOD1VertexCount
                && LOD1VertexCount > LOD2VertexCount
                && LOD2VertexCount > 0;
        }

        /// <summary>
        /// Returns true if transition distances increase properly (LOD0 < LOD1 < Cull).
        /// </summary>
        public bool AreTransitionDistancesValid()
        {
            return LOD0TransitionDistance > 0f
                && LOD1TransitionDistance > LOD0TransitionDistance
                && CullDistance > LOD1TransitionDistance;
        }

        /// <summary>
        /// Returns the appropriate LOD level (0, 1, 2, or -1 for culled) for the given distance.
        /// </summary>
        public int GetLODLevel(float cameraDistance)
        {
            if (cameraDistance < 0f) return 0;
            if (cameraDistance < LOD0TransitionDistance) return 0;
            if (cameraDistance < LOD1TransitionDistance) return 1;
            if (cameraDistance < CullDistance) return 2;
            return -1; // culled
        }
    }

    /// <summary>
    /// Comprehensive validation result for ball model configuration.
    /// </summary>
    public class BallModelValidationResult
    {
        public bool IsValid;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public void AddError(string message)
        {
            Errors.Add(message);
            IsValid = false;
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }
    }

    /// <summary>
    /// Configuration for the soccer ball 3D model and PBR material.
    /// Defines polygon budget, texture names, shader settings, and collider radius.
    /// </summary>
    public class BallModelConfig
    {
        /// <summary>Maximum triangle count for high-fidelity ball model.</summary>
        public int MaxTriangles = 5000;

        public int TextureResolution = 4096;

        /// <summary>Albedo texture name (classic black/white pentagon pattern).</summary>
        public string AlbedoTextureName = "SoccerBall_Albedo";

        /// <summary>Normal map name for panel seam detail.</summary>
        public string NormalMapName = "SoccerBall_Normal";

        /// <summary>Roughness/smoothness texture name.</summary>
        public string SmoothnessMapName = "SoccerBall_Smoothness";

        /// <summary>Material smoothness value (slight sheen).</summary>
        public float Smoothness = 0.4f;

        /// <summary>URP Lit shader for PBR rendering.</summary>
        public string ShaderName = "Universal Render Pipeline/Lit";

        /// <summary>Whether the mesh pivot is at the center (required for correct rotation).</summary>
        public bool PivotAtCenter = true;

        /// <summary>SphereCollider radius matching the visual mesh bounds.</summary>
        public float SphereColliderRadius = 0.11f;

        /// <summary>Whether visual rotation is driven by Rigidbody angular velocity.</summary>
        public bool RotationFromAngularVelocity = true;

        /// <summary>Model import path relative to Assets folder.</summary>
        public string ModelPath = "Models/Ball/SoccerBall.fbx";

        /// <summary>Full PBR texture set for the ball material.</summary>
        public PBRTextureSet PBRTextures = new PBRTextureSet();

        /// <summary>Mesh polygon budget and UV configuration.</summary>
        public BallMeshConfig MeshConfig = new BallMeshConfig();

        /// <summary>Collider and physics material settings.</summary>
        public BallColliderConfig ColliderConfig = new BallColliderConfig();

        /// <summary>LOD configuration for distance-based detail levels.</summary>
        public BallLODConfig LODConfig = new BallLODConfig();

        /// <summary>
        /// Validates the entire ball model configuration and returns a detailed result.
        /// </summary>
        public BallModelValidationResult ValidateBallModel()
        {
            var result = new BallModelValidationResult { IsValid = true };

            // Validate PBR texture set
            if (!PBRTextures.IsComplete())
            {
                var missing = PBRTextures.GetMissingTextures();
                result.AddError($"Incomplete PBR texture set. Missing: {string.Join(", ", missing)}");
            }

            // Validate mesh config
            if (!MeshConfig.IsTriangleCountValid(MaxTriangles))
            {
                result.AddError($"Triangle count {MaxTriangles} exceeds mesh budget of {MeshConfig.MaxTriangleCount}");
            }

            if (!MeshConfig.IsUVMappingValid())
            {
                result.AddError($"Invalid UV mapping type: {MeshConfig.UVMappingType}");
            }

            // Validate collider
            if (!ColliderConfig.IsRadiusValid())
            {
                result.AddError($"Collider radius {ColliderConfig.Radius} is outside valid range (0.05-0.5)");
            }

            if (!ColliderConfig.IsBouncinessValid())
            {
                result.AddError($"Bounciness {ColliderConfig.Bounciness} must be in range 0..1");
            }

            // Validate LOD
            if (!LODConfig.AreVertexCountsValid())
            {
                result.AddError("LOD vertex counts must descend: LOD0 > LOD1 > LOD2 > 0");
            }

            if (!LODConfig.AreTransitionDistancesValid())
            {
                result.AddError("LOD transition distances must increase: LOD0 < LOD1 < Cull");
            }

            // Validate shader name
            if (string.IsNullOrEmpty(ShaderName))
            {
                result.AddError("Shader name must not be empty");
            }

            // Validate model path
            if (string.IsNullOrEmpty(ModelPath))
            {
                result.AddError("Model path must not be empty");
            }

            // Warnings for common issues
            if (TextureResolution < 2048)
            {
                result.AddWarning($"Texture resolution {TextureResolution} is below recommended 2048 for AAA quality");
            }

            if (Smoothness < 0.2f || Smoothness > 0.7f)
            {
                result.AddWarning($"Smoothness {Smoothness} is outside typical range (0.2-0.7) for a soccer ball");
            }

            return result;
        }

        /// <summary>
        /// Convenience method: returns true if vertex count is within the mesh budget.
        /// </summary>
        public bool IsVertexCountValid(int vertexCount)
        {
            return MeshConfig.IsVertexCountValid(vertexCount);
        }

        /// <summary>
        /// Convenience method: returns true if the full PBR texture set is assigned.
        /// </summary>
        public bool IsPBRSetComplete()
        {
            return PBRTextures.IsComplete();
        }
    }
}
