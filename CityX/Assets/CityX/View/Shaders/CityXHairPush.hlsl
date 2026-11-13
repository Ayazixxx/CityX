// HAIR KEPT OUT OF THE BODY AND UNDER WHAT IS SLUNG ON THE BACK (owner, 2026-11-12: "is there a better fix for the shirt spikes stabbing through the shirt
// than them bending?", the hair poking out through the Giant Boomerang on the back). A material with _CITYX_HAIR (the hair's) has its vertices pushed, on
// the graphics card every frame, in every pass that draws it (colour, depth, depth-normals for the outlines, shadows), so it looks the same everywhere:
//   * out of the upper body's own pieces (the torso, the shoulder bar, the neck column, each arm to its elbow: FusedBodyShape.UpperBody), laid just
//     over them, under the shirt, so no lock goes through the shirt whatever the head does (Character/Body/HairPush sets them, the nearest bodies' only);
//   * into a board slung on a back (Items/BackClip): whatever of it is out past the board's middle inside its outline, however far out, is laid on that
//     middle, inside the board (whose faces thin to its edges), so no lone lock pokes out of its face; a little past its edges partly, easing off, so the
//     hair there seems to wrap round them. Hair on the body's side of the board is never moved: pressed all the way under the board, the hair beside it
//     went into the neck and head and left skin showing (owner, 2026-11-12: "clear bald spots in between the hair and boomerang", "no 'lone spikes'
//     poking through", "they can clip through slightly around the edges"). Hair on a scalp is not pressed at all (nearer than 5 cm, less) nor any into a head, and the
//     board goes through the hair there instead (owner: "there is a frame where the bald scalp is viewable ... i would much rather have a frame where
//     the boomerang clips through the hair"; as it is put back the head leans into the board). Nothing is cut, so no scalp shows.
// The board first and the body last, so the body always wins.
#ifndef CITYX_HAIR_PUSH_INCLUDED
#define CITYX_HAIR_PUSH_INCLUDED

#define CITYX_HAIR_PIECES_MAX 40
#define CITYX_BACK_BOARDS_MAX 8
#define CITYX_HAIR_BODIES_MAX 8

// The body's pieces: each a round cone from A to B, its radius at each end (room for the shirt included), in the world.
float4 _HairPushA[CITYX_HAIR_PIECES_MAX];
float4 _HairPushB[CITYX_HAIR_PIECES_MAX];
float _HairPushCount;

// The bodies' heads: each a sphere (centre, radius with the room off the scalp), in the world.
float4 _HairPushHeads[CITYX_HAIR_BODIES_MAX];
float _HairPushHeadCount;

// The boards: a frame each (x across it, y out of its face away from the back, z along it, its middle at 0), its outline a V of two arms in x/z
// narrowing from the elbow to the tips: one tip (x, z) and the elbow (x, z); the other tip (x, z), the arms' half width at the elbow and at the tips.
float4x4 _BackClipToBoard[CITYX_BACK_BOARDS_MAX];
float4x4 _BackClipFromBoard[CITYX_BACK_BOARDS_MAX];
float _BackClipCount;
float4 _BackClipVeeA[CITYX_BACK_BOARDS_MAX];
float4 _BackClipVeeB[CITYX_BACK_BOARDS_MAX];

// How far past a board's edges hair is still pressed into it, easing off to nothing, metres.
#define CITYX_BOARD_EASE 0.04

// How far inside an arm from tip to elbow a point is (positive inside), the arm narrowing from its elbow's half width to its tip's.
float CityXBoardInArm(float2 p, float2 tip, float2 elbow, float elbowHalf, float tipHalf)
{
    float2 ab = elbow - tip;
    float t = saturate(dot(p - tip, ab) / max(dot(ab, ab), 1e-6));
    return lerp(tipHalf, elbowHalf, t) - length(p - (tip + ab * t));
}

// How far off a head hair is pressed into a board in full, metres: nearer, less, and none at all on the scalp.
#define CITYX_SCALP_FADE 0.05

// Moved from one place toward another: not at all where it lies on a head's scalp (it covers the scalp there, so the board goes through it instead),
// in full from CITYX_SCALP_FADE off it, and never on into a head.
float3 CityXHairOffScalps(float3 from, float3 to)
{
    float3 move = to - from;
    float reach = 1.0;
    int heads = (int)_HairPushHeadCount;
    for (int h = 0; h < CITYX_HAIR_BODIES_MAX; h++)
    {
        if (h >= heads)
        {
            break;
        }
        float3 away = from - _HairPushHeads[h].xyz;
        float radius = _HairPushHeads[h].w;
        reach = min(reach, smoothstep(radius, radius + CITYX_SCALP_FADE, length(away)));
        float outside = dot(away, away) - radius * radius;
        float a = dot(move, move), b = dot(move, away);
        float d = b * b - a * outside;
        if (outside > 0.0 && a > 1e-10 && b < 0.0 && d > 0.0)
        {
            reach = min(reach, (-b - sqrt(d)) / a);
        }
    }
    return from + move * reach;
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
        float4 a = _BackClipVeeA[i], b = _BackClipVeeB[i];
        if (p.y > 0.0)
        {
            float inside = max(CityXBoardInArm(p.xz, a.xy, a.zw, b.z, b.w), CityXBoardInArm(p.xz, b.xy, a.zw, b.z, b.w));
            p.y *= 1.0 - smoothstep(-CITYX_BOARD_EASE, 0.0, inside);
            world = CityXHairOffScalps(world,mul(_BackClipFromBoard[i], float4(p, 1.0)).xyz);
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
