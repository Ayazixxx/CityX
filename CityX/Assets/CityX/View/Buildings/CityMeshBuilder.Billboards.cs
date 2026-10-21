using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A billboard on a flat roof (World/RoofBillboards, World Gacha phase 4): the board facing the street, its ad painted on in two colours, the
    /// steel behind it (legs, struts, rails), a catwalk with a railing in front and, mostly, floodlights on the railing turned on the board. Boxes
    /// like the rest of the roof plant; in the wall buffer and the collider. On a building with power a floodlit board is lit at night: its
    /// lamps' lenses glow and its face takes their light in its own colours (SurfaceLook.PackFloodlit), a pool over each lamp fading going up.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        // The ad: the strong colours of a street's billboards (the walls keep theirs restrained).
        private static readonly Color[] AdColours =
        {
            new Color(0.86f, 0.13f, 0.11f), new Color(0.95f, 0.78f, 0.12f), new Color(0.93f, 0.93f, 0.90f), new Color(0.10f, 0.32f, 0.70f),
            new Color(0.12f, 0.55f, 0.30f), new Color(0.93f, 0.48f, 0.10f), new Color(0.08f, 0.08f, 0.09f), new Color(0.80f, 0.22f, 0.55f),
            new Color(0.10f, 0.62f, 0.66f),
        };

        private static readonly Color BillboardSteel = new Color(0.32f, 0.33f, 0.35f);

        // The back and edges of the board, a lighter galvanised grey than the steel holding it up; its face is a skin of paint this thick.
        private static readonly Color BillboardBack = new Color(0.50f, 0.51f, 0.52f);

        private const float PaintThickness = 0.01f;

        /// <summary>
        /// The ground a building's billboard takes on its roof (in the builder's frame: x across, z from the back of its legs to the front of its
        /// catwalk), widened a little, or Rect.zero for none. Pure: AddRoof keeps the roof plant off it and leaves the board off a roof where it
        /// would stand on the stair's hatch.
        /// </summary>
        private static Rect BillboardGround(in RoofBillboard b, float x0, float x1, float z1)
        {
            if (!b.Exists)
            {
                return Rect.zero;
            }
            float cx = (x0 + x1) * 0.5f + b.Offset;
            float front = z1 - RoofBillboards.SetBack;
            float legsBack = front - RoofBillboards.Thickness - RoofBillboards.LegsBehind - RoofBillboards.LegSize;
            return Rect.MinMaxRect(cx - b.Width * 0.5f - 0.4f, legsBack - 0.4f, cx + b.Width * 0.5f + 0.4f, front + RoofBillboards.CatwalkDepth);
        }

        /// <summary>
        /// The billboard (<paramref name="b"/>) on a roof at <paramref name="roofY"/> whose street wall is at <paramref name="z1"/> (the builder's
        /// frame: the street is +Z). <paramref name="lit"/> takes the floodlit face and the lamps' lenses when the building has power (else null:
        /// they are dark). <paramref name="far"/> leaves the small things off (the catwalk, its railing, the lamps), not the floodlit face.
        /// </summary>
        private static void AddRoofBillboard(MeshBuffer buffer, MeshBuffer lit, in RoofBillboard b, float x0, float x1, float z1, float roofY, bool far)
        {
            Color32 saved = buffer.Look;
            float cx = (x0 + x1) * 0.5f + b.Offset;
            float bx0 = cx - b.Width * 0.5f, bx1 = cx + b.Width * 0.5f;
            float front = z1 - RoofBillboards.SetBack;
            float back = front - RoofBillboards.Thickness;
            float yb = roofY + b.Lift, yt = yb + b.Height;

            // The board: a grey steel panel, its face painted in the ad's background colour.
            buffer.Look = SurfaceLook.Pack(BillboardBack, (int)WallMaterial.Metal, 0.4f);
            buffer.AddBox(new Vector3(bx0, yb, back), new Vector3(bx1, yt, front - PaintThickness), true);
            buffer.Look = SurfaceLook.Pack(AdColours[b.Background % AdColours.Length], (int)WallMaterial.Plaster, 0.12f);
            buffer.AddBox(new Vector3(bx0, yb, front - PaintThickness), new Vector3(bx1, yt, front), true);

            // The ad's accent, a centimetre proud of the face.
            buffer.Look = SurfaceLook.Pack(AdColours[b.Accent % AdColours.Length], (int)WallMaterial.Plaster, 0.08f);
            for (int i = 0; AccentPart(b.Layout, i, bx0, bx1, yb, yt, out Rect part); i++)
            {
                buffer.AddBox(new Vector3(part.xMin, part.yMin, front), new Vector3(part.xMax, part.yMax, front + AccentProud));
            }

            // Its lettering, prouder still: a headline and a line or two more, in white or black, whichever stands out from what it is on.
            for (int i = 0; TextLine(b, i, bx0, bx1, yb, yt, out Rect line, out bool onAccent); i++)
            {
                buffer.Look = SurfaceLook.Pack(LetteringOn(onAccent ? b.Accent : b.Background), (int)WallMaterial.Plaster, 0.08f);
                buffer.AddBox(new Vector3(line.xMin, line.yMin, front), new Vector3(line.xMax, line.yMax, front + TextProud));
            }

            // Lit at night by its lamps on a building with power (SurfaceLook.PackFloodlit), each colour's light just in front of its own paint
            // and behind whatever stands prouder (the background's behind the accent's face, the accent's behind the lettering's), so what is in
            // front hides it. Far away too: lit boards are seen from afar.
            int lamps = Mathf.Max(2, Mathf.RoundToInt(b.Width / 3.5f));
            if (lit != null && b.Floodlit)
            {
                Color32 savedLit = lit.Look;
                lit.Look = SurfaceLook.PackFloodlit(AdColours[b.Background % AdColours.Length]);
                AddFloodWash(lit, Rect.MinMaxRect(bx0, yb, bx1, yt), front + WashProud, bx0, bx1, yb, yt, lamps);
                lit.Look = SurfaceLook.PackFloodlit(AdColours[b.Accent % AdColours.Length]);
                for (int i = 0; AccentPart(b.Layout, i, bx0, bx1, yb, yt, out Rect part); i++)
                {
                    AddFloodWash(lit, part, front + AccentProud + WashProud, bx0, bx1, yb, yt, lamps);
                }
                for (int i = 0; TextLine(b, i, bx0, bx1, yb, yt, out Rect line, out bool onAccent); i++)
                {
                    lit.Look = SurfaceLook.PackFloodlit(LetteringOn(onAccent ? b.Accent : b.Background));
                    AddFloodWash(lit, line, front + TextProud + WashProud, bx0, bx1, yb, yt, lamps);
                }
                lit.Look = savedLit;
            }

            // The steel behind it: legs from the roof to near its top, struts from each leg to the board, rails along its back.
            buffer.Look = SurfaceLook.Pack(BillboardSteel, (int)WallMaterial.Metal, 0.55f);
            int legs = b.Width > 9.0f ? 3 : 2;
            float legZ1 = back - RoofBillboards.LegsBehind, legZ0 = legZ1 - RoofBillboards.LegSize;
            for (int i = 0; i < legs; i++)
            {
                float lx = Mathf.Lerp(bx0 + 0.8f, bx1 - 0.8f, i / (float)(legs - 1));
                float half = RoofBillboards.LegSize * 0.5f;
                buffer.AddBox(new Vector3(lx - half, roofY, legZ0), new Vector3(lx + half, yt - 0.3f, legZ1));
                foreach (float sy in new[] { yb + 0.3f, yt - 0.6f })
                {
                    buffer.AddBox(new Vector3(lx - 0.06f, sy, legZ1), new Vector3(lx + 0.06f, sy + 0.12f, back), true);
                }
            }
            buffer.AddBox(new Vector3(bx0, yb + 0.2f, back - 0.12f), new Vector3(bx1, yb + 0.32f, back), true);
            buffer.AddBox(new Vector3(bx0, yt - 0.7f, back - 0.12f), new Vector3(bx1, yt - 0.58f, back), true);

            if (!far)
            {
                // The catwalk in front, a grating on brackets, with a railing; floodlights on the railing turned on the board.
                float cy = yb - RoofBillboards.CatwalkBelow;
                float cz1 = front + RoofBillboards.CatwalkDepth;
                buffer.AddBox(new Vector3(bx0, cy, front), new Vector3(bx1, cy + 0.06f, cz1), true);
                buffer.AddBox(new Vector3(bx0, cy + 1.05f, cz1 - 0.05f), new Vector3(bx1, cy + 1.10f, cz1));
                int posts = Mathf.Max(2, Mathf.CeilToInt(b.Width / 2.0f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float px = Mathf.Lerp(bx0 + 0.03f, bx1 - 0.03f, i / (float)(posts - 1));
                    buffer.AddBox(new Vector3(px - 0.025f, cy + 0.06f, cz1 - 0.05f), new Vector3(px + 0.025f, cy + 1.05f, cz1));
                }
                if (b.Floodlit)
                {
                    for (int i = 0; i < lamps; i++)
                    {
                        float lx = Mathf.Lerp(bx0, bx1, (i + 0.5f) / lamps);
                        // An arm up from the railing, the lamp head on it leaning toward the board.
                        buffer.AddBox(new Vector3(lx - 0.025f, cy + 1.10f, cz1 - 0.05f), new Vector3(lx + 0.025f, cy + 1.55f, cz1));
                        Vector3 h0 = new Vector3(lx - 0.18f, cy + 1.50f, cz1 - 0.25f), h1 = new Vector3(lx + 0.18f, cy + 1.72f, cz1 + 0.02f);
                        buffer.AddBox(h0, h1, true);
                        if (lit != null)
                        {
                            // The lens, on the head's face toward the board.
                            Color32 savedLit = lit.Look;
                            lit.Look = SurfaceLook.PackGlass(0, 7, 0.0f);
                            float lz = h0.z - 0.004f;
                            AddFace(lit, Vector3.back, new Vector3(h0.x + 0.03f, h0.y + 0.03f, lz), new Vector3(h1.x - 0.03f, h0.y + 0.03f, lz),
                                new Vector3(h1.x - 0.03f, h1.y - 0.03f, lz), new Vector3(h0.x + 0.03f, h1.y - 0.03f, lz));
                            lit.Look = savedLit;
                        }
                    }
                }
            }
            buffer.Look = saved;
        }

        /// <summary>How far the ad's accent and its lettering stand proud of the board's face, and a floodlit face of the paint under it, metres.</summary>
        private const float AccentProud = 0.012f;

        private const float TextProud = 0.024f;

        private const float WashProud = 0.006f;

        /// <summary>The lettering on a paint of the ad's palette: black on a light one, white on a dark one.</summary>
        private static Color LetteringOn(int paint)
        {
            Color c = AdColours[paint % AdColours.Length];
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b > 0.5f ? AdColours[6] : AdColours[2];
        }

        /// <summary>
        /// The <paramref name="i"/>th line of a board's lettering (x across, y up), false past the last, and whether it is on the accent (else on
        /// the background): a headline and a shorter line or two, laid out round the accent -- on the band, beside the block, on the framed
        /// block and under it. How long each line is comes off the board's <see cref="RoofBillboard.Copy"/>.
        /// </summary>
        private static bool TextLine(in RoofBillboard b, int i, float bx0, float bx1, float yb, float yt, out Rect line, out bool onAccent)
        {
            float w = bx1 - bx0, h = yt - yb;
            float d0 = CopyDie(b.Copy, 0), d1 = CopyDie(b.Copy, 1), d2 = CopyDie(b.Copy, 2), d3 = CopyDie(b.Copy, 3);
            line = default;
            onAccent = false;
            float x0, length;
            switch (b.Layout)
            {
                case 0:
                    // A headline on the band, a line over it, now and then one under it; centred.
                    if (i == 0) { onAccent = true; length = w * (0.55f + 0.3f * d0); line = Rect.MinMaxRect(-length * 0.5f, 0.44f * h, length * 0.5f, 0.56f * h); }
                    else if (i == 1) { length = w * (0.3f + 0.25f * d1); line = Rect.MinMaxRect(-length * 0.5f, 0.76f * h, length * 0.5f, 0.84f * h); }
                    else if (i == 2 && d3 > 0.5f) { length = w * (0.2f + 0.2f * d2); line = Rect.MinMaxRect(-length * 0.5f, 0.16f * h, length * 0.5f, 0.22f * h); }
                    else return false;
                    break;
                case 1:
                case 2:
                    // Beside the block, from the left: a headline, a shorter line under it, now and then a third.
                    x0 = b.Layout == 1 ? 0.44f * w : 0.05f * w;
                    float room = 0.51f * w;
                    if (i == 0) { length = room * (0.75f + 0.25f * d0); line = Rect.MinMaxRect(x0, 0.62f * h, x0 + length, 0.76f * h); }
                    else if (i == 1) { length = room * (0.5f + 0.35f * d1); line = Rect.MinMaxRect(x0, 0.44f * h, x0 + length, 0.52f * h); }
                    else if (i == 2 && d3 > 0.35f) { length = room * (0.35f + 0.35f * d2); line = Rect.MinMaxRect(x0, 0.30f * h, x0 + length, 0.38f * h); }
                    else return false;
                    line.x -= w * 0.5f;   // to the middle-relative frame the others are in
                    break;
                default:
                    // A headline on the framed block, a line under the block, now and then one over it; centred.
                    if (i == 0) { onAccent = true; length = 0.4f * w * (0.6f + 0.25f * d0); line = Rect.MinMaxRect(-length * 0.5f, 0.45f * h, length * 0.5f, 0.56f * h); }
                    else if (i == 1) { length = w * (0.25f + 0.2f * d1); line = Rect.MinMaxRect(-length * 0.5f, 0.14f * h, length * 0.5f, 0.21f * h); }
                    else if (i == 2 && d3 > 0.5f) { length = w * (0.2f + 0.25f * d2); line = Rect.MinMaxRect(-length * 0.5f, 0.79f * h, length * 0.5f, 0.85f * h); }
                    else return false;
                    break;
            }
            line.x += (bx0 + bx1) * 0.5f;
            line.y += yb;
            return true;
        }

        /// <summary>The <paramref name="k"/>th die, 0..1, a board's lettering reads off its <see cref="RoofBillboard.Copy"/>.</summary>
        private static float CopyDie(float copy, int k)
        {
            return Mathf.Repeat(copy * (k + 1) * 7.31f + k * 0.37f, 1.0f);
        }

        /// <summary>
        /// The <paramref name="i"/>th piece of a board's accent (x across, y up), false past the last: by its <paramref name="layout"/>, a band
        /// across the middle, a block at the left or the right end, or a border round a block in the middle.
        /// </summary>
        private static bool AccentPart(int layout, int i, float bx0, float bx1, float yb, float yt, out Rect part)
        {
            float w = bx1 - bx0, h = yt - yb;
            const float rim = 0.18f;
            part = default;
            switch (layout)
            {
                case 0:
                    if (i == 0) part = Rect.MinMaxRect(bx0, yb + h * 0.36f, bx1, yb + h * 0.64f);
                    return i == 0;
                case 1:
                    if (i == 0) part = Rect.MinMaxRect(bx0, yb, bx0 + w * 0.38f, yt);
                    return i == 0;
                case 2:
                    if (i == 0) part = Rect.MinMaxRect(bx1 - w * 0.38f, yb, bx1, yt);
                    return i == 0;
                default:
                    switch (i)
                    {
                        case 0: part = Rect.MinMaxRect(bx0, yb, bx1, yb + rim); return true;
                        case 1: part = Rect.MinMaxRect(bx0, yt - rim, bx1, yt); return true;
                        case 2: part = Rect.MinMaxRect(bx0, yb + rim, bx0 + rim, yt - rim); return true;
                        case 3: part = Rect.MinMaxRect(bx1 - rim, yb + rim, bx1, yt - rim); return true;
                        case 4: part = Rect.MinMaxRect(bx0 + w * 0.3f, yb + h * 0.3f, bx1 - w * 0.3f, yt - h * 0.3f); return true;
                        default: return false;
                    }
            }
        }

        /// <summary>
        /// One floodlit face (SurfaceLook.PackFloodlit, in <paramref name="lit"/>'s current look) over <paramref name="r"/> of a board from
        /// <paramref name="bx0"/> to <paramref name="bx1"/> and <paramref name="yb"/> to <paramref name="yt"/>, at <paramref name="z"/>, facing the
        /// street: UV'd as the shader reads it, x counting the board's <paramref name="lamps"/> along it, y from its foot (0) to its top (1).
        /// </summary>
        private static void AddFloodWash(MeshBuffer lit, Rect r, float z, float bx0, float bx1, float yb, float yt, int lamps)
        {
            float perX = lamps / (bx1 - bx0), perY = 1.0f / (yt - yb);
            Vector2 shift = lit.UvOffset;
            lit.AddQuad(new Vector3(r.xMin, r.yMin, z), new Vector3(r.xMax, r.yMin, z), new Vector3(r.xMax, r.yMax, z), new Vector3(r.xMin, r.yMax, z),
                Vector3.forward,
                new Vector2((r.xMin - bx0) * perX, (r.yMin - yb) * perY) - shift, new Vector2((r.xMax - bx0) * perX, (r.yMin - yb) * perY) - shift,
                new Vector2((r.xMax - bx0) * perX, (r.yMax - yb) * perY) - shift, new Vector2((r.xMin - bx0) * perX, (r.yMax - yb) * perY) - shift);
        }
    }
}
