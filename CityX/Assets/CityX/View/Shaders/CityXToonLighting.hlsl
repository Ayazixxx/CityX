#ifndef CITYX_TOON_LIGHTING_INCLUDED
#define CITYX_TOON_LIGHTING_INCLUDED

// CEL SHADING, like Jet Set Radio's (owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines",
// "players and buildings and terrain, everything").
//
// Every lit surface in the game lights itself through this file: CityX/Lit (the characters, their clothes, every prop; URP's Lit with only
// its lighting call changed) and CityX/Facade (the walls, roofs and ground). With the global keyword CITYX_TOON (CelShading.cs: on unless
// turned off) a light is a toon light:
//   the sun (or the moon) lights a surface fully or not at all, split where the surface turns away from it or falls in shadow; the shade
//     side keeps only the sky's light and a little of the sun's;
//   a lamp (a street light, a room light, a flashlight) lights the side of a body or a prop that faces it one flat tone, split as the sun's
//     is, and fades with distance as its light does; on the walls, roofs and ground (CityX/Facade) it is URP's own soft light, so its pool
//     on a street is an area of light that fades out (flat steps drew rings round every lamp: owner, 2026-11-09, "it keeps reflecting as
//     circles instead of just an area of light like most light sources in video games do");
//   a shiny surface (wet in the rain, metal) gets one hard highlight, no soft sheen;
//   the soft dark corners of screen-space ambient occlusion are left out, and a normal map's fine bumps are not lit (a cel surface is flat).
// Without the keyword everything here is URP's own lighting, called as before, so the game looks exactly as it did with cel shading off.
// The thick black lines are a separate full-screen pass (CityXOutline.shader).
//
// THE DARK UNDER THE GROUND is here too, as both shaders need it (docs/SEWERS.md; owner, 2026-11-09: "pitch black like night time in
// minecraft with barely any visibility and the flashlight/ other light sources need to light up the darkness"). With the eye under the
// ground (_CityXUnderground) a body gets nothing of the sun, the moon, the sky or the city's own lamps; what is left is what shines down
// there, and a trace round the eye (CityXDarkTrace) by which the nearest few metres are shapes and no more.
//
// The numbers are globals set by CelShading.cs (live: /celshade), so no material changes for a retune. Each is (0, 0, 0, 0) until set,
// which lights everything flat and fully: the toon light never goes dark for want of a setting.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "CityXSnow.hlsl"

// The sun or the moon. x: the light (N.L times its shadow, 0..1) at which a surface turns from its shade side to its lit side; y: half the
// width of that edge; z: how much of the light the shade side gets; w: how much the lit side gets.
float4 _CityXToonSun;
// x: the specular (URP's DirectBRDFSpecular times the light) at which a hard highlight shows, y: its strength.
float4 _CityXToonShine;

// How far out of the sky's light the eye is, 0 (under the open sky) to 1 (deep in a tunnel); and what an eye still makes out in the dark:
// rgb the light of it on a surface at the eye, a how many metres from the eye it reaches. Both set by DayNightCycle, 0 until then.
float _CityXUnderground;
float4 _CityXDarkTrace;

// Which side of a wall a light is on (View/CityLightLayers.cs): a light on the interior layer lights only indoor surfaces, one on the
// exterior layer only outdoor ones. The city's own lamps carry one or the other; a light somebody carries or lights carries neither.
// MUST match CityLightLayers.Interior / Exterior.
#define CITYX_INTERIOR_LIGHT_LAYER (1u << 1)
#define CITYX_EXTERIOR_LIGHT_LAYER (1u << 2)
#define CITYX_CITY_LIGHT_LAYERS (CITYX_INTERIOR_LIGHT_LAYER | CITYX_EXTERIOR_LIGHT_LAYER)

// A light somebody carries (a torch's beam, and the glow round it) is on the beam layer. Under the ground it does not fall off with the
// distance: it lights what it falls on, near or far, as a bare light of its strength would CITYX_BEAM_MATCH_METRES off, and only its
// range ends it (URP fades every light out over the last of its range). A tunnel is long and its walls lie almost along the beam: by
// the square of the distance a beam bright enough to show the far end of one burns the wall beside the hand white, and one that spares
// the wall is gone ten metres on. Under the sky it falls off as any light does: thrown so down a street it floods every house front
// thirty metres off. MUST match CityLightLayers.Beam.
#define CITYX_BEAM_LIGHT_LAYER (1u << 3)
#define CITYX_BEAM_MATCH_METRES 4.0

