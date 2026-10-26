// A GHOST'S SURFACE (the Wraith, owner 2026-10-26: "somewhat transparent and glowing because he is a ghost"): see-through, brighter and
// more solid at its edges (a Fresnel rim, as a thin glowing body looks side-on), smoke drifting slowly up inside it (value noise moved by
// time), and alpha from the vertex: where it is under 1 the surface is not faded evenly but ERODED into streaks of smoke (noise against a
// threshold that rises as the vertex alpha falls, the dissolve every ghost VFX is built on; stretched tall and scrolled upward, the streaks
// are smoke rising), so the body turns to wisps where its legs would be and a wisp thins to nothing at its tip. The owner, on a body
// that merely faded: "that just looks like a tornado sitting beneath him, make his body turn to wisps and fade away".
//
// Unlit: it is its own light. Drawn with the transparent things, after the cel look's black lines (CityXOutline runs before the
// transparents) and with no depth written, so nothing outlines it and it never hides what is behind it; the wisps, open ribbons, draw both
// sides (_Cull). No shadow pass: a ghost throws no shadow.
//
// _Sway moves the vertices (only the wisps' material sets it): each point swings sideways by its distance down the ribbon (uv.y, 0 at the
// top), each ribbon on its own phase (vertex colour r), so the tails writhe without any bones. _Fade and _Flash are set live (Wraith.cs):
// the whole thing fading away when it dies, and a flash of white when it is hit.
Shader "CityX/Ghost"
{
    Properties
    {
        _BaseColor("Body", Color) = (0.88, 0.93, 0.98, 1)
        [HDR] _RimColor("Rim", Color) = (1.05, 1.05, 1.08, 1)
        _RimPower("Rim power", Range(0.5, 8.0)) = 2.5
        _Alpha("Alpha", Range(0.0, 1.0)) = 0.55
        _RimAlpha("Rim alpha", Range(0.0, 1.0)) = 0.9
        _Glow("Glow", Range(0.0, 4.0)) = 1.4
        _Flow("Inner smoke", Range(0.0, 1.0)) = 0.35
        _FlowSpeed("Smoke speed", Float) = 0.25
        _Sway("Sway (m)", Float) = 0.0
        _SwaySpeed("Sway speed", Float) = 1.2
        // A veil's side edges feathered to nothing (alpha by sin of uv.x across it): the wisps, cut as flat strips, would show as paper.
        _EdgeFeather("Edge feather", Range(0.0, 1.0)) = 0.0
        _Fade("Fade", Range(0.0, 1.0)) = 1.0
        _Flash("Flash", Range(0.0, 1.0)) = 0.0
        // A closed body culls its back faces (drawn through the front with no depth written, they banded and doubled it); the wisps,
        // open ribbons, draw both sides.
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _RimColor;
            float _RimPower;
            float _Alpha;
            float _RimAlpha;
            float _Glow;
            float _Flow;
            float _FlowSpeed;
            float _Sway;
            float _SwaySpeed;
            float _EdgeFeather;
            float _Fade;
            float _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float alpha : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                float across : TEXCOORD5;
            };

            // The streaks the body erodes into: how tall they are stretched (the noise's x and z scale against its y), how fast they rise,
            // and how soft their edges are.
            static const float3 StreakScale = float3(7.0, 2.2, 7.0);
            static const float StreakRise = 0.45;
            static const float StreakEdge = 0.1;

            // Value noise: smooth random in [0, 1] over space (a hash per lattice point, blended), cheap enough for every pixel of one ghost.
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.17, 0.23));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                // The wisps writhe: a sideways swing growing down the ribbon, each ribbon on its own phase.
                float down = input.uv.y;
                float phase = input.color.r * 6.2831853;
                float t = _Time.y * _SwaySpeed;
                positionOS.x += _Sway * down * sin(t + phase + down * 4.0);
                positionOS.z += _Sway * down * cos(t * 0.8 + phase * 1.7 + down * 3.0);
                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.alpha = input.color.a;
                output.across = input.uv.x;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
                float3 toCamera = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(dot(normal, toCamera)), _RimPower);

                // Smoke drifting up inside: two octaves of noise over world space, carried upward by time.
                float3 flowAt = input.positionWS * 2.2 - float3(0.0, _Time.y * _FlowSpeed, 0.0);
                float smoke = Noise(flowAt) * 0.65 + Noise(flowAt * 2.3 + 7.1) * 0.35;
                float inner = lerp(1.0 - _Flow, 1.0 + _Flow, smoke);

                // Eroded into smoke where the vertex alpha falls: streaks of tall noise rising through the body's own frame, kept where they
                // are denser than a threshold that climbs as the alpha falls, so the surface frays into fewer, thinner tendrils and is gone
                // where the alpha is 0. What is left also thins with the alpha, so the tendrils fade as they fray.
                float torn = 1.0 - saturate(input.alpha);
                float3 streakAt = input.positionOS * StreakScale - float3(0.0, _Time.y * StreakRise, 0.0);
                float streak = Noise(streakAt) * 0.6 + Noise(streakAt * 2.1 + 3.7) * 0.4;
                float threshold = 0.15 + 0.7 * torn;
                float left = smoothstep(threshold - StreakEdge, threshold + StreakEdge, streak) * lerp(0.5, 1.0, input.alpha);
                left *= lerp(1.0, sin(input.across * 3.14159265), _EdgeFeather);

                float3 colour = lerp(_BaseColor.rgb, _RimColor.rgb, rim) * _Glow * inner;
                colour = lerp(colour, float3(1.6, 1.7, 1.8), _Flash);
                float alpha = lerp(_Alpha, _RimAlpha, rim) * inner * left * _Fade;
                alpha = saturate(alpha + _Flash * 0.3 * left * _Fade);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
