using UnityEditor;
using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Editor
{
    /// <summary>
    /// AssetPostprocessor for soccer ball FBX model files.
    /// Auto-configures mesh import settings, material slots, and collider setup
    /// for ball models imported into Assets/Models/Ball/.
    /// </summary>
    public class BallModelImporter : AssetPostprocessor
    {
        private static readonly string BallModelPath = "Assets/Models/Ball/";
        private static readonly BallModelConfig DefaultConfig = new BallModelConfig();

        /// <summary>
        /// Pre-processes ball model import settings: mesh compression, normals, tangents.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(BallModelPath))
                return;

            var importer = assetImporter as ModelImporter;
            if (importer == null)
                return;

            // Mesh import settings for high-quality ball model
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            // No animation for ball model
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;

            // Generate colliders is off — we add SphereCollider manually
            importer.addCollider = false;

            // Scale for Unity units (1 unit = 1 meter)
            importer.globalScale = 1f;

            // Materials: use external materials for PBR control
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            Debug.Log($"[BallModelImporter] Configured import settings for: {assetPath}");
        }

        /// <summary>
        /// Post-processes the ball model after import: validates mesh and logs info.
        /// </summary>
        private void OnPostprocessModel(GameObject model)
        {
            if (!assetPath.StartsWith(BallModelPath))
                return;

            var meshFilters = model.GetComponentsInChildren<MeshFilter>();
            int totalTriangles = 0;
            int totalVertices = 0;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    totalTriangles += mf.sharedMesh.triangles.Length / 3;
                    totalVertices += mf.sharedMesh.vertexCount;
                }
            }

            if (totalTriangles > DefaultConfig.MaxTriangles)
            {
                Debug.LogWarning(
                    $"[BallModelImporter] Ball model '{assetPath}' has {totalTriangles} triangles, " +
                    $"exceeding budget of {DefaultConfig.MaxTriangles}. Consider reducing polygon count.");
            }

            Debug.Log(
                $"[BallModelImporter] Ball model imported: {totalVertices} vertices, " +
                $"{totalTriangles} triangles (budget: {DefaultConfig.MaxTriangles})");
        }

        /// <summary>
        /// Assigns a PBR material to the ball model after import.
        /// Configures the URP Lit shader with correct smoothness and metallic values.
        /// </summary>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!assetPath.StartsWith(BallModelPath))
                return null; // null = use default behavior

            // Configure for URP Lit shader
            var urpLitShader = Shader.Find(DefaultConfig.ShaderName);
            if (urpLitShader != null)
            {
                material.shader = urpLitShader;
                material.SetFloat("_Smoothness", DefaultConfig.Smoothness);
                material.SetFloat("_Metallic", 0f); // Soccer balls are non-metallic
                Debug.Log($"[BallModelImporter] Applied URP Lit shader to ball material: {material.name}");
            }
            else
            {
                Debug.LogWarning(
                    $"[BallModelImporter] Could not find shader '{DefaultConfig.ShaderName}'. " +
                    "Ensure URP is installed and configured.");
            }

            return material;
        }

        /// <summary>
        /// Sets up a prefab with the correct SphereCollider radius after model import.
        /// Call from a menu item or build script.
        /// </summary>
        [MenuItem("OpenFifa/Ball/Setup Ball Collider")]
        public static void SetupBallCollider()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[BallModelImporter] No GameObject selected. Select a ball model in the scene.");
                return;
            }

            var collider = selected.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = selected.AddComponent<SphereCollider>();
            }

            var config = new BallModelConfig();
            collider.radius = config.ColliderConfig.Radius;
            collider.center = Vector3.zero;

            // Create or assign physics material
            var physicsMat = new PhysicsMaterial("BallPhysicsMaterial")
            {
                bounciness = config.ColliderConfig.Bounciness,
                staticFriction = config.ColliderConfig.StaticFriction,
                dynamicFriction = config.ColliderConfig.DynamicFriction,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
            collider.sharedMaterial = physicsMat;

            Debug.Log($"[BallModelImporter] Ball collider set up: radius={config.ColliderConfig.Radius}, " +
                      $"bounciness={config.ColliderConfig.Bounciness}");
        }

        /// <summary>
        /// Creates a PBR material for the ball with all texture slots configured.
        /// Call from a menu item.
        /// </summary>
        [MenuItem("OpenFifa/Ball/Create Ball PBR Material")]
        public static void CreateBallPBRMaterial()
        {
            var config = new BallModelConfig();
            var shader = Shader.Find(config.ShaderName);

            if (shader == null)
            {
                Debug.LogError($"[BallModelImporter] Shader '{config.ShaderName}' not found. Is URP installed?");
                return;
            }

            var material = new Material(shader)
            {
                name = "SoccerBall_PBR"
            };

            material.SetFloat("_Smoothness", config.Smoothness);
            material.SetFloat("_Metallic", 0f);

            // Attempt to load textures from config paths
            LoadAndAssignTexture(material, "_BaseMap", config.PBRTextures.AlbedoPath);
            LoadAndAssignTexture(material, "_BumpMap", config.PBRTextures.NormalPath);
            LoadAndAssignTexture(material, "_MetallicGlossMap", config.PBRTextures.MetallicPath);
            LoadAndAssignTexture(material, "_OcclusionMap", config.PBRTextures.AOPath);

            // Save material asset
            string materialPath = "Assets/Materials/Ball/SoccerBall_PBR.mat";
            EnsureDirectoryExists("Assets/Materials/Ball");
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BallModelImporter] Created PBR material at: {materialPath}");
        }

        private static void LoadAndAssignTexture(Material material, string propertyName, string texturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/{texturePath}.png");
            if (texture == null)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/{texturePath}.tga");
            }

            if (texture != null)
            {
                material.SetTexture(propertyName, texture);
                Debug.Log($"[BallModelImporter] Assigned texture '{texturePath}' to {propertyName}");
            }
            else
            {
                Debug.LogWarning($"[BallModelImporter] Texture not found at 'Assets/{texturePath}.[png|tga]'");
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
