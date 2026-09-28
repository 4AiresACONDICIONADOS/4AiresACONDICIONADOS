#ifndef BOE_COMMON_INCLUDED
#define BOE_COMMON_INCLUDED

// -----------------------------------------------------------------------------
// Breath of Eclipse — shared shader helpers.
// Globals are driven from C#: FlashFrameSystem (anime flash frames) and
// EnvironmentKit (moon direction). They are not per-material, so they live
// outside UnityPerMaterial and keep every shader SRP Batcher compatible.
// -----------------------------------------------------------------------------

float _BoE_FlashFrame;   // 1 during a flash frame
float _BoE_FlashMode;    // 0 = dark background / lit characters, 1 = black silhouettes on bright color
float4 _BoE_FlashColor;  // element color of the flash (LDR)
float4 _BoE_MoonDir;     // world direction towards the moon

float BoEHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float BoEHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float BoEValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = BoEHash31(i);
    float n100 = BoEHash31(i + float3(1, 0, 0));
    float n010 = BoEHash31(i + float3(0, 1, 0));
    float n110 = BoEHash31(i + float3(1, 1, 0));
    float n001 = BoEHash31(i + float3(0, 0, 1));
    float n101 = BoEHash31(i + float3(1, 0, 1));
    float n011 = BoEHash31(i + float3(0, 1, 1));
    float n111 = BoEHash31(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
}

float BoEFbm3(float3 p)
{
    float v = BoEValueNoise3(p) * 0.55;
    v += BoEValueNoise3(p * 2.03 + 17.1) * 0.3;
    v += BoEValueNoise3(p * 4.11 + 3.7) * 0.15;
    return v;
}

// Dissolve: clips the pixel when the noise is below the dissolve amount and returns an edge-glow factor.
float BoEDissolve(float3 positionOS, float amount)
{
    if (amount <= 0.001)
        return 0.0;
    float n = BoEFbm3(positionOS * 4.0);
    float d = n - amount * 1.05;
    clip(d);
    return 1.0 - smoothstep(0.0, 0.07, d);
}

// Screen-space aspect ratio (width / height) from derivatives: works for any full-screen UI quad.
float BoEAspectFromUV(float2 uv)
{
    float dx = abs(ddx(uv.x)) + abs(ddy(uv.x));
    float dy = abs(ddx(uv.y)) + abs(ddy(uv.y));
    return dy / max(dx, 1e-6);
}

#endif
