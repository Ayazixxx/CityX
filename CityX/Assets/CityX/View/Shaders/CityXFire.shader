// FIRE (docs/FIRE.md; owner, 2026-10-29: "right now it just looks like a bunch of triangles.. make it look like real life fire like gta or red
// dead"). Drawn on the fire's particles (FireLook), one material per layer, every pixel worked out each frame instead of read from a painted
// flipbook: the way the realtime-VFX crowd builds flames (an Unreal campfire, a Godot fire shader, Source's particle fires) -- a soft mask the
// shape of a flame, bent by one noise rising through it and eaten from its edges by a finer one (subtract the noise, then smoothstep, so the
// edges tear into licks instead of being cut), coloured by a black-body ramp: dark red where it is coolest, orange, yellow, white-hot in the core.
// Its values go past 1 so the game's bloom makes the core glow.
//
// The particle hands each quad its uv (xy), a random number of its own (z, StableRandom.x: no two flames burn alike) and its age 0..1 (w,
// AgePercent: a flame is born quickly, burns, and burns away from its tips). Vertex colour: rgb a tint, a the strength (the cell's heat).
//
// _Mode 0, flame: premultiplied, its light added to what is behind it and only part of that hidden (_Occlusion), so it glows at night and
// stays orange against a bright sky (an additive flame washed out pink there). _Mode 1, smoke: a billowing puff eaten at its edges by the
// same noise, lit orange by the fire under it while young, alpha-blended. Both fade where they meet the street or a wall (soft particles,
// from the depth texture) and close to the camera, and write no depth.
Shader "CityX/Fire"
{
    Properties
    {
        _NoiseTex("Noise (tileable: R fine, G coarse)", 2D) = "gray" {}
        _Mode("Mode (0 flame, 1 smoke)", Float) = 0
        _Brightness("Brightness", Float) = 2.0
        _Occlusion("How much a flame hides what is behind it", Range(0, 1)) = 0.6
        _Rise("How fast the flame's noise rises", Float) = 1.5
        _SoftFade("Soft fade where it meets something (m)", Float) = 0.4
        _NearFade("Fade close to the camera (m)", Float) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Fire"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _NoiseTex_ST;
            float _Mode;
            float _Brightness;
            float _Occlusion;
            float _Rise;
            float _SoftFade;
            float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -positions.positionVS.z;
                return output;
            }

            float Fine(float2 at)
            {
                return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, at).r;
            }

            float Coarse(float2 at)
            {
                return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, at).g;
            }

            // Where the quad meets the street or a wall it fades out (soft particles); within _NearFade of the camera too, so a flame in your
            // face is not a wall of colour.
            float Fades(Varyings input)
            {
                float2 screen = input.screenPos.xy / input.screenPos.w;
                float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                float soft = saturate((scene - input.eyeDepth) / _SoftFade);
                float near = saturate((input.eyeDepth - _ProjectionParams.y) / _NearFade);
                return soft * near;
            }

            half4 Flame(Varyings input)
            {
                float2 uv = input.uv.xy;
                float2 seed = float2(input.uv.z * 13.17, input.uv.z * 5.73);
                float age = input.uv.w;
                float t = _Time.y * _Rise;

                // Two noises rising through the flame: the coarse one bends it and splits it into tongues, the fine one tears its edges.
                float coarse = Coarse(float2(uv.x * 0.8, uv.y * 0.6 - t * 0.45) + seed);
                float bend = (coarse - 0.5) * 0.55 * uv.y;
                float fine = Fine(float2(uv.x * 1.7 + bend, uv.y * 1.4 - t) + seed.yx);

                // The body: a soft teardrop standing on the quad's bottom middle, narrowing upward, its centre line swaying with the coarse noise.
                float2 p = float2((uv.x - 0.5 - bend) * (2.1 + 1.8 * uv.y), uv.y * 0.95 + 0.05);
                float body = 1.0 - length(p);

                // Eaten by the noise, more toward the top, and burning away from the tips at the end of its life. Faded to nothing at the
                // quad's own edges (a flame cut flat by its quad's bottom showed as a straight line across the fire).
                float heat = body * 1.4 - fine * (0.28 + 0.6 * uv.y) - (1.0 - coarse) * 0.22 * uv.y - age * age * 0.5;
                heat *= saturate(age * 10.0);
                heat *= input.color.a;
                heat *= smoothstep(0.0, 0.2, uv.y) * smoothstep(0.0, 0.1, 1.0 - uv.y) * smoothstep(0.0, 0.08, uv.x) * smoothstep(0.0, 0.08, 1.0 - uv.x);
                float c = saturate(heat * 1.8);

                // Black body: red falls off slowest, green faster, blue fastest -- dark red, orange, yellow, white-hot.
                float3 colour = float3(1.5 * c, 1.5 * c * c * c, c * c * c * c * c * c) * _Brightness * input.color.rgb;
                float fade = Fades(input);
                float alpha = smoothstep(0.0, 0.4, c) * _Occlusion * fade;
                return half4(colour * fade, alpha);
            }

            half4 Smoke(Varyings input)
            {
                float2 uv = input.uv.xy;
                float2 seed = float2(input.uv.z * 11.31, input.uv.z * 3.97);
                float age = input.uv.w;
                float t = _Time.y * 0.12;

                // A round puff whose edge billows: the noise eats it from outside in, and it thins as it ages.
                float billow = Coarse(uv * 0.75 + seed - float2(0.0, t)) * 0.6 + Fine(uv * 1.5 + seed.yx - float2(0.0, t * 1.7)) * 0.4;
                float r = length(uv - 0.5) * 2.0;
                float puff = saturate(1.0 - r);
                float density = saturate(puff * 1.9 - (1.0 - billow) * 1.15);
                density *= smoothstep(0.0, 0.12, age) * (1.0 - smoothstep(0.45, 1.0, age));

                // Lit orange by the fire under it while young, cooling to its own grey as it rises.
                float3 colour = lerp(float3(0.45, 0.2, 0.08), input.color.rgb, saturate(age * 3.0));
                float alpha = density * input.color.a * Fades(input);
                return half4(colour * alpha, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Mode < 0.5 ? Flame(input) : Smoke(input);
            }
            ENDHLSL
        }
    }
}
