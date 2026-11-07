// LIGHT SHAFTS (docs/LIGHTING.md): sunbeams through the air between the towers, and the dark of a building's shadow standing in the haze.
// Drawn by LightShaftsFeature before post-processing, after the city's air (CityXHaze), in two passes. TRACE, at half the screen's width and
// height: along each pixel's line of sight, a few steps through the air ask the sun's shadow map whether the sun reaches that point. The haze
// already glows evenly with the sun's light; this takes the shadow into it: the air the sun reaches glows more and the air in a tower's
// shadow less, by about as much, so beams and the shadows of towers show in the haze without the whole picture going pale (Toft, Bowman and
// Christensen, "Volumetric light" in GPU Pro 5; the idea of Unity's and Unreal's volumetric fog, per pixel with no froxel grid). LAY OVER, at
// full size: the traced light added to the picture, stretched smooth.
//
// How thick the air is comes from the haze's own numbers (_CityXHazeParams, set by DayNightCycle through CityHaze), so the beams thicken
// and thin with the haze; with no haze set (a test scene) _CityXShaftDensity stands in. Scaled by the sun's brightness: none at night.
// A strength of 0 hands the picture back untouched.
Shader "CityX/LightShafts"
{
    Properties
    {
        _CityXShaftStrength ("Strength", Range(0, 4)) = 1
        _CityXShaftDistance ("How far the steps go (m)", Float) = 140
        _CityXShaftDensity ("Air thickness with no haze set (per m)", Float) = 0.0015
        _CityXShaftBalance ("How much the shadowed air darkens (0: only brighten)", Range(0, 1)) = 0.5
        _CityXShaftMaxDarken ("The most the shadowed air takes away (share of the picture)", Range(0, 1)) = 0.25
        _CityXShaftForward ("How much brighter toward the sun", Range(0, 8)) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _CityXShaftStrength;
        float _CityXShaftDistance;
        float _CityXShaftDensity;
        float _CityXShaftForward;
        float _CityXShaftBalance;
        float _CityXShaftMaxDarken;
        ENDHLSL

        Pass
        {
            Name "Trace"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Trace
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            float4 _CityXHazeParams;   // x: density at BaseY (per metre), y: falloff (per metre), z: BaseY (CityHaze)

            static const int Steps = 24;

            // Below this the main light is the moon (or the sun under the horizon): its shafts would not show, so none are traced.
            static const float MinSunBrightness = 0.1;

            // Whether the sun reaches p: one hardware comparison in the shadow map, not the soft shadows' filter (up to 16 reads a step): the
            // steps' jitter blurs the air's edges anyway.
            float SunReaches(float3 p)
            {
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 coord = TransformWorldToShadowCoord(p);
                float lit = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, coord.xyz);
                return BEYOND_SHADOW_FAR(coord) ? 1.0 : lit;
            #else
                return 1.0;
            #endif
            }

            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-7;
            #else
                return rawDepth >= 1.0 - 1e-7;
            #endif
            }

            // The air's thickness at height y: the haze's exponential fall-off with height, or the stand-in.
            float DensityAt(float y)
            {
                if (_CityXHazeParams.x > 0.0)
                {
                    return _CityXHazeParams.x * exp(-max(_CityXHazeParams.y, 1e-5) * (y - _CityXHazeParams.z));
                }
                return _CityXShaftDensity;
            }

            // The light the air adds (or, in a shadow in the haze, takes away) along this pixel's line of sight.
            half4 Trace(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_CityXShaftStrength <= 0.0)
                {
                    return 0.0;
                }
                Light sun = GetMainLight();
                if (Luminance(sun.color) <= MinSunBrightness)
                {
                    return 0.0;
                }

                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                bool sky = IsSky(rawDepth);
            #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
            #endif
                float3 world = ComputeWorldSpacePosition(uv, sky ? (UNITY_REVERSED_Z ? 1e-6 : 1.0 - 1e-6) : rawDepth, UNITY_MATRIX_I_VP);
                float3 toPoint = world - _WorldSpaceCameraPos;
                float reach = length(toPoint);
                float3 dir = toPoint / max(reach, 1e-4);
                reach = min(reach, _CityXShaftDistance);

                // Forward scattering: the air glows most looking toward the sun (a broad lobe and a tight one, the haze's own shape).
                float toward = saturate(dot(dir, normalize(sun.direction)));
                float phase = 0.2 + _CityXShaftForward * (pow(toward, 4.0) * 0.6 + pow(toward, 32.0) * 0.4);

                float stepLength = reach / Steps;
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                float lit = 0.0;
                float all = 0.0;
                for (int i = 0; i < Steps; i++)
                {
                    float3 p = _WorldSpaceCameraPos + dir * ((i + jitter) * stepLength);
                    float air = DensityAt(p.y) * stepLength;
                    lit += SunReaches(p) * air;
                    all += air;
                }
                // The lit air's glow, less a share of the glow the haze already gives all of it, so shadowed air comes out darker than the haze.
                // Only where there is haze to darken (none set: a test scene, nothing to take away).
                float haze = _CityXHazeParams.x > 0.0 ? 1.0 : 0.0;
                float glow = (1.0 - exp(-lit)) - _CityXShaftBalance * haze * (1.0 - exp(-all));
                return half4(sun.color * glow * phase * _CityXShaftStrength, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "LayOver"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment LayOver

            TEXTURE2D_X(_CityXShaftsTexture);

            // The traced light on the picture, never darker than a share of it: the shadow takes back the haze's sunlight, it cannot make a
            // wall darker than it is in clear air.
            half4 LayOver(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                half3 light = SAMPLE_TEXTURE2D_X(_CityXShaftsTexture, sampler_LinearClamp, uv).rgb;
                return half4(max(scene.rgb + light, scene.rgb * (1.0 - _CityXShaftMaxDarken)), scene.a);
            }
            ENDHLSL
        }
    }
}
