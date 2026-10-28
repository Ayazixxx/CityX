using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Light drawn in code (Self Smite's bolt, the arcane orbs): a soft round spot, and URP's unlit particle material made from it, additive for
    /// light or blended for smoke. A template (an editor-made asset with soft particles on, SelfSmiteAbility.LightMaterial) is copied when there is
    /// one, so a glow fades where it meets the street and a build ships that shader variant.
    /// </summary>
    public static class SoftGlow
    {
        private static Texture2D _spot;

        /// <summary>A see-through material of <paramref name="colour"/> painted with the soft spot.</summary>
        public static Material Make(string name, Color colour, bool additive, Material template = null)
        {
            Material material;
            if (template != null)
            {
                material = new Material(template) { name = name, hideFlags = HideFlags.DontSave };
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            }
            SeeThroughMaterial.Apply(material, additive);
            material.SetColor("_BaseColor", colour);
            material.color = colour;
            material.SetTexture("_BaseMap", Spot());
            material.mainTexture = Spot();
            return material;
        }

        /// <summary>
        /// A soft white spot, opaque in the middle and gone at the edge: across a ribbon it is the soft edge, on a quad a puff. (1 - d^2)^3 falls
        /// to nothing with no slope left at the rim, so a big glow shows no ring where its quad ends (rendered at night, 2026-10-15).
        /// </summary>
        public static Texture2D Spot()
        {
            if (_spot != null)
            {
                return _spot;
            }
            const int size = 64;
            _spot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftGlowSpot", hideFlags = HideFlags.DontSave };
            _spot.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                    float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float d2 = Mathf.Clamp01(dx * dx + dy * dy);
                    float fall = 1.0f - d2;
                    float a = fall * fall * fall;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255.0f));
                }
            }
            _spot.SetPixels32(pixels);
            _spot.Apply(false, true);
            return _spot;
        }
    }
}
