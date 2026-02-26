using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Builds the pitch GameObject hierarchy at runtime.
    /// Creates: ground plane, boundary colliders (with goal openings),
    /// center circle marking, and goal area markings.
    /// </summary>
    public class PitchBuilder : MonoBehaviour
    {
        [SerializeField] private PitchConfig _pitchConfigAsset;

        private PitchConfigData _config;

        private void Awake()
        {
            if (_pitchConfigAsset != null)
            {
                _config = _pitchConfigAsset.ToData();
                BuildPitch(_config);
            }
        }

        /// <summary>
        /// Builds the entire pitch from a PitchConfigData. Can be called from tests.
        /// </summary>
        public void BuildPitch(PitchConfigData config)
        {
            _config = config;
            CreatePitchSurface();
            CreateBoundaryColliders();
            CreateCenterCircle();
            CreateGoalAreaMarkings();
        }

        private void CreatePitchSurface()
        {
            var pitch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pitch.name = "Pitch";
            pitch.transform.SetParent(transform);
            pitch.transform.localPosition = Vector3.zero;
            // Cube is 1x1x1 by default; scale to pitch dimensions
            // Y is thin (0.1), X is length, Z is width
            pitch.transform.localScale = new Vector3(_config.PitchLength, 0.1f, _config.PitchWidth);

            // Assign green material
            var renderer = pitch.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback if URP shader not available
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = new Color(0.2f, 0.6f, 0.15f, 1f); // Green
            renderer.sharedMaterial = mat;

            // Set layer
            pitch.layer = LayerMask.NameToLayer("Pitch");
            if (pitch.layer == -1) pitch.layer = 0;
        }

        private void CreateBoundaryColliders()
        {
            float halfLength = _config.HalfLength;
            float halfWidth = _config.HalfWidth;
            float wallHeight = _config.BoundaryWallHeight;
            float wallThickness = _config.BoundaryWallThickness;
            float goalHalfWidth = _config.GoalOpeningHalfWidth;

            // North wall (positive Z)
            CreateWall("BoundaryNorth",
                new Vector3(0, wallHeight / 2f, halfWidth + wallThickness / 2f),
                new Vector3(_config.PitchLength + wallThickness * 2f, wallHeight, wallThickness));

            // South wall (negative Z)
            CreateWall("BoundarySouth",
                new Vector3(0, wallHeight / 2f, -(halfWidth + wallThickness / 2f)),
                new Vector3(_config.PitchLength + wallThickness * 2f, wallHeight, wallThickness));

            // East wall (positive X) - split into two parts with goal opening
            // Upper part (positive Z side of goal)
            float sideWallLength = (halfWidth - goalHalfWidth);
            CreateWall("BoundaryEastUpper",
                new Vector3(halfLength + wallThickness / 2f, wallHeight / 2f, goalHalfWidth + sideWallLength / 2f),
                new Vector3(wallThickness, wallHeight, sideWallLength));

            // Lower part (negative Z side of goal)
            CreateWall("BoundaryEastLower",
                new Vector3(halfLength + wallThickness / 2f, wallHeight / 2f, -(goalHalfWidth + sideWallLength / 2f)),
                new Vector3(wallThickness, wallHeight, sideWallLength));

            // East goal back wall (behind the goal opening)
            CreateWall("BoundaryEastBack",
                new Vector3(halfLength + _config.GoalAreaDepth + wallThickness / 2f, wallHeight / 2f, 0f),
                new Vector3(wallThickness, wallHeight, _config.GoalWidth + wallThickness));

            // East goal side walls (connect opening to back wall)
            CreateWall("BoundaryEastGoalSideUpper",
                new Vector3(halfLength + _config.GoalAreaDepth / 2f, wallHeight / 2f, goalHalfWidth + wallThickness / 2f),
                new Vector3(_config.GoalAreaDepth, wallHeight, wallThickness));

            CreateWall("BoundaryEastGoalSideLower",
                new Vector3(halfLength + _config.GoalAreaDepth / 2f, wallHeight / 2f, -(goalHalfWidth + wallThickness / 2f)),
                new Vector3(_config.GoalAreaDepth, wallHeight, wallThickness));

            // West wall (negative X) - split into two parts with goal opening
            CreateWall("BoundaryWestUpper",
                new Vector3(-(halfLength + wallThickness / 2f), wallHeight / 2f, goalHalfWidth + sideWallLength / 2f),
                new Vector3(wallThickness, wallHeight, sideWallLength));

            CreateWall("BoundaryWestLower",
                new Vector3(-(halfLength + wallThickness / 2f), wallHeight / 2f, -(goalHalfWidth + sideWallLength / 2f)),
                new Vector3(wallThickness, wallHeight, sideWallLength));

            // West goal back wall
            CreateWall("BoundaryWestBack",
                new Vector3(-(halfLength + _config.GoalAreaDepth + wallThickness / 2f), wallHeight / 2f, 0f),
                new Vector3(wallThickness, wallHeight, _config.GoalWidth + wallThickness));

            // West goal side walls
            CreateWall("BoundaryWestGoalSideUpper",
                new Vector3(-(halfLength + _config.GoalAreaDepth / 2f), wallHeight / 2f, goalHalfWidth + wallThickness / 2f),
                new Vector3(_config.GoalAreaDepth, wallHeight, wallThickness));

            CreateWall("BoundaryWestGoalSideLower",
                new Vector3(-(halfLength + _config.GoalAreaDepth / 2f), wallHeight / 2f, -(goalHalfWidth + wallThickness / 2f)),
                new Vector3(_config.GoalAreaDepth, wallHeight, wallThickness));
        }

        private void CreateWall(string name, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(transform);
            wall.transform.localPosition = position;

            var collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = Vector3.zero;

            // Set layer
            int boundaryLayer = LayerMask.NameToLayer("Boundary");
            wall.layer = boundaryLayer != -1 ? boundaryLayer : 0;
        }

        private void CreateCenterCircle()
        {
            var centerCircle = new GameObject("CenterCircle");
            centerCircle.transform.SetParent(transform);
            centerCircle.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            // Use LineRenderer for the circle marking
            var lineRenderer = centerCircle.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            // Create circle points
            int segments = 64;
            lineRenderer.positionCount = segments;
            float radius = _config.CenterCircleRadius;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
            }

            // White line material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            mat.color = Color.white;
            lineRenderer.material = mat;

            // Center spot
            var centerSpot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            centerSpot.name = "CenterSpot";
            centerSpot.transform.SetParent(centerCircle.transform);
            centerSpot.transform.localPosition = Vector3.zero;
            centerSpot.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
            var spotRenderer = centerSpot.GetComponent<MeshRenderer>();
            spotRenderer.sharedMaterial = mat;

            // Remove unnecessary collider from spot
            var spotCollider = centerSpot.GetComponent<Collider>();
            if (spotCollider != null) Object.Destroy(spotCollider);
        }

        private void CreateGoalAreaMarkings()
        {
            float halfLength = _config.HalfLength;
            float goalAreaDepth = _config.GoalAreaDepth;
            float goalHalfWidth = _config.GoalOpeningHalfWidth;

            // GoalAreaA at positive X (east)
            CreateGoalAreaMarking("GoalAreaA",
                new Vector3(halfLength - goalAreaDepth / 2f, 0.06f, 0f),
                goalAreaDepth,
                _config.GoalWidth + 4f); // Slightly wider than goal for area marking

            // GoalAreaB at negative X (west)
            CreateGoalAreaMarking("GoalAreaB",
                new Vector3(-(halfLength - goalAreaDepth / 2f), 0.06f, 0f),
                goalAreaDepth,
                _config.GoalWidth + 4f);

            // Halfway line
            CreateLineMarking("HalfwayLine",
                new Vector3(0f, 0.06f, 0f),
                new Vector3(0.1f, 0.01f, _config.PitchWidth));
        }

        private void CreateGoalAreaMarking(string name, Vector3 position, float depth, float width)
        {
            var goalArea = new GameObject(name);
            goalArea.transform.SetParent(transform);
            goalArea.transform.localPosition = position;

            var lineRenderer = goalArea.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 4;

            float halfDepth = depth / 2f;
            float halfWidth = width / 2f;

            lineRenderer.SetPosition(0, new Vector3(-halfDepth, 0, halfWidth));
            lineRenderer.SetPosition(1, new Vector3(halfDepth, 0, halfWidth));
            lineRenderer.SetPosition(2, new Vector3(halfDepth, 0, -halfWidth));
            lineRenderer.SetPosition(3, new Vector3(-halfDepth, 0, -halfWidth));

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            mat.color = Color.white;
            lineRenderer.material = mat;
        }

        private void CreateLineMarking(string name, Vector3 position, Vector3 scale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(transform);
            line.transform.localPosition = position;
            line.transform.localScale = scale;

            var renderer = line.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            mat.color = Color.white;
            renderer.sharedMaterial = mat;

            // Remove collider - this is just a visual marking
            var collider = line.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }
    }
}
