using System.Collections.Generic;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Runtime-generated broadcast turf on stock URP/Lit (SRP-batcher friendly, no custom shader):
    /// - _BaseMap (1024 x 512, sRGB, covers pitch + run-off once): olive base, soft mow stripes, macro variation,
    ///   goalmouth / spot wear; alpha = smoothness (stripe-dependent sheen).
    /// - _BumpMap (512 x 256): ground undulation and per-stripe blade lean (view-dependent stripe sheen).
    /// - _DetailAlbedoMap / _DetailNormalMap (512 x 512, linear, tiling every ~3 m): blade speckle; their mips fade to
    ///   neutral, which is the distance blend.
    /// All textures are mipmapped, trilinear, anisotropic 8, made non-readable after upload (no CPU copy kept).
    /// Keyword variants (_NORMALMAP, _DETAIL_MULX2, _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) ship in player builds via the
    /// template material Resources/OpenFootball/ShaderKeep/KeepTurfLit.mat, which this clones.
    /// </summary>
    public static class TurfTextures
    {
        public const string TemplateResource = "OpenFootball/ShaderKeep/KeepTurfLit";
        public const int BaseWidth = 1024, BaseHeight = 512, NormalWidth = 512, NormalHeight = 256, DetailSize = 512;

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>
        /// Mobile URP decodes normal maps as XYZ or (DXT5nm-style setting) AG; desktop decodes AG*R / RG. Writing x into
        /// alpha satisfies both mobile decoders; desktop needs alpha = 1.
        /// </summary>
        public static bool NormalAlphaHoldsX
        {
            get
            {
#if UNITY_IOS || UNITY_ANDROID || UNITY_VISIONOS || UNITY_TVOS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Turf material for a surface of surfaceLength x surfaceWidth (field plus run-off) whose UV0 spans it once
        /// (u along the length). Cached per geometry and settings, so rebuilding the pitch does not regenerate.
        /// </summary>
        public static Material GetMaterial(float surfaceLength, float surfaceWidth, float fieldLength, float fieldWidth, TurfSettings settings)
        {
            settings = settings ?? new TurfSettings();
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}x{1}/{2}x{3}/{4}",
                surfaceLength, surfaceWidth, fieldLength, fieldWidth, settings.CacheKey());
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mat = CreateBaseMaterial();
            if (mat == null) return null;
            mat.name = "ProceduralTurf";
            bool ax = NormalAlphaHoldsX;

            var baseMap = MakeTexture("TurfBase", BaseWidth, BaseHeight, false, TextureWrapMode.Clamp,
                TurfMath.BaseMap(BaseWidth, BaseHeight, surfaceLength, surfaceWidth, fieldLength, fieldWidth, settings));
            var normal = MakeTexture("TurfNormal", NormalWidth, NormalHeight, true, TextureWrapMode.Clamp,
                TurfMath.BaseNormalMap(NormalWidth, NormalHeight, surfaceLength, surfaceWidth, fieldLength, fieldWidth, settings, ax));
            var detail = MakeTexture("TurfDetail", DetailSize, DetailSize, true, TextureWrapMode.Repeat,
                TurfMath.DetailAlbedo(DetailSize, settings));
            var detailN = MakeTexture("TurfDetailNormal", DetailSize, DetailSize, true, TextureWrapMode.Repeat,
                TurfMath.DetailNormal(DetailSize, settings, ax));

            SetTex(mat, "_BaseMap", baseMap);
            SetTex(mat, "_MainTex", baseMap);
            // Slightly green tint (not white) so legacy checks of material.color still read as grass; negligible on the albedo.
            SetColor(mat, "_BaseColor", new Color(0.985f, 1f, 0.985f, 1f));
            SetColor(mat, "_Color", new Color(0.985f, 1f, 0.985f, 1f));
            SetTex(mat, "_BumpMap", normal);
            SetFloat(mat, "_BumpScale", 1f);
            SetTex(mat, "_DetailAlbedoMap", detail);
            SetTex(mat, "_DetailNormalMap", detailN);
            SetFloat(mat, "_DetailAlbedoMapScale", 1f);
            SetFloat(mat, "_DetailNormalMapScale", 0.6f);
            TurfMath.DetailTiling(surfaceLength, surfaceWidth, settings.DetailTileMetres, out float tx, out float ty);
            if (mat.HasProperty("_DetailAlbedoMap")) mat.SetTextureScale("_DetailAlbedoMap", new Vector2(tx, ty));
            SetFloat(mat, "_Metallic", 0f);
            SetFloat(mat, "_Smoothness", 1f);               // multiplier on the base-map alpha
            SetFloat(mat, "_SmoothnessTextureChannel", 1f); // albedo alpha
            SetFloat(mat, "_SpecularHighlights", 1f);
            SetFloat(mat, "_EnvironmentReflections", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_DETAIL_MULX2");
            mat.DisableKeyword("_DETAIL_SCALED");
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            Cache[key] = mat;
            return mat;
        }

        private static Material CreateBaseMaterial()
        {
            var template = Resources.Load<Material>(TemplateResource);
            if (template != null) return new Material(template);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // batch mode without URP
            return shader != null ? new Material(shader) : null;
        }

        private static Texture2D MakeTexture(string name, int w, int h, bool linear, TextureWrapMode wrap, byte[] data)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixelData(data, 0);
            tex.Apply(true, true);
            return tex;
        }

        private static void SetTex(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
        private static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        private static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }

        /// <summary>
        /// Up-facing quad for a unit cube's top face (local y = 0.5, x and z in [-0.5, 0.5]); UV u along x, v along z,
        /// tangent +x with w = -1 so the normal map's green points to +z.
        /// </summary>
        public static Mesh BuildTopQuad()
        {
            var mesh = new Mesh { name = "TurfTop" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var t = new Vector4(1f, 0f, 0f, -1f);
            mesh.tangents = new[] { t, t, t, t };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 }; // clockwise seen from above
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
