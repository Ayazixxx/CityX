// ICE THAT SEEMS TO HOLD LIGHT (Glacier Spikes, docs/GLACIER_SPIKES.md): a port of the IceMaterial in achrefelouafi's
// LinearAbiltyCastingThreeJS (MIT). Opaque, so it stands in the world and the cel look's black lines trace it, but faked to read as
// translucent: a deep tint where a face looks straight at the eye and the pale ice colour where it turns away, a bright rim at the edges,
// cracks and veins of light inside, a frost line at the foot, glints that come and go, and a glow from within for a moment after each
// crystal breaks the ground.
//
// Lit as every surface is (CityXLitFragment: the cel light, shadows, lamps, the dark under the ground); the glow is its emission.
//
// Drawn instanced (GlacierSpikesField): each crystal's own variety comes from where it stands. The field's line and clock (_IceStart,
// _IceDirection, _IceClock: age, front speed, length, glow seconds) tell each crystal when the front reached it, which is when it glows.
// The depth-normals pass writes the crystal's smooth normal (uv1, IceCrystalMesh) so the black lines draw its outline, not every facet.
Shader "CityX/Ice"
{
    Properties
    {
        _DeepColor("Deep", Color) = (0.243, 0.451, 0.478, 1)
        _IceColor("Ice", Color) = (0.541, 0.855, 1.0, 1)
        _RimColor("Rim", Color) = (0.949, 0.996, 1.0, 1)
        _CoreColor("Core", Color) = (0.388, 0.529, 0.592, 1)
        _Density("Depth tint", Float) = 1.15
        _FresnelPower("Fresnel power", Float) = 2.4
        _Fresnel("Fresnel", Float) = 2.3
        _Fracture("Cracks", Float) = 1.0
        _FractureScale("Crack scale (per metre)", Float) = 4.6
        _Veins("Veins", Float) = 0.6
        _VeinScale("Vein scale", Float) = 3.2
        _Sparkle("Glints", Float) = 1.0
        _SparkleSpeed("Glint speed", Float) = 0.7
        _FrostLine("Frost line", Float) = 0.5
        _FacetSharp("Facet contrast", Float) = 0.68
        _EdgeGlow("Rim glow", Float) = 2.0
        _Translucency("Inner glow", Float) = 1.5
        _BodyGlow("Light through the thin faces", Float) = 0.35
        _BirthGlow("Birth glow", Float) = 3.2
        _Glow("Glow", Float) = 1.35
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.84
        _IceStart("Line start", Vector) = (0, 0, 0, 0)
        _IceDirection("Line direction", Vector) = (0, 0, 1, 0)
        _IceClock("Age, speed, length, glow seconds", Vector) = (100, 36, 21, 0.45)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _DeepColor;
        half4 _IceColor;
        half4 _RimColor;
        half4 _CoreColor;
        float _Density;
        float _FresnelPower;
        float _Fresnel;
        float _Fracture;
        float _FractureScale;
        float _Veins;
        float _VeinScale;
        float _Sparkle;
        float _SparkleSpeed;
        float _FrostLine;
        float _FacetSharp;
        float _EdgeGlow;
        float _Translucency;
        float _BodyGlow;
        float _BirthGlow;
        float _Glow;
        float _Smoothness;
        float4 _IceStart;
        float4 _IceDirection;
        float4 _IceClock;
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
            #pragma vertex IceVertex
            #pragma fragment IceFragment

            #pragma multi_compile_fragment _ CITYX_TOON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CityXToonLighting.hlsl"

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
                float3 positionOS : TEXCOORD2;
                float2 seedAndBirth : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            // Value noise: a hash per lattice point, blended (the ghost's).
            float IceHash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.17, 0.23));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float IceNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(IceHash(i + float3(0, 0, 0)), IceHash(i + float3(1, 0, 0)), f.x),
                                 lerp(IceHash(i + float3(0, 1, 0)), IceHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(IceHash(i + float3(0, 0, 1)), IceHash(i + float3(1, 0, 1)), f.x),
                                 lerp(IceHash(i + float3(0, 1, 1)), IceHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Three octaves, -1 to 1.
            float IceFbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    sum += (IceNoise(p) * 2.0 - 1.0) * amplitude;
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return sum / 0.875;
            }

            // Ridged noise, 0 to 1: thin bright lines where the noise crosses its middle, the look of cracks.
            float IceRidged(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                float weight = 0.0;
                for (int i = 0; i < 4; i++)
                {
                    float n = 1.0 - abs(IceNoise(p) * 2.0 - 1.0);
                    sum += n * n * amplitude;
                    weight += amplitude;
                    p = p * 2.1 + 31.7;
                    amplitude *= 0.5;
                }
                return sum / weight;
            }

            Varyings IceVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                float3 base = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                // Where the crystal stands decides its own variety, and how far down the line it is, when the front reached it.
                float seed = frac(sin(dot(base.xz, float2(12.9898, 78.233))) * 43758.5453);
                float along = clamp(dot(base - _IceStart.xyz, _IceDirection.xyz), 0.0, _IceClock.z);
                float arrived = along / max(_IceClock.y, 0.01);
                float birth = saturate(1.0 - (_IceClock.x - arrived) / max(_IceClock.w, 0.02));
                output.seedAndBirth = float2(seed, birth);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 IceFragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 toEye = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float seed = input.seedAndBirth.x;
                float birth = input.seedAndBirth.y;
                float facing = saturate(dot(normal, toEye));
                float thickness = saturate(facing * _Density);
                float rim = pow(1.0 - facing, _FresnelPower);
                float fresnel = rim * _Fresnel;

                float cracks = smoothstep(0.55, 0.98, IceRidged(input.positionWS * _FractureScale + seed * 37.0));
                float veins = smoothstep(0.45, 0.92, IceFbm(input.positionOS * _VeinScale * 4.0 + seed * 11.0) * 0.5 + 0.5);
                float rime = (1.0 - smoothstep(0.0, 0.55, input.positionOS.y)) * (0.5 + 0.5 * IceFbm(input.positionOS * 9.0 + seed * 5.0));

                half3 body = lerp(_IceColor.rgb, _DeepColor.rgb, thickness);
                body = lerp(body, _RimColor.rgb, veins * _Veins * 0.55);
                body = lerp(body, _RimColor.rgb, cracks * _Fracture * 0.4);
                body = lerp(body, _RimColor.rgb, saturate(rime) * _FrostLine);
                body *= lerp(1.0, 0.55 + 0.9 * facing, _FacetSharp);

                float glint = IceNoise(input.positionWS * 24.0 + float3(0.0, _Time.y * _SparkleSpeed, 0.0) + seed * 23.0) * 2.0 - 1.0;
                glint = pow(saturate(glint), 14.0) * smoothstep(0.0, 0.7, fresnel + 0.3);

                half3 glow = _RimColor.rgb * rim * _EdgeGlow;
                glow += _CoreColor.rgb * (cracks * _Fracture * 0.8 + veins * _Veins * 0.35) * _Translucency;
                // The cel light flattens the reference's environment reflections, which is where its cyan came from: the thin, turned-away
                // faces give back a little of the ice's own colour instead, as light passing through would.
                glow += _IceColor.rgb * (1.0 - thickness) * _BodyGlow;
                glow += _RimColor.rgb * glint * _Sparkle * 1.5;
                glow += _CoreColor.rgb * birth * _BirthGlow;
                glow *= _Glow;
                glow /= 1.0 + glow * 0.22;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = toEye;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = body;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = glow;
                surfaceData.alpha = 1.0;

                half4 colour = CityXLitFragment(inputData, surfaceData);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1.0;
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex IceShadowVertex
            #pragma fragment IceShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 IceShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 IceShadowFragment() : SV_Target
            {
                return 0;
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
            #pragma vertex IceDepthVertex
            #pragma fragment IceDepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 IceDepthVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half IceDepthFragment(float4 positionCS : SV_POSITION) : SV_Target
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
            #pragma vertex IceNormalsVertex
            #pragma fragment IceNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 smoothNormalOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings IceNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.smoothNormalOS);
                return output;
            }

            void IceNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
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
