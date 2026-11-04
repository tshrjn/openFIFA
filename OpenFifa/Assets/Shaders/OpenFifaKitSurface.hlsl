#ifndef OPENFIFA_KIT_SURFACE_INCLUDED
#define OPENFIFA_KIT_SURFACE_INCLUDED

// Procedural football kit surface: mapping, patterns, prints, fabric micro-detail, wear.
// Engine-agnostic maths only; lighting is in OpenFifaKitForwardPass.hlsl.

#include "OpenFifaKitInput.hlsl"

// Regions for the object-space (capsule / proxy body) mapping.
#define KIT_REGION_SHIRT  0
#define KIT_REGION_SHORTS 1
#define KIT_REGION_SOCKS  2
#define KIT_REGION_BOOTS  3
#define KIT_REGION_SKIN   4

// Mirrors OpenFifa.Core.JerseyPattern.
#define KIT_PATTERN_SOLID      0
#define KIT_PATTERN_STRIPES    1
#define KIT_PATTERN_HOOPS      2
#define KIT_PATTERN_HALVES     3
#define KIT_PATTERN_GRADIENT   4
#define KIT_PATTERN_PINSTRIPES 5
#define KIT_PATTERN_CHEVRON    6
#define KIT_PATTERN_SASH       7

// Mirrors OpenFifa.Core.CollarStyle.
#define KIT_COLLAR_VNECK 0
#define KIT_COLLAR_ROUND 1
#define KIT_COLLAR_POLO  2

// Print atlas layout (matches OpenFifa.Core.KitPrintLayout): left half back, right half front.
#define KIT_ATLAS_PANEL_WIDTH 0.5

struct KitSpace
{
    float2 uv;           // x: around the body / across the panel (0.5 = front centre in object mode), y: 0 hem .. 1 neckline
    float3 kitPos;       // object mode: object position scaled to metres, rotated so the kit front is +Z
    float  region;       // KIT_REGION_*
    float2 knitCoord;    // metres along (around, up) the surface, drives micro detail
    float3 knitTangentOS;
    float3 knitBitangentOS;
    float2 backUV;       // back print panel coordinates 0..1 (outside = no print)
    float  backMask;
    float2 frontUV;      // front print panel coordinates 0..1 (viewer-facing)
    float  frontMask;
    float2 licensedUV;   // uv used to sample licensed textures
    float  isObjectMode;
};

float3 KitObjectScale()
{
    float4x4 m = GetObjectToWorldMatrix();
    return float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
}

float KitInside01(float2 uv)
{
    float2 inside = step(0.0, uv) * step(uv, 1.0);
    return inside.x * inside.y;
}

// Anti-aliased periodic step: fraction of [x - w/2, x + w/2] covered by the "on" part
// (last `duty` of each period). Box-filtered analytically, so stripes never shimmer.
float KitFilteredStripe(float x, float duty)
{
    float w = max(fwidth(x), 1e-4);
    float2 x2 = float2(x - 0.5 * w, x + 0.5 * w);
    float2 integral = floor(x2) * duty + max(frac(x2) - (1.0 - duty), 0.0);
    return saturate((integral.y - integral.x) / w);
}

// Anti-aliased band |d| < halfWidth.
float KitBand(float d, float halfWidth)
{
    float aa = max(fwidth(d), 1e-4);
    return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(d));
}

