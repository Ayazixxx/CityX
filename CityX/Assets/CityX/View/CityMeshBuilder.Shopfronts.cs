using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A street of shops (World/Shopfronts, World Gacha phase 3): a shop with its roller shutter down -- a slatted metal curtain in a shallow recess
    /// from the pavement up, the box it rolls into over it -- and the band of shop signs over the shopfronts, one per shop in its own colours, some
    /// lit from behind (lit glass: dark by day, glowing at night like the windows).
    /// </summary>
    public static partial class CityMeshBuilder
    {
        // Shutters: mostly bare galvanised steel, now and then painted.
        private static readonly Color[] ShutterColours =
        {
            new Color(0.62f, 0.63f, 0.64f), new Color(0.62f, 0.63f, 0.64f), new Color(0.55f, 0.57f, 0.58f), new Color(0.70f, 0.70f, 0.68f),
            new Color(0.30f, 0.42f, 0.36f), new Color(0.32f, 0.38f, 0.50f), new Color(0.66f, 0.60f, 0.48f),
        };

        // What a lit sign glows with (kLightColours in the glass shader): pink, blue, amber, the whites.
        private static readonly int[] SignLights = { 6, 3, 4, 2, 7, 0, 6, 3 };

        // A shop sign's board: where a street of muted walls gets its colour (the walls keep theirs restrained, BuildingLook). White and cream
        // boards most often, then the reds, yellows, blues and greens of a Japanese shopping street.
        private static readonly Color[] ShopSignColours =
        {
            new Color(0.88f, 0.87f, 0.84f), new Color(0.88f, 0.87f, 0.84f), new Color(0.84f, 0.79f, 0.66f), new Color(0.66f, 0.13f, 0.11f),
            new Color(0.86f, 0.70f, 0.16f), new Color(0.13f, 0.29f, 0.56f), new Color(0.16f, 0.43f, 0.26f), new Color(0.82f, 0.43f, 0.13f),
            new Color(0.11f, 0.11f, 0.12f), new Color(0.48f, 0.12f, 0.20f),
        };

        /// <summary>
        /// A shop's roller shutter, down: from <paramref name="y0"/> (the pavement) to <paramref name="y1"/> across <paramref name="u0"/>..
        /// <paramref name="u1"/>, <see cref="Shopfronts.ShutterRecess"/> in from the wall's face, with the recess's sides and head, and the box over
        /// it. The slats are the metal cladding's own seams turned on their side and drawn four times as close (one every 12.5 cm).
        /// </summary>
        private static void AddShutter(MeshBuffer buffer, BuildingSite site, in Wall wall, float u0, float u1, float y0, float y1, Vector3 outward,
            int column)
        {
            Color32 saved = buffer.Look;
            double tone = Shopfronts.SignDie(site, 100 + column, 7);
            Color colour = ShutterColours[Mathf.Min(ShutterColours.Length - 1, (int)(Shopfronts.SignDie(site, 0, 6) * ShutterColours.Length))];
            buffer.Look = SurfaceLook.Pack(colour, (int)WallMaterial.Metal, 0.25f + 0.45f * (float)tone);
            Wall curtain = Inward(wall, outward, Shopfronts.ShutterRecess);
            Vector3 along = wall.RunsInZ ? Vector3.forward : Vector3.right;

            // The curtain: one quad, its texture turned (U up, V along) so the cladding's 0.5 m seams run across it, four times as close.
            buffer.AddQuad(curtain.At(u0, y0), curtain.At(u1, y0), curtain.At(u1, y1), curtain.At(u0, y1), outward,
                new Vector2(y0 * 4.0f, u0 * 0.1f), new Vector2(y0 * 4.0f, u1 * 0.1f), new Vector2(y1 * 4.0f, u1 * 0.1f), new Vector2(y1 * 4.0f, u0 * 0.1f));

            // The recess it hangs in: its two sides and its head.
            AddFace(buffer, along, wall.At(u0, y0), curtain.At(u0, y0), curtain.At(u0, y1), wall.At(u0, y1));
            AddFace(buffer, -along, wall.At(u1, y0), curtain.At(u1, y0), curtain.At(u1, y1), wall.At(u1, y1));
            AddFace(buffer, Vector3.down, wall.At(u0, y1), wall.At(u1, y1), curtain.At(u1, y1), curtain.At(u0, y1));

            // The box it rolls up into, on the wall over the opening.
            AddOutset(buffer, wall, outward, u0 - 0.05f, u1 + 0.05f, y1, y1 + Shopfronts.ShutterBoxHeight, Shopfronts.ShutterBoxDepth, true);
            buffer.Look = saved;
        }

        /// <summary>
        /// The band of shop signs along one stretch of shopfront (<paramref name="u0"/>..<paramref name="u1"/>, beside the door), from
        /// <paramref name="bottom"/> to <paramref name="top"/>: about one sign per <see cref="Shopfronts.MetresPerSign"/>, each its own colour, and
        /// some of them lit -- a panel of lit glass on the board's face, inside a rim of the board. <paramref name="first"/> numbers the signs
        /// along the whole shopfront (each sign rolls its own dice).
        /// </summary>
        private static int AddSignBand(MeshBuffer buffer, MeshBuffer lit, BuildingSite site, in Wall wall, float u0, float u1, float bottom, float top,
            Vector3 outward, int first)
        {
            float length = u1 - u0;
            if (length < 0.8f)
            {
                return first;
            }
            int signs = Mathf.Clamp(Mathf.RoundToInt(length / Shopfronts.MetresPerSign), 1, 6);
            float width = (length - (signs - 1) * Shopfronts.SignGap) / signs;
            const float proud = 0.07f;
            Color32 saved = buffer.Look;
            for (int i = 0; i < signs; i++)
            {
                int sign = first + i;
                float a = u0 + i * (width + Shopfronts.SignGap), b = a + width;
                Color colour = ShopSignColours[Mathf.Min(ShopSignColours.Length - 1, (int)(Shopfronts.SignDie(site, sign, 0) * ShopSignColours.Length))];
                // A board's depth and height vary a little from shop to shop, so the band reads as many signs, not one.
                float depth = proud + 0.04f * (float)Shopfronts.SignDie(site, sign, 1);
                float drop = 0.12f * (float)Shopfronts.SignDie(site, sign, 2);
                buffer.Look = SurfaceLook.Pack(colour, (int)WallMaterial.Metal, 0.25f);
                Vector3 b0 = wall.At(a, bottom + drop);
                Vector3 b1 = wall.At(b, top) + outward * depth;
                buffer.AddBox(Vector3.Min(b0, b1), Vector3.Max(b0, b1));
                if (lit != null && Shopfronts.SignDie(site, sign, 3) < Shopfronts.LitSignShare && b - a > 0.6f)
                {
                    // Lit from behind: the face inside a 10 cm rim of the board is lit glass.
                    Color32 savedLit = lit.Look;
                    lit.Look = SurfaceLook.PackGlass(0, SignLights[Mathf.Min(SignLights.Length - 1, (int)(Shopfronts.SignDie(site, sign, 4) * SignLights.Length))], 0.0f);
                    Vector3 face = outward * (depth + 0.004f);
                    AddFace(lit, outward, wall.At(a + 0.1f, bottom + drop + 0.1f) + face, wall.At(b - 0.1f, bottom + drop + 0.1f) + face,
                        wall.At(b - 0.1f, top - 0.1f) + face, wall.At(a + 0.1f, top - 0.1f) + face);
                    lit.Look = savedLit;
                }
            }
            buffer.Look = saved;
            return first + signs;
        }
    }
}
