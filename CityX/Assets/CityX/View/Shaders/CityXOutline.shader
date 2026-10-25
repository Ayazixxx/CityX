// THE THICK BLACK LINES of the cel look (owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines",
// "players and buildings and terrain, everything"). A full-screen pass (URP Full Screen Pass renderer feature, put on by OutlineSetup)
// after the opaque world is drawn and before the glass and the air (CityXHaze), finding edges in the depth and the normals the frame already
// has (the screen-space ambient occlusion makes the depth-normals prepass). Edge detection over the whole picture, not a shell drawn round
// each model: it needs nothing per model and holds at a building's sharp corners, where a shell splits (Ameye, "5 ways to draw an outline").
//   An OUTLINE where the depth jumps: something in front of something further off (a body against the street, a roof against the sky).
//     Measured on the inverse of the depth, which on any flat surface changes evenly across the screen (it is linear in screen space on a
//     plane), so a street seen end on or a wall seen edge on never lights up, only a real step; and relative to the depth there, so it is
//     the same near and far.
//   A FOLD where the surface turns sharply (a building's corner, a kerb, a step, a window's reveal): the normals on either side differ by
//     more than the fold threshold. Gentle turns (a body's curve, a hill) stay clean.
// A line is as wide on screen near as far, and on any screen (its width is given for a picture 1080 pixels tall and scaled to this one); the
// lines fade away into the distance (_CityXOutlineFade), so the far city is not a black mesh. Set by CelShading.cs; with CITYX_TOON off, or a
// strength of 0, the picture is handed back untouched.
Shader "CityX/Outline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "CityXOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ CITYX_TOON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CityXOutline;        // x: a line's width in pixels on a picture 1080 tall, y: the outline's depth step, z: the fold's
                                         // threshold (1 - the cosine of the angle), w: strength (0 = no lines)
            float4 _CityXOutlineFade;    // x: the distance (m) the lines start to fade, y: the distance they are gone
            float4 _CityXOutlineColor;   // rgb: the lines' colour

            static const float ReferenceHeight = 1080.0;

            // Nothing drawn there: the sky.
            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-7;
            #else
                return rawDepth >= 1.0 - 1e-7;
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
            #if !defined(CITYX_TOON)
                return scene;
            #else
                if (_CityXOutline.w <= 0.0)
                {
                    return scene;
                }
                float width = max(1.0, _CityXOutline.x * _ScreenSize.y / ReferenceHeight);
                float2 offsets[4] = { float2(-width, 0.0), float2(width, 0.0), float2(0.0, -width), float2(0.0, width) };

                float rawC = SampleSceneDepth(uv);
                float q[4];
                float3 n[4];
                bool sky = IsSky(rawC);
                float qC = 1.0 / LinearEyeDepth(rawC, _ZBufferParams);
                float nearest = qC;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 at = uv + offsets[i] * _ScreenSize.zw;
                    float raw = SampleSceneDepth(at);
                    sky = sky || IsSky(raw);
                    q[i] = 1.0 / LinearEyeDepth(raw, _ZBufferParams);
                    n[i] = SampleSceneNormals(at);
                    nearest = max(nearest, q[i]);
                }

                // The outline: how far the inverse depth bends from a straight line across this pixel, against the inverse depth here.
                float bend = max(abs(q[0] + q[1] - 2.0 * qC), abs(q[2] + q[3] - 2.0 * qC)) / max(qC, 1e-6);
                float outline = saturate((bend - _CityXOutline.y) / _CityXOutline.y);

                // The fold: how far the normals either side turn from this one (nothing to fold against at the sky's edge: the outline is there).
                float fold = 0.0;
                if (!sky)
                {
                    float3 nC = SampleSceneNormals(uv);
                    [unroll] for (int j = 0; j < 4; j++)
                    {
                        fold = max(fold, 1.0 - dot(nC, n[j]));
                    }
                    fold = saturate((fold - _CityXOutline.z) / (_CityXOutline.z * 0.5));
                }

                float nearestMetres = 1.0 / max(nearest, 1e-6);
                float fade = 1.0 - smoothstep(_CityXOutlineFade.x, _CityXOutlineFade.y, nearestMetres);
                float ink = max(outline, fold) * fade * saturate(_CityXOutline.w);
                return half4(lerp(scene.rgb, _CityXOutlineColor.rgb, ink), scene.a);
            #endif
            }
            ENDHLSL
        }
    }
}
