#ifndef CITYX_SNOW_INCLUDED
#define CITYX_SNOW_INCLUDED

// SNOW LYING ON THE CITY (docs/SNOW.md). Whitens what faces up, wherever the sky can reach, by patches that grow together as it deepens: the way whole-world
// snow is done in games (a surface's facing, not a texture per surface). Set by View/Weather/SnowEffect.cs, and 0 until it is, so a clear day costs one test.

// x: how much lies, 0..1.
float4 _CityXSnow;
// x, y: the middle of the map of what is overhead, in the world; z: half its width; w: the map's uv per metre. Past its edge the sky is taken to be open.
float4 _CityXSnowMap;
TEXTURE2D(_CityXSnowHeights);
SAMPLER(sampler_CityXSnowHeights);

#define CITYX_SNOW_COLOUR half3(0.86, 0.89, 0.93)
#define CITYX_SNOW_SMOOTHNESS 0.15

// How far under the first surface down from the sky a point must be for it to be under cover, metres: that surface's own snow is not under it.
#define CITYX_SNOW_ROOF_FROM 0.5
#define CITYX_SNOW_ROOF_FULL 1.5

float CityXSnowHash(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float CityXSnowNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = CityXSnowHash(i);
    float b = CityXSnowHash(i + float2(1, 0));
    float c = CityXSnowHash(i + float2(0, 1));
    float d = CityXSnowHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// How high the first surface down from the sky is over a point, or very low if nothing is known there.
float CityXSnowRoofAt(float2 xz, float lod)
{
    return SAMPLE_TEXTURE2D_LOD(_CityXSnowHeights, sampler_CityXSnowHeights, xz * _CityXSnowMap.w, lod).r;
}

// Whether the sky reaches a point, 0..1.
float CityXSnowOpenSky(float3 positionWS)
{
    float2 fromMiddle = abs(positionWS.xz - _CityXSnowMap.xy);
    if (max(fromMiddle.x, fromMiddle.y) >= _CityXSnowMap.z)
    {
        return 1.0;
    }
    return 1.0 - smoothstep(CITYX_SNOW_ROOF_FROM, CITYX_SNOW_ROOF_FULL, CityXSnowRoofAt(positionWS.xz, 0.0) - positionWS.y);
}

// How much snow lies on a surface here, 0..1.
half CityXSnowLying(float3 positionWS, half3 normalWS)
{
    float lying = _CityXSnow.x;
    if (lying <= 0.0)
    {
        return 0.0;
    }
    half facing = smoothstep(0.45, 0.8, normalWS.y);
    if (facing <= 0.0)
    {
        return 0.0;
    }
    float patches = CityXSnowNoise(positionWS.xz * 0.45) * 0.55 + CityXSnowNoise(positionWS.xz * 1.7) * 0.3 + CityXSnowNoise(positionWS.xz * 6.3) * 0.15;
    float from = 1.05 - lying * 1.2;
    return facing * smoothstep(from, from + 0.05, patches) * CityXSnowOpenSky(positionWS);
}

// The surface with the snow on it, before it is lit.
void CityXApplySnow(inout SurfaceData surfaceData, float3 positionWS, half3 normalWS)
{
    half on = CityXSnowLying(positionWS, normalWS);
    if (on > 0.0)
    {
        surfaceData.albedo = lerp(surfaceData.albedo, CITYX_SNOW_COLOUR, on);
        surfaceData.smoothness = lerp(surfaceData.smoothness, CITYX_SNOW_SMOOTHNESS, on);
        surfaceData.metallic = lerp(surfaceData.metallic, 0.0, on);
    }
}

#endif
