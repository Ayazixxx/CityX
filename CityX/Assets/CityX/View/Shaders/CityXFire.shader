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
// same noise, lit orange by the fire under it while young, alpha-blended. _Mode 2, burning gas (the fire spells): a billowing puff of fire
// cooling as it ages, in the fire spells' bold heat bands (CityXFireBands.hlsl), its cold part smoke. All fade where they meet the street or a
// wall (soft particles, from the depth texture) and close to the camera, and write no depth.
Shader "CityX/Fire"
{
    Properties
    {
        _NoiseTex("Noise (tileable: R fine, G coarse)", 2D) = "gray" {}
        _Mode("Mode (0 flame, 1 smoke, 2 burning gas)", Float) = 0
        _Brightness("Brightness", Float) = 2.0
        _Occlusion("How much a flame hides what is behind it", Range(0, 1)) = 0.6
        _SmokeAlpha("How much burning gas's smoke hides", Range(0, 1)) = 0.45
        _SmokeTint("Burning gas's smoke", Color) = (0.24, 0.2, 0.18, 1)
        _Rise("How fast the flame's noise rises", Float) = 1.5
        _SoftFade("Soft fade where it meets something (m)", Float) = 0.4
        _NearFade("Fade close to the camera (m)", Float) = 0.8
        _HeadAtStart("Burning gas: its head at uv.x 0 (1) or 1 (0) of a stretched quad", Float) = 0
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
            #include "CityXFireBands.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _NoiseTex_ST;
            float _Mode;
            float _Brightness;
            float _Occlusion;
            float _SmokeAlpha;
            float4 _SmokeTint;
            float _Rise;
            float _SoftFade;
            float _NearFade;
            float _HeadAtStart;
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

                // Black body: red falls off slowest, green faster, blue fastest -- dark red, orange, yellow, a golden white-hot. Not a true
                // white: the tone mapping greyed it, and the hearts of the flames read as grey (2026-11-15, as the fire spells' bands).
                float3 colour = float3(1.5 * c, 1.3 * c * c * c, 0.45 * c * c * c * c * c * c) * _Brightness * input.color.rgb;
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

            // Burning gas: a TONGUE of fire, its quad stretched along its flight by the renderer (uv.x runs along it), a round head forward
            // and a tail licking back, torn ragged by the noise flowing back along it the way fire streams off anything moving. Drawn as round
            // puffs, a fireball's wake and a stream read as a string of beads with grey middles (2026-11-15: "it doesnt look good enough", the
            // owner's reference a torch whose flame is one body of curling tongues). The vertex colour's red is the share of a full life the
            // puff will live (a puff cut short by a wall dies hot, not cooled), its alpha the heat it is born with.
            half4 Gas(Varyings input)
            {
                float2 uv = input.uv.xy;
                float2 seed = float2(input.uv.z * 7.31, input.uv.z * 2.93);
                float age = input.uv.w;
                float life = age * max(input.color.r, 0.05);
                float t = _Time.y;

                // 0 at the tail, 1 at the head. The head leads where the puff flies, but a wake's trails it (vertex green 0): a wake's puffs
                // drift back off the ball, and their flames must stream back from it, not point ahead of it.
                float along = _HeadAtStart > 0.5 ? 1.0 - uv.x : uv.x;
                along = input.color.g < 0.5 ? 1.0 - along : along;
                float tail = 1.0 - along;
                float across = uv.y * 2.0 - 1.0;

                // The coarse noise flows back along the tongue, bending it and whipping its tail; the fine one tears its edge, more toward the
                // tail and as it ages.
                float coarse = Coarse(float2(along * 0.55 + t * 0.7, uv.y * 0.75) + seed);
                float fine = Fine(float2(along * 1.4 + t * 2.1, uv.y * 1.7) + seed.yx);
                across += (coarse - 0.5) * 1.2 * tail * tail;

                // Its outline: a round head (to 0.6 along), narrowing to a point at the tail.
                float head = (along - 0.6) / 0.4;
                float width = along > 0.6 ? sqrt(saturate(1.0 - head * head)) : pow(saturate(along / 0.6), 0.65);
                float edge = width * (0.72 + 0.5 * coarse) - fine * (0.1 + 0.55 * tail + 0.3 * life) - abs(across);
                float density = saturate(edge / 0.07);
                density *= smoothstep(0.0, 0.05, age) * (1.0 - smoothstep(0.7, 1.0, age));

                // Hottest in its head's middle while it is young; its rim and its tail cool first, then the whole of it, down to smoke.
                float inner = saturate(edge / max(width, 0.05));
                float heat = input.color.a * saturate(1.0 - life * 1.15) * (0.3 + 0.85 * inner) * (0.6 + 0.4 * along) + (coarse - 0.5) * 0.12;
                float burning = FireBurning(heat);
                float fade = Fades(input);
                float alpha = density * lerp(_SmokeAlpha, _Occlusion, burning) * fade;
                float3 colour = FireBands(heat) * burning * density * fade + _SmokeTint.rgb * alpha * (1.0 - burning);
                return half4(colour, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Mode < 0.5)
                {
                    return Flame(input);
                }
                return _Mode < 1.5 ? Smoke(input) : Gas(input);
            }
            ENDHLSL
        }
    }
}
