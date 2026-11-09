using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// How one surface of the city is to look, packed into the one vertex colour a mesh vertex carries.
    ///
    /// THE POINT IS TO GET A HUNDRED LOOKS OUT OF ONE MATERIAL. The frame rate in this game follows draw calls, not triangles
    /// (docs/PERFORMANCE.md), so a brick building, a plaster building and a metal-clad building cannot be three materials. They are
    /// the same facade shader, which reads the look off each vertex: which of the eight wall materials in the texture array to draw,
    /// what colour to tint it, and how weathered it is. A building stamps its look on its buffer (<see cref="MeshBuffer.Look"/>) and
    /// every vertex it writes carries it.
    ///
    ///   rgb    the tint, as an authoring (gamma) colour: the shader multiplies the material by it
    ///   alpha  255 minus a code, where code = slice * 16 + indoors * 8 + wear (slice 0..15, indoors 0 or 1, wear 0..7, 7 = filthy)
    ///
    /// INDOORS marks a surface inside a building (2026-10-09, the lighting pass): the facade shader dims the sky's light on it (a room is not
    /// lit by the open sky the way the street is) and lets only the lights inside the building reach it, so a lit room never shines through
    /// its own walls onto the street (CityLightLayers). Wear went from 16 steps to 8 to make room for it: nobody can tell the difference.
    ///
    /// ALPHA IS INVERTED so that the colour Unity gives a mesh with no vertex colours at all, opaque white, decodes to slice 0 with no
    /// wear and a white tint: a buffer nobody styled looks like plain material, exactly as it did before there were looks.
    /// </summary>
    public static class SurfaceLook
    {
        /// <summary>Materials in the facade texture array: eight wall materials, then paving, asphalt, bare earth and setts, then four old bricks.</summary>
        public const int Slices = 16;

        /// <summary>The ground's materials, after the eight wall ones (WallMaterial in World): paving slabs, road asphalt, bare earth, granite setts.</summary>
        public const int Paving = 8;
        public const int Asphalt = 9;
        public const int Earth = 10;
        public const int Setts = 11;

        /// <summary>Old brick, as the sewers are built of, in four colours from here on: grey, blue, grey-yellow, green-black (FacadeTextures).</summary>
        public const int OldBrick = 12;
        public const int OldBrickColours = 4;

        /// <summary>Steps of wear a look can carry (0 = clean .. 7 = filthy).</summary>
        public const int WearLevels = 8;

        /// <summary>The bit of the code that marks a surface as inside a building.</summary>
        public const int IndoorBit = 8;

        /// <summary>How many codes one slice spans (its wear steps, indoors and out).</summary>
        private const int CodesPerSlice = 16;

        /// <summary>White, slice 0, clean, outdoors: what an unstyled vertex is.</summary>
        public static readonly Color32 Default = new Color32(255, 255, 255, 255);

        public static Color32 Pack(Color tint, int slice, float wear, bool indoors = false)
        {
            return Pack(new Color32(Byte(tint.r), Byte(tint.g), Byte(tint.b), 255), slice, wear, indoors);
        }

        public static Color32 Pack(Color32 tint, int slice, float wear, bool indoors = false)
        {
            int s = Mathf.Clamp(slice, 0, Slices - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(wear * (WearLevels - 1)), 0, WearLevels - 1);
            int code = s * CodesPerSlice + (indoors ? IndoorBit : 0) + w;
            return new Color32(tint.r, tint.g, tint.b, (byte)(255 - code));
        }

        /// <summary>Reads a packed look back. The same arithmetic the shader does.</summary>
        public static void Unpack(Color32 look, out Color32 tint, out int slice, out float wear)
        {
            Unpack(look, out tint, out slice, out wear, out _);
        }

        public static void Unpack(Color32 look, out Color32 tint, out int slice, out float wear, out bool indoors)
        {
            int code = 255 - look.a;
            slice = code / CodesPerSlice;
            int rest = code % CodesPerSlice;
            indoors = (rest & IndoorBit) != 0;
            wear = (rest & (IndoorBit - 1)) / (float)(WearLevels - 1);
            tint = new Color32(look.r, look.g, look.b, 255);
        }

        /// <summary>Whether a packed look is of a surface inside a building.</summary>
        public static bool IsIndoors(Color32 look)
        {
            return ((255 - look.a) & IndoorBit) != 0;
        }

        /// <summary>
        /// The look of a window's glass (read by CityX/Glass): its tint (0..7), the kind of light it shows when lit (0..7), and how far a
        /// blind is drawn down it (0..1). Never 255 in both of the last two bytes, which is what an unstyled vertex reads as.
        /// </summary>
        /// <remarks>
        /// <paramref name="clear"/> (the red byte) marks glass with a real room behind it: the shader lets more of what is behind it through, so a lit
        /// room is seen as a room from the street (2026-10-09). Glass with nothing behind it (a distant building's strips laid on its wall) stays dark.
        /// </remarks>
        public static Color32 PackGlass(int tint, int light, float blind, bool clear = false)
        {
            int b = (Mathf.Clamp(tint, 0, 7) << 4) | Mathf.Clamp(light, 0, 7);
            int a = Mathf.Clamp(Mathf.RoundToInt(blind * 200.0f), 0, 200);
            return new Color32(clear ? (byte)255 : (byte)0, 0, (byte)b, (byte)a);
        }

        /// <summary>
        /// The blue byte that marks a FLOODLIT face for CityX/Glass: a light colour past the eight real ones (PackGlass keeps its lights to 0..7).
        /// </summary>
        public const byte FloodlitMarker = 0xF8;

        /// <summary>
        /// The look of a FLOODLIT face (read by CityX/Glass, 2026-10-15): not glass but the light lamps throw on the opaque paint just behind it,
        /// added at night in <paramref name="paint"/>'s own colour (carried in the red, green and alpha bytes) and nothing by day. A billboard
        /// lit from the lamps along its foot: UV'd with x counting the lamps along it (a pool of light over each, at the middle of each whole
        /// step) and y from 0 at its foot to 1 at its top (it fades going up).
        /// </summary>
        public static Color32 PackFloodlit(Color paint)
        {
            return new Color32(Byte(paint.r), Byte(paint.g), FloodlitMarker, Byte(paint.b));
        }

        /// <summary>Whether a packed glass look is a floodlit face (<see cref="PackFloodlit"/>).</summary>
        public static bool IsFloodlit(Color32 look)
        {
            return look.b == FloodlitMarker;
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255.0f), 0, 255);
        }
    }
}
