// A per-material toon/cel shader: lighting quantised into discrete bands
// that follow the SURFACE (normal against light direction), not the
// screen. This is deliberately not the technique the Godot build tried
// and the user rejected -- that was a full-screen posterize pass applied
// after everything was already lit; this instead replaces the lighting
// model itself, so a band edge is a line on the character's own geometry,
// the way Genshin/Breath of the Wild's toon shading reads.
//
// HAND-WRITTEN RATHER THAN SHADER GRAPH, for the same reason every other
// generated asset in this project is code: a Shader Graph .shadergraph is
// an opaque binary blob that cannot be reviewed in a diff or repaired
// without opening a graph editor.
//
// READS THE SAME AMBIENT THE REST OF THE SCENE DOES. DayNightCycle drives
// RenderSettings.ambientLight in AmbientMode.Flat every frame; SampleSH()
// picks that up automatically (Unity bakes flat ambient into the SH
// zeroth band), so the character dims and warms with the same day/night
// cycle everything else in the city follows, rather than carrying its own
// fixed lighting.
Shader "CityX/ToonLit"
{
    Properties
    {
        _BaseColor("Base Colour", Color) = (0.72, 0.60, 0.52, 1)

        // Not pure black: a real shadow band picks up a cooler, darker
        // tint of the surface rather than going flat, which is what keeps
        // a toon-shaded body reading as lit-from-somewhere rather than
        // half cut out.
        _ShadowColor("Shadow Tint", Color) = (0.55, 0.55, 0.65, 1)

        // Where the lit/shadow band splits, in N.L terms: above this is
        // lit, below it is shadow. 0.5 is roughly "facing the light or
        // not"; push it up for a body that reads mostly-shadowed, down
        // for mostly-lit.
        _ShadowThreshold("Shadow Threshold", Range(0.0, 1.0)) = 0.45

        // A small smoothstep width across the band edge -- zero would be
        // a razor-sharp cutoff, which aliases badly on a smooth-blended
        // capsule body. Small enough that the band still reads as a hard
        // edge from normal play distance.
        _BandSoftness("Band Softness", Range(0.001, 0.25)) = 0.04

        // A thin bright line at silhouette edges, the other half of the
        // toon look besides the lighting bands -- helps a character
        // separate from a similarly-lit background.
        _RimColor("Rim Colour", Color) = (1.0, 1.0, 1.0, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimStrength("Rim Strength", Range(0.0, 1.0)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float4 _ShadowColor;
            float _ShadowThreshold;
            float _BandSoftness;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = positions.positionWS;
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // N.L in [0,1], not [-1,1] -- a surface facing away from
                // the light is exactly as shadowed as one in shade, there
                // is no "negative light" band to draw.
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float lit = ndotl * mainLight.shadowAttenuation;

                // The actual toon step: smoothstep rather than a hard
                // compare, so the band edge is a couple of pixels wide
                // instead of a jagged single-pixel staircase.
                float band = smoothstep(
                    _ShadowThreshold - _BandSoftness, _ShadowThreshold + _BandSoftness, lit);

                float3 litColour = _BaseColor.rgb * mainLight.color.rgb;
                float3 shadowColour = _BaseColor.rgb * _ShadowColor.rgb;
                float3 colour = lerp(shadowColour, litColour, band);

                // Flat ambient, matching DayNightCycle's own AmbientMode.Flat
                // -- SampleSH reads whatever RenderSettings.ambientLight
                // currently is regardless of normal, so this rises and falls
                // with the same day/night cycle as everything else.
                colour += _BaseColor.rgb * SampleSH(normalWS);

                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = 1.0 - saturate(dot(normalWS, viewWS));
                rim = pow(rim, _RimPower) * _RimStrength * lit;
                colour += _RimColor.rgb * rim;

                colour = MixFog(colour, input.fogFactor);

                return half4(colour, _BaseColor.a);
            }
            ENDHLSL
        }

        // Lets the character cast a real shadow onto the street rather
        // than floating shadowless under the sun/moon -- copied from
        // URP's own Lit shader, which every custom lit shader in this
        // pipeline needs verbatim.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
                return positionCS;
            }

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Needed by URP's depth texture / SSAO / any pass that samples
        // _CameraDepthTexture rather than the main colour target -- without
        // it the character silently drops out of anything reading depth.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
