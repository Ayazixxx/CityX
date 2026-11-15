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
//   * under a high collar (a coat's), drawn into the body below the collar's rim (CityXHairTuck).
// The board first and the body after it, so the body wins over the board; the collar last, as what it tucks in is inside the body on purpose.
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

// The bodies' high collars (a coat's fur collar), by the same place in the list as their heads: a point on the neck's line at the height of the collar's
// top behind the neck (w 1 when the body wears one), up out of the collar, and forward.
float4 _HairTuckRim[CITYX_HAIR_BODIES_MAX];
float4 _HairTuckUp[CITYX_HAIR_BODIES_MAX];
float4 _HairTuckForward[CITYX_HAIR_BODIES_MAX];

// Hair is tucked in full this far under a collar's rim, easing in from nothing at the rim, metres.
#define CITYX_TUCK_EASE 0.04
// In front of the throat nothing is tucked (a coat's collar opens there): from the neck's line forward, easing off over CITYX_TUCK_FRONT, and within
// CITYX_TUCK_THROAT of the middle, easing back in over CITYX_TUCK_FRONT more, metres. The locks beside the ears, a little in front of the neck's line, are
// tucked: left out, they went down outside the fur and on into the coat below it.
#define CITYX_TUCK_FRONT 0.06
#define CITYX_TUCK_THROAT 0.12
// The head's sphere keeps tucked hair off the scalp and out of the head only near the rim, letting go from this far under it to twice as far, metres:
// there the hair fills the gap between the head and the fur (drawn in, the scalp would show down that gap). Further down the sphere is not the head: it
// reaches the shoulders (the collar's rim is under a head radius below its middle), and the locks hanging beside the neck, stopped at it, still came out
// below the fur.
#define CITYX_TUCK_DEEP 0.06
// A body's pieces in the list: the torso, the shoulder bar, the neck column, the upper arms (Character/Body/HairPush: PiecesPerBody, FusedBodyShape.UpperBody).
#define CITYX_PIECES_PER_BODY 5
#define CITYX_NECK_PIECE 2

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
// in full from scalpFade off it (0: wherever it is off it, only what is already in the scalp's room staying), and never on into a head.
float3 CityXHairOffScalps(float3 from, float3 to, float scalpFade)
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
        float outside = dot(away, away) - radius * radius;
        if (scalpFade > 0.0)
        {
            reach = min(reach, smoothstep(radius, radius + scalpFade, length(away)));
        }
        else if (outside <= 0.0)
        {
            reach = 0.0;
        }
        float a = dot(move, move), b = dot(move, away);
        float d = b * b - a * outside;
        if (outside > 0.0 && a > 1e-10 && b < 0.0 && d > 0.0)
        {
            reach = min(reach, (-b - sqrt(d)) / a);
        }
    }
    return from + move * reach;
}

// Under a high collar (owner, 2026-11-15: "make it so the kick hair doesnt clip through the midnight coat ... the back spikes are clipping through"): hair hanging
// below the collar's rim, behind and beside the neck, is drawn in to the neck's own line (its column's middle), under the coat, so it goes down into the
// collar as hair does, rather than out through the coat's back. (Laid on the body instead, it showed in the gap the coat opens under the collar as the
// shoulders turn: the body is not drawn there, under the coat. Drawn to the nearest of the torso, shoulder bar and neck, a lock beside the neck went down
// to the shoulder bar, stretched out through the coat's shoulder.) It is done after the push out of the body, so the body does not put it back. Near the rim none goes into a head (CityXHairOffScalps, CITYX_TUCK_DEEP): hair on the scalp there is pressed down onto
// it, so the head keeps its hair. (Left where it lay within a few centimetres of the scalp, as hair under a board is, the locks curling off the nape
// still came out under the collar at a sprint.)
float3 CityXHairTuck(float3 world)
{
    int heads = (int)_HairPushHeadCount;
    for (int h = 0; h < CITYX_HAIR_BODIES_MAX; h++)
    {
        if (h >= heads)
        {
            break;
        }
        if (_HairTuckRim[h].w < 0.5)
        {
            continue;
        }
        float3 fromRim = world - _HairTuckRim[h].xyz;
        float3 across = normalize(cross(_HairTuckUp[h].xyz, _HairTuckForward[h].xyz));
        float throat = smoothstep(0.0, CITYX_TUCK_FRONT, dot(fromRim, _HairTuckForward[h].xyz))
            * (1.0 - smoothstep(CITYX_TUCK_THROAT, CITYX_TUCK_THROAT + CITYX_TUCK_FRONT, abs(dot(fromRim, across))));
        float below = -dot(fromRim, _HairTuckUp[h].xyz);
        float under = smoothstep(0.0, CITYX_TUCK_EASE, below) * (1.0 - throat);
        if (under <= 0.0)
        {
            continue;
        }
        int neck = h * CITYX_PIECES_PER_BODY + CITYX_NECK_PIECE;
        float3 from = _HairPushA[neck].xyz, to = _HairPushB[neck].xyz;
        float3 along = to - from;
        float3 onto = from + along * saturate(dot(world - from, along) / max(dot(along, along), 1e-6));
        float3 tucked = lerp(world, onto, under);
        world = lerp(CityXHairOffScalps(world, tucked, 0.0), tucked, smoothstep(CITYX_TUCK_DEEP, 2.0 * CITYX_TUCK_DEEP, below));
    }
    return world;
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
            world = CityXHairOffScalps(world, mul(_BackClipFromBoard[i], float4(p, 1.0)).xyz, CITYX_SCALP_FADE);
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
    world = CityXHairTuck(world);
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
