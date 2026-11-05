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
    float  isSkinned;    // skinned body map (mode 2)
    float  bodySpace;    // 1 when kitPos / uv describe a whole body (object or skinned mode)
    float  neckY;        // body modes: kit-space Y of the neckline
    float  hemY;         // body modes: kit-space Y of the shirt hem
    half4  regionW;      // weights: shirt, shorts, socks, boots (skin = the rest minus gloves)
    half   gloveW;       // goalkeeper gloves
    half   armW;         // skinned: 1 on the arms (sleeve cuff)
    float2 limb;         // skinned: (armT, legT) joint-chain parameters
    float  height;       // skinned: metres above the soles
    half   torsoW;       // skinned: 1 on the torso
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

// Anti-aliased step at edge, widened by `soft` (parameter units).
float KitSoftStep(float edge, float x, float soft)
{
    float w = max(fwidth(x) * 0.75, 1e-4) + soft;
    return smoothstep(edge - w, edge + w, x);
}

half4 KitOneHotRegion(float region)
{
    return half4(region == KIT_REGION_SHIRT, region == KIT_REGION_SHORTS,
                 region == KIT_REGION_SOCKS, region == KIT_REGION_BOOTS);
}

// ----------------------------------------------------------------------------------------
// Mapping
// ----------------------------------------------------------------------------------------

// Skinned body map (mode 2). body0 = (armT, legT, armWeight, legWeight), body1 = rest-pose kit-space
// position (metres; origin on the torso axis at the neck base, +X right, +Y up, +Z front), w = 1 when baked.
void KitComputeSkinnedSpace(inout KitSpace k, float4 body0, float4 body1)
{
    k.isSkinned = 1.0;
    k.bodySpace = 1.0;
    float3 kp = body1.xyz;
    k.kitPos = kp;
    float2 rad = max(_BodyTorsoLines.zw, 0.02);
    float hem = min(_BodyTorsoLines.x, -0.05);
    k.neckY = 0.0;
    k.hemY = hem;

    // Around-the-body angle on the torso ellipse (0.5 = front centre), height 0 hem .. 1 neckline.
    float theta = atan2(kp.x / rad.x, kp.z / rad.y);
    k.uv = float2(theta * INV_TWO_PI + 0.5, (kp.y - hem) / -hem);
    k.knitCoord = float2(theta * 0.5 * (rad.x + rad.y), kp.y);
    k.knitTangentOS = 0;
    k.knitBitangentOS = 0;

    float soft = _BodyEdgeSoftness;
    float armT = body0.x;
    float legT = body0.y;
    k.limb = float2(armT, legT);
    k.height = body1.w - 1.0;
    half arm = KitSoftStep(0.5, body0.z, 0.05);
    half leg = KitSoftStep(0.5, body0.w, 0.05) * (1.0 - arm);
    half torso = (1.0 - arm) * (1.0 - leg);
    k.armW = arm;
    k.torsoW = torso;

    // Arms: sleeve to _BodyLimbLimits.x, bare forearm, gloves from _BodyLimbLimits.y.
    half sleeve = 1.0 - KitSoftStep(_BodyLimbLimits.x, armT, soft);
    half glove = KitSoftStep(_BodyLimbLimits.y, armT, soft) * (1.0 - sleeve);

    // Legs: shorts to mid thigh, bare knee, socks from just below the knee, boots from the ankle.
    half legShorts = 1.0 - KitSoftStep(_BodyLimbLimits.z, legT, soft);
    half boots = KitSoftStep(_BodyTorsoLines.y, legT, soft);
    half socks = KitSoftStep(_BodyLimbLimits.w, legT, soft) * (1.0 - boots);

    // Torso: neckline (dipping slightly at the front, rising over the trapezius), shirt, shorts below the waist.
    float collarY = -0.012 - 0.03 * saturate(kp.z / rad.y) + 0.5 * max(abs(kp.x) - 0.055, 0.0);
    half neckSkin = KitSoftStep(collarY, kp.y, soft * 0.5);
    half torsoShorts = (1.0 - KitSoftStep(hem, kp.y, soft)) * (1.0 - neckSkin);
    half torsoShirt = (1.0 - neckSkin) * (1.0 - torsoShorts);
    k.neckY = collarY;

    k.regionW = half4(torso * torsoShirt + arm * sleeve,
                      torso * torsoShorts + leg * legShorts,
                      leg * socks,
                      leg * boots);
    k.gloveW = arm * glove;

    half4 w = k.regionW;
    half skinW = saturate(1.0 - dot(w, 1.0) - k.gloveW);
    k.region = KIT_REGION_SKIN;
    half best = skinW;
    if (w.x > best) { best = w.x; k.region = KIT_REGION_SHIRT; }
    if (w.y > best) { best = w.y; k.region = KIT_REGION_SHORTS; }
    if (w.z > best) { best = w.z; k.region = KIT_REGION_SOCKS; }
    if (w.w > best) { best = w.w; k.region = KIT_REGION_BOOTS; }

    // Planar prints on the torso only (not the sleeves). Back panel seen from behind: viewer's right = +X.
    half printable = torso * torsoShirt;
    k.backUV = float2((kp.x - _BackPrintRect.x) / _BackPrintRect.z + 0.5,
                      (kp.y - _BackPrintRect.y) / _BackPrintRect.w + 0.5);
    k.frontUV = float2((-kp.x - _FrontPrintRect.x) / _FrontPrintRect.z + 0.5,
                       (kp.y - _FrontPrintRect.y) / _FrontPrintRect.w + 0.5);
    float depth = kp.z / rad.y;
    k.backMask = KitInside01(k.backUV) * saturate((-depth - 0.1) * 5.0) * printable;
    k.frontMask = KitInside01(k.frontUV) * saturate((depth - 0.1) * 5.0) * printable;

    k.licensedUV = k.uv;
}

