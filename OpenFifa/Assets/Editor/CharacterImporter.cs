using UnityEditor;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// AssetPostprocessor for FBX files under Assets/Models/Characters/.
    /// Auto-configures humanoid rig, material extraction, and LOD groups
    /// on import to ensure all character models meet AAA standards.
    /// </summary>
    public class CharacterImporter : AssetPostprocessor
    {
        /// <summary>Path prefix that triggers character import processing.</summary>
        public const string CharacterModelPath = "Assets/Models/Characters/";

        /// <summary>Max texture dimension for character textures.</summary>
        public const int MaxTextureSize = 2048;

        /// <summary>LOD0 triangle budget.</summary>
        public const int LOD0TriangleBudget = 30000;

        /// <summary>LOD1 triangle budget.</summary>
        public const int LOD1TriangleBudget = 5000;

        /// <summary>LOD0 screen relative transition height.</summary>
        public const float LOD0ScreenHeight = 0.4f;

        /// <summary>LOD1 screen relative transition height.</summary>
        public const float LOD1ScreenHeight = 0.1f;

        private bool IsCharacterModel => assetPath.StartsWith(CharacterModelPath) &&
                                          assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Called before the model is imported. Configures humanoid rig and import settings.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!IsCharacterModel) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Configure humanoid rig for animation retargeting
            importer.animationType = ModelImporterAnimationType.Human;

            // Import materials for team color assignment
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // Mesh optimization
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            // Normal import for smooth shading
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            // Ensure blendshapes are imported for facial animation
            importer.importBlendShapes = true;

            // Read/write mesh data for runtime access (needed for LOD validation)
            importer.isReadable = true;

            Debug.Log($"[CharacterImporter] Configured humanoid rig for: {assetPath}");
        }

        /// <summary>
        /// Called before texture import. Configures resolution and compression.
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CharacterModelPath)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            // Set max texture size
            importer.maxTextureSize = MaxTextureSize;

            // Enable mipmaps for LOD-appropriate filtering
            importer.mipmapEnabled = true;

            // Use high quality compression
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            // Anisotropic filtering for oblique viewing angles
            importer.anisoLevel = 4;

            Debug.Log($"[CharacterImporter] Configured texture settings for: {assetPath}");
        }

        /// <summary>
        /// Called after the model is imported. Sets up LOD groups and validates.
        /// </summary>
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsCharacterModel) return;

            // Attempt to auto-configure LODGroup if the model has LOD meshes
            SetupLODGroup(root);

            // Log validation info
            var meshFilters = root.GetComponentsInChildren<MeshFilter>();
            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();

            int totalTriangles = 0;
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                    totalTriangles += mf.sharedMesh.triangles.Length / 3;
            }
            foreach (var sr in skinnedRenderers)
            {
                if (sr.sharedMesh != null)
                    totalTriangles += sr.sharedMesh.triangles.Length / 3;
            }

            if (totalTriangles > LOD0TriangleBudget)
            {
                Debug.LogWarning(
                    $"[CharacterImporter] Model '{assetPath}' has {totalTriangles} triangles, " +
                    $"exceeding LOD0 budget of {LOD0TriangleBudget}. Consider optimizing.");
            }

            Debug.Log(
                $"[CharacterImporter] Imported '{assetPath}': {totalTriangles} triangles, " +
                $"{meshFilters.Length} mesh filters, {skinnedRenderers.Length} skinned renderers.");
        }

        /// <summary>
        /// Attempts to configure a LODGroup on the imported model.
        /// Looks for child objects named with LOD0/LOD1 conventions.
        /// </summary>
        private void SetupLODGroup(GameObject root)
        {
            var lod0 = FindChildRenderers(root, "LOD0");
            var lod1 = FindChildRenderers(root, "LOD1");

            if (lod0.Length == 0) return; // No LOD convention found, skip

            var lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            if (lod1.Length > 0)
            {
                // Two-level LOD
                var lods = new LOD[2];
                lods[0] = new LOD(LOD0ScreenHeight, lod0);
                lods[1] = new LOD(LOD1ScreenHeight, lod1);
                lodGroup.SetLODs(lods);
            }
            else
            {
                // Single LOD level (LOD0 only, cull at low screen height)
                var lods = new LOD[1];
                lods[0] = new LOD(LOD1ScreenHeight, lod0);
                lodGroup.SetLODs(lods);
            }

            lodGroup.RecalculateBounds();
            Debug.Log($"[CharacterImporter] Configured LODGroup with {(lod1.Length > 0 ? 2 : 1)} levels.");
        }

        /// <summary>
        /// Finds renderers on child objects whose names contain the given LOD tag.
        /// </summary>
        private Renderer[] FindChildRenderers(GameObject root, string lodTag)
        {
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains(lodTag, System.StringComparison.OrdinalIgnoreCase))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                        renderers.Add(renderer);
                }
            }
            return renderers.ToArray();
        }

        // ─── MenuItem Actions ───────────────────────────────────────────

        /// <summary>
        /// Validates the currently selected character model in the Project window.
        /// </summary>
        [MenuItem("OpenFifa/Characters/Validate Selected Model")]
        public static void ValidateSelectedModel()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                Debug.LogError("[CharacterImporter] No asset selected. Select a character model in the Project window.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[CharacterImporter] Selected asset is not an FBX file.");
                return;
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"[CharacterImporter] Could not load model at: {path}");
                return;
            }

            // Run validation
            CharacterModelValidator.ValidateAndLog(go, path);
        }

        /// <summary>
        /// Sets up URP-compatible materials on the selected character model.
        /// </summary>
        [MenuItem("OpenFifa/Characters/Setup URP Materials")]
        public static void SetupURPMaterials()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                Debug.LogError("[CharacterImporter] No GameObject selected.");
                return;
            }

            var renderers = selected.GetComponentsInChildren<Renderer>();
            int materialCount = 0;

            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("[CharacterImporter] URP Lit shader not found. Ensure URP is installed.");
                return;
            }

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (materials[i].shader != urpLitShader)
                    {
                        materials[i].shader = urpLitShader;
                        materialCount++;
                    }
                }
                renderer.sharedMaterials = materials;
            }

            Debug.Log($"[CharacterImporter] Updated {materialCount} material(s) to URP Lit shader.");
        }

        /// <summary>
        /// Reimports all FBX files under the character model path.
        /// </summary>
        [MenuItem("OpenFifa/Characters/Reimport All Character Models")]
        public static void ReimportAllCharacterModels()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Characters" });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            Debug.Log($"[CharacterImporter] Reimported {count} character model(s).");
        }
    }
}
