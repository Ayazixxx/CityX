using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// What the flashlight's beam is drawn with (docs/FLASHLIGHT.md), made once in code: the cookie its light is shaped by, the shaft of light in
    /// the air (three soft wedges crossed about the beam, fading along it and to their sides: the way games fake a beam in the air without fog,
    /// a volume from every side), and the glow on its lens.
    /// </summary>
    public static class FlashlightLook
    {
        public const int CookieSize = 256;

        /// <summary>The shaft is this wide at the lens, as a share of its far end, and made of this many wedges crossed about the beam.</summary>
        public const float ShaftNearShare = 0.04f;

        public const int ShaftWedges = 3;

        private static Texture2D _cookie;
        private static Mesh _shaft;
        private static Material _shaftMaterial;
        private static Material _glareMaterial;

        /// <summary>
        /// The cookie: <see cref="FlashlightRules.CookieAt"/> out from the middle as the light reads it (<see cref="FlashlightRules.CookieTexelAt"/>),
        /// grey in every channel (the light reads its colour).
        /// </summary>
        public static Texture2D Cookie => _cookie != null
            ? _cookie
            : (_cookie = GlowShapes.Paint("FlashlightCookie", CookieSize, TextureWrapMode.Clamp, (u, v) =>
            {
                float x = u * 2.0f - 1.0f, y = v * 2.0f - 1.0f;
                return FlashlightRules.CookieTexelAt(Mathf.Sqrt(x * x + y * y));
            }, true));

        /// <summary>
        /// <see cref="ShaftWedges"/> flat wedges along +Z from 0 to 1, crossed about it at even turns, each <see cref="ShaftNearShare"/> wide at its
        /// start and 1 at its end, drawn from both faces; u across each, v along.
        /// </summary>
        public static Mesh Shaft
        {
            get
            {
                if (_shaft != null)
                {
                    return _shaft;
                }
                float near = ShaftNearShare * 0.5f;
                var vertices = new System.Collections.Generic.List<Vector3>();
                var uvs = new System.Collections.Generic.List<Vector2>();
                var triangles = new System.Collections.Generic.List<int>();
                for (int w = 0; w < ShaftWedges; w++)
                {
                    Quaternion turn = Quaternion.AngleAxis(180.0f * w / ShaftWedges, Vector3.forward);
                    int first = vertices.Count;
                    vertices.Add(turn * new Vector3(-near, 0.0f, 0.0f));
                    vertices.Add(turn * new Vector3(near, 0.0f, 0.0f));
                    vertices.Add(turn * new Vector3(0.5f, 0.0f, 1.0f));
                    vertices.Add(turn * new Vector3(-0.5f, 0.0f, 1.0f));
                    uvs.Add(new Vector2(0.0f, 0.0f));
                    uvs.Add(new Vector2(1.0f, 0.0f));
                    uvs.Add(new Vector2(1.0f, 1.0f));
                    uvs.Add(new Vector2(0.0f, 1.0f));
                    triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2, first, first + 1, first + 2, first, first + 2, first + 3 });
                }
                _shaft = new Mesh { name = "FlashlightShaft", hideFlags = HideFlags.DontSave };
                _shaft.SetVertices(vertices);
                _shaft.SetUVs(0, uvs);
                _shaft.SetTriangles(triangles, 0);
                _shaft.RecalculateNormals();
                _shaft.RecalculateBounds();
                return _shaft;
            }
        }

        /// <summary>The shaft's light: soft to its sides, brightest at the lens and gone by its end, added to what is behind it.</summary>
        public static Material ShaftMaterial => _shaftMaterial != null
            ? _shaftMaterial
            : (_shaftMaterial = GlowShapes.Painted("FlashlightShaft",
                new Color(FlashlightRules.Colour.r, FlashlightRules.Colour.g, FlashlightRules.Colour.b, FlashlightRules.ShaftStrength), true,
                GlowShapes.Paint("FlashlightShaft", 64, TextureWrapMode.Clamp, (u, v) =>
                {
                    float x = u * 2.0f - 1.0f;
                    float across = Mathf.Max(0.0f, 1.0f - x * x);
                    return across * across * Mathf.Pow(1.0f - v, 1.5f) * Mathf.SmoothStep(0.0f, 1.0f, v / 0.04f);
                })));

        /// <summary>The glow on the lens: a soft warm spot, added.</summary>
        public static Material GlareMaterial => _glareMaterial != null
            ? _glareMaterial
            : (_glareMaterial = SoftGlow.Make("FlashlightGlare", FlashlightRules.Colour, true));
    }
}
