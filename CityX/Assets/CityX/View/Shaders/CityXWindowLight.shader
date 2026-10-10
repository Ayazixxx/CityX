// The light of a lit room seen through its window from outside: a soft additive veil just behind the glass, added to the real room that shows
// through it (2026-10-09 lighting pass). It used to be an opaque flat panel, which read as a painted rectangle, not as a lit room; now the room
// itself is what you see, and this only lifts it so a lit window still reads from down the street. Brighter towards the top of the window, where
// the lit ceiling is, and soft at the edges. The colour (the room's light) comes per room from a MaterialPropertyBlock (CityStreamer).
Shader "CityX/WindowLight"
{
    Properties
    {
        [HDR] _BaseColor("Light", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            // Before the glass in front of it, which then lays its own reflection and tint over both.
            "Queue" = "Transparent-20"
        }

        Pass
        {
            Name "WindowLight"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Back   // seen from the street only: from inside the room it is not there

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WindowLightVertex
            #pragma fragment WindowLightFragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WindowLightVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 WindowLightFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 uv = saturate(input.uv);
                half top = lerp(0.45, 1.0, smoothstep(0.0, 1.0, uv.y));
                half2 fromEdge = min(uv, 1.0 - uv);
                half edge = lerp(0.55, 1.0, smoothstep(0.0, 0.18, min(fromEdge.x, fromEdge.y)));
                half3 light = _BaseColor.rgb * top * edge;
                // Additive: fade to nothing (not to the fog's colour) with distance, as a light does.
                light = MixFogColor(light, half3(0, 0, 0), input.fogFactor);
                return half4(light, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
