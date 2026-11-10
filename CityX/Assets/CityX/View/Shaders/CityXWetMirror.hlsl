#ifndef CITYX_WET_MIRROR_INCLUDED
#define CITYX_WET_MIRROR_INCLUDED

// WET STREETS MIRROR THE CITY (docs/WET_STREETS.md). In the rain the ground open to the sky shows the lamps, the lit windows and the sky in
// the water lying on it: screen-space reflections, as most games do wet streets, but looked up by the ground itself while it is drawn, in
// the picture of the frame before (WetMirrorFeature keeps that picture and how far away each of its pixels is, at half size, with the matrix
// it was drawn with). So only wet ground pays for it, nothing indoors or under the ground ever shows it, and the glass, the rain and the
// fire drawn afterwards lie over the reflection as they should. It is one frame late, which does not show.
//
// Along the mirrored line of sight a few steps, each longer than the last, are looked up in the remembered picture: the first that lies
// behind what the picture shows there is where the line meets the city, found again more finely between the last two steps, and the
// picture's colour there is the reflection. A line that goes behind something thin (a lamp post, a body) without touching it carries on. A
// line that leaves the picture reflects nothing, and the ground keeps its own dull shine of the sky; one that reaches the end of its steps
// reflects what the picture shows there, which is the sky or a tower too far off to step to.

TEXTURE2D(_CityXMirrorPicture);
TEXTURE2D(_CityXMirrorDepth);

// x: how strong (0 while no picture is kept: a dry day, the effect switched off). y: how many steps down the picture's sizes the dull film
// of water between the puddles reads it, a puddle reading it sharp.
float4 _CityXMirror;
// World to the remembered picture's clip space (y up, whatever the platform).
float4x4 _CityXMirrorMatrix;

// The steps: 20 of them, from 30 cm to 68 m, 270 m in all; and how many times the crossing is halved down between two of them (a 68 m step
// to half a metre).
#define CITYX_MIRROR_STEPS 20
#define CITYX_MIRROR_FIRST_STEP 0.3
#define CITYX_MIRROR_STEP_GROWTH 1.33
#define CITYX_MIRROR_REFINES 7

// Past its last step a line is taken to run on for ever: what it mirrors is where it points, this far along it.
#define CITYX_MIRROR_FOR_EVER 20000.0

// A line this far behind what the picture shows has gone behind it, not into it: in metres, and as a share of how far away it is.
#define CITYX_MIRROR_THICK 0.6
#define CITYX_MIRROR_THICK_PER_METRE 0.03

// Water mirrors more the flatter it is looked along (Schlick, with more than water's own 2% head-on: a street at night is the look wanted).
#define CITYX_MIRROR_HEAD_ON 0.05

// The thin film between the puddles mirrors this much of what a puddle does.
#define CITYX_MIRROR_FILM 0.45

// Where a world point is in the remembered picture: xy its place (0..1 across and down), z how far from that picture's eye. z <= 0: behind it.
float3 CityXMirrorPlace(float3 positionWS)
{
    float4 clip = mul(_CityXMirrorMatrix, float4(positionWS, 1.0));
    float2 uv = clip.xy / max(clip.w, 1e-4) * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    return float3(uv, clip.w);
}

float CityXMirrorDepthAt(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_CityXMirrorDepth, sampler_PointClamp, uv, 0.0).r;
}

bool CityXMirrorOffPicture(float3 place)
{
    return place.z <= 0.0 || any(place.xy < 0.0) || any(place.xy > 1.0);
}

// How far a point at <place> in the remembered picture lies behind what the picture shows there, in metres. What faces up counts as nothing
// (CityXMirrorMemory.shader marks it, by a negative distance): a line mirrored off the ground is climbing, and a climbing line cannot meet
// the top of anything. Looked along, one row of a far street's distances is metres from the next, and a line just over the road was "behind"
// it on every other row: the road mirrored itself in stripes.
float CityXMirrorBehind(float3 place)
{
    float shownAt = CityXMirrorDepthAt(place.xy);
    return shownAt < 0.0 ? -1.0 : place.z - shownAt;
}

