#ifndef OPENFIFA_KIT_INPUT_INCLUDED
#define OPENFIFA_KIT_INPUT_INCLUDED

// Shared material inputs for every pass of OpenFifa/Kit.
// All non-texture properties live in UnityPerMaterial so the shader stays SRP Batcher compatible.
// Keep this block identical for all passes (it is, since every pass includes this file).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    // Kit colours (sRGB authored; Unity converts Color properties to linear in linear colour space)
    half4 _PrimaryColor;
    half4 _SecondaryColor;
    half4 _TertiaryColor;
    half4 _ShortsColor;
    half4 _SocksColor;
    half4 _SkinColor;
    half4 _BootColor;
    half4 _GloveColor;
    half4 _BootAccentColor;

    // Pattern
    float _PatternType;      // OpenFifa.Core.JerseyPattern
    float _PatternScale;
    float _CollarStyle;      // OpenFifa.Core.CollarStyle

    // Mapping
    float _MappingMode;      // 0 = object space (capsule / full-body proxy), 1 = mesh UV (jersey mesh), 2 = skinned body map
    float _FacingAngle;      // object mode: kit front direction around object Y, radians
    float4 _BodyLines;       // object mode, object-space Y: x neckline, y shirt hem, z shorts hem, w boot top
    float4 _BackPrintRect;   // object mode: centre x/y, width, height in metres. UV mode: uv min xy, size zw
    float4 _FrontPrintRect;  // same convention as _BackPrintRect, for the front panel
    float4 _BodyLimbLimits;  // skinned mode: x sleeve end armT, y glove start armT, z shorts hem legT, w sock top legT
    float4 _BodyTorsoLines;  // skinned mode: x waist Y (kit space), y boot top legT, z torso half width, w half depth
    float _BodyEdgeSoftness; // skinned mode: extra edge blur in limb-parameter / metre units
    half _SkinWrap;
    half4 _HairColor;
    float4 _HeadStyle;       // x hair style (0 bald, 1 buzz, 2 short, 3 braids), y facial hair (0 none, 1 stubble, 2 beard, 3 goatee, 4 moustache), z hairline offset, w eye tone
    float4 _HeadParams;      // kit-space head centre y, centre z, half width, half depth          // wrap-diffuse amount on skin (soft subsurface-like terminator)

    // Name / number print
    half4 _NumberColor;
    half4 _NumberOutlineColor;
    float _UseNameNumber;
    half _PrintSmoothness;
    half _PrintBevel;

    // Decals (rects in front-panel normalized space: centre xy, size zw)
    float _UseCrest;
    float4 _CrestRect;
    float _UseSponsor;
    float4 _SponsorRect;

    // Fabric
    half _FabricSmoothness;
    half _KnitScale;
    half _KnitStrength;
    half _WrinkleStrength;
    half4 _SheenColor;
    half _SheenStrength;
    half _SheenRoughness;

    // Licensed texture overrides
    float _UseLicensedTextures;
    float _LicensedHasNormal;
    float _LicensedHasMask;
    half _LicensedNormalScale;

    // Match wear
    half _Sweat;
    half _Dirt;
    half4 _DirtColor;
CBUFFER_END

TEXTURE2D(_NameNumberTex);  SAMPLER(sampler_NameNumberTex);
TEXTURE2D(_CrestTex);       SAMPLER(sampler_CrestTex);
TEXTURE2D(_SponsorTex);     SAMPLER(sampler_SponsorTex);
TEXTURE2D(_KitAlbedo);      SAMPLER(sampler_KitAlbedo);
TEXTURE2D(_KitNormal);      SAMPLER(sampler_KitNormal);
TEXTURE2D(_KitMask);        SAMPLER(sampler_KitMask);

#endif // OPENFIFA_KIT_INPUT_INCLUDED
