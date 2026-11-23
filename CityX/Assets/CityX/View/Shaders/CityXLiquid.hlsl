// A DRINK SEEN THROUGH ITS BOTTLE (docs/PHIZZ_CAN.md; owner, 2026-11-23: "make the glass bottle full of piss yellow liquid and when you drink its, it visually
// lowers until empty"). The bottles' glass is opaque, so the game's black line is drawn round it (a see-through one gets none and reads as a smudge): the
// drink is the glass itself coloured below the liquid's level. The level is a height in the world, so it stays level as the bottle tips, and it goes down
// as the bottle is drunk (FullBottle sets both per bottle, on its own renderers; never set, the level is below everything and nothing shows).
#ifndef CITYX_LIQUID_INCLUDED
#define CITYX_LIQUID_INCLUDED

float _CityXLiquidLevel;
half4 _CityXLiquidColour;

// How far under the level the top of the drink shows lighter, metres: its surface.
#define CITYX_LIQUID_SURFACE 0.012

void CityXApplyLiquid(inout SurfaceData surfaceData, float3 positionWS)
{
    float below = _CityXLiquidLevel - positionWS.y;
    if (below > 0.0)
    {
        half surface = below < CITYX_LIQUID_SURFACE ? 1.0h : 0.0h;
        surfaceData.albedo = lerp(_CityXLiquidColour.rgb, min(_CityXLiquidColour.rgb * 1.25h, 1.0h), surface);
    }
}

#endif