KitSpace KitComputeSpace(float3 positionOS, float3 normalOS, float2 meshUV, float4 body0, float4 body1)
{
    KitSpace k = (KitSpace)0;

    if (_MappingMode > 1.5)
    {
        KitComputeSkinnedSpace(k, body0, body1);
    }
    else if (_MappingMode < 0.5)
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
        k.bodySpace = 1.0;
        k.neckY = neck;
        k.hemY = hem;
        k.regionW = KitOneHotRegion(k.region);
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
        k.regionW = KitOneHotRegion(k.region);
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
        if (k.bodySpace > 0.5)
        {
            // A tilted plane through the torso: wraps round the body like a real sash.
            float mid = lerp(k.hemY, k.neckY, 0.55);
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
    if (k.bodySpace < 0.5) return half2(0, 0);

    float below = k.neckY - k.kitPos.y;                 // metres below the neckline
    float frontFacing = step(0.0, k.kitPos.z);
    float across = abs(k.kitPos.x);
    int style = (int)round(_CollarStyle);

    float trim = 0.0;
    float skin = 0.0;

    if (style == KIT_COLLAR_VNECK)
    {
        float vDepth = 0.055 - across * 1.1;        // V edge, metres below neck (shallow, like a match shirt)
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

half3 KitKnitNormalTS(float2 kc, half strength, half fabric)
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

    half2 xy = -(slope * strength * fade) + wrinkle * fabric;   // no drape wrinkles on bare skin
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
// Head: painted face, hair and facial hair for the skinned body (kit space, metres)
// ----------------------------------------------------------------------------------------

// Soft ellipse: 1 inside, anti-aliased edge.
float KitEllipse(float2 p, float2 c, float2 r)
{
    float d = length((p - c) / r) - 1.0;
    float aa = max(fwidth(d), 1e-4);
    return 1.0 - smoothstep(-aa, aa, d);
}

// Paints the head onto `albedo` (only where `skin` > 0). Returns the hair coverage (for smoothness / sheen).
half KitPaintHead(KitSpace k, half skin, inout half3 albedo)
{
    float3 kp = k.kitPos;
    float2 p = kp.xy;
    float nz = (kp.z - _HeadParams.y) / max(_HeadParams.w, 0.01);
    half onHead = k.torsoW * KitSoftStep(0.085, kp.y, 0.004) * skin;
    half front = smoothstep(0.35, 0.65, nz);
    float ax = abs(kp.x);
    half3 skinCol = _SkinColor.rgb;
    half3 hairCol = _HairColor.rgb;
    int hairStyle = (int)round(_HeadStyle.x);
    int facial = (int)round(_HeadStyle.y);

    float grain = KitValueNoise(kp * float3(900.0, 900.0, 900.0));
    float strands = KitValueNoise(kp * float3(700.0, 90.0, 700.0));

    // Hair: hairline high at the forehead, above the ears at the sides, low at the nape; sideburns.
    float hairline = lerp(0.150, 0.288, smoothstep(-0.55, 0.75, nz)) + _HeadStyle.z;
    hairline = min(hairline, lerp(0.288, 0.252, smoothstep(0.055, 0.08, ax) * smoothstep(0.8, 0.2, nz)));
    half hair = KitSoftStep(hairline, kp.y, 0.003);
    half sideburn = smoothstep(0.066, 0.078, ax) * step(0.205, kp.y) * smoothstep(-0.1, 0.05, nz) * smoothstep(0.6, 0.4, nz);
    hair = saturate(hair + sideburn) * onHead;
    // Keep painted hair off the skin under the ear meshes, or it shows through as a dark patch.
    half earZone = smoothstep(0.070, 0.076, ax) * smoothstep(0.185, 0.190, kp.y) * smoothstep(0.248, 0.243, kp.y)
                 * smoothstep(-0.050, -0.042, kp.z) * smoothstep(0.010, 0.004, kp.z);
    hair *= 1.0 - earZone;
    half hairAmount = hairStyle >= 2 ? 1.0 : (hairStyle == 1 ? 0.6 : 0.0);
    half3 hairShade = hairCol * lerp(0.75, 1.2, strands);
    half3 buzz = lerp(skinCol, hairCol, 0.55 + 0.35 * grain);
    half3 hairPaint = hairStyle >= 2 ? hairShade : buzz;
    albedo = lerp(albedo, hairPaint, hair * hairAmount);

    // Facial hair: soft edges that follow the jaw and cheeks, density varies with grain (no hard mask).
    half beardCheek = smoothstep(0.205, 0.172, kp.y - 0.25 * ax);            // long fade up the cheek
    half beardJaw = smoothstep(0.108, 0.128, kp.y);                           // just under the jaw line
    half jaw = onHead * smoothstep(-0.05, 0.30, nz) * beardCheek * beardJaw;   // stops in front of the ears
    half lips = KitEllipse(p, float2(0.0, 0.161), float2(0.019, 0.0058));
    half lipZone = smoothstep(0.75, 1.0, length((p - float2(0.0, 0.161)) / float2(0.024, 0.009)));
    half beardZone = jaw * lipZone;
    half moustache = onHead * front * smoothstep(0.030, 0.020, ax) * smoothstep(0.1635, 0.1665, kp.y) * smoothstep(0.177, 0.172, kp.y);
    half goatee = onHead * front * smoothstep(1.0, 0.55, length((p - float2(0.0, 0.140)) / float2(0.019, 0.024))) * lipZone;
    half facialMask = 0.0;
    half facialAmount = 1.0;
    half density = 0.55 + 0.45 * grain;
    if (facial == 1) { facialMask = beardZone; facialAmount = 0.22 + 0.22 * grain; }
    else if (facial == 2) { facialMask = saturate(beardZone + 0.8 * moustache); facialAmount = 0.5 * density + 0.12; }
    else if (facial == 3) { facialMask = saturate(goatee + 0.7 * moustache); facialAmount = 0.5 * density + 0.12; }
    // Short trimmed hair over skin: blend toward a skin-tinted hair colour so the skin shows through.
    albedo = lerp(albedo, lerp(hairShade, skinCol * 0.55, 0.25), facialMask * facialAmount * (1.0 - hair));

    // Face features (front only): soft, anatomical shading instead of sticker shapes.
    half f = onHead * front * (1.0 - hair);
    float eyeY = 0.226;
    half eyes = 0.0, iris = 0.0, pupil = 0.0, lidLine = 0.0, lowerLid = 0.0, socket = 0.0, bag = 0.0;
    [unroll] for (int side = -1; side <= 1; side += 2)
    {
        float2 c = float2(side * 0.031, eyeY);
        // Almond eye: narrower, upper lid covers the top of the iris.
        float2 q = (p - c) / float2(0.0115, 0.0046);
        q.y -= 0.25 * q.x * q.x * sign(q.y);
        half e = 1.0 - smoothstep(0.85, 1.0, length(q));
        eyes = max(eyes, e);
        iris = max(iris, 1.0 - smoothstep(0.0031, 0.0038, length(p - c - float2(0.0, -0.0006))));   // sclera corners show
        pupil = max(pupil, 1.0 - smoothstep(0.0016, 0.0022, length(p - c - float2(0.0, -0.0006))));
        float2 lq = (p - c - float2(0.0, 0.0030)) / float2(0.0135, 0.0030);
        lidLine = max(lidLine, (1.0 - smoothstep(0.6, 1.0, length(lq))) * step(c.y + 0.0012, p.y));
        lowerLid = max(lowerLid, (1.0 - smoothstep(0.5, 1.0, length((p - c - float2(0.0, -0.0062)) / float2(0.012, 0.0028)))));
        socket = max(socket, 1.0 - smoothstep(0.5, 1.0, length((p - c - float2(side * -0.002, 0.004)) / float2(0.019, 0.012))));
        bag = max(bag, 1.0 - smoothstep(0.4, 1.0, length((p - c - float2(0.0, -0.011)) / float2(0.013, 0.005))));
    }
    half3 irisCol = lerp(half3(0.13, 0.08, 0.05), half3(0.22, 0.31, 0.37), _HeadStyle.w);
    half lightness = saturate(dot(skinCol, half3(0.3, 0.5, 0.2)) * 1.6);
    // On deep tones the socket and lash line are kept light so the eyes stay readable in shade.
    albedo *= 1.0 - f * (0.22 * socket * lerp(0.4, 1.0, lightness) + 0.08 * bag);                                  // brow ridge / socket shade
    half3 sclera = lerp(half3(0.72, 0.66, 0.60), skinCol, 0.15);
    albedo = lerp(albedo, sclera, f * eyes);
    albedo = lerp(albedo, irisCol, f * eyes * iris);
    albedo = lerp(albedo, half3(0.02, 0.02, 0.02), f * eyes * pupil);
    albedo = lerp(albedo, skinCol * 0.28, f * lidLine * 0.85 * lerp(0.35, 1.0, lightness));                          // lash line / upper lid
    albedo = lerp(albedo, skinCol * 0.70, f * lowerLid * 0.35 * (1.0 - eyes));

    // Brows: thick at the inner end, tapering outwards, soft edges and hair grain.
    float bx = saturate((ax - 0.012) / 0.040);
    float browY = 0.2425 + 0.006 * bx - 0.004 * bx * bx;
    float browHalf = lerp(0.0036, 0.0016, bx);
    half brows = f * smoothstep(0.009, 0.014, ax) * smoothstep(0.054, 0.046, ax)
               * (1.0 - smoothstep(browHalf * 0.6, browHalf, abs(kp.y - browY)));
    albedo = lerp(albedo, hairCol * 0.85, brows * (0.65 + 0.35 * strands));

    // Nose: shaded sides, lit bridge, soft alae instead of dot nostrils.
    half noseZone = f * smoothstep(0.235, 0.22, kp.y) * smoothstep(0.183, 0.19, kp.y);
    half noseSide = noseZone * smoothstep(0.004, 0.010, ax) * smoothstep(0.019, 0.013, ax);
    albedo *= 1.0 - 0.16 * noseSide;
    albedo = lerp(albedo, albedo * 1.08, noseZone * smoothstep(0.004, 0.0, ax) * 0.8);
    half nostrils = f * max(KitEllipse(p, float2(0.0085, 0.1885), float2(0.0042, 0.0022)), KitEllipse(p, float2(-0.0085, 0.1885), float2(0.0042, 0.0022)));
    albedo = lerp(albedo, skinCol * 0.45, nostrils * 0.45);
    half philtrum = f * smoothstep(0.006, 0.002, ax) * smoothstep(0.163, 0.168, kp.y) * smoothstep(0.186, 0.180, kp.y);
    albedo *= 1.0 - 0.06 * philtrum;

    // Cheeks and nose warmth (stronger on lighter skin), contour under the cheekbones.
    half cheeks = f * (1.0 - smoothstep(0.0, 1.0, length((float2(ax, kp.y) - float2(0.040, 0.198)) / float2(0.030, 0.024))));
    albedo = lerp(albedo, albedo * half3(1.06, 0.90, 0.88), (cheeks + 0.6 * noseZone) * 0.3 * lightness);
    half contour = f * (1.0 - smoothstep(0.4, 1.0, length((float2(ax, kp.y) - float2(0.052, 0.178)) / float2(0.014, 0.022))));
    albedo *= 1.0 - 0.07 * contour;

    // Lips: soft-edged, darker upper lip, a fine mouth line.
    half upperLip = lips * step(0.1615, kp.y);
    half3 lipCol = skinCol * lerp(half3(0.85, 0.66, 0.64), half3(0.78, 0.6, 0.58), upperLip);
    half lipSoft = 1.0 - smoothstep(0.6, 1.0, length((p - float2(0.0, 0.161)) / float2(0.019, 0.0058)));
    albedo = lerp(albedo, lipCol, f * lipSoft * (1.0 - facialMask * 0.6));
    albedo = lerp(albedo, skinCol * 0.4, f * smoothstep(0.019, 0.013, ax) * (1.0 - smoothstep(0.0004, 0.0012, abs(kp.y - 0.1615))) * 0.8);
    albedo *= 1.0 - f * 0.08 * (1.0 - smoothstep(0.4, 1.0, length((p - float2(0.0, 0.150)) / float2(0.016, 0.005))));  // under-lip shadow

    // Ears are meshes (EarBuilder); no painted ear rim.

    return saturate(hair * hairAmount * step(1.5, hairStyle) + facialMask * step(1.5, facial));
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
    half  skinMask;        // 1 on bare skin (wrap lighting)
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
    half collarSkin = 0.0;   // skin showing inside the V of the shirt
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
        collarSkin = collar.y;
    }

    // Derivative-based terms are evaluated before any per-pixel branching.
    half shortsStripe = KitBand(abs(abs(atan2(k.kitPos.x, k.kitPos.z)) - HALF_PI), 0.07);
    half socksBand = KitBand(k.kitPos.y - (_BodyLines.z * KitObjectScale().y - 0.05), 0.018);
    half sleeveCuff = 0.0;
    if (k.isSkinned > 0.5)
    {
        // Skinned body: trim on the shorts hem, a band near the sock top and a cuff on the sleeve.
        shortsStripe = KitBand(k.limb.y - (_BodyLimbLimits.z - 0.035), 0.016);
        socksBand = KitBand(k.limb.y - (_BodyLimbLimits.w + 0.065), 0.022);
        sleeveCuff = KitBand(k.limb.x - (_BodyLimbLimits.x - 0.04), 0.024) * k.armW;
    }
    shirt = lerp(shirt, _TertiaryColor.rgb, sleeveCuff);

    half3 socksBase = _SocksColor.rgb;
    half3 bootCol = _BootColor.rgb;
    if (k.isSkinned > 0.5)
    {
        // Side panels down the flanks of the shirt (from the armpit to the hem).
        float2 rad = max(_BodyTorsoLines.zw, 0.02);
        float side = abs(abs(atan2(k.kitPos.x / rad.x, k.kitPos.z / rad.y)) - HALF_PI);
        half panel = KitBand(side, 0.16) * k.torsoW * step(k.kitPos.y, -0.12);
        shirt = lerp(shirt, lerp(shirt, _TertiaryColor.rgb, 0.85), panel);

        // Socks: turned-down cuff at the top (slightly darker knit) with the trim band, shin pad below.
        float cuffT = k.limb.y - _BodyLimbLimits.w;
        half cuff = 1.0 - KitSoftStep(0.13, cuffT, 0.0);
        socksBase *= lerp(1.0, 0.82, cuff);

        // Boots: contrasting sole and a swept side stripe in the accent colour.
        half sole = 1.0 - KitSoftStep(0.022, k.height, 0.002);
        float d = (k.limb.y - 2.45) + (k.height - 0.045) * 6.0;
        half stripe = KitBand(d, 0.07) * step(0.03, k.height) * step(2.15, k.limb.y);
        bootCol = lerp(bootCol, _BootAccentColor.rgb, saturate(sole + stripe));
    }

    // Region blend (weights are one-hot in object/UV modes, soft-edged in skinned mode).
    half4 w = k.regionW;
    half glove = k.gloveW;
    half skin = saturate(1.0 - dot(w, 1.0) - glove);
    // Skin inside the V is skin, not fabric: no knit, sheen, sweat or dirt there, and it gets the skin lighting.
    skin += collarSkin * w.x;
    half fabric = w.x * (1.0 - collarSkin) + w.y + w.z;
    half3 shortsCol = lerp(_ShortsColor.rgb, _TertiaryColor.rgb, shortsStripe);
    half3 socksCol = lerp(socksBase, _TertiaryColor.rgb, socksBand);
    half3 albedo = shirt * (w.x * (1.0 - collarSkin)) + shortsCol * w.y + socksCol * w.z + bootCol * w.w
                 + _GloveColor.rgb * glove + _SkinColor.rgb * skin;
    int region = (int)k.region;

    fabricSmoothness = fabricSmoothness * fabric + 0.62 * w.w + 0.35 * glove + 0.38 * skin;
    o.sheenMask *= fabric;
    knitStrength *= fabric;

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
    half garment = fabric;
    float3 noiseP = k.bodySpace > 0.5 ? k.kitPos * 9.0 : float3(k.uv * 14.0, 0.0);
    half n1 = KitValueNoise(noiseP);
    half n2 = KitValueNoise(noiseP * 2.7 + 11.0);

    half sweatZone;
    if (k.bodySpace > 0.5)
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

    half dirtZone = k.bodySpace > 0.5
        ? saturate(1.0 - k.uv.y) * 0.7 + (region == KIT_REGION_SHORTS ? 0.6 : 0.0) + (region == KIT_REGION_SOCKS ? 0.9 : 0.0)
        : saturate(1.0 - k.uv.y);
    half dirt = saturate((n2 * 0.6 + n1 * 0.4 - (1.0 - _Dirt * dirtZone)) * 3.0) * garment;
    albedo = lerp(albedo, _DirtColor.rgb, dirt * 0.85);
    o.smoothness = lerp(o.smoothness, 0.12, dirt);
    o.sheenMask *= 1.0 - dirt;

    half hairCover = 0.0;
    if (k.isSkinned > 0.5)
    {
        hairCover = KitPaintHead(k, skin, albedo);
        o.smoothness = lerp(o.smoothness, 0.45, hairCover);
    }
    o.albedo = albedo;
    o.skinMask = skin * (1.0 - hairCover);

    // --- Micro normal ------------------------------------------------------------------------
    half3 knit = KitKnitNormalTS(k.knitCoord, knitStrength, fabric);
    // Whiteout blend of licensed normal and knit detail.
    o.normalTS = normalize(half3(licensedNormal.xy + knit.xy, licensedNormal.z * knit.z));

    return o;
}

#endif // OPENFIFA_KIT_SURFACE_INCLUDED
