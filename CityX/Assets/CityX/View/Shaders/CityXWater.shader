// WATER (docs/WATER.md): one surface shader, the way stylised games draw their water now -- no simulation, everything from the depth and
// colour URP already keeps of the scene behind:
//   what is under the water seen through it, bent a little by the ripples (the opaque texture, sampled off the ripple normal; a sample
//     that lands on something in front of the water is not used, so nothing above it smears in);
//   the floor tinted and then hidden with depth (the depth texture: how far the floor is below the surface);
//   bright veins of light on the floor in shallow water (caustics, two drifting noises crossing), cut hard as the cel look is;
//   the sky in the water at grazing angles (Fresnel, the ambient light the way the surface faces);
//   one hard highlight where the sun or the moon glints off a ripple, and nothing where the surface is in shadow (a sewer's water has none);
//   a foam line where the water meets anything standing in it -- a wall, a leg, a step -- broken and moving;
//   ripples moving the way the water flows (_Flow), the cheap form of Valve's flow-mapped swamps and sewers.
// Lit by the light falling on it (the sun's or the moon's colour and the sky's), so by night it is as dark as the night is.
// Drawn just before the other transparent things, after the cel look's black lines; it writes no depth. Its underside, seen from below the
// surface, is the deep colour over what is above.
Shader "CityX/Water"
{
    Properties
    {
        _ShallowTint("Shallow tint (multiplies what is under the water)", Color) = (0.72, 0.86, 0.88, 1)
        _DeepColor("Deep colour", Color) = (0.06, 0.2, 0.24, 1)
        _DeepAt("Hidden at depth (m)", Float) = 1.6
        _FoamColor("Foam", Color) = (0.92, 0.96, 0.97, 1)
        _FoamWidth("Foam line width (m)", Float) = 0.12
        _Refraction("Refraction", Range(0.0, 0.1)) = 0.025
        _RippleScale("Ripple size (1/m)", Float) = 1.3
        _RippleStrength("Ripple strength", Range(0.0, 1.0)) = 0.45
        _Flow("Flow (x, z) m/s", Vector) = (0.0, 0.0, 0.0, 0.0)
        _Drift("Still water's drift (m/s)", Float) = 0.12
        _Caustics("Caustics", Range(0.0, 2.0)) = 0.6
        _Reflection("Sky reflection", Range(0.0, 1.0)) = 0.55
        _Highlight("Highlight", Range(0.0, 3.0)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-1"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _ShallowTint;
            float4 _DeepColor;
            float _DeepAt;
            float4 _FoamColor;
            float _FoamWidth;
            float _Refraction;
            float _RippleScale;
            float _RippleStrength;
            float4 _Flow;
            float _Drift;
            float _Caustics;
            float _Reflection;
            float _Highlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            // Smooth 2D noise in [0, 1] (a hash per lattice point, blended): cheap, a handful of these per pixel of water.
            float Hash(float2 p)
            {
                p = frac(p * float2(0.3183099, 0.3678794) + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * (p.x + p.y));
            }

            float Noise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // The ripples' height at a point: two layers of noise drifting with the flow (and slowly across each other when the water is still).
            float Ripples(float2 p, float2 flow, float t)
            {
                float2 drift = flow + float2(_Drift, _Drift * 0.6);
                float a = Noise(p * _RippleScale - drift * t * _RippleScale);
                float b = Noise(p * _RippleScale * 2.3 + float2(5.2, 1.3) - (flow - float2(_Drift * 0.7, -_Drift)) * t * _RippleScale * 1.6);
                return a * 0.65 + b * 0.35;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -positions.positionVS.z;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float t = _Time.y;
                float2 flow = _Flow.xz;

                // The ripple normal, from the slope of the ripple height (two small steps across it).
                const float delta = 0.08;
                float2 p = input.positionWS.xz;
                float h = Ripples(p, flow, t);
                float hx = Ripples(p + float2(delta, 0.0), flow, t);
                float hz = Ripples(p + float2(0.0, delta), flow, t);
                float3 normal = normalize(float3((h - hx) * _RippleStrength / delta * 0.25, 1.0, (h - hz) * _RippleStrength / delta * 0.25));

                float3 toCamera = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float sunUp = saturate(sun.direction.y * 1.5 + 0.2);
                float3 skyLight = SampleSH(float3(0.0, 1.0, 0.0));
                float3 light = sun.color * sun.shadowAttenuation * sunUp * 0.6 + skyLight;

                // How much water lies between the surface and what is behind it: along the view (for the foam) and straight down (for the colour).
                float sceneRaw = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float thickness = max(0.0, sceneEye - input.eyeDepth);

                // Bent by the ripples, unless the bent sample lands on something in front of the water.
                float2 bent = screenUV + normal.xz * _Refraction * saturate(thickness * 2.0);
                float bentRaw = SampleSceneDepth(bent);
                if (LinearEyeDepth(bentRaw, _ZBufferParams) < input.eyeDepth)
                {
                    bent = screenUV;
                    bentRaw = sceneRaw;
                }
                float3 behind = SampleSceneColor(bent);
                float3 floorWS = ComputeWorldSpacePosition(bent, bentRaw, UNITY_MATRIX_I_VP);
                float depth = max(0.0, input.positionWS.y - floorWS.y);

                if (!frontFace)
                {
                    // From under the surface: the deep colour over what is above.
                    float3 under = lerp(behind * _ShallowTint.rgb, _DeepColor.rgb * light, 0.55);
                    return half4(MixFog(under, input.fogFactor), 1.0);
                }

                // Caustics on the floor in shallow water: where two drifting noises cross, a thin bright vein; strongest in the shallows, in the sun.
                float2 floorAt = floorWS.xz * 3.2;
                float c1 = Noise(floorAt + float2(t * 0.31, t * 0.17));
                float c2 = Noise(floorAt * 1.3 - float2(t * 0.23, -t * 0.29) + 3.7);
                float vein = smoothstep(0.92, 0.97, 1.0 - abs(c1 - c2));
                float caustics = vein * _Caustics * saturate(1.0 - depth / max(0.01, _DeepAt)) * sun.shadowAttenuation * sunUp;
                // Only on what faces up (a floor, a step): on a leg or a wall the floor's pattern would run down it in stripes.
                float3 underNormal = normalize(cross(ddy(floorWS), ddx(floorWS)));
                caustics *= saturate((abs(underNormal.y) - 0.6) * 3.0);
                behind += sun.color * caustics * 0.2;

                // Tinted and then hidden with depth.
                float hidden = saturate(depth / max(0.01, _DeepAt));
                hidden = hidden * (2.0 - hidden);
                float3 colour = lerp(behind * _ShallowTint.rgb, _DeepColor.rgb * light, hidden);

                // The sky in it at grazing angles.
                float fresnel = pow(1.0 - saturate(dot(normal, toCamera)), 4.0) * _Reflection;
                float3 sky = SampleSH(reflect(-toCamera, normal));
                colour = lerp(colour, sky, fresnel);

                // One hard glint of the sun or the moon off a ripple.
                float glint = pow(saturate(dot(reflect(-sun.direction, normal), toCamera)), 90.0);
                colour += sun.color * sun.shadowAttenuation * step(0.35, glint) * _Highlight * sunUp;

                // The foam line where the water meets something standing in it, broken and moving.
                float foamNoise = Noise(p * 3.5 + flow * t * 3.5 + float2(t * 0.4, 0.0));
                float foam = step(max(thickness, depth), _FoamWidth * (0.45 + 0.9 * foamNoise));   // only where something breaks the surface, not under it
                colour = lerp(colour, _FoamColor.rgb * light, foam);

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