// Where water stands deeper, 0..1: patches a few metres across, more of them the wetter the city is.
half CityXPuddles(float2 xz, float wetness)
{
    float n = FacadeNoise(xz * 0.11) * 0.6 + FacadeNoise(xz * 0.37 + 19.7) * 0.4;
    float from = lerp(0.78, 0.46, wetness);
    return smoothstep(from, from + 0.12, n);
}

// The lit colour of a wet, up-facing surface open to the sky, with the city mirrored in it.
half3 CityXWetMirror(half3 colour, float3 positionWS, half3 normalWS, half3 viewDirectionWS, float wetness, float2 pixel)
{
    float strength = _CityXMirror.x * wetness * smoothstep(0.75, 0.95, normalWS.y);
    if (strength <= 0.0)
    {
        return colour;
    }
    half puddle = CityXPuddles(positionWS.xz, wetness);
    float facing = saturate(dot(normalWS, viewDirectionWS));
    float fresnel = CITYX_MIRROR_HEAD_ON + (1.0 - CITYX_MIRROR_HEAD_ON) * pow(1.0 - facing, 5.0);
    float share = strength * fresnel * lerp(CITYX_MIRROR_FILM, 1.0, puddle);
    if (share < 0.01)
    {
        return colour;
    }

    float3 ray = reflect(-viewDirectionWS, normalWS);
    // Each pixel's steps are a little longer or shorter than its neighbours', so what falls between two steps is missed as grain, not in bands.
    float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
    float stepLength = CITYX_MIRROR_FIRST_STEP * lerp(0.8, 1.2, jitter);
    float before = 0.0;
    float at = stepLength;
    float3 place = float3(0.0, 0.0, 0.0);
    bool found = false;
    bool off = false;

    for (int i = 0; i < CITYX_MIRROR_STEPS; i++)
    {
        place = CityXMirrorPlace(positionWS + ray * at);
        if (CityXMirrorOffPicture(place))
        {
            off = true;
            break;
        }
        if (CityXMirrorBehind(place) > 0.0)
        {
            // Behind what the picture shows: where between the last step and this one did it go behind?
            float inFront = before, gone = at;
            for (int r = 0; r < CITYX_MIRROR_REFINES; r++)
            {
                float middle = (inFront + gone) * 0.5;
                if (CityXMirrorBehind(CityXMirrorPlace(positionWS + ray * middle)) > 0.0)
                {
                    gone = middle;
                }
                else
                {
                    inFront = middle;
                }
            }
            float3 crossing = CityXMirrorPlace(positionWS + ray * gone);
            float behind = CityXMirrorBehind(crossing);
            if (behind < CITYX_MIRROR_THICK + crossing.z * CITYX_MIRROR_THICK_PER_METRE)
            {
                place = crossing;
                found = true;
                break;
            }
        }
        before = at;
        stepLength *= CITYX_MIRROR_STEP_GROWTH;
        at += stepLength;
    }
    if (!found && !off)
    {
        place = CityXMirrorPlace(positionWS + ray * CITYX_MIRROR_FOR_EVER);
        off = CityXMirrorOffPicture(place);
    }
    if (off)
    {
        return colour;
    }

    // Faded out toward the picture's edges, where a turn of the head would bring in what it does not hold.
    float2 fromEdge = min(place.xy, 1.0 - place.xy);
    float edge = saturate(min(fromEdge.x, fromEdge.y) / 0.08);
    float blur = lerp(_CityXMirror.y, 0.0, puddle) + (found ? 0.0 : 1.0);
    half3 mirrored = SAMPLE_TEXTURE2D_LOD(_CityXMirrorPicture, sampler_TrilinearClamp, place.xy, blur).rgb;
    return lerp(colour, mirrored, share * edge);
}

#endif
