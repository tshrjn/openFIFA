using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Editor-time validator for ball model assets.
    /// Validates vertex count, UV quality, PBR texture completeness, and collider radius.
    /// </summary>
    public static class BallModelValidator
    {
        private static readonly BallModelConfig DefaultConfig = new BallModelConfig();

        /// <summary>
        /// Validates a ball model GameObject in the scene or project.
        /// Returns a BallModelValidationResult with errors and warnings.
        /// </summary>
        public static BallModelValidationResult ValidateBallGameObject(GameObject ballObject)
        {
            var result = new BallModelValidationResult { IsValid = true };

            if (ballObject == null)
            {
                result.AddError("Ball GameObject is null");
                return result;
            }

            // Validate mesh
            ValidateMesh(ballObject, result);

            // Validate collider
            ValidateCollider(ballObject, result);

            // Validate material / PBR setup
            ValidateMaterial(ballObject, result);

            return result;
        }

        /// <summary>
        /// Validates ball mesh: vertex count, triangle count, UV channels.
        /// </summary>
        public static void ValidateMesh(GameObject ballObject, BallModelValidationResult result)
        {
            var meshFilter = ballObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = ballObject.GetComponentInChildren<MeshFilter>();
            }

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                result.AddError("No MeshFilter or Mesh found on ball GameObject");
                return;
            }

            var mesh = meshFilter.sharedMesh;
            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles.Length / 3;

            // Vertex count validation
            if (!DefaultConfig.MeshConfig.IsVertexCountValid(vertexCount))
            {
                result.AddError(
                    $"Vertex count {vertexCount} exceeds budget of {DefaultConfig.MeshConfig.MaxVertexCount}");
            }

            // Triangle count validation
            if (!DefaultConfig.MeshConfig.IsTriangleCountValid(triangleCount))
            {
                result.AddError(
                    $"Triangle count {triangleCount} exceeds budget of {DefaultConfig.MeshConfig.MaxTriangleCount}");
            }

            // UV channel validation
            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                result.AddError("Mesh has no UV coordinates (UV0). PBR textures require UV mapping.");
            }
            else
            {
                // Check for degenerate UVs (all zeros)
                bool hasValidUV = false;
                foreach (var uv in mesh.uv)
                {
                    if (uv.sqrMagnitude > 0.0001f)
                    {
                        hasValidUV = true;
                        break;
                    }
                }
                if (!hasValidUV)
                {
                    result.AddWarning("All UV coordinates appear to be at origin. UV mapping may be incorrect.");
                }
            }

            // Normal validation
            if (mesh.normals == null || mesh.normals.Length == 0)
            {
                result.AddWarning("Mesh has no normals. Lighting may not work correctly.");
            }

            // Tangent validation (needed for normal mapping)
            if (mesh.tangents == null || mesh.tangents.Length == 0)
            {
                result.AddWarning("Mesh has no tangents. Normal mapping will not render correctly.");
            }
        }

        /// <summary>
        /// Validates the SphereCollider: existence, radius match, center position.
        /// </summary>
        public static void ValidateCollider(GameObject ballObject, BallModelValidationResult result)
        {
            var collider = ballObject.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = ballObject.GetComponentInChildren<SphereCollider>();
            }

            if (collider == null)
            {
                result.AddError("No SphereCollider found on ball GameObject");
                return;
            }

            // Check radius is within expected range
            float expectedRadius = DefaultConfig.ColliderConfig.Radius;
            float tolerance = 0.02f;
            if (Mathf.Abs(collider.radius - expectedRadius) > tolerance)
            {
                result.AddWarning(
                    $"Collider radius {collider.radius} differs from expected {expectedRadius} " +
                    $"by more than tolerance {tolerance}");
            }

            if (!DefaultConfig.ColliderConfig.IsRadiusValid())
            {
                result.AddError(
                    $"Collider radius {collider.radius} is outside valid range");
            }

            // Check center is at origin
            if (collider.center.sqrMagnitude > 0.001f)
            {
                result.AddWarning(
                    $"Collider center is at {collider.center}, expected (0,0,0). " +
                    "This may cause physics/visual mismatch.");
            }

            // Check physics material
            if (collider.sharedMaterial == null)
            {
                result.AddWarning("No PhysicsMaterial assigned to ball collider");
            }
        }

        /// <summary>
        /// Validates the PBR material setup: shader, texture slots, smoothness.
        /// </summary>
        public static void ValidateMaterial(GameObject ballObject, BallModelValidationResult result)
        {
            var renderer = ballObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = ballObject.GetComponentInChildren<Renderer>();
            }

            if (renderer == null)
            {
                result.AddError("No Renderer found on ball GameObject");
                return;
            }

            var material = renderer.sharedMaterial;
            if (material == null)
            {
                result.AddError("No material assigned to ball Renderer");
                return;
            }

            // Check shader
            if (material.shader == null || material.shader.name != DefaultConfig.ShaderName)
            {
                string shaderName = material.shader != null ? material.shader.name : "(null)";
                result.AddWarning(
                    $"Ball material uses shader '{shaderName}', expected '{DefaultConfig.ShaderName}'");
            }

            // Check for magenta (missing shader) — pink means shader compile error
            if (material.shader != null && material.shader.name == "Hidden/InternalErrorShader")
            {
                result.AddError("Ball material has a shader error (magenta/pink). Fix the shader reference.");
            }

            // Check albedo texture
            if (material.HasProperty("_BaseMap"))
            {
                var albedo = material.GetTexture("_BaseMap");
                if (albedo == null)
                {
                    result.AddWarning("No albedo texture (_BaseMap) assigned to ball material");
                }
            }

            // Check normal map
            if (material.HasProperty("_BumpMap"))
            {
                var normal = material.GetTexture("_BumpMap");
                if (normal == null)
                {
                    result.AddWarning("No normal map (_BumpMap) assigned to ball material");
                }
            }
        }

        /// <summary>
        /// Menu item to validate the selected ball model in the scene.
        /// </summary>
        [MenuItem("OpenFifa/Ball/Validate Selected Ball")]
        public static void ValidateSelectedBall()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[BallModelValidator] No GameObject selected.");
                return;
            }

            var validationResult = ValidateBallGameObject(selected);

            if (validationResult.IsValid && validationResult.Warnings.Count == 0)
            {
                Debug.Log("[BallModelValidator] Ball model validation PASSED with no issues.");
            }
            else
            {
                foreach (var error in validationResult.Errors)
                {
                    Debug.LogError($"[BallModelValidator] ERROR: {error}");
                }
                foreach (var warning in validationResult.Warnings)
                {
                    Debug.LogWarning($"[BallModelValidator] WARNING: {warning}");
                }

                string summary = validationResult.IsValid ? "PASSED with warnings" : "FAILED";
                Debug.Log(
                    $"[BallModelValidator] Validation {summary}: " +
                    $"{validationResult.Errors.Count} errors, {validationResult.Warnings.Count} warnings");
            }
        }

        /// <summary>
        /// Validates the ball model configuration (pure data, no scene objects required).
        /// </summary>
        [MenuItem("OpenFifa/Ball/Validate Ball Config")]
        public static void ValidateBallConfig()
        {
            var config = new BallModelConfig();
            var validationResult = config.ValidateBallModel();

            if (validationResult.IsValid)
            {
                Debug.Log("[BallModelValidator] Ball configuration validation PASSED.");
            }
            else
            {
                foreach (var error in validationResult.Errors)
                {
                    Debug.LogError($"[BallModelValidator] CONFIG ERROR: {error}");
                }
            }

            foreach (var warning in validationResult.Warnings)
            {
                Debug.LogWarning($"[BallModelValidator] CONFIG WARNING: {warning}");
            }
        }
    }
}
