// HAIR UNDER WHAT IS SLUNG ON THE BACK (owner, 2026-11-12: "the hairstyles clip through the boomerang when its worn on the back ... is there a way to make
// any amount of hair that passes through it invisible on the other side"). A material with _BACKCLIP (the hair's) does not draw what of it is past the
// inner face of a board slung on a back and inside its outline: the hair seems to tuck under it. Up to CITYX_BACK_CLIP_MAX boards, set each frame as
// globals by Items/BackClip (a board's frame: x across it, y out of its face away from the back, z along it; its outline a V of two arms in x/z).
// The fragment's world position is worked out from its own screen position and depth, so any pass can ask, whatever its Varyings carry.
#ifndef CITYX_BACK_CLIP_INCLUDED
#define CITYX_BACK_CLIP_INCLUDED

#define CITYX_BACK_CLIP_MAX 8
// How far past a board's side edges, metres, the hair coming out of its face side is still cut: tips poking out right at its thin edge.
#define EdgeReach 0.015
// How far past its side edges hair on its outer side (away from the head: nothing of the scalp behind it) is still cut, metres: strand tips just past
// the edge showed as dots over its face from an angle.
#define FaceReach 0.04

float4x4 _BackClipToBoard[CITYX_BACK_CLIP_MAX];
float _BackClipCount;
// Each board's outline, a V in its own frame: one tip (x, z) and the elbow (x, z); the other tip (x, z), each arm's half width, and how far behind the
// board's middle (y) its inner face is.
float4 _BackClipVeeA[CITYX_BACK_CLIP_MAX];
float4 _BackClipVeeB[CITYX_BACK_CLIP_MAX];
// Each arm's half width at the tips (it narrows from the elbow's), and how far beyond its outer face the hair is still tucked under it: only hair right
// at the board, not hair seen past its edge from an angle, nor the back of a head beside its upper tip.
float4 _BackClipEdge[CITYX_BACK_CLIP_MAX];

// How far inside an arm from tip to elbow a point is (positive inside), the arm narrowing from its elbow's half width to its tip's; and how far across the
// arm it is, 0 on its middle line to 1 at its edge.
float CityXBackClipInArm(float2 p, float2 tip, float2 elbow, float elbowHalf, float tipHalf, out float across)
{
    float2 ab = elbow - tip;
    float t = saturate(dot(p - tip, ab) / max(dot(ab, ab), 1e-6));
    float width = lerp(tipHalf, elbowHalf, t);
    float distance = length(p - (tip + ab * t));
    across = distance / max(width, 1e-4);
    return width - distance;
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
        float4 a = _BackClipVeeA[i], b = _BackClipVeeB[i], e = _BackClipEdge[i];
        // Only what comes out of its outer face, which follows the blade's own section (thickest down its middle, thinning to its edges, as
        // BoomerangBuilder lofts it): hair into it and under it is drawn, the board hiding it, so no scalp shows past its edge, and no tip pokes out of
        // its face, at its thin edges either (owner, 2026-11-12: "make the hair clipped some amount into the boomerang ... but no so far that i comes
        // out", then "these little bits").
        float acrossA, acrossB;
        float insideA = CityXBackClipInArm(p.xz, a.xy, a.zw, b.z, e.x, acrossA);
        float insideB = CityXBackClipInArm(p.xz, b.xy, a.zw, b.z, e.x, acrossB);
        float across = saturate(insideA > insideB ? acrossA : acrossB);
        float face = b.w * sqrt(saturate(1.0 - across * across)) * lerp(1.0, 0.55, across);
        float inside = max(insideA, insideB);
        bool outOfItsFace = inside > -FaceReach && p.y > face * 0.6 && p.y < b.w + e.y;
        // ...and tips poking out past its thin side edges, a strip along them through its thickness (too narrow to show the scalp under it).
        bool outOfItsEdge = inside > -EdgeReach && inside < EdgeReach && p.y > -b.w && p.y < b.w + e.y;
        if (outOfItsFace || outOfItsEdge)
        {
            clip(-1.0);
        }
    }
#endif
}

#endif
