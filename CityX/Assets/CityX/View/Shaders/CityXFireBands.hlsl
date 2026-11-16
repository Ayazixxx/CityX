// THE FIRE SPELLS' HEAT IN BOLD BANDS (docs/FIRE.md, "The fire spells"). The cel look's answer to a black-body ramp: heat 0..1 picks one of five
// colours -- a dark ember red, red, orange, yellow, white-hot -- with an edge a pixel wide between them (no gradient across a band, as the
// game's cel light has none), the hot ones past 1 so the bloom catches them. Below the first band nothing burns (smoke). The colours are the
// meteor's of achrefelouafi's LinearAbiltyCastingThreeJS (MIT): #2b0d05, #ff3d10, #ff6a12, #ffb02e, and a pale gold for its #fff3d0
// (below), here in linear. SpellFire.Band is
// the same ramp in C# (keep the two in step).
//
// And a cheap 3D value noise (hashed lattice, trilinear), for what has no particle uv to read the fire's noise texture with: the fireball's core.
#ifndef CITYX_FIRE_BANDS_INCLUDED
#define CITYX_FIRE_BANDS_INCLUDED

static const float3 FireBandEmber = float3(0.024, 0.004, 0.0015) * 9.0;
static const float3 FireBandRed = float3(1.0, 0.047, 0.005) * 0.8;
static const float3 FireBandOrange = float3(1.0, 0.144, 0.006) * 1.3;
static const float3 FireBandYellow = float3(1.0, 0.434, 0.027) * 1.8;
// White-hot is a pale gold, not the reference's near white: the game's tone mapping (Neutral) greys anything that bright and that pale, and
// the hearts of the fire read as grey smoke (2026-11-15).
static const float3 FireBandWhite = float3(1.0, 0.68, 0.22) * 1.9;

// Where each band begins, in heat.
static const float FireBandEmberAt = 0.08;
static const float FireBandRedAt = 0.22;
static const float FireBandOrangeAt = 0.42;
static const float FireBandYellowAt = 0.64;
static const float FireBandWhiteAt = 0.86;

// 0 below the edge, 1 above it, the step a pixel wide wherever the heat is (fwidth), so a band's edge is crisp but not jagged.
float FireBandStep(float edge, float heat, float aa)
{
    return smoothstep(edge - aa, edge + aa, heat);
}

// Whether anything burns at this heat (0 is smoke, or nothing).
float FireBurning(float heat)
{
    float aa = max(fwidth(heat), 1e-4) * 0.75;
    return FireBandStep(FireBandEmberAt, heat, aa);
}

float3 FireBands(float heat)
{
    float aa = max(fwidth(heat), 1e-4) * 0.75;
    float3 colour = FireBandEmber * FireBandStep(FireBandEmberAt, heat, aa);
    colour = lerp(colour, FireBandRed, FireBandStep(FireBandRedAt, heat, aa));
    colour = lerp(colour, FireBandOrange, FireBandStep(FireBandOrangeAt, heat, aa));
    colour = lerp(colour, FireBandYellow, FireBandStep(FireBandYellowAt, heat, aa));
    colour = lerp(colour, FireBandWhite, FireBandStep(FireBandWhiteAt, heat, aa));
    return colour;
}

float FireHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Value noise, 0..1.
float FireValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FireHash13(i);
    float b = FireHash13(i + float3(1.0, 0.0, 0.0));
    float c = FireHash13(i + float3(0.0, 1.0, 0.0));
    float d = FireHash13(i + float3(1.0, 1.0, 0.0));
    float e = FireHash13(i + float3(0.0, 0.0, 1.0));
    float g = FireHash13(i + float3(1.0, 0.0, 1.0));
    float h = FireHash13(i + float3(0.0, 1.0, 1.0));
    float k = FireHash13(i + float3(1.0, 1.0, 1.0));
    return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
}

// Three octaves of it, -1..1 (signed, so its zero crossings draw the thin, forking seams of a crack).
float FireFbm(float3 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 3; octave++)
    {
        sum += amplitude * (FireValueNoise(p) * 2.0 - 1.0);
        p = p * 2.17 + float3(1.7, 9.2, 4.1);
        amplitude *= 0.5;
    }
    return sum / 0.875;
}

#endif
