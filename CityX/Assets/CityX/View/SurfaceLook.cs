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
    ///   alpha  255 minus a code, where code = slice * 32 + wear (wear 0..31, 31 = filthy)
    ///
    /// ALPHA IS INVERTED so that the colour Unity gives a mesh with no vertex colours at all, opaque white, decodes to slice 0 with no
    /// wear and a white tint: a buffer nobody styled looks like plain material, exactly as it did before there were looks.
    /// </summary>
    public static class SurfaceLook
    {
        /// <summary>Wall materials in the facade texture array.</summary>
        public const int Slices = 8;

        /// <summary>Steps of wear a look can carry (0 = clean .. 31 = filthy).</summary>
        public const int WearLevels = 32;

        /// <summary>White, slice 0, clean: what an unstyled vertex is.</summary>
        public static readonly Color32 Default = new Color32(255, 255, 255, 255);

        public static Color32 Pack(Color tint, int slice, float wear)
        {
            return Pack(new Color32(Byte(tint.r), Byte(tint.g), Byte(tint.b), 255), slice, wear);
        }

        public static Color32 Pack(Color32 tint, int slice, float wear)
        {
            int s = Mathf.Clamp(slice, 0, Slices - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(wear * (WearLevels - 1)), 0, WearLevels - 1);
            int code = s * WearLevels + w;
            return new Color32(tint.r, tint.g, tint.b, (byte)(255 - code));
        }

        /// <summary>Reads a packed look back. The same arithmetic the shader does.</summary>
        public static void Unpack(Color32 look, out Color32 tint, out int slice, out float wear)
        {
            int code = 255 - look.a;
            slice = code / WearLevels;
            wear = (code % WearLevels) / (float)(WearLevels - 1);
            tint = new Color32(look.r, look.g, look.b, 255);
        }

        /// <summary>
        /// The look of a window's glass (read by CityX/Glass): its tint (0..7), the kind of light it shows when lit (0..7), and how far a
        /// blind is drawn down it (0..1). Never 255 in both of the last two bytes, which is what an unstyled vertex reads as.
        /// </summary>
        public static Color32 PackGlass(int tint, int light, float blind)
        {
            int b = (Mathf.Clamp(tint, 0, 7) << 4) | Mathf.Clamp(light, 0, 7);
            int a = Mathf.Clamp(Mathf.RoundToInt(blind * 200.0f), 0, 200);
            return new Color32(0, 0, (byte)b, (byte)a);
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255.0f), 0, 255);
        }
    }
}
