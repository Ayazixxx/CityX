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
    ///   alpha  255 minus a code, where code = slice * 16 + sheltered * 8 + wear (slice 0..15, sheltered 0 or 1, wear 0..7, 7 = filthy)
    ///
    /// INDOORS marks a surface inside a building (2026-10-09, the lighting pass): the facade shader dims the sky's light on it (a room is not
    /// lit by the open sky the way the street is) and lets only the lights inside the building reach it, so a lit room never shines through
    /// its own walls onto the street (CityLightLayers). Wear went from 16 steps to 8 to make room for it: nobody can tell the difference.
    ///
    /// UNDER THE GROUND (the sewers) is the same bit with the filthy half of the wear: a sheltered surface worn <see cref="UndergroundWear"/>
    /// or more is under the ground, one worn less is a building's inside. Rooms are kept clean and tunnels never are, so neither misses the
    /// half it gave up, and all sixteen materials stay. The facade shader gives such a surface nothing of the sky at all, no sun, no moon
    /// and none of the city's own lamps: what lights a tunnel is what shines in it.
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

        /// <summary>The bit of the code that marks a surface as out of the sky's light: inside a building, or under the ground.</summary>
        public const int IndoorBit = 8;

        /// <summary>The step of wear from which a sheltered surface is under the ground; one worn less is inside a building.</summary>
        public const int UndergroundWear = 4;

        /// <summary>How many codes one slice spans (its wear steps, sheltered and not).</summary>
        private const int CodesPerSlice = 16;

        /// <summary>White, slice 0, clean, outdoors: what an unstyled vertex is.</summary>
        public static readonly Color32 Default = new Color32(255, 255, 255, 255);

        public static Color32 Pack(Color tint, int slice, float wear, bool indoors = false)
        {
            return Pack(Bytes(tint), slice, wear, indoors);
        }

        /// <summary>A look out of doors, or (<paramref name="indoors"/>) inside a building, where the wear keeps to its clean half.</summary>
        public static Color32 Pack(Color32 tint, int slice, float wear, bool indoors = false)
        {
            return Pack(tint, slice, wear, indoors, 0, indoors ? UndergroundWear - 1 : WearLevels - 1);
        }

        /// <summary>A look under the ground (a tunnel, a sewer room, a shaft): the wear keeps to its filthy half.</summary>
        public static Color32 PackUnderground(Color tint, int slice, float wear)
        {
            return Pack(Bytes(tint), slice, wear, true, UndergroundWear, WearLevels - 1);
        }

        private static Color32 Pack(Color32 tint, int slice, float wear, bool sheltered, int leastWear, int mostWear)
        {
            int s = Mathf.Clamp(slice, 0, Slices - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(wear * (WearLevels - 1)), leastWear, mostWear);
            int code = s * CodesPerSlice + (sheltered ? IndoorBit : 0) + w;
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

        /// <summary>Whether a packed look is of a surface out of the sky's light: inside a building, or under the ground.</summary>
        public static bool IsIndoors(Color32 look)
        {
            return ((255 - look.a) & IndoorBit) != 0;
        }

        /// <summary>Whether a packed look is of a surface under the ground (<see cref="PackUnderground"/>).</summary>
        public static bool IsUnderground(Color32 look)
        {
            return IsIndoors(look) && ((255 - look.a) & (IndoorBit - 1)) >= UndergroundWear;
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

        private static Color32 Bytes(Color tint)
        {
            return new Color32(Byte(tint.r), Byte(tint.g), Byte(tint.b), 255);
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255.0f), 0, 255);
        }
    }
}
