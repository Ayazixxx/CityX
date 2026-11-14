using UnityEngine;

namespace CityX.UI
{
    /// <summary>What the painted icons ask of a pixel (MenuIcons, SkillIcons, ToolIcons): is it inside a polygon, and how far is it from a stroke.</summary>
    public static class IconGeometry
    {
        /// <summary>Whether <paramref name="point"/> is inside the polygon (even-odd: a ray to the right crosses its outline an odd number of times).</summary>
        public static bool InPolygon(Vector2[] points, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 p = points[i];
                Vector2 q = points[j];
                if (p.y > point.y != q.y > point.y && point.x < (q.x - p.x) * (point.y - p.y) / (q.y - p.y) + p.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            float lengthSquared = along.sqrMagnitude;
            if (lengthSquared < 1e-6f)
            {
                return Vector2.Distance(point, a);
            }
            float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / lengthSquared);
            return Vector2.Distance(point, a + along * t);
        }
    }
}