float KitHash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float KitValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(KitHash31(i + float3(0, 0, 0)), KitHash31(i + float3(1, 0, 0)), f.x),
             lerp(KitHash31(i + float3(0, 1, 0)), KitHash31(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(KitHash31(i + float3(0, 0, 1)), KitHash31(i + float3(1, 0, 1)), f.x),
             lerp(KitHash31(i + float3(0, 1, 1)), KitHash31(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

// ----------------------------------------------------------------------------------------
// Mapping
// ----------------------------------------------------------------------------------------

KitSpace KitComputeSpace(float3 positionOS, float3 normalOS, float2 meshUV)
{
    KitSpace k = (KitSpace)0;

    if (_MappingMode < 0.5)
    {
        // Object-space mapping for the capsule proxy (and any upright body proxy):
        // cylindrical around object Y, front = +Z rotated by _FacingAngle.
        k.isObjectMode = 1.0;
        float3 s = KitObjectScale();
        float3 p = positionOS * s;

        float sa, ca;
        sincos(_FacingAngle, sa, ca);
        // Rotate so the kit front direction (sin a, 0, cos a) maps to +Z.
        float3 kp = float3(p.x * ca - p.z * sa, p.y, p.x * sa + p.z * ca);
        float3 kn = float3(normalOS.x * ca - normalOS.z * sa, normalOS.y, normalOS.x * sa + normalOS.z * ca);
        k.kitPos = kp;

        float theta = atan2(kp.x, kp.z);                 // 0 = front centre, +-PI = back centre
        float radius = max(length(kp.xz), 1e-3);

        float neck = _BodyLines.x * s.y;
        float hem = _BodyLines.y * s.y;
        k.uv = float2(theta * INV_TWO_PI + 0.5, (kp.y - hem) / max(neck - hem, 1e-3));

        k.region = KIT_REGION_SHIRT;
        if (kp.y > neck)                       k.region = KIT_REGION_SKIN;
        else if (kp.y < _BodyLines.w * s.y)    k.region = KIT_REGION_BOOTS;
        else if (kp.y < _BodyLines.z * s.y)    k.region = KIT_REGION_SOCKS;
        else if (kp.y < hem)                   k.region = KIT_REGION_SHORTS;

        // Knit runs around the body; the direction is independent of facing.
        float thetaObj = atan2(positionOS.x, positionOS.z);
        float sObj, cObj;
        sincos(thetaObj, sObj, cObj);
        k.knitTangentOS = float3(cObj, 0.0, -sObj);
        k.knitBitangentOS = float3(0.0, 1.0, 0.0);
        k.knitCoord = float2(theta * radius, kp.y);

        // Planar prints. Back panel is seen from behind: viewer's right = +X (kit space).
        k.backUV = float2((kp.x - _BackPrintRect.x) / _BackPrintRect.z + 0.5,
                          (kp.y - _BackPrintRect.y) / _BackPrintRect.w + 0.5);
        k.frontUV = float2((-kp.x - _FrontPrintRect.x) / _FrontPrintRect.z + 0.5,
                           (kp.y - _FrontPrintRect.y) / _FrontPrintRect.w + 0.5);
        float shirt = (k.region == KIT_REGION_SHIRT) ? 1.0 : 0.0;
        k.backMask = KitInside01(k.backUV) * saturate((-kn.z - 0.1) * 4.0) * shirt;
        k.frontMask = KitInside01(k.frontUV) * saturate((kn.z - 0.1) * 4.0) * shirt;

        k.licensedUV = k.uv;
    }
    else
    {
        // Mesh UV mapping for a skinned jersey mesh. uv.x across the garment, uv.y up.
        // Print panels are UV rects (min xy, size zw) for the specific mesh layout.
        k.isObjectMode = 0.0;
        k.uv = meshUV;
        k.kitPos = float3((meshUV.x - 0.5), meshUV.y - 0.5, 0.0);
        k.region = KIT_REGION_SHIRT;
        k.knitTangentOS = 0;    // signals: use the mesh tangent frame
        k.knitBitangentOS = 0;
        k.knitCoord = meshUV * 1.6;   // roughly metres for an adult jersey UV layout

        k.backUV = (meshUV - _BackPrintRect.xy) / max(_BackPrintRect.zw, 1e-4);
        k.frontUV = (meshUV - _FrontPrintRect.xy) / max(_FrontPrintRect.zw, 1e-4);
        k.backMask = KitInside01(k.backUV);
        k.frontMask = KitInside01(k.frontUV);

        k.licensedUV = meshUV;
    }

    return k;
}

// ----------------------------------------------------------------------------------------
// Patterns
// ----------------------------------------------------------------------------------------

// 0 = primary colour, 1 = secondary colour.
half KitPatternMask(KitSpace k)
{
    int type = (int)round(_PatternType);
    float scale = max(_PatternScale, 0.1);
    float2 uv = k.uv;

    if (type == KIT_PATTERN_STRIPES)
    {
        float n = max(2.0, 2.0 * round(4.0 * scale));       // even count -> seamless around the body
        return KitFilteredStripe(uv.x * n, 0.5);
    }
    if (type == KIT_PATTERN_HOOPS)
    {
        float n = max(2.0, round(4.5 * scale));
        return KitFilteredStripe(uv.y * n + 0.25, 0.5);
    }
    if (type == KIT_PATTERN_HALVES)
    {
        return KitFilteredStripe(uv.x, 0.5);
    }
    if (type == KIT_PATTERN_GRADIENT)
    {
        // Sublimated halftone fade: dots grow towards the hem, resolve to a smooth fade at distance.
        float g = saturate(1.15 - uv.y * 1.25);
        g = g * g;
        float freq = 55.0 * scale;
        float2 cell = frac(k.knitCoord * freq) - 0.5;
        float r = sqrt(g) * 0.62;
        float d = length(cell);
        float aa = max(fwidth(d), 1e-4);
        float dots = 1.0 - smoothstep(r - aa, r + aa, d);
        float resolve = saturate(fwidth(k.knitCoord.x * freq) * 1.5);
        return lerp(dots, g, resolve);
    }
    if (type == KIT_PATTERN_PINSTRIPES)
    {
        float n = max(4.0, 2.0 * round(12.0 * scale));
        return KitFilteredStripe(uv.x * n, 0.08);
    }
    if (type == KIT_PATTERN_CHEVRON)
    {
        float across = (uv.x - 0.5) * 2.0;                  // -1..1 across the front in object mode
        float d = uv.y - (0.70 - abs(across) * 0.9);
        return KitBand(d, 0.065 * scale);
    }
    if (type == KIT_PATTERN_SASH)
    {
        float d;
        if (k.isObjectMode > 0.5)
        {
            // A tilted plane through the torso: wraps round the body like a real sash.
            float neck = _BodyLines.x * KitObjectScale().y;
            float hem = _BodyLines.y * KitObjectScale().y;
            float mid = lerp(hem, neck, 0.55);
            d = dot(float2(k.kitPos.x, k.kitPos.y - mid), normalize(float2(1.0, 1.15)));
            return KitBand(d, 0.055 * scale);
        }
        d = (uv.x - 0.5) + (uv.y - 0.5) * 0.85;
        return KitBand(d, 0.07 * scale);
    }
    return 0.0;
}

// Collar trim and neck opening for the object mapping. Returns x: trim weight, y: skin (neck opening) weight.
half2 KitCollar(KitSpace k)
{
    if (k.isObjectMode < 0.5) return half2(0, 0);

    float neck = _BodyLines.x * KitObjectScale().y;
    float below = neck - k.kitPos.y;                 // metres below the neckline
    float frontFacing = step(0.0, k.kitPos.z);
    float across = abs(k.kitPos.x);
    int style = (int)round(_CollarStyle);

    float trim = 0.0;
    float skin = 0.0;

    if (style == KIT_COLLAR_VNECK)
    {
        float vDepth = 0.12 - across * 1.6;          // V edge, metres below neck
        float inV = frontFacing * step(below, vDepth);
        skin = inV;
        trim = max(frontFacing * KitBand(below - vDepth, 0.012) * step(across, 0.075), KitBand(below, 0.016));
    }
    else if (style == KIT_COLLAR_POLO)
    {
        trim = KitBand(below - 0.022, 0.024);
        trim = max(trim, frontFacing * step(across, 0.014) * step(below, 0.13));
    }
    else
    {
        trim = KitBand(below - 0.012, 0.014);
    }
    return half2(trim, skin);
}

// ----------------------------------------------------------------------------------------
// Fabric micro detail (tangent space, x along knitTangent, y along knitBitangent)
// ----------------------------------------------------------------------------------------

half3 KitKnitNormalTS(float2 kc, half strength)
{
    float f = max(_KnitScale, 1.0);                  // cycles per metre
    float2 a = kc * f * TWO_PI;

    // Pique / mesh knit: offset bumps plus finer vertical wales.
    float2 sa, ca;
    sincos(a, sa, ca);
    float2 slope = float2(ca.x * sa.y, sa.x * ca.y);
    slope.x += 0.35 * cos(a.x * 2.0 + a.y);
    slope.y += 0.35 * cos(a.x * 2.0 + a.y);

    // Fade out before it aliases (reaches pixel size).
    float footprint = max(fwidth(kc.x), fwidth(kc.y)) * f;
    float fade = saturate(1.0 - footprint * 1.6);

    // Low-frequency drape wrinkles survive at distance.
    float2 w = kc * float2(5.3, 8.1);
    float2 wrinkle = float2(cos(w.x + 1.7 * sin(w.y)), cos(w.y * 1.3 + w.x * 0.6)) * _WrinkleStrength;

    half2 xy = -(slope * strength * fade) + wrinkle;
    return normalize(half3(xy, 1.0));
}

// Bump from a scalar height via screen-space derivatives (Mikkelsen surface gradient).
float3 KitPerturbNormalByHeight(float3 normalWS, float3 positionWS, float height)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhdx = ddx(height);
    float dhdy = ddy(height);
    float3 r1 = cross(dpdy, normalWS);
    float3 r2 = cross(normalWS, dpdx);
    float det = dot(dpdx, r1);
    float3 surfGrad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * normalWS - surfGrad);
}

// ----------------------------------------------------------------------------------------
// Surface assembly
// ----------------------------------------------------------------------------------------

struct KitSurface
{
    half3 albedo;
    half  smoothness;
    half  occlusion;
    half  sheenMask;       // 1 = knit fabric, 0 = prints / skin / boots
    half3 normalTS;        // micro detail in the knit tangent frame
    half  printHeight;     // metres, applied with KitPerturbNormalByHeight
};

KitSurface KitBuildSurface(KitSpace k)
{
    KitSurface o = (KitSurface)0;
    o.occlusion = 1.0;
    o.sheenMask = 1.0;
    o.smoothness = _FabricSmoothness;
    half knitStrength = _KnitStrength;

    // --- Base garment colour -------------------------------------------------------------
    half3 shirt;
    half fabricSmoothness = _FabricSmoothness;
    half3 licensedNormal = half3(0, 0, 1);

    if (_UseLicensedTextures > 0.5)
    {
        shirt = SAMPLE_TEXTURE2D(_KitAlbedo, sampler_KitAlbedo, k.licensedUV).rgb;
        if (_LicensedHasMask > 0.5)
        {
            half4 mask = SAMPLE_TEXTURE2D(_KitMask, sampler_KitMask, k.licensedUV);
            o.occlusion = mask.r;
            fabricSmoothness = mask.g;
            o.sheenMask = mask.b;
            knitStrength *= mask.b;
        }
        if (_LicensedHasNormal > 0.5)
        {
            licensedNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_KitNormal, sampler_KitNormal, k.licensedUV), _LicensedNormalScale);
        }
    }
    else
    {
        half t = KitPatternMask(k);
        shirt = lerp(_PrimaryColor.rgb, _SecondaryColor.rgb, t);
        half2 collar = KitCollar(k);
        shirt = lerp(shirt, _TertiaryColor.rgb, collar.x);
        shirt = lerp(shirt, _SkinColor.rgb, collar.y);
    }

    // Derivative-based terms are evaluated before the per-pixel region branch.
    half shortsStripe = KitBand(abs(abs(atan2(k.kitPos.x, k.kitPos.z)) - HALF_PI), 0.07);
    half socksBand = KitBand(k.kitPos.y - (_BodyLines.z * KitObjectScale().y - 0.05), 0.018);

    half3 albedo = shirt;
    int region = (int)k.region;
    if (region == KIT_REGION_SHORTS)
    {
        // Shorts with a contrasting side stripe.
        albedo = lerp(_ShortsColor.rgb, _TertiaryColor.rgb, shortsStripe);
    }
    else if (region == KIT_REGION_SOCKS)
    {
        albedo = lerp(_SocksColor.rgb, _TertiaryColor.rgb, socksBand);
    }
    else if (region == KIT_REGION_BOOTS)
    {
        albedo = _BootColor.rgb;
        fabricSmoothness = 0.62;
        o.sheenMask = 0.0;
        knitStrength = 0.0;
    }
    else if (region == KIT_REGION_SKIN)
    {
        albedo = _SkinColor.rgb;
        fabricSmoothness = 0.38;
        o.sheenMask = 0.0;
        knitStrength = 0.0;
    }

    o.smoothness = fabricSmoothness;

    // --- Name / number (heat-pressed vinyl) ------------------------------------------------
    // Sample both panels unconditionally (no divergent-branch derivatives).
    float2 backAtlas = float2(saturate(k.backUV.x) * KIT_ATLAS_PANEL_WIDTH, saturate(k.backUV.y));
    float2 frontAtlas = float2(KIT_ATLAS_PANEL_WIDTH + saturate(k.frontUV.x) * KIT_ATLAS_PANEL_WIDTH, saturate(k.frontUV.y));
    half4 backPrint = SAMPLE_TEXTURE2D(_NameNumberTex, sampler_NameNumberTex, backAtlas) * k.backMask;
    half4 frontPrint = SAMPLE_TEXTURE2D(_NameNumberTex, sampler_NameNumberTex, frontAtlas) * k.frontMask;
    half4 printSample = max(backPrint, frontPrint) * step(0.5, _UseNameNumber);

    half fill = printSample.r;
    half cover = printSample.g;
    half height = printSample.b;

    albedo = lerp(albedo, _NumberOutlineColor.rgb, cover);
    albedo = lerp(albedo, _NumberColor.rgb, fill);
    // Pressed edge: film thins and darkens slightly where coverage is high but height is still ramping.
    half edge = saturate(cover - height);
    albedo *= 1.0 - 0.12 * edge;

    o.smoothness = lerp(o.smoothness, _PrintSmoothness, cover);
    o.sheenMask *= 1.0 - cover;
    knitStrength *= 1.0 - 0.75 * cover;          // vinyl mostly hides the knit, a little shows through
    o.printHeight = height * _PrintBevel;

    // --- Crest and sponsor decals (front panel space) -------------------------------------
    float2 crestUV = (k.frontUV - (_CrestRect.xy - 0.5 * _CrestRect.zw)) / max(_CrestRect.zw, 1e-4);
    half4 crest = SAMPLE_TEXTURE2D(_CrestTex, sampler_CrestTex, saturate(crestUV));
    half crestA = crest.a * KitInside01(crestUV) * k.frontMask * step(0.5, _UseCrest);
    albedo = lerp(albedo, crest.rgb, crestA);
    o.smoothness = lerp(o.smoothness, 0.3, crestA);  // woven / embroidered badge
    o.sheenMask *= 1.0 - 0.5 * crestA;

    float2 sponsorUV = (k.frontUV - (_SponsorRect.xy - 0.5 * _SponsorRect.zw)) / max(_SponsorRect.zw, 1e-4);
    half4 sponsor = SAMPLE_TEXTURE2D(_SponsorTex, sampler_SponsorTex, saturate(sponsorUV));
    half sponsorA = sponsor.a * KitInside01(sponsorUV) * k.frontMask * step(0.5, _UseSponsor);
    albedo = lerp(albedo, sponsor.rgb, sponsorA);
    o.smoothness = lerp(o.smoothness, _PrintSmoothness, sponsorA);
    o.sheenMask *= 1.0 - sponsorA;

    // --- Match wear: sweat and dirt ----------------------------------------------------------
    half garment = (region == KIT_REGION_SHIRT || region == KIT_REGION_SHORTS || region == KIT_REGION_SOCKS) ? 1.0 : 0.0;
    float3 noiseP = k.isObjectMode > 0.5 ? k.kitPos * 9.0 : float3(k.uv * 14.0, 0.0);
    half n1 = KitValueNoise(noiseP);
    half n2 = KitValueNoise(noiseP * 2.7 + 11.0);

    half sweatZone;
    if (k.isObjectMode > 0.5)
    {
        float radius = max(length(k.kitPos.xz), 1e-3);
        half sides = saturate(1.0 - abs(k.kitPos.z) / radius * 1.6);       // under the arms
        half spine = saturate(1.0 - abs(k.kitPos.x) / 0.07);               // down chest and back
        half upper = saturate((k.uv.y - 0.35) * 2.0);
        sweatZone = saturate(sides * upper + spine * 0.8 * upper);
    }
    else
    {
        sweatZone = saturate((k.uv.y - 0.3) * 1.5);
    }
    if (_UseLicensedTextures > 0.5 && _LicensedHasMask > 0.5)
    {
        sweatZone = SAMPLE_TEXTURE2D(_KitMask, sampler_KitMask, k.licensedUV).a;
    }
    half sweat = saturate(_Sweat * sweatZone * (0.55 + 0.9 * n1)) * garment * (1.0 - cover * 0.8);
    albedo *= 1.0 - 0.3 * sweat;                     // wet polyester darkens
    o.smoothness = lerp(o.smoothness, 0.55, sweat * 0.6);
    o.sheenMask *= 1.0 - 0.6 * sweat;

    half dirtZone = k.isObjectMode > 0.5
        ? saturate(1.0 - k.uv.y) * 0.7 + (region == KIT_REGION_SHORTS ? 0.6 : 0.0) + (region == KIT_REGION_SOCKS ? 0.9 : 0.0)
        : saturate(1.0 - k.uv.y);
    half dirt = saturate((n2 * 0.6 + n1 * 0.4 - (1.0 - _Dirt * dirtZone)) * 3.0) * garment;
    albedo = lerp(albedo, _DirtColor.rgb, dirt * 0.85);
    o.smoothness = lerp(o.smoothness, 0.12, dirt);
    o.sheenMask *= 1.0 - dirt;

    o.albedo = albedo;

    // --- Micro normal ------------------------------------------------------------------------
    half3 knit = KitKnitNormalTS(k.knitCoord, knitStrength);
    // Whiteout blend of licensed normal and knit detail.
    o.normalTS = normalize(half3(licensedNormal.xy + knit.xy, licensedNormal.z * knit.z));

    return o;
}

#endif // OPENFIFA_KIT_SURFACE_INCLUDED
