using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Static validation utilities for character models.
    /// Checks triangle counts per LOD, bone structure, required material slots,
    /// and LODGroup configuration against AAA quality standards.
    /// </summary>
    public static class CharacterModelValidator
    {
        /// <summary>Expected material slot names for a character uniform.</summary>
        public static readonly string[] RequiredMaterialSlots = new[]
        {
            "Jersey",
            "Shorts",
            "Socks"
        };

        // ─── Triangle Count Validation ──────────────────────────────────

        /// <summary>
        /// Validates the total triangle count of a model against LOD budgets.
        /// </summary>
        /// <param name="root">Root GameObject of the character model.</param>
        /// <param name="lodLevel">LOD level to validate against (0 or 1).</param>
        /// <returns>Validation result.</returns>
        public static CharacterValidationResult ValidateTriangleCount(GameObject root, int lodLevel = 0)
        {
            var result = new CharacterValidationResult();
            var config = new CharacterModelConfig();

            int totalTriangles = CountTriangles(root);
            result.TriangleCount = totalTriangles;

            int maxBudget = config.LOD.GetMaxTriangles(lodLevel);

            if (totalTriangles > maxBudget)
            {
                result.AddError(
                    $"LOD{lodLevel} triangle count {totalTriangles} exceeds budget of {maxBudget}. " +
                    $"Reduce by {totalTriangles - maxBudget} triangles.");
            }

            if (lodLevel == 0 && totalTriangles < config.MinTrianglesPerModel)
            {
                result.AddWarning(
                    $"LOD0 triangle count {totalTriangles} is below minimum {config.MinTrianglesPerModel}. " +
                    "Model may lack sufficient detail.");
            }

            return result;
        }

        /// <summary>
        /// Counts total triangles across all mesh types in the hierarchy.
        /// </summary>
        public static int CountTriangles(GameObject root)
        {
            int total = 0;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null)
                    total += mf.sharedMesh.triangles.Length / 3;
            }

            foreach (var sr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (sr.sharedMesh != null)
                    total += sr.sharedMesh.triangles.Length / 3;
            }

            return total;
        }

        // ─── Bone Structure Validation ──────────────────────────────────

        /// <summary>
        /// Validates the bone structure of a character model's Animator.
        /// </summary>
        /// <param name="root">Root GameObject with an Animator component.</param>
        /// <returns>Validation result with bone-related errors.</returns>
        public static CharacterValidationResult ValidateBoneStructure(GameObject root)
        {
            var result = new CharacterValidationResult();
            var config = new CharacterModelConfig();

            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                result.AddError("No Animator component found on model or children.");
                return result;
            }

            if (animator.avatar == null)
            {
                result.AddError("Animator has no Avatar assigned. Configure Humanoid rig in import settings.");
                return result;
            }

            if (!animator.avatar.isHuman)
            {
                result.AddError("Avatar is not configured as Humanoid. Change rig type to Humanoid in import settings.");
                return result;
            }

            // Collect bone names from the skeleton
            var boneNames = CollectBoneNames(root);
            result.BoneCount = boneNames.Count;

            if (!config.BoneRequirements.IsBoneCountValid(boneNames.Count))
            {
                result.AddError(
                    $"Bone count {boneNames.Count} is below minimum {config.BoneRequirements.MinBoneCount}.");
            }

            var missing = config.BoneRequirements.FindMissingBones(boneNames);
            if (missing.Count > 0)
            {
                result.AddError(
                    $"Missing required bones: {string.Join(", ", missing)}");
            }

            return result;
        }

        /// <summary>
        /// Collects all bone/transform names from the hierarchy.
        /// </summary>
        public static List<string> CollectBoneNames(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>()
                       .Select(t => t.name)
                       .ToList();
        }

        // ─── Material Slot Validation ───────────────────────────────────

        /// <summary>
        /// Validates that the model has the expected material slots for uniform rendering.
        /// </summary>
        /// <param name="root">Root GameObject of the character model.</param>
        /// <returns>Validation result with material-related errors.</returns>
        public static CharacterValidationResult ValidateMaterialSlots(GameObject root)
        {
            var result = new CharacterValidationResult();

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                result.AddError("No Renderer components found on model.");
                return result;
            }

            // Collect all material names
            var materialNames = new HashSet<string>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                        materialNames.Add(mat.name);
                }
            }

            // Check for required slots (by name convention)
            foreach (var requiredSlot in RequiredMaterialSlots)
            {
                bool found = materialNames.Any(name =>
                    name.IndexOf(requiredSlot, System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (!found)
                {
                    result.AddWarning(
                        $"No material matching '{requiredSlot}' found. " +
                        "Team color per-slot assignment may use fallback single-renderer mode.");
                }
            }

            // Check for URP compatibility
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader != null)
            {
                foreach (var renderer in renderers)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.shader != urpLitShader)
                        {
                            result.AddWarning(
                                $"Material '{mat.name}' uses shader '{mat.shader.name}' " +
                                "instead of URP Lit. Run 'OpenFifa > Characters > Setup URP Materials'.");
                        }
                    }
                }
            }

            return result;
        }

        // ─── LODGroup Validation ────────────────────────────────────────

        /// <summary>
        /// Validates LODGroup configuration on a character model.
        /// </summary>
        /// <param name="root">Root GameObject of the character model.</param>
        /// <returns>Validation result with LOD-related errors.</returns>
        public static CharacterValidationResult ValidateLODGroup(GameObject root)
        {
            var result = new CharacterValidationResult();
            var config = new CharacterModelConfig();

            var lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                result.AddWarning("No LODGroup found. Character will render at full detail at all distances.");
                return result;
            }

            var lods = lodGroup.GetLODs();
            if (lods.Length < 2)
            {
                result.AddWarning(
                    $"LODGroup has only {lods.Length} level(s). Expected at least 2 (LOD0 + LOD1) for performance.");
            }

            // Validate each LOD level
            for (int i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];
                if (lod.renderers == null || lod.renderers.Length == 0)
                {
                    result.AddError($"LOD{i} has no renderers assigned.");
                    continue;
                }

                // Count triangles for this LOD level
                int lodTriangles = 0;
                foreach (var renderer in lod.renderers)
                {
                    if (renderer == null) continue;

                    var mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        lodTriangles += mf.sharedMesh.triangles.Length / 3;

                    var sr = renderer as SkinnedMeshRenderer;
                    if (sr != null && sr.sharedMesh != null)
                        lodTriangles += sr.sharedMesh.triangles.Length / 3;
                }

                int budget = config.LOD.GetMaxTriangles(i);
                if (budget > 0 && lodTriangles > budget)
                {
                    result.AddError(
                        $"LOD{i} has {lodTriangles} triangles, exceeding budget of {budget}.");
                }
            }

            return result;
        }

        // ─── Full Validation ────────────────────────────────────────────

        /// <summary>
        /// Runs all validation checks on a character model and returns a combined result.
        /// </summary>
        /// <param name="root">Root GameObject of the character model.</param>
        /// <returns>Combined validation result.</returns>
        public static CharacterValidationResult ValidateAll(GameObject root)
        {
            var combined = new CharacterValidationResult();

            var triResult = ValidateTriangleCount(root);
            var boneResult = ValidateBoneStructure(root);
            var matResult = ValidateMaterialSlots(root);
            var lodResult = ValidateLODGroup(root);

            MergeResult(combined, triResult);
            MergeResult(combined, boneResult);
            MergeResult(combined, matResult);
            MergeResult(combined, lodResult);

            combined.TriangleCount = triResult.TriangleCount;
            combined.BoneCount = boneResult.BoneCount;

            return combined;
        }

        /// <summary>
        /// Runs full validation and logs results to the Unity console.
        /// </summary>
        /// <param name="root">Root GameObject of the character model.</param>
        /// <param name="assetPath">Asset path for log messages.</param>
        public static void ValidateAndLog(GameObject root, string assetPath = "")
        {
            var result = ValidateAll(root);
            string modelName = string.IsNullOrEmpty(assetPath) ? root.name : assetPath;

            if (result.IsValid && result.Warnings.Count == 0)
            {
                Debug.Log(
                    $"[CharacterValidator] PASS: '{modelName}' — " +
                    $"{result.TriangleCount} tris, {result.BoneCount} bones. All checks passed.");
            }
            else if (result.IsValid)
            {
                Debug.LogWarning(
                    $"[CharacterValidator] PASS with warnings: '{modelName}' — " +
                    $"{result.TriangleCount} tris, {result.BoneCount} bones.\n" +
                    $"Warnings:\n  - {string.Join("\n  - ", result.Warnings)}");
            }
            else
            {
                Debug.LogError(
                    $"[CharacterValidator] FAIL: '{modelName}' — " +
                    $"{result.TriangleCount} tris, {result.BoneCount} bones.\n" +
                    $"Errors:\n  - {string.Join("\n  - ", result.Errors)}" +
                    (result.Warnings.Count > 0
                        ? $"\nWarnings:\n  - {string.Join("\n  - ", result.Warnings)}"
                        : ""));
            }
        }

        private static void MergeResult(CharacterValidationResult target, CharacterValidationResult source)
        {
            foreach (var error in source.Errors)
                target.AddError(error);
            foreach (var warning in source.Warnings)
                target.AddWarning(warning);
        }
    }
}
