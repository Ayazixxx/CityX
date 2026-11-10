#ifndef CITYX_FACADE_FORWARD_PASS_INCLUDED
#define CITYX_FACADE_FORWARD_PASS_INCLUDED

// The facade shader's forward pass: URP's Lit forward pass, with the surface taken from a texture ARRAY of wall materials and the
// building's look read off the vertex.
//
//   vertex colour rgb  the tint, an authoring (gamma) colour
//   vertex colour a    255 minus (slice * 16 + sheltered * 8 + wear): which wall material, whether it is inside a building or under
//                      the ground, and how weathered (SurfaceLook.cs packs it)
//
// Kept as close to LitForwardPass.hlsl as it can be so lighting, shadows, Forward+, probes, SSAO and decals behave exactly as they do
// on every other material in the city; only InitializeFacadeSurface is new.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "CityXToonLighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

TEXTURE2D_ARRAY(_FacadeAlbedo);
SAMPLER(sampler_FacadeAlbedo);
TEXTURE2D_ARRAY(_FacadeNormal);
SAMPLER(sampler_FacadeNormal);

// Set globally (Shader.SetGlobalFloat): _CityXWetness by the rain (0 dry, 1 soaked); _CityXIndoorShade by DayNightCycle, how much of the
// sky's light the inside of a building does NOT get (0 = all of it, like the street; 0.7 = a third of it). 0 when nothing sets it.
CBUFFER_START(CityXGlobals)
float _CityXWetness;
float _CityXIndoorShade;
CBUFFER_END

// A room light is a fitting, not a point: its light falls off as 1 / (d^2 + r^2), not 1 / d^2, with r this radius in metres (the squared value).
// Without it the wall a metre from a lamp got thirty times the light of the floor six metres under it and burnt white while the floor stayed dim.
#define CITYX_FITTING_RADIUS_SQ 2.6

// A room light's falloff, softened near it (CITYX_FITTING_RADIUS_SQ).
void CityXSoftenFitting(inout Light light, uint i, float3 positionWS)
{
    if ((light.layerMask & CITYX_INTERIOR_LIGHT_LAYER) != 0)
    {
        float3 toLight = CityXAdditionalLightPosition(i) - positionWS;
        float d2 = dot(toLight, toLight);
        light.distanceAttenuation *= d2 / (d2 + CITYX_FITTING_RADIUS_SQ);
    }
}

// The bounced light of the lit rooms nearest the camera (View/Lighting/RoomFill.cs packs them, RoomLightDirector sets them): an even light on
// every surface of a lit room, which its lamps (spots shining down) do not give. A surface belongs to the room it FACES: the point
// CITYX_ROOM_FILL_NUDGE in front of it is tested against each room's box, less the rooms carved from it. Per room, CITYX_ROOM_FILL_STRIDE
// entries: (origin x, origin z, floor y, height), (x axis x, z, z axis x, z), floor plan (xMin, zMin, xMax, zMax), two cuts the same, colour.
// MUST match RoomFill.Stride / RoomFill.Nudge and RoomLightDirector.MaxFilledRooms.
#define CITYX_MAX_ROOM_FILLS 32
#define CITYX_ROOM_FILL_STRIDE 6
#define CITYX_ROOM_FILL_NUDGE 0.05

CBUFFER_START(CityXRoomFills)
float4 _CityXRoomFills[CITYX_MAX_ROOM_FILLS * CITYX_ROOM_FILL_STRIDE];
float _CityXRoomFillCount;
CBUFFER_END

// The lit rooms' bounced light on the surface at positionWS facing normalWS (RoomFill.Covers is the same test).
half3 CityXRoomFill(float3 positionWS, float3 normalWS)
{
    float3 q = positionWS + normalWS * CITYX_ROOM_FILL_NUDGE;
    half3 fill = half3(0.0, 0.0, 0.0);
    int count = min((int)_CityXRoomFillCount, CITYX_MAX_ROOM_FILLS);
    [loop] for (int i = 0; i < count; i++)
    {
        int at = i * CITYX_ROOM_FILL_STRIDE;
        float4 frame = _CityXRoomFills[at];
        float4 axes = _CityXRoomFills[at + 1];
        float4 area = _CityXRoomFills[at + 2];
        float4 cutA = _CityXRoomFills[at + 3];
        float4 cutB = _CityXRoomFills[at + 4];
        float2 d = q.xz - frame.xy;
        float2 p = float2(dot(d, axes.xy), dot(d, axes.zw));
        float y = q.y - frame.z;
        bool inside = y >= 0.0 && y <= frame.w && all(p >= area.xy) && all(p <= area.zw);
        bool cut = (all(p >= cutA.xy) && all(p <= cutA.zw)) || (all(p >= cutB.xy) && all(p <= cutB.zw));
        if (inside && !cut)
        {
            fill += half3(_CityXRoomFills[at + 5].rgb);
        }
    }
    return fill;
}

