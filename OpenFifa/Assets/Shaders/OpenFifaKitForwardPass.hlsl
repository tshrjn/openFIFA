#ifndef OPENFIFA_KIT_FORWARD_PASS_INCLUDED
#define OPENFIFA_KIT_FORWARD_PASS_INCLUDED

// UniversalForward pass for OpenFifa/Kit. Structure mirrors URP 17 LitForwardPass.hlsl so all
// URP lighting features (shadows, additional lights, Forward+/cluster loop, SSAO, fog,
// reflection probes, light layers) behave like Lit. The fabric sheen lobe is added on top.

#include "OpenFifaKitSurface.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct KitAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct KitVaryings
{
    float2 uv                : TEXCOORD0;
    float3 positionWS        : TEXCOORD1;
    float3 normalWS          : TEXCOORD2;
    half4  tangentWS         : TEXCOORD3;   // xyz tangent, w sign
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4  fogFactorAndVertexLight : TEXCOORD4;
#else
    half   fogFactor         : TEXCOORD4;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord       : TEXCOORD5;
#endif
    float3 positionOS        : TEXCOORD6;
    float3 normalOS          : TEXCOORD7;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion    : TEXCOORD9;
#endif
    float4 positionCS        : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

KitVaryings KitForwardVertex(KitAttributes input)
{
    KitVaryings output = (KitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half fogFactor = 0;
#if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = input.texcoord;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
    output.positionOS = input.positionOS.xyz;
    output.normalOS = input.normalOS;

    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;
    return output;
}

// Charlie sheen distribution (Estevez & Kulla 2017) with the Neubelt visibility approximation.
half3 KitSheenLobe(half3 sheenColor, half roughness, half3 N, half3 V, half3 L, half3 lightColor)
{
    half3 H = SafeNormalize(L + V);
    half NdotL = saturate(dot(N, L));
    half NdotV = saturate(dot(N, V));
    half NdotH = saturate(dot(N, H));
    half invR = 1.0 / max(roughness, 0.07);
    half sin2h = max(1.0 - NdotH * NdotH, 0.0078125);
    half D = (2.0 + invR) * pow(sin2h, invR * 0.5) * INV_TWO_PI;
    half Vis = 1.0 / max(4.0 * (NdotL + NdotV - NdotL * NdotV), 1e-3);
    return sheenColor * (D * Vis * NdotL) * lightColor;
}

void KitForwardFragment(
    KitVaryings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    float3 normalOS = normalize(input.normalOS);
    KitSpace kit = KitComputeSpace(input.positionOS, normalOS, input.uv);
    KitSurface surf = KitBuildSurface(kit);

    // --- Normal: geometric -> knit / licensed detail -> heat-press bevel --------------------
    float3 geomNormalWS = normalize(input.normalWS);
    float3 tangentWS;
    float3 bitangentWS;
    if (kit.isObjectMode > 0.5)
    {
        tangentWS = TransformObjectToWorldDir(kit.knitTangentOS);
        tangentWS = normalize(tangentWS - geomNormalWS * dot(geomNormalWS, tangentWS));
        bitangentWS = cross(geomNormalWS, tangentWS);
    }
    else
    {
        tangentWS = normalize(input.tangentWS.xyz);
        bitangentWS = input.tangentWS.w * cross(geomNormalWS, tangentWS);
    }
    half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, geomNormalWS);
    float3 normalWS = TransformTangentToWorld(surf.normalTS, tangentToWorld);
    normalWS = KitPerturbNormalByHeight(normalize(normalWS), input.positionWS, surf.printHeight);
    normalWS = NormalizeNormalPerPixel(normalWS);

    // --- URP InputData (as LitForwardPass) --------------------------------------------------
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    // Players are dynamic: light probes / ambient SH only, no lightmaps.
    inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(0);

    // --- Fabric BRDF inputs: dielectric, soft, low specular -------------------------------
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = surf.albedo;
    surfaceData.metallic = 0.0;
    surfaceData.specular = half3(0.0, 0.0, 0.0);
    surfaceData.smoothness = surf.smoothness;
    surfaceData.occlusion = surf.occlusion;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = 0;
    surfaceData.alpha = 1.0;

    half4 color = UniversalFragmentPBR(inputData, surfaceData);

    // --- Sheen: soft grazing highlight of polyester knit -----------------------------------
    half sheenAmount = _SheenStrength * surf.sheenMask;
    if (sheenAmount > 0.001)
    {
        half3 sheenColor = _SheenColor.rgb * lerp(half3(1, 1, 1), surf.albedo, 0.5) * sheenAmount;
        half3 V = inputData.viewDirectionWS;
        half3 N = inputData.normalWS;

        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
        half3 mainColor = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
        half3 sheen = KitSheenLobe(sheenColor, _SheenRoughness, N, V, mainLight.direction, mainColor);

        // Ambient sheen: rim brightening from the sky / probes.
        half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
        sheen += sheenColor * fresnel * inputData.bakedGI * surf.occlusion;

        color.rgb += sheen;
    }

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // OPENFIFA_KIT_FORWARD_PASS_INCLUDED
