using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A POURED LIQUID AS ONE STREAM (docs/FIRE.md; the owner, 2026-10-31, on a Tuatara Games post faking liquid streams with a spun, deformed
    /// tube: "i guess we could do it for the pour of the gasoline"): a thin tube of mesh along the fall from the spout to where the gasoline
    /// lands, the same arc the drops used to be thrown on, thinning as it falls, with a wave of thickening running down it and a slight sway at
    /// its foot, so it reads as liquid on the move rather than a row of dots. Rebuilt in place every frame it shows (fixed arrays, no garbage);
    /// the maths is here, plain, and JerryCanItem puts it on a mesh.
    /// </summary>
    public sealed class PourStream
    {
        /// <summary>Rings along the arc and sides round the tube: 168 vertices, enough for a smooth bend at a few centimetres across.</summary>
        public const int Rings = 20;

        public const int Sides = 7;

        /// <summary>The tube's radius at the spout and at the landing (a stream thins as it speeds up).</summary>
        public const float SpoutRadius = 0.018f;

        public const float LandingRadius = 0.011f;

        /// <summary>The thickening wave: how much of the radius it adds, how many waves lie along the stream, how many pass a point a second.</summary>
        public const float WobbleShare = 0.25f;

        public const float WobbleWaves = 3.0f;

        public const float FlowPerSecond = 2.5f;

        /// <summary>How far the foot of the stream sways sideways, and how fast.</summary>
        public const float SwayMetres = 0.012f;

        public const float SwayPerSecond = 1.7f;

        public const int VertexCount = (Rings + 1) * (Sides + 1);

        public readonly Vector3[] Vertices = new Vector3[VertexCount];
        public readonly Vector3[] Normals = new Vector3[VertexCount];
        public readonly Vector2[] Uvs = new Vector2[VertexCount];
        public readonly int[] Triangles = new int[Rings * Sides * 6];

        public PourStream()
        {
            int t = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int side = 0; side < Sides; side++)
                {
                    int a = ring * (Sides + 1) + side, b = a + Sides + 1;
                    Triangles[t++] = a;
                    Triangles[t++] = b;
                    Triangles[t++] = a + 1;
                    Triangles[t++] = a + 1;
                    Triangles[t++] = b;
                    Triangles[t++] = b + 1;
                }
            }
        }

        /// <summary>The liquid's speed leaving the spout so that it lands at <paramref name="landing"/> after <paramref name="seconds"/> of falling.</summary>
        public static Vector3 LaunchVelocity(Vector3 from, Vector3 landing, float seconds) =>
            (landing - from) / seconds - 0.5f * Physics.gravity * seconds;

        /// <summary>Where the liquid is <paramref name="share"/> of the way along its fall (0 at the spout, 1 at the landing).</summary>
        public static Vector3 PointOn(Vector3 from, Vector3 landing, float seconds, float share)
        {
            float t = share * seconds;
            return from + LaunchVelocity(from, landing, seconds) * t + 0.5f * Physics.gravity * (t * t);
        }

        /// <summary>The tube's radius <paramref name="share"/> of the way down, at <paramref name="time"/>: the taper with the wave on it.</summary>
        public static float RadiusAt(float share, float time) =>
            Mathf.Lerp(SpoutRadius, LandingRadius, share) * (1.0f + WobbleShare * Mathf.Sin(2.0f * Mathf.PI * (WobbleWaves * share - FlowPerSecond * time)));

        /// <summary>The stream from <paramref name="from"/> to <paramref name="landing"/> at <paramref name="time"/>, in the world, into the arrays.</summary>
        public void Fill(Vector3 from, Vector3 landing, float seconds, float time)
        {
            Vector3 launch = LaunchVelocity(from, landing, seconds);
            Vector3 side = Vector3.Cross(Vector3.up, landing - from);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            for (int ring = 0; ring <= Rings; ring++)
            {
                float share = ring / (float)Rings;
                float t = share * seconds;
                Vector3 centre = PointOn(from, landing, seconds, share) + side * (SwayMetres * share * Mathf.Sin(2.0f * Mathf.PI * (1.3f * share - SwayPerSecond * time)));
                Vector3 tangent = launch + Physics.gravity * t;
                tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.down;
                Vector3 across = Vector3.Cross(tangent, side);
                across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.forward;
                Vector3 other = Vector3.Cross(across, tangent);
                float radius = RadiusAt(share, time);
                for (int s = 0; s <= Sides; s++)
                {
                    float angle = 2.0f * Mathf.PI * s / Sides;
                    Vector3 normal = across * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    int i = ring * (Sides + 1) + s;
                    Vertices[i] = centre + normal * radius;
                    Normals[i] = normal;
                    Uvs[i] = new Vector2(s / (float)Sides, share);
                }
            }
        }
    }
}
