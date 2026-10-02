#ifndef CITYX_GLASS_FORWARD_PASS_INCLUDED
#define CITYX_GLASS_FORWARD_PASS_INCLUDED

// The window glass pass: URP's Lit forward pass for a transparent, premultiplied surface, with the look read off the vertex.
//
//   vertex colour  r, g  unused
//                  b     glass tint (high nibble, an index into kGlassTints) and light colour (low nibble, kLightColours)
//                  a     how far a blind is drawn down the window, 0 .. 200 (a 255 b 255 means "unstyled": plain dark glass, no blind)
//
// What it adds to plain tinted glass: a tint per building (blue, green, bronze, grey), a lit window that is one of several kinds of
// light rather than all the same, and blinds drawn part-way down lit windows, so a lit building at night reads as people's rooms.

#define _ALPHAPREMULTIPLY_ON 1
#define _SPECULAR_SETUP 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Authoring (gamma) colours of the glass: how dark it is and what hue. Index 0 is the plain dark glass the city always had.
static const float3 kGlassTints[8] =
{
    float3(0.055, 0.062, 0.075),    // 0 neutral
    float3(0.040, 0.075, 0.120),    // 1 blue
    float3(0.030, 0.085, 0.075),    // 2 teal green
    float3(0.095, 0.070, 0.045),    // 3 bronze
    float3(0.070, 0.070, 0.072),    // 4 grey
    float3(0.090, 0.130, 0.150),    // 5 pale blue-green (lighter, clearer glass)
    float3(0.035, 0.050, 0.065),    // 6 deep blue-black
    float3(0.060, 0.080, 0.060)     // 7 green-grey
};

// What a lit window glows with, as linear multipliers of the day/night cycle's emission colour.
static const float3 kLightColours[8] =
{
    float3(1.00, 0.80, 0.50),       // 0 warm white
    float3(1.00, 0.90, 0.58),       // 1 pale yellow
    float3(0.78, 0.88, 1.00),       // 2 cool white
    float3(0.40, 0.55, 1.00),       // 3 blue (a screen)
    float3(1.00, 0.55, 0.28),       // 4 amber
    float3(0.70, 1.00, 0.80),       // 5 office green-white
    float3(1.00, 0.72, 0.82),       // 6 pink
    float3(1.00, 0.95, 0.85)        // 7 bright white
};

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float4 _EmissionColor;
float _Smoothness;
CBUFFER_END

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
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
    half4 look                      : TEXCOORD4;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5;
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

void InitializeInputData(Varyings input, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    // Two-sided: a quad seen from behind is lit as the face it is, not as a back face.
    float3 normalWS = input.normalWS;
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
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

Varyings GlassPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = input.texcoord;
    output.look = input.color;
    output.normalWS = normalInput.normalWS;

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

void GlassPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // Decode the look. An unstyled vertex (opaque white) is plain dark glass.
    float tintByte = round(input.look.b * 255.0);
    float blindByte = round(input.look.a * 255.0);
    bool styled = !(tintByte > 254.5 && blindByte > 254.5);
    int tintIndex = styled ? (int)floor(tintByte / 16.0) : 0;
    int lightIndex = styled ? (int)(tintByte - floor(tintByte / 16.0) * 16.0) : 0;
    float blind = styled ? saturate(blindByte / 255.0) : 0.0;

    float3 glassTint = SRGBToLinear(kGlassTints[min(tintIndex, 7)]);
    float alpha = _BaseColor.a;

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = glassTint;
    surfaceData.alpha = alpha;
    surfaceData.metallic = 0.0;
    // Glass reflects about a tenth of the light that meets it head on (specular setup; the plain dielectric 4 reads as a dark hole).
    surfaceData.specular = half3(0.10, 0.10, 0.11);
    surfaceData.smoothness = _Smoothness;
    surfaceData.occlusion = 1.0;
    surfaceData.normalTS = half3(0, 0, 1);

    // A lit window: one of several lights, brighter low in the room than at the ceiling, with a blind drawn down from the top.
    float y01 = saturate(input.uv.y / 2.0);
    float3 light = kLightColours[min(lightIndex, 7)] * lerp(1.0, 0.72, y01);
    float blindEdge = 1.0 - blind;
    float covered = step(blindEdge, y01);
    float slats = 0.55 + 0.45 * step(0.5, frac(input.uv.y * 14.0));
    light *= lerp(1.0, 0.14 * slats, covered * step(0.001, blind));
    surfaceData.emission = _EmissionColor.rgb * light;

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, inputData);
    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = alpha;

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
