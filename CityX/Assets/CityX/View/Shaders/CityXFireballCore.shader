// THE FIREBALL'S CORE (docs/FIRE.md, "The fire spells"): a ball of molten fire, ported from the meteor of achrefelouafi's
// LinearAbiltyCastingThreeJS (MIT; its MeteorMaterial). Lava seams are the zero crossings of a noise field (thin, forking, never a scratch):
// the open seam is hot, the middle of it hotter, a charred lip either side of it; between them a red crust, mottled. The field is read in the
// ball's own space, so the seams turn with it as it tumbles. _Charge runs 0 to 1 as it flies: the seams prise open and brighten and the face
// it flies into and its rim heat up, so it arrives hotter than it left. Coloured in the fire spells' bold heat bands (CityXFireBands.hlsl),
// unlit: it is a light, not a surface. Opaque, with depth and normals, so the game's black cel line traces it like any solid thing.
Shader "CityX/FireballCore"
{
    Properties
    {
        _Charge("How hot it has grown (0 cast, 1 hot)", Range(0, 1)) = 0
        _Seed("Seed (its own seams)", Float) = 0
        _Heading("The way it flies (world)", Vector) = (0, 0, 1, 0)
        _CrackScale("Seams across its radius", Float) = 1.6
        _CrackWidth("A seam's width", Float) = 0.07
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _Charge;
        float _Seed;
        float4 _Heading;
        float _CrackScale;
        float _CrackWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "CityXFireBands.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionOS * 2.0;   // the unit ball (Unity's sphere is half a unit round)
                float3 field = p * _CrackScale + _Seed * 19.0;

                // The seams, as a cross-section: how far from the nearest zero crossing of the field (a second, finer one for the twigs off
                // the main seams), cut into the open seam, the white middle of it and the charred lip round it.
                float width = max(0.004, _CrackWidth * (1.0 + _Charge * 0.8));
                float distance = min(abs(FireFbm(field)), abs(FireFbm(field * 2.7 + 11.3)) / 0.65);
                float seam = 1.0 - smoothstep(width * 0.35, width, distance);
                float lip = 1.0 - smoothstep(width, width * 2.2, distance);
                float middle = 1.0 - smoothstep(0.0, width * 0.45, distance);

                // Molten rock is not still: brightness crawls along the inside of a seam.
                float flow = 0.6 + 0.55 * FireValueNoise(p * 4.0 + float3(0.0, _Time.y * 0.9, 0.0) + _Seed * 7.0);
                float mottle = FireFbm(p * 3.4 + _Seed * 31.0) * 0.5 + 0.5;

                float3 normal = normalize(input.normalWS);
                float facing = saturate(dot(normal, normalize(GetWorldSpaceViewDir(input.positionWS))));
                float rim = pow(1.0 - facing, 2.2);
                float lead = pow(saturate(dot(normal, normalize(_Heading.xyz))), 2.6);
                float charge2 = _Charge * _Charge;

                float crust = (lerp(0.26, 0.4, mottle) + 0.2 * _Charge) * (1.0 - lip * 0.6);
                float heat = lerp(crust, max(crust, (0.6 + 0.4 * middle) * flow), seam);
                heat += rim * (0.22 + 0.35 * charge2) + lead * 0.5 * charge2;
                return half4(FireBands(saturate(heat)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void NormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(remappedOctNormalWS), 0.0);
            #else
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
