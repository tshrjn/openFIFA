using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Builds stadium environment at runtime: skybox, pitch texture,
    /// goal posts with colliders, goal nets, and basic stands geometry.
    /// </summary>
    public class StadiumBuilder : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private Material pitchMaterial;
        [SerializeField] private Material goalPostMaterial;
        [SerializeField] private Material netMaterial;

        private StadiumConfig _config;

        private void Awake()
        {
            _config = new StadiumConfig();
        }

        private void Start()
        {
            SetupSkybox();
            SetupGoalPosts();
        }

        private void SetupSkybox()
        {
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
        }

        private void SetupGoalPosts()
        {
            CreateGoalPost(new Vector3(0f, 0f, 15f), "GoalPost_Home");
            CreateGoalPost(new Vector3(0f, 0f, -15f), "GoalPost_Away");
        }

        private void CreateGoalPost(Vector3 centerPosition, string name)
        {
            var root = new GameObject(name);
            root.transform.position = centerPosition;
            root.transform.SetParent(transform);

            float halfWidth = _config.GoalPostWidth / 2f;
            float height = _config.GoalPostHeight;
            float radius = _config.PostRadius;

            // Left post
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPost.name = "LeftPost";
            leftPost.transform.SetParent(root.transform);
            leftPost.transform.localPosition = new Vector3(-halfWidth, height / 2f, 0f);
            leftPost.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            SetupPostCollider(leftPost);

            // Right post
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.SetParent(root.transform);
            rightPost.transform.localPosition = new Vector3(halfWidth, height / 2f, 0f);
            rightPost.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            SetupPostCollider(rightPost);

            // Crossbar
            var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.SetParent(root.transform);
            crossbar.transform.localPosition = new Vector3(0f, height, 0f);
            crossbar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(radius * 2f, halfWidth, radius * 2f);
            SetupPostCollider(crossbar);

            // Net (semi-transparent plane)
            var net = GameObject.CreatePrimitive(PrimitiveType.Quad);
            net.name = "Net";
            net.transform.SetParent(root.transform);
            net.transform.localPosition = new Vector3(0f, height / 2f, -0.5f);
            net.transform.localScale = new Vector3(_config.GoalPostWidth, height, 1f);

            if (netMaterial != null)
            {
                var renderer = net.GetComponent<Renderer>();
                renderer.material = netMaterial;
                var color = renderer.material.color;
                color.a = _config.NetAlpha;
                renderer.material.color = color;
            }

            // Remove collider from net (non-physical)
            var netCollider = net.GetComponent<Collider>();
            if (netCollider != null) Destroy(netCollider);

            // Apply post material
            if (goalPostMaterial != null)
            {
                foreach (var r in new[] { leftPost, rightPost, crossbar })
                {
                    r.GetComponent<Renderer>().material = goalPostMaterial;
                }
            }
        }

        private void SetupPostCollider(GameObject post)
        {
            // Remove default collider and add MeshCollider
            var defaultCollider = post.GetComponent<Collider>();
            if (defaultCollider != null) Destroy(defaultCollider);

            var meshCollider = post.AddComponent<MeshCollider>();
            meshCollider.convex = _config.PostColliderConvex;
        }
    }
}
