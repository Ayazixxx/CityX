using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A ray against a mesh's triangles as plain arrays, for a surface that changes shape every frame: a MeshCollider would have to be cooked
    /// again each time. Front faces only, as physics has them. Written out on floats because it runs over every triangle of the body.
    /// </summary>
    public static class MeshRay
    {
        /// <summary>
        /// The nearest front face <paramref name="direction"/> meets from <paramref name="origin"/>, in the space of the vertices. On a hit,
        /// <paramref name="triangle"/> is its number (a third of its first index), and the point is <c>a + (b-a)*u + (c-a)*v</c>.
        /// </summary>
        public static bool Nearest(Vector3[] vertices, int[] triangles, Vector3 origin, Vector3 direction, out float distance, out int triangle, out float u, out float v)
        {
            distance = float.MaxValue;
            triangle = -1;
            u = 0.0f;
            v = 0.0f;
            NearestAmong(vertices, triangles, 0, triangles.Length, origin, direction, ref distance, ref triangle, ref u, ref v);
            return triangle >= 0;
        }

        /// <summary>How many triangles share a box in <see cref="FitBoxes"/>. A stroke asks several times a frame; all the body's triangles each time is too slow.</summary>
        public const int FacesPerBox = 128;

        /// <summary>Six floats (lowest x y z, highest x y z) per run of <see cref="FacesPerBox"/> triangles, for this many triangle indices.</summary>
        public static float[] NewBoxes(int triangleIndices)
        {
            return new float[(triangleIndices / (FacesPerBox * 3) + 1) * 6];
        }

        /// <summary>The box round each run of triangles, as the vertices are now.</summary>
        public static void FitBoxes(Vector3[] vertices, int[] triangles, float[] boxes)
        {
            for (int box = 0; box * 6 < boxes.Length; box++)
            {
                float lowX = float.MaxValue, lowY = float.MaxValue, lowZ = float.MaxValue;
                float highX = float.MinValue, highY = float.MinValue, highZ = float.MinValue;
                int end = Mathf.Min(triangles.Length, (box + 1) * FacesPerBox * 3);
                for (int i = box * FacesPerBox * 3; i < end; i++)
                {
                    Vector3 p = vertices[triangles[i]];
                    if (p.x < lowX) lowX = p.x;
                    if (p.y < lowY) lowY = p.y;
                    if (p.z < lowZ) lowZ = p.z;
                    if (p.x > highX) highX = p.x;
                    if (p.y > highY) highY = p.y;
                    if (p.z > highZ) highZ = p.z;
                }
                int at = box * 6;
                boxes[at] = lowX;
                boxes[at + 1] = lowY;
                boxes[at + 2] = lowZ;
                boxes[at + 3] = highX;
                boxes[at + 4] = highY;
                boxes[at + 5] = highZ;
            }
        }

        /// <summary><see cref="Nearest(Vector3[], int[], Vector3, Vector3, out float, out int, out float, out float)"/>, looking only in the boxes the ray goes through.</summary>
        public static bool Nearest(Vector3[] vertices, int[] triangles, float[] boxes, Vector3 origin, Vector3 direction, out float distance, out int triangle, out float u, out float v)
        {
            distance = float.MaxValue;
            triangle = -1;
            u = 0.0f;
            v = 0.0f;
            float perX = 1.0f / direction.x, perY = 1.0f / direction.y, perZ = 1.0f / direction.z;
            for (int box = 0; box * 6 < boxes.Length; box++)
            {
                int at = box * 6;
                float near = 0.0f, far = distance;
                if (Misses(boxes[at], boxes[at + 3], origin.x, perX, ref near, ref far)
                    || Misses(boxes[at + 1], boxes[at + 4], origin.y, perY, ref near, ref far)
                    || Misses(boxes[at + 2], boxes[at + 5], origin.z, perZ, ref near, ref far))
                {
                    continue;
                }
                int start = box * FacesPerBox * 3;
                NearestAmong(vertices, triangles, start, Mathf.Min(triangles.Length, start + FacesPerBox * 3), origin, direction, ref distance, ref triangle, ref u, ref v);
            }
            return triangle >= 0;
        }

        /// <summary>One axis of a ray against a box; a ray level with the axis (<paramref name="per"/> infinite) needs the origin between the two.</summary>
        private static bool Misses(float low, float high, float origin, float per, ref float near, ref float far)
        {
            if (float.IsInfinity(per))
            {
                return origin < low || origin > high;
            }
            float a = (low - origin) * per, b = (high - origin) * per;
            if (a > b)
            {
                (a, b) = (b, a);
            }
            if (a > near) near = a;
            if (b < far) far = b;
            return near > far;
        }

        private static void NearestAmong(Vector3[] vertices, int[] triangles, int start, int end, Vector3 origin, Vector3 direction, ref float distance, ref int triangle, ref float u, ref float v)
        {
            float dx = direction.x, dy = direction.y, dz = direction.z;
            for (int i = start; i + 2 < end; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                float e1x = b.x - a.x, e1y = b.y - a.y, e1z = b.z - a.z;
                float e2x = c.x - a.x, e2y = c.y - a.y, e2z = c.z - a.z;

                float px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
                float det = e1x * px + e1y * py + e1z * pz;
                if (det <= 0.0f)
                {
                    continue;   // seen from behind, or edge-on
                }
                float sx = origin.x - a.x, sy = origin.y - a.y, sz = origin.z - a.z;
                float along = sx * px + sy * py + sz * pz;
                if (along < 0.0f || along > det)
                {
                    continue;
                }
                float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
                float up = dx * qx + dy * qy + dz * qz;
                if (up < 0.0f || along + up > det)
                {
                    continue;
                }
                float t = (e2x * qx + e2y * qy + e2z * qz) / det;
                if (t > 0.0f && t < distance)
                {
                    distance = t;
                    triangle = i / 3;
                    u = along / det;
                    v = up / det;
                }
            }
        }
    }
}