// A room lamp's shadow strength at its full. MUST match RoomLampShadows.FullStrength.
#define CITYX_ROOM_LAMP_SHADOW_STRENGTH 0.85

// The index URP's light data is under for additional light <i> (the light loop's own index).
int CityXAdditionalLightIndex(uint i)
{
#if USE_CLUSTER_LIGHT_LOOP
    return i;
#else
    return GetPerObjectLightIndex(i);
#endif
}

// Where additional light <i> is, the way URP's GetAdditionalLight finds it.
float3 CityXAdditionalLightPosition(uint i)
{
    int lightIndex = CityXAdditionalLightIndex(i);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    return _AdditionalLightsBuffer[lightIndex].position.xyz;
#else
    return _AdditionalLightsPosition[lightIndex].xyz;
#endif
}

// Whether a room's lamp reaches a surface outside, through the windows: only a lamp holding a shadow map, the one thing that knows where the
// walls are. The light is left as it falls there: its shadow solid, where indoors it is strength-softened and would let a share of the lamp
// through the wall, and its brightness following its shadow strength, so a lamp gaining or losing its map (RoomLampShadows) fades in and out.
bool CityXShineOutThroughWindows(inout Light light, uint i)
{
    if ((light.layerMask & CITYX_INTERIOR_LIGHT_LAYER) == 0)
    {
        return false;
    }
    half4 shadow = GetAdditionalLightShadowParams(CityXAdditionalLightIndex(i));
    if (shadow.w < 0.0 || shadow.x <= 0.0)
    {
        return false;
    }
    light.shadowAttenuation = saturate((light.shadowAttenuation - (1.0 - shadow.x)) / shadow.x);
    light.distanceAttenuation *= saturate(shadow.x / CITYX_ROOM_LAMP_SHADOW_STRENGTH);
    return true;
}

// A carried light's falloff (CITYX_BEAM_LIGHT_LAYER) on what is <under> the ground (0 under the sky, 1 in a tunnel): the square of the
// distance URP divided by, put back.
void CityXThrowBeam(inout Light light, uint i, float3 positionWS, half under)
{
    if ((light.layerMask & CITYX_BEAM_LIGHT_LAYER) != 0)
    {
        float3 toLight = CityXAdditionalLightPosition(i) - positionWS;
        light.distanceAttenuation *= lerp(1.0, dot(toLight, toLight) / (CITYX_BEAM_MATCH_METRES * CITYX_BEAM_MATCH_METRES), under);
    }
}

// What an eye makes out in the dark with no light at all: a faint light as if from the eye itself, strongest on what faces it and gone a
// few metres off. The owner asked for "barely any visibility", not none: a wall an arm's length away is a shape, the tunnel beyond is black.
half3 CityXDarkTrace(float3 positionWS, half3 normalWS, half3 viewDirectionWS)
{
    float near = saturate(1.0 - distance(positionWS, _WorldSpaceCameraPos) / max(_CityXDarkTrace.a, 0.001));
    half facing = 0.4 + 0.6 * saturate(dot(normalWS, viewDirectionWS));
    return _CityXDarkTrace.rgb * (near * near * facing);
}

half CityXToonEdge(half amount, half at, half halfWidth)
{
    return smoothstep(at - halfWidth, at + halfWidth, amount);
}

// The share of the sun's light a surface gets: the lit side's or the shade side's, the edge between them a pixel or two wide.
half CityXToonSunLevel(half amount)
{
    half lit = _CityXToonSun.w > 0.0 ? CityXToonEdge(amount, _CityXToonSun.x, _CityXToonSun.y) : 1.0;
    return _CityXToonSun.w > 0.0 ? lerp(_CityXToonSun.z, _CityXToonSun.w, lit) : 1.0;
}

// The share of a lamp's light a surface gets. The side that faces the lamp (facing: N.L times the lamp's shadow) gets the sun's lit share,
// the side that faces away or stands in its shadow none, split as the sun's is; and the whole of it fades with distance (falloff: the
// lamp's own, and a spot's cone) smoothly, so what a lamp lights is an area that fades out and never a set of rings.
half CityXToonLampLevel(half facing, half falloff)
{
    half lit = _CityXToonSun.w > 0.0 ? CityXToonEdge(facing, _CityXToonSun.x, _CityXToonSun.y) * _CityXToonSun.w : facing;
    return lit * falloff;
}

