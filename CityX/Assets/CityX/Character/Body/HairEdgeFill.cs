using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Closes a notch in a hairstyle's lower edge with the hair itself (owner, 2026-10-23, of a gap on Hyde's left: "fill this gap ... not with
    /// a scalp cap but make the hair fill up that space make it blend well too"): the curl beside the notch is copied, turned round the head
    /// into it and tucked a little under the hair over it, so what shows in the gap is the same hair, shaded and shaped as its neighbour.
    /// (Drawing the edge down instead widened the gap: the curls there lie in layers, and their tips moved apart.) Pure: points in the head's
    /// own space (its centre at the origin, +y up, +z the way the face looks; turns as HairScalp's: 0 ahead, 90 the right, 270 the left).
    /// </summary>
    public static class HairEdgeFill
    {
        /// <summary>
        /// The copy lies this share of its distance from the head's centre: its tip just under the hair either side (Tuck), its root at the top
        /// deep under the hair over the notch (DeepTuck), between the two over the last <see cref="TuckRise"/> degrees up to the top; so it comes
        /// out from under the hair the way its neighbours do, and no cut edge of it shows.
        /// </summary>
        public const float Tuck = 0.985f;

        public const float DeepTuck = 0.93f;

        public const float TuckRise = 20.0f;

        /// <summary>
        /// Appends to the lists a copy of every triangle lying wholly between <paramref name="fromTurn"/> and <paramref name="toTurn"/>
        /// (degrees round the head) and below <paramref name="topHeight"/> (degrees above the head's middle), outside a head of
        /// <paramref name="headRadius"/>, turned <paramref name="byTurn"/> degrees round the head (its normals with it, its UVs as they were).
        /// Returns how many triangles were copied.
        /// </summary>
        public static int CopyCurl(List<Vector3> points, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, float headRadius,
            float fromTurn, float toTurn, float topHeight, float byTurn)
        {
            Quaternion turn = Quaternion.Euler(0.0f, byTurn, 0.0f);
            var copyOf = new Dictionary<int, int>();
            int count = triangles.Count, copied = 0;
            for (int t = 0; t + 2 < count; t += 3)
            {
                bool inside = true;
                for (int k = 0; k < 3 && inside; k++)
                {
                    Vector3 p = points[triangles[t + k]];
                    Polar(p, out float around, out float height);
                    inside = around >= fromTurn && around <= toTurn && height <= topHeight && p.magnitude > headRadius;
                }
                if (!inside) continue;
                for (int k = 0; k < 3; k++)
                {
                    int i = triangles[t + k];
                    if (!copyOf.TryGetValue(i, out int j))
                    {
                        j = points.Count;
                        Polar(points[i], out _, out float height);
                        float tuck = Mathf.Lerp(Tuck, DeepTuck, Mathf.InverseLerp(topHeight - TuckRise, topHeight, height));
                        points.Add(turn * points[i] * tuck);
                        if (normals != null) normals.Add(turn * normals[i]);
                        if (uvs != null) uvs.Add(uvs[i]);
                        copyOf[i] = j;
                    }
                    triangles.Add(j);
                }
                copied++;
            }
            return copied;
        }

        private static void Polar(Vector3 p, out float turn, out float height)
        {
            turn = Mathf.Repeat(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg, 360.0f);
            height = Mathf.Asin(Mathf.Clamp(p.y / Mathf.Max(p.magnitude, 1e-6f), -1.0f, 1.0f)) * Mathf.Rad2Deg;
        }
    }
}
