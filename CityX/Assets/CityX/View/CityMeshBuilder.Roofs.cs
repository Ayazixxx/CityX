using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A pitched roof (World/RoofShape): gable, hip, skillion, or a warehouse's low gable, standing on the top of the walls in place of the parapet
    /// and the roof plant. In the wall buffer: it draws with the facade in the same draw call, and the chunk's collider takes it (a 2.6 m body
    /// walks up any of them: the steepest is 32 degrees).
    ///
    /// The underside of each slope passes through the top edge of the walls, so the roof closes the building with the top floor's ceiling under
    /// it (the attic is shut: no stair comes up to it). Past the walls it is the eaves, and their underside (the soffit) and front edge (the
    /// fascia) are drawn. A gable or a skillion closes its ends with the wall carried up as a triangle, in the wall's own look.
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
                    break;
                }
                case RoofKind.Skillion:
                {
                    // One slope across the whole span, low over one long wall and high over the other.
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
                    break;
                }
            }
            AddRoofTopper(buffer, site, shape, axes, a0, a1, b0, b1, roofY, t, thick, looks, far);
            buffer.Look = saved;
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
        /// What the roof's type (BuildingParams.Roof) puts on a pitched roof: a chimney through the slope near the ridge, or a television aerial
        /// on the ridge. The other types are a flat roof's things.
        /// </summary>
        private static void AddRoofTopper(MeshBuffer buffer, BuildingSite site, in RoofShape shape, in RoofAxes axes, float a0, float a1, float b0,
            float b1, float roofY, float t, float thick, SiteLooks looks, bool far)
        {
            string kind = site.Params.Roof ?? "";
            double die = WorldHash.Unit01(8, 0, 0x52465757UL, unchecked((ulong)(uint)site.Seed));
            float a = Mathf.Lerp(a0 + 1.5f, a1 - 1.5f, 0.2f + 0.6f * (float)die);
            float bc = (b0 + b1) * 0.5f;
            if (shape.Kind == RoofKind.Hip)
            {
                // On the ridge, which a hip roof's ends cut short (an aerial past it stood over the falling end, in the air).
                float s = (b1 - b0) * 0.5f, middle = (a0 + a1) * 0.5f;
                a = Mathf.Clamp(a, Mathf.Min(a0 + s, middle), Mathf.Max(a1 - s, middle));
            }
            // The roof's highest line: the ridge, or a skillion's high side.
            float peak = roofY + (shape.Kind == RoofKind.Skillion ? (b1 - b0) : (b1 - b0) * 0.5f) * t;
            if (kind == "chimney")
            {
                // On the slope a metre down from the ridge (or from a skillion's high side); its top well over the ridge.
                float b = shape.Kind == RoofKind.Skillion ? (shape.LowAtMin ? b1 - 1.2f : b0 + 1.2f) : bc - 1.0f;
                float top = peak + thick + 1.1f;
                buffer.Look = looks.Base;
                Vector3 p = axes.P(a - 0.45f, roofY, b - 0.45f), q = axes.P(a + 0.45f, top, b + 0.45f);
                buffer.AddBox(Vector3.Min(p, q), Vector3.Max(p, q));
                buffer.Look = looks.Trim;
                p = axes.P(a - 0.55f, top, b - 0.55f);
                q = axes.P(a + 0.55f, top + 0.12f, b + 0.55f);
                buffer.AddBox(Vector3.Min(p, q), Vector3.Max(p, q), true);
            }
            else if (kind == "antenna" && shape.Kind != RoofKind.Skillion && !far)
            {
                // A television aerial on the ridge: a mast and three cross bars of falling length.
                float ridgeTop = peak + thick + RoofShape.RidgeHeight;
                buffer.Look = looks.Frame;
                Vector3 m = axes.P(a, ridgeTop, bc);
                AddMast(buffer, m.x, m.z, ridgeTop - 0.1f, 2.4f);
                for (int i = 0; i < 3; i++)
                {
                    float y = ridgeTop + 1.3f + i * 0.4f;
                    float half = 0.7f - i * 0.18f;
                    Vector3 c0 = axes.P(a - half, y, bc - 0.02f), c1 = axes.P(a + half, y + 0.04f, bc + 0.02f);
                    buffer.AddBox(Vector3.Min(c0, c1), Vector3.Max(c0, c1), true);
                }
            }
        }
    }
}
