// OpenFifa football kit shader (URP 17 / Unity 6.3).
//
// - Procedural kits from three colours: solid, stripes, hoops, halves, gradient (halftone),
//   pinstripes, chevron, sash (values match OpenFifa.Core.JerseyPattern).
// - Polyester knit micro-normal, Charlie sheen lobe, low-specular dielectric fabric.
// - Licensed override textures (_KitAlbedo/_KitNormal/_KitMask) take priority over the pattern
//   when _UseLicensedTextures = 1. Name/number prints still layer on top.
// - Name/number atlas (_NameNumberTex, generated at runtime by KitPrintTextureBuilder):
//   R = fill, G = fill+outline, B = edge height (heat-press bevel). Colours come from
//   _NumberColor/_NumberOutlineColor, so recolouring never regenerates the texture.
// - Crest and sponsor decal slots, sweat/dirt wear driven by _Sweat/_Dirt.
// - _MappingMode 0: object-space mapping for the capsule/proxy body (cylindrical around Y,
//   shirt/shorts/socks/boots bands from _BodyLines). 1: mesh UVs for a skinned jersey mesh,
//   with print panels given as UV rects.
//
// Passes match URP Lit: ForwardLit, ShadowCaster, DepthOnly, DepthNormals, MotionVectors.
// All scalar/vector properties live in the UnityPerMaterial CBUFFER (SRP Batcher compatible).
Shader "OpenFifa/Kit"
{
    Properties
    {
        [Header(Kit Colours)]
        _PrimaryColor("Primary", Color) = (0.12, 0.25, 0.75, 1)
        _SecondaryColor("Secondary", Color) = (1, 1, 1, 1)
        _TertiaryColor("Tertiary (trim)", Color) = (0.95, 0.8, 0.2, 1)
        _ShortsColor("Shorts", Color) = (1, 1, 1, 1)
        _SocksColor("Socks", Color) = (0.12, 0.25, 0.75, 1)
        _SkinColor("Skin (proxy body)", Color) = (0.62, 0.45, 0.35, 1)
        _BootColor("Boots (proxy body)", Color) = (0.05, 0.05, 0.06, 1)

        [Header(Pattern)]
        [Enum(Solid,0,Stripes,1,Hoops,2,Halves,3,Gradient,4,Pinstripes,5,Chevron,6,Sash,7)]
        _PatternType("Pattern", Float) = 0
        _PatternScale("Pattern Scale", Range(0.1, 10)) = 1
        [Enum(VNeck,0,Round,1,Polo,2)] _CollarStyle("Collar", Float) = 1

        [Header(Mapping)]
        [Enum(ObjectSpace,0,MeshUV,1)] _MappingMode("Mapping", Float) = 0
        _FacingAngle("Facing Angle (rad, object mode)", Float) = 0
        _BodyLines("Body Lines OS Y (neck, hem, shorts, boots)", Vector) = (0.62, -0.05, -0.52, -0.9)
        _BackPrintRect("Back Print Rect", Vector) = (0, 0.33, 0.46, 0.46)
        _FrontPrintRect("Front Print Rect", Vector) = (0, 0.33, 0.46, 0.46)

        [Header(Name and Number)]
        [NoScaleOffset] _NameNumberTex("Name/Number Atlas (R fill, G outline, B height)", 2D) = "black" {}
        [ToggleUI] _UseNameNumber("Use Name/Number", Float) = 0
        _NumberColor("Number Colour", Color) = (1, 1, 1, 1)
        _NumberOutlineColor("Number Outline Colour", Color) = (0, 0, 0, 1)
        _PrintSmoothness("Print Gloss", Range(0, 1)) = 0.55
        _PrintBevel("Print Edge Height (m)", Range(0, 0.003)) = 0.0008

        [Header(Decals)]
        [NoScaleOffset] _CrestTex("Crest", 2D) = "black" {}
        [ToggleUI] _UseCrest("Use Crest", Float) = 0
        _CrestRect("Crest Rect (front panel: centre xy, size zw)", Vector) = (0.72, 0.8, 0.13, 0.13)
        [NoScaleOffset] _SponsorTex("Sponsor", 2D) = "black" {}
        [ToggleUI] _UseSponsor("Use Sponsor", Float) = 0
        _SponsorRect("Sponsor Rect (front panel: centre xy, size zw)", Vector) = (0.5, 0.52, 0.5, 0.16)

        [Header(Fabric)]
        _FabricSmoothness("Fabric Smoothness", Range(0, 1)) = 0.3
        _KnitScale("Knit Frequency (per metre)", Float) = 280
        _KnitStrength("Knit Strength", Range(0, 1)) = 0.35
        _WrinkleStrength("Wrinkle Strength", Range(0, 0.3)) = 0.06
        _SheenColor("Sheen Colour", Color) = (1, 1, 1, 1)
        _SheenStrength("Sheen Strength", Range(0, 1)) = 0.5
        _SheenRoughness("Sheen Roughness", Range(0.07, 1)) = 0.45

        [Header(Licensed Textures)]
        [ToggleUI] _UseLicensedTextures("Use Licensed Textures", Float) = 0
        [NoScaleOffset] _KitAlbedo("Kit Albedo", 2D) = "white" {}
        [NoScaleOffset][Normal] _KitNormal("Kit Normal", 2D) = "bump" {}
        [NoScaleOffset] _KitMask("Kit Mask (R AO, G smooth, B fabric, A wear)", 2D) = "white" {}
        [ToggleUI] _LicensedHasNormal("Has Normal", Float) = 0
        [ToggleUI] _LicensedHasMask("Has Mask", Float) = 0
        _LicensedNormalScale("Normal Scale", Range(0, 2)) = 1

        [Header(Match Wear)]
        _Sweat("Sweat", Range(0, 1)) = 0
        _Dirt("Dirt", Range(0, 1)) = 0
        _DirtColor("Dirt Colour", Color) = (0.24, 0.2, 0.12, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex KitForwardVertex
            #pragma fragment KitForwardFragment

            // Universal Pipeline keywords (as URP 17.3 Lit)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords (no lightmaps: players are dynamic)
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "OpenFifaKitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex KitShadowPassVertex
            #pragma fragment KitShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "OpenFifaKitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex KitDepthVertex
            #pragma fragment KitDepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "OpenFifaKitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex KitDepthVertex
            #pragma fragment KitDepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "OpenFifaKitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "OpenFifaKitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
