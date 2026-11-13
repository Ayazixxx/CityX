// HAIR KEPT OUT OF THE BODY AND UNDER WHAT IS SLUNG ON THE BACK (owner, 2026-11-12: "is there a better fix for the shirt spikes stabbing through the shirt
// than them bending?", the hair poking out through the Giant Boomerang on the back). A material with _CITYX_HAIR (the hair's) has its vertices pushed, on
// the graphics card every frame, in every pass that draws it (colour, depth, depth-normals for the outlines, shadows), so it looks the same everywhere:
//   * out of the upper body's own pieces (the torso, the shoulder bar, the neck column, each arm to its elbow: FusedBodyShape.UpperBody), laid just
//     over them, under the shirt, so no lock goes through the shirt whatever the head does (Character/Body/HairPush sets them, the nearest bodies' only);
//   * under a board slung on a back (Items/BackClip): what of it is at or past the board's inner face inside its outline (and a little past its edges,
//     easing off) is pressed flat to that inner face, so it lies under the board and nothing pokes out of its face. Nothing is cut, so no scalp shows.
// The board first and the body last, so the body always wins: hair the board presses too far in lies just over the body, out of sight.
#ifndef CITYX_HAIR_PUSH_INCLUDED
#define CITYX_HAIR_PUSH_INCLUDED

#define CITYX_HAIR_PIECES_MAX 40
#define CITYX_BACK_BOARDS_MAX 8

// The body's pieces: each a round cone from A to B, its radius at each end (room for the shirt included), in the world.
float4 _HairPushA[CITYX_HAIR_PIECES_MAX];
float4 _HairPushB[CITYX_HAIR_PIECES_MAX];
float _HairPushCount;

// The boards: a frame each (x across it, y out of its face away from the back, z along it), its outline a V of two arms in x/z narrowing from the elbow
// to the tips: one tip (x, z) and the elbow (x, z); the other tip (x, z), the arms' half width at the elbow, half the board's thickness; the half
// width at the tips.
float4x4 _BackClipToBoard[CITYX_BACK_BOARDS_MAX];
float4x4 _BackClipFromBoard[CITYX_BACK_BOARDS_MAX];
float _BackClipCount;
float4 _BackClipVeeA[CITYX_BACK_BOARDS_MAX];
float4 _BackClipVeeB[CITYX_BACK_BOARDS_MAX];
float4 _BackClipEdge[CITYX_BACK_BOARDS_MAX];

// How far past a board's edges hair is still pressed under it, easing off to nothing, metres; and how far in from its inner face it is laid.
#define CITYX_BOARD_EASE 0.04
#define CITYX_BOARD_UNDER 0.008

// How far inside an arm from tip to elbow a point is (positive inside), the arm narrowing from its elbow's half width to its tip's.
float CityXBoardInArm(float2 p, float2 tip, float2 elbow, float elbowHalf, float tipHalf)
{
    float2 ab = elbow - tip;
    float t = saturate(dot(p - tip, ab) / max(dot(ab, ab), 1e-6));
    return lerp(tipHalf, elbowHalf, t) - length(p - (tip + ab * t));
}

float3 CityXHairPushWorld(float3 world)
{
#if defined(_CITYX_HAIR)
    int boards = (int)_BackClipCount;
    for (int i = 0; i < CITYX_BACK_BOARDS_MAX; i++)
    {
        if (i >= boards)
        {
            break;
        }
        float3 p = mul(_BackClipToBoard[i], float4(world, 1.0)).xyz;
        float4 a = _BackClipVeeA[i], b = _BackClipVeeB[i], e = _BackClipEdge[i];
        float under = -b.w - CITYX_BOARD_UNDER;
        // Only hair at or past the inner face, and not far out beyond the board (the back of a head beside its upper tip is left).
        if (p.y > under && p.y < b.w + e.y)
        {
            float inside = max(CityXBoardInArm(p.xz, a.xy, a.zw, b.z, e.x), CityXBoardInArm(p.xz, b.xy, a.zw, b.z, e.x));
            float press = smoothstep(-CITYX_BOARD_EASE, 0.0, inside);
            p.y = lerp(p.y, under, press);
            world = mul(_BackClipFromBoard[i], float4(p, 1.0)).xyz;
        }
    }
    int pieces = (int)_HairPushCount;
    for (int k = 0; k < CITYX_HAIR_PIECES_MAX; k++)
    {
        if (k >= pieces)
        {
            break;
        }
        float3 from = _HairPushA[k].xyz, to = _HairPushB[k].xyz;
        float3 along = to - from;
        float t = saturate(dot(world - from, along) / max(dot(along, along), 1e-6));
        float3 nearest = from + along * t;
        float radius = lerp(_HairPushA[k].w, _HairPushB[k].w, t);
        float3 away = world - nearest;
        float distance = length(away);
        if (distance < radius && distance > 1e-5)
        {
            world = nearest + away * (radius / distance);
        }
    }
#endif
    return world;
}

float3 CityXHairPushObject(float3 positionOS)
{
#if defined(_CITYX_HAIR)
    return TransformWorldToObject(CityXHairPushWorld(TransformObjectToWorld(positionOS)));
#else
    return positionOS;
#endif
}

#endif
