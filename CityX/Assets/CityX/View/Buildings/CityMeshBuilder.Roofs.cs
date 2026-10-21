using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A pitched roof (World/RoofShape): gable, hip, skillion, saltbox, or a warehouse's low gable or shed, standing on the top of the walls in
    /// place of the parapet and the roof plant; and the flat roof with eaves, a slab carried out past the walls with no parapet. In the wall
    /// buffer: it draws with the facade in the same draw call, and the chunk's collider takes it (a 2.6 m body walks up any of them: the
    /// steepest is 32 degrees).
    ///
    /// The underside of each slope passes through the top edge of the walls, so the roof closes the building with the top floor's ceiling under
    /// it (the attic is shut: no stair comes up to it). Past the walls it is the eaves, and their underside (the soffit) and front edge (the
    /// fascia) are drawn. A gable or a skillion closes its ends with the wall carried up as a triangle, in the wall's own look; a saltbox
    /// carries its front wall up a storey and closes its ends with its own profile.
    ///
    /// The covering is the facade's Tile material at a roof tile's size (three times the wall tile's) or its standing-seam Metal, laid with the
    /// seams running down the slope; tinted by RoofShape.Tint.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        /// <summary>Roof tiles are this many times the size of the wall tiles the Tile material draws (100 mm): 300 mm.</summary>
        private const float RoofTileScale = 3.0f;

        private static readonly Color SoffitTint = new Color(0.78f, 0.77f, 0.74f);

        /// <summary>
        /// Maps a roof's own frame -- <c>a</c> along the ridge, <c>b</c> across it -- to the building's, so every roof is written once whichever
        /// way its ridge runs.
        /// </summary>
        private readonly struct RoofAxes
        {
            private readonly bool _alongX;

            public RoofAxes(bool alongX)
            {
                _alongX = alongX;
            }

            public Vector3 P(float a, float y, float b) => _alongX ? new Vector3(a, y, b) : new Vector3(b, y, a);

            public Vector3 N(float na, float ny, float nb) => _alongX ? new Vector3(na, ny, nb) : new Vector3(nb, ny, na);
        }

        /// <summary>The roof of a building with a pitched one, on walls whose tops are at <paramref name="roofY"/>. <paramref name="far"/> leaves out the ridge cap.</summary>
        private static void AddPitchedRoof(MeshBuffer buffer, BuildingSite site, in RoofShape shape, float x0, float x1, float z0, float z1,
            float roofY, SiteLooks looks, bool far)
        {
            var axes = new RoofAxes(shape.AlongX);
            float a0 = shape.AlongX ? x0 : z0, a1 = shape.AlongX ? x1 : z1;
            float b0 = shape.AlongX ? z0 : x0, b1 = shape.AlongX ? z1 : x1;
            float t = Mathf.Tan(shape.Pitch * Mathf.Deg2Rad);
            float cos = Mathf.Cos(shape.Pitch * Mathf.Deg2Rad), sin = Mathf.Sin(shape.Pitch * Mathf.Deg2Rad);
            float thick = RoofShape.Thickness / cos;   // the roof's thickness, measured straight up
            Color32 saved = buffer.Look;
            Color32 covering = SurfaceLook.Pack(shape.Tint, (int)(shape.Covering == RoofCovering.Tile ? WallMaterial.Tile : WallMaterial.Metal),
                0.25f + 0.3f * (float)WorldHash.Unit01(7, 0, 0x52465757UL, unchecked((ulong)(uint)site.Seed)));
            Color32 trim = SurfaceLook.Pack(shape.Tint * 0.78f, (int)WallMaterial.Metal, 0.4f);
            Color32 soffit = SurfaceLook.Pack(SoffitTint, (int)WallMaterial.Plaster, 0.35f);
            float uvScale = shape.Covering == RoofCovering.Tile ? 1.0f / RoofTileScale : 1.0f;
            // Where the roof's highest line is, for the chimney or the aerial: its b, its height, and the b the chimney stands at.
            float ridgeB, peak, chimneyB;

            switch (shape.Kind)
            {
                case RoofKind.Gable:
                {
                    float bc = (b0 + b1) * 0.5f, s = (b1 - b0) * 0.5f, e = shape.Eave, v = shape.Verge;
                    float ridge = roofY + s * t;
                    float eaveY = roofY - e * t;
                    for (float side = -1.0f; side <= 1.0f; side += 2.0f)   // the -b slope, then the +b one
                    {
                        float bEave = side < 0.0f ? b0 - e : b1 + e;
                        Vector3 up = axes.N(0.0f, cos, side * sin);
                        // The slope, its underside and its two verges.
                        buffer.Look = covering;
                        Slope(buffer, axes, up, a0 - v, a1 + v, bEave, eaveY + thick, bc, ridge + thick, uvScale);
                        buffer.Look = soffit;
                        Slope(buffer, axes, -up, a0 - v, a1 + v, bEave, eaveY, bc, ridge, 1.0f);
                        buffer.Look = trim;
                        Fascia(buffer, axes, axes.N(0.0f, 0.0f, side), a0 - v, a1 + v, bEave, eaveY, eaveY + thick);
                        Verge(buffer, axes, axes.N(-1.0f, 0.0f, 0.0f), a0 - v, bEave, eaveY, bc, ridge, thick);
                        Verge(buffer, axes, axes.N(1.0f, 0.0f, 0.0f), a1 + v, bEave, eaveY, bc, ridge, thick);
                    }
                    // The gable ends: the wall carried up as a triangle under each verge.
                    buffer.Look = looks.Wall;
                    GableEnd(buffer, axes, axes.N(-1.0f, 0.0f, 0.0f), a0, b0, b1, roofY, ridge);
                    GableEnd(buffer, axes, axes.N(1.0f, 0.0f, 0.0f), a1, b0, b1, roofY, ridge);
                    if (!far)
                    {
                        RidgeCap(buffer, axes, trim, a0 - v, a1 + v, bc, ridge + thick);
                    }
                    ridgeB = bc;
                    peak = ridge;
                    chimneyB = bc - 1.0f;
                    break;
                }
                case RoofKind.Hip:
                {
                    float bc = (b0 + b1) * 0.5f, s = (b1 - b0) * 0.5f, e = shape.Eave;
                    float ridge = roofY + s * t;
                    float eaveY = roofY - e * t;
                    // The ridge's ends, the hips running in at 45 degrees on plan from the corners; a square house's roof is a pyramid.
                    float ar0 = Mathf.Min(a0 + s, (a0 + a1) * 0.5f), ar1 = Mathf.Max(a1 - s, (a0 + a1) * 0.5f);
                    for (float side = -1.0f; side <= 1.0f; side += 2.0f)   // the -b slope, then the +b one
                    {
                        float bEave = side < 0.0f ? b0 - e : b1 + e;
                        Vector3 up = axes.N(0.0f, cos, side * sin);
                        buffer.Look = covering;
                        HipSide(buffer, axes, up, a0 - e, a1 + e, bEave, eaveY + thick, ar0, ar1, bc, ridge + thick, uvScale);
                        buffer.Look = soffit;
                        HipSide(buffer, axes, -up, a0 - e, a1 + e, bEave, eaveY, ar0, ar1, bc, ridge, 1.0f);
                        buffer.Look = trim;
                        Fascia(buffer, axes, axes.N(0.0f, 0.0f, side), a0 - e, a1 + e, bEave, eaveY, eaveY + thick);

                        // The end: a triangle from its eave up to the ridge's end.
                        float aEave = side < 0.0f ? a0 - e : a1 + e;
                        float aApex = side < 0.0f ? ar0 : ar1;
                        Vector3 endUp = axes.N(side * sin, cos, 0.0f);
                        buffer.Look = covering;
                        HipEnd(buffer, axes, endUp, aEave, b0 - e, b1 + e, eaveY + thick, aApex, bc, ridge + thick, uvScale);
                        buffer.Look = soffit;
                        HipEnd(buffer, axes, -endUp, aEave, b0 - e, b1 + e, eaveY, aApex, bc, ridge, 1.0f);
                        buffer.Look = trim;
                        FasciaAcross(buffer, axes, axes.N(side, 0.0f, 0.0f), aEave, b0 - e, b1 + e, eaveY, eaveY + thick);
                    }
                    if (!far && ar1 - ar0 > 0.3f)
                    {
                        RidgeCap(buffer, axes, trim, ar0, ar1, bc, ridge + thick);
                    }
                    ridgeB = bc;
                    peak = ridge;
                    chimneyB = bc - 1.0f;
                    break;
                }
                case RoofKind.Saltbox:
                {
                    // The ridge forward of the middle, the front wall (the street door's) carried up a storey, the front slope short and the back
                    // slope long at the same pitch, down to the back wall's top: two storeys in front, one behind.
                    float e = shape.Eave, v = shape.Verge, attic = shape.Attic;
                    float span = b1 - b0;
                    float s = shape.LowAtMin ? 1.0f : -1.0f;   // from the back wall toward the front
                    float bBack = shape.LowAtMin ? b0 : b1, bFront = shape.LowAtMin ? b1 : b0;
                    float r = shape.SaltboxRidgeFromFront(span);
                    float bRidge = bFront - s * r;
                    float ridgeY = roofY + attic + r * t;
                    float frontEaveB = bFront + s * e, frontEaveY = roofY + attic - e * t;
                    float backEaveB = bBack - s * e, backEaveY = roofY - e * t;
                    for (int slope = 0; slope < 2; slope++)   // the front slope, then the back one
                    {
                        bool front = slope == 0;
                        float side = front ? s : -s;
                        float bEave = front ? frontEaveB : backEaveB, eaveY = front ? frontEaveY : backEaveY;
                        Vector3 up = axes.N(0.0f, cos, side * sin);
                        buffer.Look = covering;
                        Slope(buffer, axes, up, a0 - v, a1 + v, bEave, eaveY + thick, bRidge, ridgeY + thick, uvScale);
                        buffer.Look = soffit;
                        Slope(buffer, axes, -up, a0 - v, a1 + v, bEave, eaveY, bRidge, ridgeY, 1.0f);
                        buffer.Look = trim;
                        Fascia(buffer, axes, axes.N(0.0f, 0.0f, side), a0 - v, a1 + v, bEave, eaveY, eaveY + thick);
                        Verge(buffer, axes, axes.N(-1.0f, 0.0f, 0.0f), a0 - v, bEave, eaveY, bRidge, ridgeY, thick);
                        Verge(buffer, axes, axes.N(1.0f, 0.0f, 0.0f), a1 + v, bEave, eaveY, bRidge, ridgeY, thick);
                    }
                    // The front wall carried up its storey, and each end wall as the saltbox's own profile: along the walls' top from the back
                    // to the front, up the attic, to the ridge, and down the long slope to the back.
                    buffer.Look = looks.Wall;
                    AddFace(buffer, axes.N(0.0f, 0.0f, s), axes.P(a0, roofY, bFront), axes.P(a1, roofY, bFront), axes.P(a1, roofY + attic, bFront),
                        axes.P(a0, roofY + attic, bFront));
                    for (int end = 0; end < 2; end++)
                    {
                        float aEnd = end == 0 ? a0 : a1;
                        Vector3 n = axes.N(end == 0 ? -1.0f : 1.0f, 0.0f, 0.0f);
                        Vector3 p0 = axes.P(aEnd, roofY, bBack), p1 = axes.P(aEnd, roofY, bFront), p2 = axes.P(aEnd, roofY + attic, bFront);
                        Vector3 p3 = axes.P(aEnd, ridgeY, bRidge);
                        buffer.AddTriangle(p0, p1, p2, n, Uv(p0, n), Uv(p1, n), Uv(p2, n));
                        buffer.AddTriangle(p0, p2, p3, n, Uv(p0, n), Uv(p2, n), Uv(p3, n));
                    }
                    if (!far)
                    {
                        RidgeCap(buffer, axes, trim, a0 - v, a1 + v, bRidge, ridgeY + thick);
                    }
                    ridgeB = bRidge;
                    peak = ridgeY;
                    chimneyB = bRidge + s * 1.0f;   // on the front slope, the saltbox's own place for it
                    break;
                }
                default:
                {
                    // A skillion: one slope across the whole span, low over one long wall and high over the other.
                    float e = shape.Eave, v = shape.Verge, high = 0.3f;
                    float span = b1 - b0;
                    float rise = span * t;
                    // In the roof's frame the slope always climbs toward +b; a roof low on the +b side is the same roof turned round.
                    float lowB = shape.LowAtMin ? b0 : b1;
                    float dir = shape.LowAtMin ? 1.0f : -1.0f;
                    float bLow = lowB - dir * e;
                    float bHigh = lowB + dir * (span + high);
                    float yLow = roofY - e * t;
                    float yHigh = roofY + rise + high * t;
                    Vector3 up = axes.N(0.0f, cos, -dir * sin);
                    buffer.Look = covering;
                    Slope(buffer, axes, up, a0 - v, a1 + v, bLow, yLow + thick, bHigh, yHigh + thick, uvScale);
                    buffer.Look = soffit;
                    Slope(buffer, axes, -up, a0 - v, a1 + v, bLow, yLow, bHigh, yHigh, 1.0f);
                    buffer.Look = trim;
                    Fascia(buffer, axes, axes.N(0.0f, 0.0f, -dir), a0 - v, a1 + v, bLow, yLow, yLow + thick);
                    Fascia(buffer, axes, axes.N(0.0f, 0.0f, dir), a0 - v, a1 + v, bHigh, yHigh, yHigh + thick);
                    Verge(buffer, axes, axes.N(-1.0f, 0.0f, 0.0f), a0 - v, bLow, yLow, bHigh, yHigh, thick);
                    Verge(buffer, axes, axes.N(1.0f, 0.0f, 0.0f), a1 + v, bLow, yLow, bHigh, yHigh, thick);
                    // The high wall carried up under the slope's top, and the two side walls as triangles.
                    buffer.Look = looks.Wall;
                    float highB = lowB + dir * span;
                    AddFace(buffer, axes.N(0.0f, 0.0f, dir), axes.P(a0, roofY, highB), axes.P(a1, roofY, highB), axes.P(a1, roofY + rise, highB),
                        axes.P(a0, roofY + rise, highB));
                    for (int end = 0; end < 2; end++)
                    {
                        float aEnd = end == 0 ? a0 : a1;
                        Vector3 n = axes.N(end == 0 ? -1.0f : 1.0f, 0.0f, 0.0f);
                        Vector3 p0 = axes.P(aEnd, roofY, lowB), p1 = axes.P(aEnd, roofY, highB), p2 = axes.P(aEnd, roofY + rise, highB);
                        buffer.AddTriangle(p0, p1, p2, n, Uv(p0, n), Uv(p1, n), Uv(p2, n));
                    }
                    ridgeB = highB;
                    peak = roofY + rise;
                    chimneyB = highB - dir * 1.2f;
                    break;
                }
            }
            AddRoofTopper(buffer, site, shape, axes, a0, a1, b0, b1, roofY, ridgeB, peak, chimneyB, thick, looks, far);
            buffer.Look = saved;
        }

        /// <summary>
        /// A flat roof with eaves (RoofKind.FlatEaves): the deck's slab carried out past every wall, a fascia round its edge standing a little
        /// over the deck (the gravel stop), a pale soffit under the overhang. No parapet, no coping.
        /// </summary>
        private static void AddFlatEavesRoof(MeshBuffer buffer, BuildingSite site, in RoofShape shape, float x0, float x1, float z0, float z1,
            float roofY)
        {
            float e = shape.Eave;
            float y0 = roofY - RoofShape.FlatEavesThickness, y1 = roofY + RoofShape.FlatEavesLip;
            Color32 saved = buffer.Look;
            Color32 fascia = SurfaceLook.Pack(shape.Tint, (int)WallMaterial.Metal,
                0.25f + 0.3f * (float)WorldHash.Unit01(7, 0, 0x52465757UL, unchecked((ulong)(uint)site.Seed)));
            Color32 soffit = SurfaceLook.Pack(SoffitTint, (int)WallMaterial.Plaster, 0.35f);
            // Four strips round the walls: the south and north ones the full width, corners and all; the west and east ones between them.
            EaveStrip(buffer, fascia, soffit, x0 - e, x1 + e, z0 - e, z0, roofY, y0, y1, Vector3.back, true);
            EaveStrip(buffer, fascia, soffit, x0 - e, x1 + e, z1, z1 + e, roofY, y0, y1, Vector3.forward, true);
            EaveStrip(buffer, fascia, soffit, x0 - e, x0, z0, z1, roofY, y0, y1, Vector3.left, false);
            EaveStrip(buffer, fascia, soffit, x1, x1 + e, z0, z1, roofY, y0, y1, Vector3.right, false);
            buffer.Look = saved;
        }

        /// <summary>
        /// One strip of a flat roof's eaves, the box <paramref name="sx0"/>..<paramref name="sx1"/> by <paramref name="sz0"/>..<paramref name="sz1"/>
        /// from the soffit at <paramref name="y0"/> to the lip at <paramref name="y1"/>: its fascia on the <paramref name="outward"/> side, its
        /// top, its soffit, the lip's inner face over the deck, and (<paramref name="ends"/>) its two ends at the corners.
        /// </summary>
        private static void EaveStrip(MeshBuffer buffer, Color32 fascia, Color32 soffit, float sx0, float sx1, float sz0, float sz1, float roofY,
            float y0, float y1, Vector3 outward, bool ends)
        {
            buffer.Look = fascia;
            bool alongX = outward.z != 0.0f;   // the strip runs along x, its fascia facing -z or +z
            if (alongX)
            {
                float zOut = outward.z < 0.0f ? sz0 : sz1, zIn = outward.z < 0.0f ? sz1 : sz0;
                AddFace(buffer, outward, new Vector3(sx0, y0, zOut), new Vector3(sx1, y0, zOut), new Vector3(sx1, y1, zOut), new Vector3(sx0, y1, zOut));
                AddFace(buffer, -outward, new Vector3(sx0, roofY, zIn), new Vector3(sx1, roofY, zIn), new Vector3(sx1, y1, zIn), new Vector3(sx0, y1, zIn));
            }
            else
            {
                float xOut = outward.x < 0.0f ? sx0 : sx1, xIn = outward.x < 0.0f ? sx1 : sx0;
                AddFace(buffer, outward, new Vector3(xOut, y0, sz0), new Vector3(xOut, y0, sz1), new Vector3(xOut, y1, sz1), new Vector3(xOut, y1, sz0));
                AddFace(buffer, -outward, new Vector3(xIn, roofY, sz0), new Vector3(xIn, roofY, sz1), new Vector3(xIn, y1, sz1), new Vector3(xIn, y1, sz0));
            }
            AddFace(buffer, Vector3.up, new Vector3(sx0, y1, sz0), new Vector3(sx1, y1, sz0), new Vector3(sx1, y1, sz1), new Vector3(sx0, y1, sz1));
            if (ends)
            {
                AddFace(buffer, Vector3.left, new Vector3(sx0, y0, sz0), new Vector3(sx0, y0, sz1), new Vector3(sx0, y1, sz1), new Vector3(sx0, y1, sz0));
                AddFace(buffer, Vector3.right, new Vector3(sx1, y0, sz0), new Vector3(sx1, y0, sz1), new Vector3(sx1, y1, sz1), new Vector3(sx1, y1, sz0));
            }
            buffer.Look = soffit;
            AddFace(buffer, Vector3.down, new Vector3(sx0, y0, sz0), new Vector3(sx1, y0, sz0), new Vector3(sx1, y0, sz1), new Vector3(sx0, y0, sz1));
        }

        /// <summary>
        /// A slope from its low edge (<paramref name="bLow"/>, <paramref name="yLow"/>) to its high one, the whole length <paramref name="aFrom"/> to
        /// <paramref name="aTo"/>; the covering's UVs run along the eaves and up the slope, in metres times <paramref name="uvScale"/>.
        /// </summary>
        private static void Slope(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float aFrom, float aTo, float bLow, float yLow, float bHigh,
            float yHigh, float uvScale)
        {
            float run = Mathf.Sqrt((bHigh - bLow) * (bHigh - bLow) + (yHigh - yLow) * (yHigh - yLow)) * uvScale;
            float u0 = aFrom * uvScale, u1 = aTo * uvScale;
            buffer.AddQuad(axes.P(aFrom, yLow, bLow), axes.P(aTo, yLow, bLow), axes.P(aTo, yHigh, bHigh), axes.P(aFrom, yHigh, bHigh), normal,
                new Vector2(u0, 0.0f), new Vector2(u1, 0.0f), new Vector2(u1, run), new Vector2(u0, run));
        }

        /// <summary>A hip roof's long side: a trapezoid from the eave (its whole length) up to the ridge (its shorter length).</summary>
        private static void HipSide(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float aEave0, float aEave1, float bEave, float yEave,
            float ar0, float ar1, float bRidge, float yRidge, float uvScale)
        {
            float run = Mathf.Sqrt((bRidge - bEave) * (bRidge - bEave) + (yRidge - yEave) * (yRidge - yEave)) * uvScale;
            buffer.AddQuad(axes.P(aEave0, yEave, bEave), axes.P(aEave1, yEave, bEave), axes.P(ar1, yRidge, bRidge), axes.P(ar0, yRidge, bRidge), normal,
                new Vector2(aEave0 * uvScale, 0.0f), new Vector2(aEave1 * uvScale, 0.0f), new Vector2(ar1 * uvScale, run), new Vector2(ar0 * uvScale, run));
        }

        /// <summary>A hip roof's end: a triangle from its eave (across the whole width) up to the ridge's end.</summary>
        private static void HipEnd(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float aEave, float bEave0, float bEave1, float yEave,
            float aApex, float bApex, float yApex, float uvScale)
        {
            float run = Mathf.Sqrt((aApex - aEave) * (aApex - aEave) + (yApex - yEave) * (yApex - yEave)) * uvScale;
            buffer.AddTriangle(axes.P(aEave, yEave, bEave0), axes.P(aEave, yEave, bEave1), axes.P(aApex, yApex, bApex), normal,
                new Vector2(bEave0 * uvScale, 0.0f), new Vector2(bEave1 * uvScale, 0.0f), new Vector2(bApex * uvScale, run));
        }

        /// <summary>The front edge of the eaves along the ridge's direction: a strip the roof's thickness tall.</summary>
        private static void Fascia(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float aFrom, float aTo, float b, float y0, float y1)
        {
            AddFace(buffer, normal, axes.P(aFrom, y0, b), axes.P(aTo, y0, b), axes.P(aTo, y1, b), axes.P(aFrom, y1, b));
        }

        /// <summary>The front edge of a hip's end eave, across the ridge's direction.</summary>
        private static void FasciaAcross(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float a, float bFrom, float bTo, float y0, float y1)
        {
            AddFace(buffer, normal, axes.P(a, y0, bFrom), axes.P(a, y0, bTo), axes.P(a, y1, bTo), axes.P(a, y1, bFrom));
        }

        /// <summary>The edge of a slope at a gable's verge: from the eave up to the ridge, the roof's thickness deep.</summary>
        private static void Verge(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float a, float bLow, float yLow, float bHigh, float yHigh,
            float thick)
        {
            AddFace(buffer, normal, axes.P(a, yLow, bLow), axes.P(a, yHigh, bHigh), axes.P(a, yHigh + thick, bHigh), axes.P(a, yLow + thick, bLow));
        }

        /// <summary>A gable end: the wall carried up as a triangle from the tops of the side walls to the ridge.</summary>
        private static void GableEnd(MeshBuffer buffer, in RoofAxes axes, Vector3 normal, float a, float b0, float b1, float roofY, float ridge)
        {
            Vector3 p0 = axes.P(a, roofY, b0), p1 = axes.P(a, roofY, b1), p2 = axes.P(a, ridge, (b0 + b1) * 0.5f);
            buffer.AddTriangle(p0, p1, p2, normal, Uv(p0, normal), Uv(p1, normal), Uv(p2, normal));
        }

        /// <summary>The cap along the ridge.</summary>
        private static void RidgeCap(MeshBuffer buffer, in RoofAxes axes, Color32 look, float aFrom, float aTo, float b, float y)
        {
            buffer.Look = look;
            Vector3 p = axes.P(aFrom, y - 0.2f, b - RoofShape.RidgeWidth * 0.5f);   // its foot well down into the slopes either side
            Vector3 q = axes.P(aTo, y + RoofShape.RidgeHeight, b + RoofShape.RidgeWidth * 0.5f);
            buffer.AddBox(Vector3.Min(p, q), Vector3.Max(p, q));
        }

        /// <summary>
        /// What the roof's type (BuildingParams.Roof) puts on a pitched roof: a chimney through the slope near the ridge (at <paramref name="chimneyB"/>),
        /// or a television aerial on the ridge (at <paramref name="ridgeB"/>, whose top is at <paramref name="peak"/>). The other types are a flat roof's things.
        /// </summary>
        private static void AddRoofTopper(MeshBuffer buffer, BuildingSite site, in RoofShape shape, in RoofAxes axes, float a0, float a1, float b0,
            float b1, float roofY, float ridgeB, float peak, float chimneyB, float thick, SiteLooks looks, bool far)
        {
            string kind = site.Params.Roof ?? "";
            double die = WorldHash.Unit01(8, 0, 0x52465757UL, unchecked((ulong)(uint)site.Seed));
            float a = Mathf.Lerp(a0 + 1.5f, a1 - 1.5f, 0.2f + 0.6f * (float)die);
            if (shape.Kind == RoofKind.Hip)
            {
                // On the ridge, which a hip roof's ends cut short (an aerial past it stood over the falling end, in the air).
                float s = (b1 - b0) * 0.5f, middle = (a0 + a1) * 0.5f;
                a = Mathf.Clamp(a, Mathf.Min(a0 + s, middle), Mathf.Max(a1 - s, middle));
            }
            if (kind == "chimney")
            {
                // On the slope a metre down from the ridge (or from a skillion's high side); its top well over the ridge.
                float top = peak + thick + 1.1f;
                buffer.Look = looks.Base;
                Vector3 p = axes.P(a - 0.45f, roofY, chimneyB - 0.45f), q = axes.P(a + 0.45f, top, chimneyB + 0.45f);
                buffer.AddBox(Vector3.Min(p, q), Vector3.Max(p, q));
                buffer.Look = looks.Trim;
                p = axes.P(a - 0.55f, top, chimneyB - 0.55f);
                q = axes.P(a + 0.55f, top + 0.12f, chimneyB + 0.55f);
                buffer.AddBox(Vector3.Min(p, q), Vector3.Max(p, q), true);
            }
            else if (kind == "antenna" && shape.Kind != RoofKind.Skillion && !far)
            {
                // A television aerial on the ridge: a mast and three cross bars of falling length.
                float ridgeTop = peak + thick + RoofShape.RidgeHeight;
                buffer.Look = looks.Frame;
                Vector3 m = axes.P(a, ridgeTop, ridgeB);
                AddMast(buffer, m.x, m.z, ridgeTop - 0.1f, 2.4f);
                for (int i = 0; i < 3; i++)
                {
                    float y = ridgeTop + 1.3f + i * 0.4f;
                    float half = 0.7f - i * 0.18f;
                    Vector3 c0 = axes.P(a - half, y, ridgeB - 0.02f), c1 = axes.P(a + half, y + 0.04f, ridgeB + 0.02f);
                    buffer.AddBox(Vector3.Min(c0, c1), Vector3.Max(c0, c1), true);
                }
            }
        }
    }
}