// One light on a surface, cel shaded: what LightingPhysicallyBased gives in URP's own lighting.
half3 CityXToonLight(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS, bool specularHighlightsOff, bool sun)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half amount = NdotL * light.distanceAttenuation * light.shadowAttenuation;
    half level = sun ? CityXToonSunLevel(amount) : CityXToonLampLevel(NdotL * light.shadowAttenuation, light.distanceAttenuation);
    half3 colour = brdfData.diffuse * light.color * level;
#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff && _CityXToonShine.x > 0.0)
    {
        half shine = DirectBRDFSpecular(brdfData, normalWS, light.direction, viewDirectionWS) * amount;
        colour += brdfData.specular * light.color * (CityXToonEdge(shine, _CityXToonShine.x, _CityXToonShine.x * 0.2) * _CityXToonShine.y);
    }
#endif
    return colour;
}

// The sun or the moon on a surface: the toon light with cel shading on, URP's LightingPhysicallyBased (exactly as URP calls it) with it off.
half3 CityXSunLight(BRDFData brdfData, BRDFData brdfDataClearCoat, Light light, half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff)
{
#if defined(CITYX_TOON)
    return CityXToonLight(brdfData, light, normalWS, viewDirectionWS, specularHighlightsOff, true);
#else
    return LightingPhysicallyBased(brdfData, brdfDataClearCoat, light, normalWS, viewDirectionWS, clearCoatMask, specularHighlightsOff);
#endif
}

// The ambient occlusion lighting uses: with cel shading, only the material's own (its occlusion map), none of the screen's; without it,
// URP's own (the screen's and the material's).
AmbientOcclusionFactor CityXOcclusion(InputData inputData, SurfaceData surfaceData)
{
#if defined(CITYX_TOON)
    AmbientOcclusionFactor aoFactor;
    aoFactor.indirectAmbientOcclusion = surfaceData.occlusion;
    aoFactor.directAmbientOcclusion = 1.0;
    return aoFactor;
#else
    return CreateAmbientOcclusionFactor(inputData, surfaceData);
#endif
}

// URP's UniversalFragmentPBR (ShaderLibrary/Lighting.hlsl, URP 17.3) with every light a toon light and the toon's occlusion, for CityX/Lit.
half4 CityXToonFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    #if defined(_NORMALMAP)
    inputData.normalWS = NormalizeNormalPerPixel(inputData.tangentToWorld[2]);   // lit flat, as the surface is, not by its bumps
    #endif
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CityXOcclusion(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    // Under the ground the sky's own light is already gone (DayNightCycle takes the ambient and the reflections away); the sun, the moon
    // and the city's lamps go here. A body knows only where the eye is, not where it stands itself: one seen down a manhole from the
    // street is lit as the street is.
    half sky = 1.0 - _CityXUnderground;

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    lightingData.giColor += CityXDarkTrace(inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS) * brdfData.diffuse
                            * _CityXUnderground;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = CityXToonLight(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS,
                                                     specularHighlightsOff, true) * sky;
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CityXToonLight(brdfData, light, inputData.normalWS, inputData.viewDirectionWS,
                                                                 specularHighlightsOff, true) * sky;
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        CityXThrowBeam(light, lightIndex, inputData.positionWS, _CityXUnderground);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            // The city's own lamps cast no shadows: a street lamp would light a body in the tunnel under its road.
            half reaches = (light.layerMask & CITYX_CITY_LIGHT_LAYERS) != 0 ? sky : 1.0;
            lightingData.additionalLightsColor += CityXToonLight(brdfData, light, inputData.normalWS, inputData.viewDirectionWS,
                                                                 specularHighlightsOff, false) * reaches;
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

// CityX/Lit's lighting: what its forward pass (URP's own LitForwardPass.hlsl) calls in place of UniversalFragmentPBR.
half4 CityXLitFragment(InputData inputData, SurfaceData surfaceData)
{
#if defined(_CITYX_SNOW)
    CityXApplySnow(surfaceData, inputData.positionWS, inputData.normalWS);
#endif
#if defined(CITYX_TOON)
    return CityXToonFragmentPBR(inputData, surfaceData);
#else
    return UniversalFragmentPBR(inputData, surfaceData);
#endif
}

#endif
