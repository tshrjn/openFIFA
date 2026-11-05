using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Procedural two-tier stadium built at runtime from <see cref="StadiumLayout"/>: four raked stands with crowd cards,
    /// lower-tier corner fills, cantilever roofs with lit undersides and edge lights, corner floodlight pylons with halos,
    /// a players' tunnel and dugouts on the near (camera) side, and an LED board ring beyond the run-off.
    /// No asset files: textures are generated, materials are shared URP Lit/Unlit (SRP-batcher friendly), and each
    /// stand is one mesh with a structure submesh plus one crowd submesh per bob phase group
    /// (<see cref="CrowdVariation.PhaseGroups"/>). Roughly 30 draw calls.
    /// Crowd de-tiling: rows are cut into 3-7 seat segments that each pick a texture variant, a start seat, a height
    /// scale, a lighting tilt and a bob phase group from a hash (<see cref="CrowdVariation"/>). LED boards are cut into
    /// segments starting on different ads, in two phase groups, and switch to a scrolling goal graphic on goals
    /// (<see cref="LedBoardSchedule"/>). No custom shader: all motion is material texture offsets, so it survives
    /// player builds and iOS Metal with the stock URP shaders.
    /// Call <see cref="SetExcitement"/> (0..1; reaching 1 counts as a goal) to make the crowd bounce,
    /// <see cref="SetTeamColours"/> when the clubs are known.
    /// </summary>
    public sealed class ProceduralStadium : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public StadiumLayout Layout { get; private set; }
        public int TriangleCount { get; private set; }
        public int DrawCalls { get; private set; }
        public float Excitement => _target;

        private Material _structure, _lamp, _halo;
        private readonly Material[] _crowd = new Material[CrowdVariation.PhaseGroups];
        private readonly Material[] _led = new Material[LedBoardSchedule.PhaseGroups];
        private float _goalStart = -1000f;
        private Texture2D _palette, _crowdTex, _ledTex, _haloTex;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private uint _seed;
        private float _target, _excitement;

        /// <summary>Builds the stadium under <paramref name="parent"/> for the given pitch and club colours.</summary>
        public static ProceduralStadium Build(Transform parent, PitchSpec pitch, Color home, Color away)
            => Build(parent, pitch, home, away, null, 20261104u);

        public static ProceduralStadium Build(Transform parent, PitchSpec pitch, Color home, Color away, StadiumLayoutSpec spec, uint seed)
        {
            var go = new GameObject("Stadium (procedural)");
            if (parent != null) go.transform.SetParent(parent, false);
            var s = go.AddComponent<ProceduralStadium>();
            s.BuildInternal(StadiumLayout.Create(pitch, spec), home, away, seed);
            return s;
        }

        /// <summary>Crowd excitement 0 (calm, still) .. 1 (bouncing). Smoothed over ~0.4 s.</summary>
        public void SetExcitement(float excitement)
        {
            float e = Mathf.Clamp01(excitement);
            if (e >= 0.999f && _target < 0.999f) CelebrateGoal();
            _target = e;
        }

        /// <summary>Starts the LED goal graphic (also triggered when <see cref="SetExcitement"/> reaches 1).</summary>
        public void CelebrateGoal() => _goalStart = Time.unscaledTime;

        /// <summary>Repaints crowd shirts, seats, fascias and LED panels for new club colours.</summary>
        public void SetTeamColours(Color home, Color away)
        {
            StadiumTextures.FillPalette(_palette, home, away);
            StadiumTextures.FillCrowd(_crowdTex, home, away, _seed);
            StadiumTextures.FillLed(_ledTex, home, away);
        }

        private void Update()
        {
            // Real time: the crowd and the LED boards keep moving through presentation slow motion / freezes
            // (e.g. the offside sequence sets Time.timeScale), like a real stadium during a replay.
            _excitement = Mathf.MoveTowards(_excitement, _target, Time.unscaledDeltaTime * 2.5f);
            float t = Time.unscaledTime;
            int texH = _crowdTex != null ? _crowdTex.height : 1024;
            for (int g = 0; g < _crowd.Length; g++)
            {
                CrowdVariation.CardOffset(_excitement, t, g, texH, out float du, out float dv);
                SetOffset(_crowd[g], du, dv);
            }
            for (int g = 0; g < _led.Length; g++)
            {
                LedBoardSchedule.Offset(t, t - _goalStart, g, out float lu, out float lv);
                SetOffset(_led[g], lu, lv);
            }
        }

        private static void SetOffset(Material m, float du, float dv)
        {
            if (m == null) return;
            var o = new Vector2(du, dv);
            if (m.HasProperty(BaseMapId)) m.SetTextureOffset(BaseMapId, o);
            else if (m.HasProperty(MainTexId)) m.SetTextureOffset(MainTexId, o);
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes) if (m != null) Destroy(m);
            foreach (var o in new Object[] { _structure, _lamp, _halo, _palette, _crowdTex, _ledTex, _haloTex })
                if (o != null) Destroy(o);
            foreach (var m in _crowd) if (m != null) Destroy(m);
            foreach (var m in _led) if (m != null) Destroy(m);
        }

        // ------------------------------------------------------------------ materials

        private static Shader LitShader() =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");

        /// <summary>URP Unlit; a player build only has it if some asset references it (else Sprites/Default, always included).</summary>
        private static Shader UnlitShader() =>
            Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

        private static void SetMap(Material m, Texture t, Color c)
        {
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, t);
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, t);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        private static Material Lit(string name, Texture t, float smoothness)
        {
            var m = new Material(LitShader()) { name = name };
            SetMap(m, t, Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return m;
        }

        private static Material Unlit(string name, Texture t, Color c)
        {
            var m = new Material(UnlitShader()) { name = name };
            SetMap(m, t, c);
            return m;
        }

        /// <summary>Additive glow (URP Unlit, transparent, One One, no z-write, double sided); alpha-blended on the Sprites fallback.</summary>
        private static Material Additive(string name, Texture t, Color c)
        {
            var m = Unlit(name, t, c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        // ------------------------------------------------------------------ build

        private StadiumMeshBuffer _misc, _roof, _lamps, _halos;
        private StadiumMeshBuffer[] _ledBufs;

        private void BuildInternal(StadiumLayout layout, Color home, Color away, uint seed)
        {
            Layout = layout;
            _seed = seed;
            _palette = StadiumTextures.NewPalette();
            _crowdTex = StadiumTextures.NewCrowd();
            _ledTex = StadiumTextures.NewLed();
            _haloTex = StadiumTextures.NewHalo();
            SetTeamColours(home, away);

            _structure = Lit("Stadium Structure", _palette, 0.15f);
            for (int g = 0; g < _crowd.Length; g++) _crowd[g] = Lit("Stadium Crowd " + g, _crowdTex, 0f);
            for (int g = 0; g < _led.Length; g++) _led[g] = Unlit("Stadium LED " + g, _ledTex, new Color(1.25f, 1.25f, 1.25f));
            _lamp = Unlit("Stadium Lamps", Texture2D.whiteTexture, new Color(5f, 4.85f, 4.4f));
            _halo = Additive("Stadium Halo", _haloTex, new Color(0.85f, 0.82f, 0.75f, 1f));

            _misc = new StadiumMeshBuffer();
            _roof = new StadiumMeshBuffer();
            _ledBufs = new StadiumMeshBuffer[_led.Length];
            for (int g = 0; g < _ledBufs.Length; g++) _ledBufs[g] = new StadiumMeshBuffer();
            _lamps = new StadiumMeshBuffer();
            _halos = new StadiumMeshBuffer();

            for (int s = 0; s < 4; s++) BuildStand((StandSide)s);
            BuildCorners();
            BuildBoards();
            BuildNearSideFurniture();
            BuildPylons();
            BuildApron();

            Emit("Roofs", ShadowCastingMode.Off, true, (_roof, _structure));
            Emit("Stadium Misc", ShadowCastingMode.Off, true, (_misc, _structure));
            var ledParts = new (StadiumMeshBuffer, Material)[_led.Length];
            for (int g = 0; g < _led.Length; g++) ledParts[g] = (_ledBufs[g], _led[g]);
            Emit("LED Boards", ShadowCastingMode.Off, false, ledParts);
            Emit("Lamps", ShadowCastingMode.Off, false, (_lamps, _lamp));
            Emit("Light Halos", ShadowCastingMode.Off, false, (_halos, _halo));
        }

        private void Emit(string name, ShadowCastingMode shadows, bool receive, params (StadiumMeshBuffer buf, Material mat)[] parts)
        {
            var bufs = new List<StadiumMeshBuffer>();
            var mats = new List<Material>();
            foreach (var p in parts)
            {
                if (p.buf.TriangleCount == 0) continue;
                bufs.Add(p.buf);
                mats.Add(p.mat);
                TriangleCount += p.buf.TriangleCount;
            }
            if (bufs.Count == 0) return;
            var mesh = StadiumMeshBuffer.Combine(name, bufs.ToArray());
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = shadows;
            r.receiveShadows = receive;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            DrawCalls += bufs.Count;
        }

        private Vector3 W(StandSide s, float d, float a, float h)
        {
            var p = Layout.ToWorld(s, d, a, h);
            return new Vector3(p.X, p.Y, p.Z);
        }

        private static Vector3 Out(StandSide s)
        {
            StadiumLayout.Outward(s, out float x, out float z);
            return new Vector3(x, 0f, z);
        }

        private static Vector3 Alg(StandSide s)
        {
            StadiumLayout.Along(s, out float x, out float z);
            return new Vector3(x, 0f, z);
        }

        private static Vector2 C(StadiumCell c) => StadiumTextures.CellUv(c);

        // Face helpers in stand-local coordinates.
        private void FaceD(StadiumMeshBuffer b, StandSide s, float d, float a0, float a1, float h0, float h1, bool facePitch, Vector2 uv)
            => b.Quad(W(s, d, a0, h0), W(s, d, a1, h0), W(s, d, a1, h1), W(s, d, a0, h1), facePitch ? -Out(s) : Out(s), uv);

        private void FaceH(StadiumMeshBuffer b, StandSide s, float h, float d0, float d1, float a0, float a1, bool up, Vector2 uv)
            => b.Quad(W(s, d0, a0, h), W(s, d1, a0, h), W(s, d1, a1, h), W(s, d0, a1, h), up ? Vector3.up : Vector3.down, uv);

        /// <summary>Cap face in the (d, h) plane at along-position a, facing +along or -along. Bottom edge may slope.</summary>
        private void FaceA(StadiumMeshBuffer b, StandSide s, float a, float d0, float d1, float hb0, float hb1, float ht0, float ht1, bool positive, Vector2 uv)
            => b.Quad(W(s, d0, a, hb0), W(s, d1, a, hb1), W(s, d1, a, ht1), W(s, d0, a, ht0), positive ? Alg(s) : -Alg(s), uv);

        private void Box(StadiumMeshBuffer b, StandSide s, float d0, float d1, float a0, float a1, float h0, float h1, Vector2 uv,
            bool front = true, bool back = true, bool top = true, bool bottom = false, bool ends = true)
        {
            if (front) FaceD(b, s, d0, a0, a1, h0, h1, true, uv);
            if (back) FaceD(b, s, d1, a0, a1, h0, h1, false, uv);
            if (top) FaceH(b, s, h1, d0, d1, a0, a1, true, uv);
            if (bottom) FaceH(b, s, h0, d0, d1, a0, a1, false, uv);
            if (ends)
            {
                FaceA(b, s, a0, d0, d1, h0, h0, h1, h1, false, uv);
                FaceA(b, s, a1, d0, d1, h0, h0, h1, h1, true, uv);
            }
        }

        private static StadiumMeshBuffer[] NewCrowdBuffers()
        {
            var b = new StadiumMeshBuffer[CrowdVariation.PhaseGroups];
            for (int i = 0; i < b.Length; i++) b[i] = new StadiumMeshBuffer();
            return b;
        }

        /// <summary>
        /// A row of crowd cards along the tread from <paramref name="left"/> to <paramref name="right"/> (as seen from the
        /// pitch), cut into hashed segments (<see cref="CrowdVariation.SegmentAt"/>). Each card stands on the tread and
        /// leans back 0.12 m along <paramref name="outward"/>.
        /// </summary>
        private static void CardLine(StadiumMeshBuffer[] groups, Vector3 left, Vector3 right, Vector3 outward, int band, uint rowKey)
        {
            Vector3 span = right - left;
            float len = span.magnitude;
            if (len < 0.2f) return;
            Vector3 dir = span / len;
            float sw = StadiumTextures.SeatWidth;
            float pos = 0f;
            for (int i = 0; pos < len - 0.05f && i < 4096; i++)
            {
                var seg = CrowdVariation.SegmentAt(rowKey, i);
                float l = Mathf.Min(seg.Seats * sw, len - pos);
                Vector3 b0 = left + dir * pos, b1 = left + dir * (pos + l);
                Vector3 up = outward * 0.12f + Vector3.up * (StadiumTextures.CrowdCardHeight * seg.HeightScale);
                CrowdVariation.RowV(band, seg.Variant, out float v0, out float v1);
                float u0 = seg.StartSeat / (float)CrowdVariation.SeatsPerStrip;
                float u1 = u0 + l / (sw * CrowdVariation.SeatsPerStrip);
                // Normal tilted sideways a little per segment: the sun and ambient light vary block to block.
                var n = (-outward * 0.55f + Vector3.up * 0.85f + dir * (0.22f * seg.Shade)).normalized;
                groups[seg.Group].Quad(b0, b1, b1 + up, b0 + up, n,
                    new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
                pos += l;
            }
        }

        /// <summary>
        /// LED strip along a board or fascia from along-position <paramref name="aLeft"/> to <paramref name="aRight"/>
        /// (left as seen from the pitch), cut into segments of 2-4 ads that each start on a hashed ad and belong to a
        /// hashed scroll phase group. Maps the ad row of the texture (v 0.5..1).
        /// </summary>
        private void LedLine(System.Func<float, Vector3> bottom, System.Func<float, Vector3> top, float aLeft, float aRight,
            Vector3 normal, int board)
        {
            float len = Mathf.Abs(aRight - aLeft), sign = Mathf.Sign(aRight - aLeft);
            float pos = 0f;
            for (int i = 0; pos < len - 0.05f && i < 1024; i++)
            {
                var seg = LedBoardSchedule.SegmentAt(_seed, board, i);
                float l = Mathf.Min(seg.Ads * LedBoardSchedule.AdMetres, len - pos);
                float a0 = aLeft + sign * pos, a1 = aLeft + sign * (pos + l);
                float u0 = seg.StartAd / (float)LedBoardSchedule.AdCount;
                float u1 = u0 + l / (LedBoardSchedule.AdMetres * LedBoardSchedule.AdCount);
                _ledBufs[seg.Group].Quad(bottom(a0), bottom(a1), top(a1), top(a0), normal,
                    new Vector2(u0, 0.5f), new Vector2(u1, 0.5f), new Vector2(u1, 1f), new Vector2(u0, 1f));
                pos += l;
            }
        }

        /// <summary>Splits [a0, a1] around aisle centres (and returns the solid pieces).</summary>
        private List<(float, float)> Pieces(float a0, float a1, List<float> aisles)
        {
            var list = new List<(float, float)>();
            float half = Layout.Spec.AisleWidth * 0.5f;
            float start = a0;
            foreach (var c in aisles)
            {
                if (c - half <= start || c + half >= a1) continue;
                list.Add((start, c - half));
                start = c + half;
            }
            if (a1 - start > 0.4f) list.Add((start, a1));
            return list;
        }

        private void Rows(StadiumMeshBuffer st, StadiumMeshBuffer[] crowd, StandSide s, StadiumTier t, int tier,
            float a0, float a1, List<float> aisles, bool firstRiser)
        {
            var conc = C(StadiumCell.Concrete);
            var tread = C(StadiumCell.Tread);
            for (int i = 0; i < t.Rows; i++)
            {
                float h = t.TreadHeight(i);
                FaceH(st, s, h, t.RowFront(i), t.RowBack(i), a0, a1, true, tread);
                if (i > 0 || firstRiser)
                {
                    float hPrev = i > 0 ? t.TreadHeight(i - 1) : 0f;
                    FaceD(st, s, t.RowFront(i), a0, a1, hPrev, h, true, conc);
                }
                int band = StadiumCrowd.BandFor(s, i + tier * 31);
                float d = t.RowFront(i) + 0.28f;
                int piece = 0;
                // Viewed from the pitch, +along runs right-to-left in the (outward, up, along) frame: start at p1.
                foreach (var (p0, p1) in Pieces(a0, a1, aisles))
                    CardLine(crowd, W(s, d, p1, h), W(s, d, p0, h), Out(s), band,
                        CrowdVariation.RowKey(_seed, (int)s, tier, i * 64 + piece++));
            }
        }

        /// <summary>Stepped cap of a tier at along-position a (columns under each tread down to bottom(d)).</summary>
        private void TierCap(StadiumMeshBuffer st, StandSide s, StadiumTier t, float a, bool positive, System.Func<float, float> bottom)
        {
            var uv = C(StadiumCell.Wall);
            for (int i = 0; i < t.Rows; i++)
            {
                float d0 = t.RowFront(i), d1 = t.RowBack(i);
                FaceA(st, s, a, d0, d1, bottom(d0), bottom(d1), t.TreadHeight(i), t.TreadHeight(i), positive, uv);
            }
        }

        private void BuildStand(StandSide s)
        {
            var p = Layout.Profile(s);
            float E = Layout.HalfExtent(s);
            var st = new StadiumMeshBuffer();
            var crowd = NewCrowdBuffers();
            var aisles = Layout.AisleCentres(s);
            var wall = C(StadiumCell.Wall);
            var dark = C(StadiumCell.ConcreteDark);
            var fascia = C(s == StandSide.AwayEnd ? StadiumCell.FasciaAway : StadiumCell.Fascia);

            // Lower tier, split by the tunnel on the near side.
            var segs = new List<(float, float)>();
            if (s == StandSide.Near)
            {
                Layout.TunnelSpan(out float t0, out float t1);
                segs.Add((-E, t0));
                segs.Add((t1, E));
            }
            else segs.Add((-E, E));
            foreach (var (a0, a1) in segs)
            {
                Box(st, s, p.MinDepth, p.Lower.FrontDepth, a0, a1, 0f, p.Lower.FirstTreadHeight, fascia, back: false, ends: false);
                Rows(st, crowd, s, p.Lower, 0, a0, a1, aisles, false);
                // Concourse front wall behind the lower tier, under the upper-tier overhang.
                FaceD(st, s, p.Lower.BackDepth, a0, a1, p.Lower.TopHeight, p.SoffitHeight(p.Lower.BackDepth), true, dark);
                TierCap(st, s, p.Lower, a0, false, d => 0f);
                TierCap(st, s, p.Lower, a1, true, d => 0f);
                FaceA(st, s, a0, p.MinDepth, p.Lower.FrontDepth, 0f, 0f, p.Lower.FirstTreadHeight, p.Lower.FirstTreadHeight, false, wall);
                FaceA(st, s, a1, p.MinDepth, p.Lower.FrontDepth, 0f, 0f, p.Lower.FirstTreadHeight, p.Lower.FirstTreadHeight, true, wall);
            }
            if (s == StandSide.Near)
            {
                // Tunnel slot: floor, dark mouth at the back.
                Layout.TunnelSpan(out float t0, out float t1);
                FaceH(st, s, 0.02f, p.MinDepth, p.Lower.BackDepth, t0, t1, true, dark);
                FaceD(st, s, p.Lower.BackDepth, t0, t1, 0f, Layout.Spec.TunnelHeight, true, C(StadiumCell.Tunnel));
                FaceD(st, s, p.Lower.BackDepth, t0, t1, Layout.Spec.TunnelHeight, p.SoffitHeight(p.Lower.BackDepth), true, dark);
            }

            // Upper tier.
            var u = p.Upper;
            float uf = u.FrontDepth, pf = uf - p.FrontWallThickness;
            float soffitF = p.SoffitHeight(uf), parTop = p.UpperParapetTop;
            float ledBottom = parTop - 0.9f;
            FaceD(st, s, pf, -E, E, soffitF, ledBottom, true, fascia);
            // LED ribbon along the upper-tier fascia (left as seen from the pitch is +along).
            LedLine(a => W(s, pf - 0.01f, a, ledBottom), a => W(s, pf - 0.01f, a, parTop), E, -E, -Out(s), 4 + (int)s);
            FaceH(st, s, parTop, pf, uf, -E, E, true, wall);
            FaceD(st, s, uf, -E, E, u.FirstTreadHeight, parTop, false, wall);
            Rows(st, crowd, s, u, 1, -E, E, aisles, false);
            // Soffit (underside), slightly lit from the pitch side so it is not a black slab.
            var soffitN = (Vector3.down * 0.7f - Out(s) * 0.7f).normalized;
            st.Quad(W(s, pf, -E, soffitF), W(s, u.BackDepth, -E, p.SoffitHeight(u.BackDepth)),
                W(s, u.BackDepth, E, p.SoffitHeight(u.BackDepth)), W(s, pf, E, soffitF), soffitN, dark);
            if (p.PlatformDepth > 0f)
            {
                FaceH(st, s, u.TopHeight, u.BackDepth, p.BackDepth, -E, E, true, C(StadiumCell.Concrete));
                FaceH(st, s, p.SoffitHeight(p.BackDepth), u.BackDepth, p.BackDepth, -E, E, false, dark);
            }
            // Back wall: inner face under the roof, outer face to the street.
            FaceD(st, s, p.BackDepth, -E, E, u.TopHeight, p.RoofBackHeight, true, wall);
            FaceD(st, s, p.OuterDepth, -E, E, 0f, p.RoofBackHeight, false, C(StadiumCell.Concrete));
            // Gable ends.
            for (int k = 0; k < 2; k++)
            {
                float a = k == 0 ? -E : E;
                bool pos = k == 1;
                TierCap(st, s, u, a, pos, d => p.SoffitHeight(d));
                FaceA(st, s, a, pf, uf, soffitF, soffitF, parTop, parTop, pos, wall);
                FaceA(st, s, a, p.Lower.BackDepth, p.BackDepth, 0f, 0f, p.SoffitHeight(p.Lower.BackDepth), p.SoffitHeight(p.BackDepth), pos, wall);
                if (p.PlatformDepth > 0f)
                    FaceA(st, s, a, u.BackDepth, p.BackDepth, p.SoffitHeight(u.BackDepth), p.SoffitHeight(p.BackDepth), u.TopHeight, u.TopHeight, pos, wall);
                FaceA(st, s, a, p.BackDepth, p.OuterDepth, 0f, 0f, p.RoofBackHeight, p.RoofBackHeight, pos, C(StadiumCell.Concrete));
                // Concourse gable under the overhang between lower back and upper front.
                FaceA(st, s, a, Mathf.Min(pf, p.Lower.BackDepth), p.Lower.BackDepth, 0f, 0f, p.SoffitHeight(pf), p.SoffitHeight(p.Lower.BackDepth), pos, wall);
            }

            BuildRoof(s, p, E);

            EmitCrowded("Stand " + s, st, crowd);
        }

        /// <summary>One shadow-casting mesh: the structure submesh plus one crowd submesh per bob phase group.</summary>
        private void EmitCrowded(string name, StadiumMeshBuffer st, StadiumMeshBuffer[] crowd)
        {
            var parts = new StadiumMeshBuffer[1 + crowd.Length];
            var mats = new Material[1 + crowd.Length];
            parts[0] = st;
            mats[0] = _structure;
            for (int g = 0; g < crowd.Length; g++)
            {
                parts[1 + g] = crowd[g];
                mats[1 + g] = _crowd[g];
            }
            var mesh = StadiumMeshBuffer.Combine(name, parts);
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            foreach (var b in parts) TriangleCount += b.TriangleCount;
            DrawCalls += parts.Length;
        }

        private void BuildRoof(StandSide s, StandProfile p, float E)
        {
            var b = _roof;
            float e = E + 0.5f;
            float df = p.RoofFrontDepth, db = p.OuterDepth + 0.3f;
            float hf = p.RoofFrontHeight, hb = p.RoofBackHeight;
            float th = p.RoofThickness;
            var top = Vector3.Cross(W(s, db, 0f, hb) - W(s, df, 0f, hf), Alg(s)).normalized;
            if (top.y < 0f) top = -top;
            b.Quad(W(s, df, -e, hf), W(s, db, -e, hb), W(s, db, e, hb), W(s, df, e, hf), top, C(StadiumCell.Roof));
            // Underside: normal tilted towards the pitch and up so the sun and ambient light it (reads as a lit soffit).
            var under = (-Out(s) * 0.8f + Vector3.up * 0.6f).normalized;
            b.Quad(W(s, df + 0.3f, -e, hf - th), W(s, db, -e, hb - th), W(s, db, e, hb - th), W(s, df + 0.3f, e, hf - th),
                Vector3.down, C(StadiumCell.RoofUnder), C(StadiumCell.RoofUnder), C(StadiumCell.RoofUnder), C(StadiumCell.RoofUnder));
            // Re-point the underside normals (the quad was wound to face down).
            for (int i = b.Normals.Count - 4; i < b.Normals.Count; i++) b.Normals[i] = under;
            // Fascia at the front edge.
            float fb = hf - p.RoofFasciaDepth;
            FaceD(b, s, df, -e, e, fb, hf, true, C(StadiumCell.White));
            FaceH(b, s, fb, df, df + 0.3f, -e, e, false, C(StadiumCell.ConcreteDark));
            FaceD(b, s, df + 0.3f, -e, e, fb, hf - th, false, C(StadiumCell.RoofUnder));
            for (int k = 0; k < 2; k++)
            {
                float a = k == 0 ? -e : e;
                FaceA(b, s, a, df, db, hf - th, hb - th, hf, hb, k == 1, C(StadiumCell.Roof));
                FaceA(b, s, a, df, df + 0.3f, fb, fb, hf, hf, k == 1, C(StadiumCell.White));
            }
            // Edge lights under the fascia, with halos.
            float spacing = Layout.Spec.RoofLampSpacing;
            int n = Mathf.Max(1, Mathf.FloorToInt(2f * E / spacing));
            for (int i = 0; i <= n; i++)
            {
                float a = -E + 2f * E * i / n;
                if (i == 0) a += 1.5f;
                if (i == n) a -= 1.5f;
                float d0 = df + 0.05f, d1 = df + 0.6f, h1 = fb, h0 = fb - 0.35f;
                FaceD(_lamps, s, d0, a - 0.8f, a + 0.8f, h0, h1, true, Vector2.zero);
                FaceH(_lamps, s, h0, d0, d1, a - 0.8f, a + 0.8f, false, Vector2.zero);
                Halo(W(s, d0 - 0.2f, a, h0 + 0.1f), -Out(s), Alg(s), 5.5f);
            }
        }

        /// <summary>Two crossed glow quads: one facing <paramref name="facing"/>, one tilted down 50 degrees.</summary>
        private void Halo(Vector3 c, Vector3 facing, Vector3 side, float size)
        {
            float r = size * 0.5f;
            var f = facing.normalized;
            var sd = side.normalized;
            var up = Vector3.Cross(f, sd).normalized;
            if (up.y < 0f) up = -up;
            Quad2(c, sd * r, up * r, f);
            var f2 = (f * Mathf.Cos(50f * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(50f * Mathf.Deg2Rad)).normalized;
            var up2 = Vector3.Cross(f2, sd).normalized;
            Quad2(c, sd * r, up2 * r, f2);
        }

        private void Quad2(Vector3 c, Vector3 rx, Vector3 ry, Vector3 n)
        {
            _halos.Quad(c - rx - ry, c + rx - ry, c + rx + ry, c - rx + ry, n,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }

        // ------------------------------------------------------------------ corners

        private void BuildCorners()
        {
            var st = new StadiumMeshBuffer();
            var crowd = NewCrowdBuffers();
            var t = Layout.SideStand.Lower;
            float setback = Layout.Spec.SideSetback;
            int segs = Mathf.Max(2, Layout.Spec.CornerSegments);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var cc = Layout.CornerCentre(sx, sz);
                var c = new Vector3(cc.X, 0f, cc.Z);
                Vector3 P(float r, float th, float h) => c + new Vector3(sx * Mathf.Cos(th) * r, h, sz * Mathf.Sin(th) * r);
                Vector3 Dir(float th) => new Vector3(sx * Mathf.Cos(th), 0f, sz * Mathf.Sin(th));
                int corner = (sx + 1) + (sz + 1) / 2;
                // Home end is -Z: bias the corner crowd to that end's colours.
                StandSide nearestEnd = sz < 0 ? StandSide.HomeEnd : StandSide.AwayEnd;
                for (int k = 0; k < segs; k++)
                {
                    float th0 = 0.5f * Mathf.PI * k / segs, th1 = 0.5f * Mathf.PI * (k + 1) / segs, tm = (th0 + th1) * 0.5f;
                    for (int i = 0; i < t.Rows; i++)
                    {
                        float r0 = t.RowFront(i) - setback, r1 = t.RowBack(i) - setback, h = t.TreadHeight(i);
                        st.Quad(P(r0, th0, h), P(r1, th0, h), P(r1, th1, h), P(r0, th1, h), Vector3.up, C(StadiumCell.Tread));
                        if (i > 0)
                            st.Quad(P(r0, th0, t.TreadHeight(i - 1)), P(r0, th1, t.TreadHeight(i - 1)), P(r0, th1, h), P(r0, th0, h), -Dir(tm), C(StadiumCell.Concrete));
                        float rc = r0 + 0.28f;
                        if (rc < 0.6f) continue;
                        int band = StadiumCrowd.BandFor(k < segs / 2 ? StandSide.Near : nearestEnd, i);
                        CardLine(crowd, P(rc, th0, h), P(rc, th1, h), Dir(tm), band,
                            CrowdVariation.RowKey(_seed, 8 + corner, k, i));
                    }
                    // Back wall of the corner fill: outer face and a parapet above the top row.
                    float rb = t.BackDepth - setback, top = t.TopHeight + 1.1f;
                    st.Quad(P(rb, th0, 0f), P(rb, th1, 0f), P(rb, th1, top), P(rb, th0, top), Dir(tm), C(StadiumCell.Concrete));
                    st.Quad(P(rb, th0, t.TopHeight), P(rb, th1, t.TopHeight), P(rb, th1, top), P(rb, th0, top), -Dir(tm), C(StadiumCell.Wall));
                    st.Quad(P(rb, th0, top), P(rb + 0.3f, th0, top), P(rb + 0.3f, th1, top), P(rb, th1, top), Vector3.up, C(StadiumCell.Wall));
                }
            }
            EmitCrowded("Corner Fills", st, crowd);
        }

        // ------------------------------------------------------------------ boards, dugouts, tunnel, pylons, apron

        private void BuildBoards()
        {
            var sp = Layout.Spec;
            float d = sp.BoardSetback, h = sp.BoardHeight, lean = h * Mathf.Tan(8f * Mathf.Deg2Rad), th = sp.BoardThickness;
            for (int si = 0; si < 4; si++)
            {
                var s = (StandSide)si;
                float e = (StadiumLayout.IsEnd(s) ? Layout.Pitch.HalfWidth : Layout.Pitch.HalfLength) + sp.BoardSetback;
                LedLine(a => W(s, d, a, 0f), a => W(s, d + lean, a, h), e, -e, -Out(s) + Vector3.up * 0.14f, si);
                var bk = C(StadiumCell.BoardBack);
                _misc.Quad(W(s, d + th, -e, 0f), W(s, d + th, e, 0f), W(s, d + th, e, h), W(s, d + th, -e, h), Out(s), bk);
                _misc.Quad(W(s, d + lean, -e, h), W(s, d + th, -e, h), W(s, d + th, e, h), W(s, d + lean, e, h), Vector3.up, bk);
            }
        }

        private void BuildNearSideFurniture()
        {
            var sp = Layout.Spec;
            var s = StandSide.Near;
            var glass = C(StadiumCell.Glass);
            var white = C(StadiumCell.White);
            float d0 = sp.DugoutFront, d1 = sp.DugoutFront + sp.DugoutDepth, hh = sp.DugoutHeight;
            foreach (var ac in Layout.DugoutCentres)
            {
                float a0 = ac - sp.DugoutWidth * 0.5f, a1 = ac + sp.DugoutWidth * 0.5f;
                Box(_misc, s, d1 - 0.12f, d1, a0, a1, 0f, hh, glass, front: true, back: true, top: false, ends: false);
                Box(_misc, s, d0 - 0.2f, d1, a0, a1, hh, hh + 0.15f, white, bottom: true);
                Box(_misc, s, d0, d1 - 0.12f, a0, a0 + 0.08f, 0f, hh, glass, top: false);
                Box(_misc, s, d0, d1 - 0.12f, a1 - 0.08f, a1, 0f, hh, glass, top: false);
                Box(_misc, s, d1 - 0.75f, d1 - 0.12f, a0 + 0.2f, a1 - 0.2f, 0f, 0.48f, C(StadiumCell.Bench), back: false);
                Box(_misc, s, d1 - 0.3f, d1 - 0.12f, a0 + 0.2f, a1 - 0.2f, 0.48f, 1.05f, C(StadiumCell.Bench), back: false, ends: false);
            }
            // Players' tunnel canopy from the stand front towards the pitch.
            Layout.TunnelSpan(out float t0, out float t1);
            var p = Layout.MainStand;
            float c0 = sp.DugoutFront + 0.3f, c1 = p.MinDepth, th = sp.TunnelHeight;
            Box(_misc, s, c0, c1, t0 - 0.15f, t1 + 0.15f, th, th + 0.15f, white, back: false, bottom: true);
            Box(_misc, s, c0, c1, t0 - 0.15f, t0, 0f, th, glass, back: false);
            Box(_misc, s, c0, c1, t1, t1 + 0.15f, 0f, th, glass, back: false);
            FaceH(_misc, s, 0.015f, c0, c1, t0, t1, true, C(StadiumCell.ConcreteDark));
        }

        private void BuildPylons()
        {
            var sp = Layout.Spec;
            var steel = C(StadiumCell.Steel);
            var frame = C(StadiumCell.LampFrame);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var pb = Layout.PylonBase(sx, sz);
                var b = new Vector3(pb.X, 0f, pb.Z);
                float H = sp.PylonHeight;
                // Tapered square mast.
                float r0 = 1.1f, r1 = 0.55f;
                var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) };
                for (int k = 0; k < 4; k++)
                {
                    var a = corners[k];
                    var c = corners[(k + 1) % 4];
                    var n = (a + c).normalized;
                    _misc.Quad(b + a * r0, b + c * r0, b + c * r1 + Vector3.up * H, b + a * r1 + Vector3.up * H, n, steel);
                }
                // Head frame facing the centre spot, tilted down.
                var head = b + Vector3.up * (H + 2.5f);
                var toCentre = new Vector3(-b.x, 0f, -b.z).normalized;
                var face = (toCentre * Mathf.Cos(22f * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(22f * Mathf.Deg2Rad)).normalized;
                var side = Vector3.Cross(Vector3.up, toCentre).normalized;
                var up = Vector3.Cross(face, side).normalized;
                if (up.y < 0f) up = -up;
                float hw = 5f, hhh = 3f, depth = 0.6f;
                Vector3 F(float x, float y, float z) => head + side * x + up * y - face * z;
                _misc.Quad(F(-hw, -hhh, depth), F(hw, -hhh, depth), F(hw, hhh, depth), F(-hw, hhh, depth), -face, frame);
                _misc.Quad(F(-hw, -hhh, 0f), F(hw, -hhh, 0f), F(hw, hhh, 0f), F(-hw, hhh, 0f), face, frame);
                _misc.Quad(F(-hw, hhh, 0f), F(hw, hhh, 0f), F(hw, hhh, depth), F(-hw, hhh, depth), up, frame);
                _misc.Quad(F(-hw, -hhh, 0f), F(hw, -hhh, 0f), F(hw, -hhh, depth), F(-hw, -hhh, depth), -up, frame);
                _misc.Quad(F(-hw, -hhh, 0f), F(-hw, hhh, 0f), F(-hw, hhh, depth), F(-hw, -hhh, depth), -side, frame);
                _misc.Quad(F(hw, -hhh, 0f), F(hw, hhh, 0f), F(hw, hhh, depth), F(hw, -hhh, depth), side, frame);
                // 5 x 3 lamp grid on the front.
                for (int ix = 0; ix < 5; ix++)
                for (int iy = 0; iy < 3; iy++)
                {
                    float x0 = -hw + 0.4f + ix * (2f * hw - 0.8f) / 5f, x1 = x0 + (2f * hw - 0.8f) / 5f - 0.35f;
                    float y0 = -hhh + 0.4f + iy * (2f * hhh - 0.8f) / 3f, y1 = y0 + (2f * hhh - 0.8f) / 3f - 0.35f;
                    _lamps.Quad(F(x0, y0, -0.02f), F(x1, y0, -0.02f), F(x1, y1, -0.02f), F(x0, y1, -0.02f), face, Vector2.zero);
                }
                Halo(head + face * 0.6f, face, side, 22f);
            }
        }

        private void BuildApron()
        {
            float ix = Layout.Pitch.HalfWidth + Layout.Spec.RunOff - 0.05f, iz = Layout.Pitch.HalfLength + Layout.Spec.RunOff - 0.05f;
            float ox = ix + 60f, oz = iz + 60f, y = -0.01f;
            var uv = C(StadiumCell.Apron);
            _misc.Quad(new Vector3(-ox, y, iz), new Vector3(ox, y, iz), new Vector3(ox, y, oz), new Vector3(-ox, y, oz), Vector3.up, uv);
            _misc.Quad(new Vector3(-ox, y, -oz), new Vector3(ox, y, -oz), new Vector3(ox, y, -iz), new Vector3(-ox, y, -iz), Vector3.up, uv);
            _misc.Quad(new Vector3(ix, y, -iz), new Vector3(ox, y, -iz), new Vector3(ox, y, iz), new Vector3(ix, y, iz), Vector3.up, uv);
            _misc.Quad(new Vector3(-ox, y, -iz), new Vector3(-ix, y, -iz), new Vector3(-ix, y, iz), new Vector3(-ox, y, iz), Vector3.up, uv);
        }
    }
}
