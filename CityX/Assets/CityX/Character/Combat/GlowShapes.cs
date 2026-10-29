using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Light, frost and dust drawn in code (no image files): a white texture whose alpha is a function across it, a see-through material
    /// painted with one (<see cref="SoftGlow"/>'s, its texture swapped), and a unit quad lying flat on the ground. Shared by every effect that
    /// paints its own shape: the arcane spells' faces and rings, the staff beams' bands, the healing aura's rings, the giant's dust ring, the
    /// flashlight's cookie and beam.
    /// </summary>
    public static class GlowShapes
    {
        private static Mesh _flatQuad;

        /// <summary>
        /// A white <paramref name="size"/>-square texture whose alpha is <paramref name="alpha"/>(u, v) (0..1 across it), made once by its caller;
        /// <paramref name="grey"/>, its colour that too (a light's cookie reads the colour).
        /// </summary>
        public static Texture2D Paint(string name, int size, TextureWrapMode wrap, System.Func<float, float, float> alpha, bool grey = false)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.DontSave, wrapMode = wrap };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size)) * 255.0f);
                    byte c = grey ? a : (byte)255;
                    pixels[y * size + x] = new Color32(c, c, c, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>A see-through material of <paramref name="colour"/> (glowing when <paramref name="additive"/>) painted with <paramref name="texture"/>.</summary>
        public static Material Painted(string name, Color colour, bool additive, Texture2D texture, Material template = null)
        {
            Material material = SoftGlow.Make(name, colour, additive, template);
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            return material;
        }

        /// <summary>A unit quad lying flat, facing up (+Y), its UVs across it.</summary>
        public static Mesh FlatQuad
        {
            get
            {
                if (_flatQuad != null)
                {
                    return _flatQuad;
                }
                _flatQuad = new Mesh { name = "GlowFlatQuad", hideFlags = HideFlags.DontSave };
                _flatQuad.SetVertices(new[] { new Vector3(-0.5f, 0.0f, -0.5f), new Vector3(0.5f, 0.0f, -0.5f), new Vector3(0.5f, 0.0f, 0.5f), new Vector3(-0.5f, 0.0f, 0.5f) });
                _flatQuad.SetUVs(0, new[] { new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f) });
                _flatQuad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _flatQuad.RecalculateNormals();
                _flatQuad.RecalculateBounds();
                return _flatQuad;
            }
        }
    }
}
