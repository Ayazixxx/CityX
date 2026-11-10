// What the wet streets' mirror remembers of a frame besides its picture (WetMirrorFeature, CityXWetMirror.hlsl): how far from the eye each
// pixel is, in metres, at half size, and negative where the surface there faces up. One pixel of the four is taken, never their average: the
// average of a lamp post and the wall behind it is a distance where nothing is.
Shader "CityX/MirrorMemory"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Distance"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Distance

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // A surface this much upward (the cosine of its slope: 45 degrees) is ground, or the top of something.
            #define CITYX_MIRROR_FACES_UP 0.7

            float3 WorldAt(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
            #endif
                return ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
            }

            // Whether the surface at p, going off along a and b, faces up, seen from the eye.
            bool FacesUp(float3 p, float3 a, float3 b)
            {
                float3 facing = normalize(cross(a, b));
                return (dot(facing, _WorldSpaceCameraPos - p) < 0.0 ? -facing.y : facing.y) > CITYX_MIRROR_FACES_UP;
            }

            float Distance(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _CameraDepthTexture_TexelSize.xy;
                float3 p = WorldAt(uv);
                float3 right = WorldAt(uv + float2(texel.x, 0.0)) - p;
                float3 left = p - WorldAt(uv - float2(texel.x, 0.0));
                float3 up = WorldAt(uv + float2(0.0, texel.y)) - p;
                float3 down = p - WorldAt(uv - float2(0.0, texel.y));
                // Any of the four corners round the pixel will do: at the edge of a painted line or a paving slab the pixel next door is a
                // few millimetres off, and one pair of neighbours across that step reads as a wall a pixel wide.
                bool ground = FacesUp(p, right, up) || FacesUp(p, right, down) || FacesUp(p, left, up) || FacesUp(p, left, down);
                float distance = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                return ground ? -distance : distance;
            }
            ENDHLSL
        }
    }
}