// What a tile of each wall material covers, in metres across and up. MUST match FacadeTextures.TileMetresX / TileMetresY.
static const float2 kTileMetres[16] =
{
    float2(4.0, 4.0),       // concrete
    float2(2.025, 2.1),     // brick
    float2(3.0, 3.0),       // plaster
    float2(2.0, 2.0),       // metal
    float2(4.0, 4.0),       // stone
    float2(1.6, 1.6),       // tile
    float2(3.2, 3.2),       // spandrel
    float2(3.0, 3.0),       // roof
    float2(4.0, 4.0),       // paving
    float2(6.0, 6.0),       // asphalt
    float2(5.0, 5.0),       // earth
    float2(2.0, 2.0),       // setts
    float2(2.025, 2.1),     // grey brick (old brick, under the ground)
    float2(2.025, 2.1),     // blue brick
    float2(2.025, 2.1),     // stock brick
    float2(2.025, 2.1)      // moss brick
};

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    half4  color        : COLOR;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float3 positionWS               : TEXCOORD1;
    float3 normalWS                 : TEXCOORD2;
    half4 tangentWS                 : TEXCOORD3;    // xyz: tangent, w: sign
    half4 look                      : TEXCOORD4;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float FacadeHash(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

// Smooth value noise, 0..1, in whatever units p is in.
float FacadeNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FacadeHash(i);
    float b = FacadeHash(i + float2(1, 0));
    float c = FacadeHash(i + float2(0, 1));
    float d = FacadeHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

void InitializeFacadeSurface(Varyings input, out SurfaceData surfaceData, out half3 normalTS, out bool indoors, out bool underground)
{
    surfaceData = (SurfaceData)0;

    // The look: slice, sheltered and wear out of the alpha (SurfaceLook.Pack), tint out of the colour. Sheltered is a building's inside
    // or, with the filthy half of the wear, under the ground (SurfaceLook.UndergroundWear).
    float code = round((1.0 - input.look.a) * 255.0);
    float slice = floor(code / 16.0);
    float rest = code - slice * 16.0;
    indoors = rest > 7.5;
    underground = rest > 11.5;
    float wear = (rest - (indoors ? 8.0 : 0.0)) / 7.0;
    float3 tint = SRGBToLinear(input.look.rgb);

    float2 tileUV = input.uv / kTileMetres[(int)slice];
    float4 albedoSample = SAMPLE_TEXTURE2D_ARRAY(_FacadeAlbedo, sampler_FacadeAlbedo, tileUV, slice);
    half3 n = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_FacadeNormal, sampler_FacadeNormal, tileUV, slice));

    float3 albedo = albedoSample.rgb * tint;
    float smoothness = albedoSample.a;

    // Broad, slow variation so a wall is not one flat tint: patches of a few metres, never the same on two buildings (the UV
    // origin is different on each).
    float broad = FacadeNoise(input.uv * 0.13) * 0.65 + FacadeNoise(input.uv * 0.41 + 7.3) * 0.35;
    albedo *= 1.0 + (broad - 0.5) * 0.30;

    // Weathering: vertical rain streaks, and a general dulling and darkening.
    float streak = FacadeNoise(float2(input.uv.x * 1.9, input.uv.y * 0.07));
    streak = smoothstep(0.35, 0.85, streak);
    float dirt = wear * (0.45 + 0.55 * streak) * (0.7 + 0.6 * broad);
    float luma = dot(albedo, float3(0.299, 0.587, 0.114));
    albedo = lerp(albedo, float3(luma, luma * 0.95, luma * 0.88), dirt * 0.45);
    albedo *= lerp(1.0, 0.50, dirt);
    smoothness *= lerp(1.0, 0.55, dirt);

    // Rain: wet surfaces are darker and shinier.
    albedo *= lerp(1.0, 0.70, _CityXWetness);
    smoothness = lerp(smoothness, max(smoothness, 0.72), _CityXWetness);

    surfaceData.albedo = albedo;
    surfaceData.alpha = 1.0;
    surfaceData.metallic = 0.0;
    surfaceData.specular = half3(0.0, 0.0, 0.0);
    surfaceData.smoothness = smoothness;
    // Inside a building the open sky is mostly hidden: its light (the ambient and the sky's reflection, which URP scales by occlusion) is dimmed
    // there. The sun and the moon still come in through the windows, and the room's own lights are what lights it at night. Under the
    // ground there is no sky at all.
    surfaceData.occlusion = underground ? 0.0 : (indoors ? 1.0 - _CityXIndoorShade : 1.0);
    surfaceData.emission = half3(0.0, 0.0, 0.0);
