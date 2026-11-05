// FALLING SNOW (docs/SNOW.md): one mesh of quads, one draw, nothing on the CPU. Every flake is a fixed point of a field that falls and drifts through the world and
// is wrapped into a box round the camera, so a sprint or a jetpack never leaves the snow behind, as particles falling from a box overhead would. A flake under
// cover (View/Weather/SnowEffect.cs's map of what is overhead) is not drawn, so none falls indoors or under a deck. Unlit: lit by the sky's own light, so it
// goes blue and dim by night. Nothing writes depth, so the cel look's black lines leave it alone.
Shader "CityX/Snowfall"
{
    Properties
    {
        _Box("Box round the camera, metres", Vector) = (40, 24, 40, 0)
        _Show("Share of the flakes shown", Range(0, 1)) = 0
        _Wind("Wind, m/s", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Box;
            float _Show;
            float4 _Wind;
            CBUFFER_END

            float4 _CityXSnowMap;
            TEXTURE2D(_CityXSnowHeights);
            SAMPLER(sampler_CityXSnowHeights);

            // Flake sizes, metres; fall speeds, m/s; the width of a flake's sideways wander.
            #define SMALLEST 0.035
            #define LARGEST 0.1
            #define SLOWEST 1.1
            #define FASTEST 2.2
            #define WANDER 0.5
            // A flake closer than this to the eye fades away, and one at the box's edge, as a share of its half width.
            #define NEAR_GONE 0.35
            #define NEAR_FULL 1.2
            #define EDGE_GONE 0.5
            #define EDGE_FULL 0.36

            struct Attributes
            {
                float3 seed : POSITION;       // where in the box, 0..1
                float2 corner : TEXCOORD0;    // -1..1
                float4 flake : TEXCOORD1;     // x: its rank, for how many are shown; y: its size; z: its speed; w: its phase, each 0..1
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                half4 colour : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float t = _Time.y;
                float phase = input.flake.w * 6.2832;
                float3 p = input.seed * _Box.xyz;
                p.y -= t * lerp(SLOWEST, FASTEST, input.flake.z);
                p.xz += t * _Wind.xz + float2(sin(t * 0.9 + phase), cos(t * 0.7 + phase * 1.7)) * WANDER;

                float3 fromEye = frac((p - _WorldSpaceCameraPos) / _Box.xyz) * _Box.xyz - _Box.xyz * 0.5;
                float3 world = _WorldSpaceCameraPos + fromEye;

                float roof = SAMPLE_TEXTURE2D_LOD(_CityXSnowHeights, sampler_CityXSnowHeights, world.xz * _CityXSnowMap.w, 0.0).r;
                float shown = input.flake.x <= _Show && roof <= world.y ? 1.0 : 0.0;

                float away = length(fromEye);
                float halfBox = min(_Box.x, _Box.z) * 0.5;
                float fade = smoothstep(NEAR_GONE, NEAR_FULL, away) * (1.0 - smoothstep(EDGE_FULL * 2.0 * halfBox, EDGE_GONE * 2.0 * halfBox, away));

                float size = lerp(SMALLEST, LARGEST, input.flake.y) * shown;
                float3 view = TransformWorldToView(world) + float3(input.corner * size, 0.0);
                output.positionCS = TransformWViewToHClip(view);
                output.corner = input.corner;
                half3 light = SampleSH(half3(0.0, 1.0, 0.0)) + GetMainLight().color * 0.35;
                output.colour = half4(light, fade);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float soft = saturate(1.0 - dot(input.corner, input.corner));
                return half4(MixFog(input.colour.rgb, input.fog), input.colour.a * soft * soft);
            }
            ENDHLSL
        }
    }
}
