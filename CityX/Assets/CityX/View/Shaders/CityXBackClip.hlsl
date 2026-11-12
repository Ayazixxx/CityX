// HAIR UNDER WHAT IS SLUNG ON THE BACK (owner, 2026-11-12: "the hairstyles clip through the boomerang when its worn on the back ... is there a way to make
// any amount of hair that passes through it invisible on the other side"). A material with _BACKCLIP (the hair's) does not draw what of it is past the
// inner face of a board slung on a back and inside its outline: the hair seems to tuck under it. Up to CITYX_BACK_CLIP_MAX boards, set each frame as
// globals by Items/BackClip (a board's frame: x across it, y out of its face away from the back, z along it; its outline a V of two arms in x/z).
// The fragment's world position is worked out from its own screen position and depth, so any pass can ask, whatever its Varyings carry.
#ifndef CITYX_BACK_CLIP_INCLUDED
#define CITYX_BACK_CLIP_INCLUDED

#define CITYX_BACK_CLIP_MAX 8

float4x4 _BackClipToBoard[CITYX_BACK_CLIP_MAX];
float _BackClipCount;
// Each board's outline, a V in its own frame: one tip (x, z) and the elbow (x, z); the other tip (x, z), each arm's half width, and how far behind the
// board's middle (y) its inner face is.
float4 _BackClipVeeA[CITYX_BACK_CLIP_MAX];
float4 _BackClipVeeB[CITYX_BACK_CLIP_MAX];

float CityXBackClipToSegment(float2 p, float2 a, float2 b)
{
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
    return length(p - (a + ab * t));
}

void CityXBackClip(float4 positionCS)
{
#if defined(_BACKCLIP)
    int count = (int)_BackClipCount;
    if (count <= 0)
    {
        return;
    }
    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    float3 world = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);
    for (int i = 0; i < CITYX_BACK_CLIP_MAX; i++)
    {
        if (i >= count)
        {
            break;
        }
        float3 p = mul(_BackClipToBoard[i], float4(world, 1.0)).xyz;
        float4 a = _BackClipVeeA[i], b = _BackClipVeeB[i];
        if (p.y > -b.w)
        {
            float d = min(CityXBackClipToSegment(p.xz, a.xy, a.zw), CityXBackClipToSegment(p.xz, a.zw, b.xy));
            clip(d - b.z);
        }
    }
#endif
}

#endif
