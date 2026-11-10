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
// The numbers are globals set by CelShading.cs (live: /celshade), so no material changes for a retune. Each is (0, 0, 0, 0) until set,
// which lights everything flat and fully: the toon light never goes dark for want of a setting.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "CityXSnow.hlsl"

// The sun or the moon. x: the light (N.L times its shadow, 0..1) at which a surface turns from its shade side to its lit side; y: half the
// width of that edge; z: how much of the light the shade side gets; w: how much the lit side gets.
float4 _CityXToonSun;
// x: the specular (URP's DirectBRDFSpecular times the light) at which a hard highlight shows, y: its strength.
float4 _CityXToonShine;

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

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = CityXToonLight(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS,
                                                     specularHighlightsOff, true);
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
                                                                 specularHighlightsOff, true);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CityXToonLight(brdfData, light, inputData.normalWS, inputData.viewDirectionWS,
                                                                 specularHighlightsOff, false);
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
