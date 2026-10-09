// The city's air: exponential height fog with sun in-scattering, as a full-screen pass (URP Full Screen Pass Renderer Feature, before
// post-processing). It is what makes distance read as distance -- the near street sharp, the towers a block away softer and paler, the far
// skyline melting into the sky -- and it hides the edge of the streamed city, which the old flat linear fog did with a grey wall.
//
// Density at height y: Density * exp(-Falloff * (y - BaseY)). Along a view ray from the camera the optical depth has a closed form
// (Wenzel, "Real-time atmospheric effects in games", 2006; Unreal's exponential height fog), so no ray marching:
//   tau = Density * exp(-Falloff * (camY - BaseY)) * (1 - exp(-Falloff * dirY * L)) / (Falloff * dirY)
// and the colour is scene * exp(-tau) + inscatter * (1 - exp(-tau)), the in-scattered light being the haze colour, brightened toward the sun.
// Sky pixels (no depth) are hazed only in a band at and below the horizon, so the sky overhead keeps the dome's own colours.
//
// Every parameter is a global set by DayNightCycle; a Density of 0 (any scene that never sets them, like the title screen) leaves the image alone.
Shader "CityX/Haze"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "CityXHaze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CityXHazeColor;       // rgb: the haze lit by the sky (the horizon colour)
            float4 _CityXHazeSunColor;    // rgb: the haze looking toward the sun
            float4 _CityXHazeSunDir;      // xyz: direction TO the sun (unit)
            float4 _CityXHazeParams;      // x: density at BaseY (per metre), y: falloff (per metre), z: BaseY, w: max haze (0..1)
            float4 _CityXHazeEdge;        // x: distance the streamed-city edge fade starts, y: where it is total (metres); 0 = none; z, w: the clear bubble round the camera (haze starts at z, full at w)
            float4 _CityXHazeSkyTop;      // rgb: the sky dome's zenith colour
            float4 _CityXHazeSky;         // x: the dome's gradient curve (SkyCycle.SkyGradientCurve), y: the dome's energy

            // How high above the horizon (as the sine of the elevation, about 10 degrees) the haze still tints the sky.
            static const float SkyBand = 0.17;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float density = _CityXHazeParams.x;
                if (density <= 0.0)
                {
                    return scene;
                }
                float falloff = max(_CityXHazeParams.y, 1e-5);
                float baseY = _CityXHazeParams.z;
                float maxHaze = _CityXHazeParams.w;

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = rawDepth <= 0.000001;
                #else
                    bool sky = rawDepth >= 0.999999;
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, sky ? (UNITY_REVERSED_Z ? 0.000001 : 0.999999) : rawDepth, UNITY_MATRIX_I_VP);
                float3 toPoint = world - _WorldSpaceCameraPos;
                float length_ = length(toPoint);
                float3 dir = toPoint / max(length_, 1e-4);

                float camHeight = _WorldSpaceCameraPos.y - baseY;
                float atCam = density * exp(-falloff * camHeight);
                float k = falloff * dir.y;
                float haze;
                if (sky)
                {
                    // The sky dome already fades to the haze colour at the horizon; the air only has to close the band at and below it, where the
                    // dome's ground half would otherwise show past the edge of the streamed city (looking out from a roof or a jetpack).
                    haze = 1.0 - smoothstep(0.0, SkyBand, dir.y);
                }
                else
                {
                    float tau;
                    if (abs(k * length_) < 1e-3)
                    {
                        tau = atCam * length_;
                    }
                    else
                    {
                        tau = atCam * (1.0 - exp(-k * length_)) / k;
                    }
                    haze = min(1.0 - exp(-tau), maxHaze);

                    // A clear bubble round the camera: no haze at all near the player, fading in gently out to _CityXHazeEdge.w.
                    if (_CityXHazeEdge.w > 0.0)
                    {
                        haze *= smoothstep(_CityXHazeEdge.z, _CityXHazeEdge.w, length_);
                    }

                    // The streamed city stops at a hard edge; whatever the air does, everything past the fade is haze.
                    if (_CityXHazeEdge.y > 0.0)
                    {
                        haze = max(haze, smoothstep(_CityXHazeEdge.x, _CityXHazeEdge.y, length_));
                    }
                }

                // In-scattering: the colour of the sky in the direction looked (the dome's own gradient, so a far tower fades into exactly the sky
                // behind it rather than into a pale wall), brighter and warmer toward the sun (a broad glow and a tighter one).
                float energy = _CityXHazeSky.y > 0.0 ? _CityXHazeSky.y : 1.0;
                float3 skyColour = _CityXHazeColor.rgb;
                if (dir.y > 0.0 && _CityXHazeSky.x > 0.0)
                {
                    float above = 1.0 - acos(saturate(dir.y)) / (PI * 0.5);
                    float skyBlend = saturate(1.0 - pow(max(1.0 - above, 0.0), 1.0 / _CityXHazeSky.x));
                    skyColour = lerp(_CityXHazeColor.rgb, _CityXHazeSkyTop.rgb, skyBlend);
                }
                skyColour *= energy;
                float sunDot = saturate(dot(dir, _CityXHazeSunDir.xyz));
                float glow = pow(sunDot, 6.0) * 0.6 + pow(sunDot, 48.0) * 0.4;
                float3 inscatter = skyColour + (_CityXHazeSunColor.rgb - _CityXHazeColor.rgb) * glow;

                return half4(lerp(scene.rgb, inscatter, haze), scene.a);
            }
            ENDHLSL
        }
    }
}
