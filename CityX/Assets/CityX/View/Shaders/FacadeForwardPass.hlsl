#ifndef CITYX_FACADE_FORWARD_PASS_INCLUDED
#define CITYX_FACADE_FORWARD_PASS_INCLUDED

// The facade shader's forward pass: URP's Lit forward pass, with the surface taken from a texture ARRAY of wall materials and the
// building's look read off the vertex.
//
//   vertex colour rgb  the tint, an authoring (gamma) colour
//   vertex colour a    255 minus (slice * 16 + wear): which wall material, and how weathered (SurfaceLook.cs packs it)
//
// Kept as close to LitForwardPass.hlsl as it can be so lighting, shadows, Forward+, probes, SSAO and decals behave exactly as they do
// on every other material in the city; only InitializeFacadeSurface is new.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

TEXTURE2D_ARRAY(_FacadeAlbedo);
SAMPLER(sampler_FacadeAlbedo);
TEXTURE2D_ARRAY(_FacadeNormal);
SAMPLER(sampler_FacadeNormal);

// Set globally (Shader.SetGlobalFloat) by the rain: 0 dry, 1 soaked.
CBUFFER_START(CityXGlobals)
float _CityXWetness;
CBUFFER_END

// What a tile of each wall material covers, in metres across and up. MUST match FacadeTextures.TileMetresX / TileMetresY.
static const float2 kTileMetres[12] =
{
    float2(4.0, 4.0),       // concrete
    float2(2.025, 2.1),     // brick
    float2(3.0, 3.0),       // plaster
    float2(2.0, 2.0),       // metal
    float2(4.0, 4.0),       // stone
    float2(1.6, 1.6),       // tile
    float2(3.2, 3.2),       // spandrel
    float2(3.0, 3.0),       // roof
    float2(3.2, 3.2),       // paving
    float2(6.0, 6.0),       // asphalt
    float2(5.0, 5.0),       // earth
    float2(2.0, 2.0)        // setts
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

void InitializeFacadeSurface(Varyings input, out SurfaceData surfaceData, out half3 normalTS)
{
    surfaceData = (SurfaceData)0;

    // The look: slice and wear out of the alpha, tint out of the colour.
    float code = round((1.0 - input.look.a) * 255.0);
    float slice = floor(code / 16.0);
    float wear = (code - slice * 16.0) / 15.0;
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
    surfaceData.occlusion = 1.0;
    surfaceData.emission = half3(0.0, 0.0, 0.0);
    surfaceData.normalTS = n;
    normalTS = n;
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
    InitializeFacadeSurface(input, surfaceData, normalTS);

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

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0;

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
