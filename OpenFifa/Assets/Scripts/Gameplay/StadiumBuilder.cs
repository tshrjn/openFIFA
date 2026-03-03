using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Builds stadium environment at runtime: skybox, pitch texture,
    /// goal posts with colliders, goal nets, stand sections, floodlight towers,
    /// advertising boards, corner flags, dugouts, scoreboard marker, and tunnel.
    /// </summary>
    public class StadiumBuilder : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private Material pitchMaterial;
        [SerializeField] private Material goalPostMaterial;
        [SerializeField] private Material netMaterial;

        private StadiumConfig _config;
        private StadiumLightingConfig _lightingConfig;

        /// <summary>
        /// Expose config for testing/inspection.
        /// </summary>
        public StadiumConfig Config => _config;

        private void Awake()
        {
            _config = new StadiumConfig();
            _lightingConfig = StadiumLightingConfig.CreateNightStadium();
        }

        private void Start()
        {
            SetupSkybox();
            SetupGoalPosts();
            BuildStands();
            BuildFloodlightTowers();
            BuildAdvertisingBoards();
            BuildCornerFlags();
            BuildDugouts();
            BuildScoreboardMarker();
            BuildTunnel();
        }

        /// <summary>
        /// Allows external code (e.g., tests or editor tools) to inject configs.
        /// </summary>
        public void Initialize(StadiumConfig config, StadiumLightingConfig lightingConfig)
        {
            _config = config;
            _lightingConfig = lightingConfig;
        }

        /// <summary>
        /// Build all stadium elements programmatically. Can be called from editor/tests.
        /// </summary>
        public void BuildAll()
        {
            if (_config == null) _config = new StadiumConfig();
            if (_lightingConfig == null) _lightingConfig = StadiumLightingConfig.CreateNightStadium();

            SetupSkybox();
            SetupGoalPosts();
            BuildStands();
            BuildFloodlightTowers();
            BuildAdvertisingBoards();
            BuildCornerFlags();
            BuildDugouts();
            BuildScoreboardMarker();
            BuildTunnel();
        }

        // ------------------------------------------------------------------
        // Skybox
        // ------------------------------------------------------------------

        private void SetupSkybox()
        {
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
        }

        // ------------------------------------------------------------------
        // Goal Posts
        // ------------------------------------------------------------------

        private void SetupGoalPosts()
        {
            float halfLength = _config.PitchLength / 2f;
            CreateGoalPost(new Vector3(halfLength, 0f, 0f), "GoalPost_Home");
            CreateGoalPost(new Vector3(-halfLength, 0f, 0f), "GoalPost_Away");
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
            leftPost.transform.localPosition = new Vector3(0f, height / 2f, -halfWidth);
            leftPost.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            SetupPostCollider(leftPost);

            // Right post
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPost.name = "RightPost";
            rightPost.transform.SetParent(root.transform);
            rightPost.transform.localPosition = new Vector3(0f, height / 2f, halfWidth);
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
            net.transform.localPosition = new Vector3(-0.5f, height / 2f, 0f);
            net.transform.localScale = new Vector3(_config.GoalPostWidth, height, 1f);
            net.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

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

        // ------------------------------------------------------------------
        // Stand Sections (8 tiered sections around the pitch)
        // ------------------------------------------------------------------

        private void BuildStands()
        {
            if (!_config.HasStandsGeometry) return;

            var standsRoot = new GameObject("Stands");
            standsRoot.transform.SetParent(transform);
            standsRoot.transform.localPosition = Vector3.zero;

            float halfLength = _config.PitchLength / 2f;
            float halfWidth = _config.PitchWidth / 2f;

            for (int i = 0; i < _config.Sections.Count; i++)
            {
                var section = _config.Sections[i];
                var sectionRoot = new GameObject($"StandSection_{i}");
                sectionRoot.transform.SetParent(standsRoot.transform);

                // Position based on angle offset
                Vector3 sectionPos = GetStandSectionPosition(i, section, halfLength, halfWidth);
                sectionRoot.transform.localPosition = sectionPos;

                float rotY = GetStandSectionRotation(i);
                sectionRoot.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

                // Build tiered geometry for this section
                BuildTieredStand(sectionRoot.transform, section);
            }
        }

        private Vector3 GetStandSectionPosition(int index, StandsSection section, float halfLength, float halfWidth)
        {
            float dist = section.DistanceFromPitch;

            switch (index)
            {
                case 0: // North (positive Z)
                    return new Vector3(0f, 0f, halfWidth + dist + section.Depth / 2f);
                case 1: // South (negative Z)
                    return new Vector3(0f, 0f, -(halfWidth + dist + section.Depth / 2f));
                case 2: // East (positive X)
                    return new Vector3(halfLength + dist + section.Depth / 2f, 0f, 0f);
                case 3: // West (negative X)
                    return new Vector3(-(halfLength + dist + section.Depth / 2f), 0f, 0f);
                case 4: // NE corner
                    return new Vector3(halfLength + dist, 0f, halfWidth + dist);
                case 5: // NW corner
                    return new Vector3(-(halfLength + dist), 0f, halfWidth + dist);
                case 6: // SE corner
                    return new Vector3(halfLength + dist, 0f, -(halfWidth + dist));
                case 7: // SW corner
                    return new Vector3(-(halfLength + dist), 0f, -(halfWidth + dist));
                default:
                    return Vector3.zero;
            }
        }

        private float GetStandSectionRotation(int index)
        {
            switch (index)
            {
                case 0: return 0f;     // North faces south (toward pitch)
                case 1: return 180f;   // South faces north
                case 2: return 270f;   // East faces west
                case 3: return 90f;    // West faces east
                case 4: return 315f;   // NE corner
                case 5: return 45f;    // NW corner
                case 6: return 225f;   // SE corner
                case 7: return 135f;   // SW corner
                default: return 0f;
            }
        }

        private void BuildTieredStand(Transform parent, StandsSection section)
        {
            float tierHeight = section.TierHeight;

            for (int tier = 0; tier < section.TierCount; tier++)
            {
                var tierObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tierObj.name = $"Tier_{tier}";
                tierObj.transform.SetParent(parent);

                // Each tier steps back and up
                float depthOffset = tier * (section.Depth / section.TierCount);
                float yOffset = tier * tierHeight + tierHeight / 2f;

                tierObj.transform.localPosition = new Vector3(0f, yOffset, depthOffset);
                tierObj.transform.localScale = new Vector3(
                    section.Width,
                    tierHeight,
                    section.Depth / section.TierCount
                );

                // Apply a concrete/gray material
                var renderer = tierObj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = CreateStandMaterial(tier);
                renderer.receiveShadows = true;

                // Remove collider from stands — not part of gameplay physics
                var col = tierObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        private static Material CreateStandMaterial(int tier)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            // Alternate slightly between tiers for visual interest
            float gray = 0.35f + tier * 0.05f;
            mat.color = new Color(gray, gray, gray + 0.02f);
            mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }

        // ------------------------------------------------------------------
        // Floodlight Towers (4 cylinder + spot light)
        // ------------------------------------------------------------------

        private void BuildFloodlightTowers()
        {
            if (!_config.HasFloodlights) return;

            var floodlightsRoot = new GameObject("FloodlightTowers");
            floodlightsRoot.transform.SetParent(transform);
            floodlightsRoot.transform.localPosition = Vector3.zero;

            for (int i = 0; i < _config.FloodlightTowers.Count; i++)
            {
                var tower = _config.FloodlightTowers[i];
                BuildFloodlightTower(floodlightsRoot.transform, tower, i);
            }
        }

        private void BuildFloodlightTower(Transform parent, FloodlightTowerData data, int index)
        {
            var towerRoot = new GameObject($"FloodlightTower_{index}");
            towerRoot.transform.SetParent(parent);
            towerRoot.transform.localPosition = new Vector3(data.X, 0f, data.Z);

            // Tower pole (cylinder)
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(towerRoot.transform);
            pole.transform.localPosition = new Vector3(0f, data.Y / 2f, 0f);
            pole.transform.localScale = new Vector3(0.6f, data.Y / 2f, 0.6f);

            var poleRenderer = pole.GetComponent<MeshRenderer>();
            poleRenderer.sharedMaterial = CreateMetalMaterial();

            // Remove collider from pole
            var poleCol = pole.GetComponent<Collider>();
            if (poleCol != null) Destroy(poleCol);

            // Light housing (small cube at top)
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "LightHousing";
            housing.transform.SetParent(towerRoot.transform);
            housing.transform.localPosition = new Vector3(0f, data.Y, 0f);
            housing.transform.localScale = new Vector3(2f, 0.5f, 2f);

            var housingRenderer = housing.GetComponent<MeshRenderer>();
            housingRenderer.sharedMaterial = CreateMetalMaterial();

            var housingCol = housing.GetComponent<Collider>();
            if (housingCol != null) Destroy(housingCol);

            // Spot light pointing down toward pitch center
            var lightObj = new GameObject("SpotLight");
            lightObj.transform.SetParent(towerRoot.transform);
            lightObj.transform.localPosition = new Vector3(0f, data.Y - 0.3f, 0f);

            // Aim toward pitch center
            Vector3 targetDir = new Vector3(-data.X, -data.Y + 1f, -data.Z).normalized;
            lightObj.transform.rotation = Quaternion.LookRotation(targetDir);

            var spotLight = lightObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.intensity = _lightingConfig.FloodlightIntensity;
            spotLight.range = _lightingConfig.FloodlightRange;
            spotLight.spotAngle = data.ConeAngle;
            spotLight.color = new Color(
                _lightingConfig.FloodlightColorR,
                _lightingConfig.FloodlightColorG,
                _lightingConfig.FloodlightColorB
            );
            spotLight.shadows = LightShadows.Soft;
        }

        private static Material CreateMetalMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = new Color(0.5f, 0.5f, 0.55f);
            mat.SetFloat("_Metallic", 0.7f);
            mat.SetFloat("_Smoothness", 0.6f);
            return mat;
        }

        // ------------------------------------------------------------------
        // Advertising Boards
        // ------------------------------------------------------------------

        private void BuildAdvertisingBoards()
        {
            if (!_config.HasAdvertisingBoards) return;

            var boardsRoot = new GameObject("AdvertisingBoards");
            boardsRoot.transform.SetParent(transform);
            boardsRoot.transform.localPosition = Vector3.zero;

            for (int i = 0; i < _config.AdvertisingBoards.Count; i++)
            {
                var board = _config.AdvertisingBoards[i];
                BuildAdvertisingBoard(boardsRoot.transform, board, i);
            }
        }

        private void BuildAdvertisingBoard(Transform parent, AdvertisingBoardData data, int index)
        {
            var boardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardObj.name = $"AdBoard_{index}";
            boardObj.transform.SetParent(parent);
            boardObj.transform.localPosition = new Vector3(data.X, data.Y + data.Height / 2f, data.Z);
            boardObj.transform.localRotation = Quaternion.Euler(0f, data.RotationY, 0f);
            boardObj.transform.localScale = new Vector3(data.Width, data.Height, 0.05f);

            var renderer = boardObj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateAdBoardMaterial(index);

            // Remove collider — not gameplay relevant
            var col = boardObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static Material CreateAdBoardMaterial(int index)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            // Cycle through brand-ish colors for visual variety
            Color[] adColors =
            {
                new Color(0.1f, 0.2f, 0.6f),  // Blue sponsor
                new Color(0.8f, 0.1f, 0.1f),  // Red sponsor
                new Color(0.1f, 0.5f, 0.1f),  // Green sponsor
                new Color(0.9f, 0.7f, 0.0f),  // Gold sponsor
                new Color(0.2f, 0.2f, 0.2f),  // Dark sponsor
                new Color(0.9f, 0.9f, 0.9f),  // White sponsor
            };
            mat.color = adColors[index % adColors.Length];
            mat.SetFloat("_Smoothness", 0.3f);
            return mat;
        }

        // ------------------------------------------------------------------
        // Corner Flags
        // ------------------------------------------------------------------

        private void BuildCornerFlags()
        {
            if (!_config.HasCornerFlags) return;

            var flagsRoot = new GameObject("CornerFlags");
            flagsRoot.transform.SetParent(transform);
            flagsRoot.transform.localPosition = Vector3.zero;

            for (int i = 0; i < _config.CornerFlags.Count; i++)
            {
                var flag = _config.CornerFlags[i];
                BuildCornerFlag(flagsRoot.transform, flag, i);
            }
        }

        private void BuildCornerFlag(Transform parent, CornerFlagData data, int index)
        {
            var flagRoot = new GameObject($"CornerFlag_{index}");
            flagRoot.transform.SetParent(parent);
            flagRoot.transform.localPosition = new Vector3(data.X, data.Y, data.Z);

            // Pole (thin cylinder)
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(flagRoot.transform);
            pole.transform.localPosition = new Vector3(0f, data.PoleHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(data.PoleRadius * 2f, data.PoleHeight / 2f, data.PoleRadius * 2f);

            var poleRenderer = pole.GetComponent<MeshRenderer>();
            var poleMat = CreateColorMaterial(Color.yellow);
            poleRenderer.sharedMaterial = poleMat;

            var poleCol = pole.GetComponent<Collider>();
            if (poleCol != null) Destroy(poleCol);

            // Flag (small triangle approximated by a cube)
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            flag.transform.SetParent(flagRoot.transform);
            flag.transform.localPosition = new Vector3(0.1f, data.PoleHeight - 0.1f, 0f);
            flag.transform.localScale = new Vector3(0.2f, 0.15f, 0.01f);

            var flagRenderer = flag.GetComponent<MeshRenderer>();
            flagRenderer.sharedMaterial = CreateColorMaterial(new Color(1f, 0.3f, 0f)); // Orange flag

            var flagCol = flag.GetComponent<Collider>();
            if (flagCol != null) Destroy(flagCol);
        }

        // ------------------------------------------------------------------
        // Dugouts
        // ------------------------------------------------------------------

        private void BuildDugouts()
        {
            if (!_config.HasDugouts) return;

            var dugoutsRoot = new GameObject("Dugouts");
            dugoutsRoot.transform.SetParent(transform);
            dugoutsRoot.transform.localPosition = Vector3.zero;

            for (int i = 0; i < _config.Dugouts.Count; i++)
            {
                var dugout = _config.Dugouts[i];
                BuildDugout(dugoutsRoot.transform, dugout, i);
            }
        }

        private void BuildDugout(Transform parent, DugoutData data, int index)
        {
            var dugoutRoot = new GameObject($"Dugout_{data.TeamLabel}");
            dugoutRoot.transform.SetParent(parent);
            dugoutRoot.transform.localPosition = new Vector3(data.X, data.Y, data.Z);

            // Back wall
            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "BackWall";
            backWall.transform.SetParent(dugoutRoot.transform);
            backWall.transform.localPosition = new Vector3(0f, data.Height / 2f, -data.Depth / 2f);
            backWall.transform.localScale = new Vector3(data.Width, data.Height, 0.1f);
            backWall.GetComponent<MeshRenderer>().sharedMaterial = CreateDugoutMaterial();
            var bwCol = backWall.GetComponent<Collider>();
            if (bwCol != null) Destroy(bwCol);

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(dugoutRoot.transform);
            roof.transform.localPosition = new Vector3(0f, data.Height, 0f);
            roof.transform.localScale = new Vector3(data.Width, 0.1f, data.Depth);
            roof.GetComponent<MeshRenderer>().sharedMaterial = CreateDugoutMaterial();
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);

            // Bench (seating)
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bench";
            bench.transform.SetParent(dugoutRoot.transform);
            bench.transform.localPosition = new Vector3(0f, 0.4f, -data.Depth * 0.3f);
            bench.transform.localScale = new Vector3(data.Width * 0.9f, 0.1f, 0.4f);
            bench.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(new Color(0.15f, 0.15f, 0.15f));
            var benchCol = bench.GetComponent<Collider>();
            if (benchCol != null) Destroy(benchCol);
        }

        private static Material CreateDugoutMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = new Color(0.2f, 0.2f, 0.25f);
            mat.SetFloat("_Smoothness", 0.4f);
            mat.SetFloat("_Metallic", 0.3f);
            return mat;
        }

        // ------------------------------------------------------------------
        // Scoreboard Marker
        // ------------------------------------------------------------------

        private void BuildScoreboardMarker()
        {
            if (!_config.HasScoreboard) return;

            var data = _config.Scoreboard;
            var scoreboardRoot = new GameObject("Scoreboard");
            scoreboardRoot.transform.SetParent(transform);
            scoreboardRoot.transform.localPosition = new Vector3(data.X, data.Y, data.Z);

            // Scoreboard face
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "ScoreboardFace";
            face.transform.SetParent(scoreboardRoot.transform);
            face.transform.localPosition = Vector3.zero;
            face.transform.localScale = new Vector3(data.Width, data.Height, 0.2f);

            var renderer = face.GetComponent<MeshRenderer>();
            var mat = CreateColorMaterial(new Color(0.05f, 0.05f, 0.08f));
            renderer.sharedMaterial = mat;

            var col = face.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Support pillars
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = side < 0 ? "LeftPillar" : "RightPillar";
                pillar.transform.SetParent(scoreboardRoot.transform);
                pillar.transform.localPosition = new Vector3(side * (data.Width / 2f + 0.3f), -data.Y / 2f, 0f);
                pillar.transform.localScale = new Vector3(0.3f, data.Y / 2f, 0.3f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = CreateMetalMaterial();

                var pillarCol = pillar.GetComponent<Collider>();
                if (pillarCol != null) Destroy(pillarCol);
            }
        }

        // ------------------------------------------------------------------
        // Tunnel Entrance
        // ------------------------------------------------------------------

        private void BuildTunnel()
        {
            if (!_config.HasTunnels) return;

            var data = _config.Tunnel;
            var tunnelRoot = new GameObject("Tunnel");
            tunnelRoot.transform.SetParent(transform);
            tunnelRoot.transform.localPosition = new Vector3(data.X, data.Y, data.Z);

            // Tunnel floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TunnelFloor";
            floor.transform.SetParent(tunnelRoot.transform);
            floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            floor.transform.localScale = new Vector3(data.Width, 0.02f, data.Depth);
            floor.GetComponent<MeshRenderer>().sharedMaterial = CreateColorMaterial(new Color(0.3f, 0.3f, 0.3f));
            var floorCol = floor.GetComponent<Collider>();
            if (floorCol != null) Destroy(floorCol);

            // Left wall
            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.SetParent(tunnelRoot.transform);
            leftWall.transform.localPosition = new Vector3(-data.Width / 2f, data.Height / 2f, 0f);
            leftWall.transform.localScale = new Vector3(0.15f, data.Height, data.Depth);
            leftWall.GetComponent<MeshRenderer>().sharedMaterial = CreateDugoutMaterial();
            var lwCol = leftWall.GetComponent<Collider>();
            if (lwCol != null) Destroy(lwCol);

            // Right wall
            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.SetParent(tunnelRoot.transform);
            rightWall.transform.localPosition = new Vector3(data.Width / 2f, data.Height / 2f, 0f);
            rightWall.transform.localScale = new Vector3(0.15f, data.Height, data.Depth);
            rightWall.GetComponent<MeshRenderer>().sharedMaterial = CreateDugoutMaterial();
            var rwCol = rightWall.GetComponent<Collider>();
            if (rwCol != null) Destroy(rwCol);

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "TunnelRoof";
            roof.transform.SetParent(tunnelRoot.transform);
            roof.transform.localPosition = new Vector3(0f, data.Height, 0f);
            roof.transform.localScale = new Vector3(data.Width, 0.15f, data.Depth);
            roof.GetComponent<MeshRenderer>().sharedMaterial = CreateDugoutMaterial();
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);
        }

        // ------------------------------------------------------------------
        // Shared Utilities
        // ------------------------------------------------------------------

        private static Material CreateColorMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