#if defined(CITYX_TOON)
    n = half3(0.0, 0.0, 1.0);   // cel shaded: lit flat, as the wall is, not by its bricks (CityXToonLighting.hlsl)
#endif
    surfaceData.normalTS = n;
    normalTS = n;
}

// URP's UniversalFragmentPBR (ShaderLibrary/Lighting.hlsl, URP 17.3) with one change: the city's lights know which side of a wall they are on
// (CITYX_INTERIOR_LIGHT_LAYER / CITYX_EXTERIOR_LIGHT_LAYER). Without it a lit room's lamps, which cast no shadows, would shine straight through
// the building's walls onto the pavement and the building next door. The sun and the moon go through CityXSunLight, cel shaded with CITYX_TOON.
// A surface under the ground gets none of them: no sun, no moon, none of the city's lamps on either side of a wall, only the lights that
// carry no side (a flashlight, a fire, a spell) and the dark's trace round the eye (CityXToonLighting.hlsl).
half4 CityXFragmentPBR(InputData inputData, SurfaceData surfaceData, bool indoors, bool underground)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
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
    // URP's CreateAmbientOcclusionFactor, keeping the screen's occlusion alone for a lit room's bounced light: the indoor shade in
    // surfaceData.occlusion is the sky's, not the room's.
#if defined(CITYX_TOON)
    AmbientOcclusionFactor aoFactor = CityXOcclusion(inputData, surfaceData);   // the indoor shade alone, none of the screen's
    half screenOcclusion = 1.0;
#else
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half screenOcclusion = aoFactor.indirectAmbientOcclusion;
    aoFactor.indirectAmbientOcclusion = min(screenOcclusion, surfaceData.occlusion);
#endif
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    if (underground)
    {
        lightingData.giColor += CityXDarkTrace(inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS) * brdfData.diffuse;
    }
    else if (indoors)
    {
        lightingData.giColor += CityXRoomFill(inputData.positionWS, inputData.normalWS) * brdfData.diffuse * screenOcclusion;
    }
#ifdef _LIGHT_LAYERS
    if (!underground && IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#else
    if (!underground)
#endif
    {
        lightingData.mainLightColor = CityXSunLight(brdfData, brdfDataClearCoat, mainLight, inputData.normalWS,
                                                    inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
    }

    // The lights that belong on the other side of this surface's wall; under the ground, both sides' (the whole city is above).
    uint wrongSide = underground ? CITYX_CITY_LIGHT_LAYERS : (indoors ? CITYX_EXTERIOR_LIGHT_LAYER : CITYX_INTERIOR_LIGHT_LAYER);

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        CityXSoftenFitting(light, lightIndex, inputData.positionWS);

#ifdef _LIGHT_LAYERS
        if (!underground && IsMatchingLightLayer(light.layerMask, meshRenderingLayers) && (light.layerMask & wrongSide) == 0)
#else
        if (!underground && (light.layerMask & wrongSide) == 0)
#endif
        {
            lightingData.additionalLightsColor += CityXSunLight(brdfData, brdfDataClearCoat, light, inputData.normalWS,
                                                                inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        CityXSoftenFitting(light, lightIndex, inputData.positionWS);
        CityXThrowBeam(light, lightIndex, inputData.positionWS, underground ? 1.0 : 0.0);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers) && (light.layerMask & wrongSide) == 0)
#else
        if ((light.layerMask & wrongSide) == 0)
#endif
        {
            // A lamp's light on a wall or a street is soft with the cel look too: flat steps drew rings round every lamp (CityXToonLighting.hlsl).
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light, inputData.normalWS,
                                                                          inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
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

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

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

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

Varyings FacadePassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = input.texcoord;
    output.look = input.color;
    output.normalWS = normalInput.normalWS;
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

    output.positionWS = vertexInput.positionWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

void FacadePassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    half3 normalTS;
    bool indoors;
    bool underground;
    InitializeFacadeSurface(input, surfaceData, normalTS, indoors, underground);
    if (!indoors)
    {
        CityXApplySnow(surfaceData, input.positionWS, normalize(input.normalWS));
    }

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = CityXFragmentPBR(inputData, surfaceData, indoors, underground);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0;

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
