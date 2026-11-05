using UnityEngine;

namespace CityX.View
{
    /// <summary>The mesh CityX/Snowfall moves: a quad per flake, each knowing only where in the box it starts and what sort of flake it is. The shader does the rest.</summary>
    public static class FlakeField
    {
        /// <summary>The field is everywhere the camera goes, so it is never culled.</summary>
        private const float EverywhereMetres = 100000.0f;

        public static Mesh Build(int flakes, System.Random random)
        {
            var seeds = new Vector3[flakes * 4];
            var corners = new Vector2[flakes * 4];
            var kinds = new Vector4[flakes * 4];
            var triangles = new int[flakes * 6];
            for (int i = 0; i < flakes; i++)
            {
                var seed = new Vector3(Value(random), Value(random), Value(random));
                // Ranks evenly spaced, so a share of them shown is that share of the flakes.
                var kind = new Vector4((i + 0.5f) / flakes, Value(random), Value(random), Value(random));
                int at = i * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    seeds[at + corner] = seed;
                    kinds[at + corner] = kind;
                }
                corners[at] = new Vector2(-1.0f, -1.0f);
                corners[at + 1] = new Vector2(1.0f, -1.0f);
                corners[at + 2] = new Vector2(1.0f, 1.0f);
                corners[at + 3] = new Vector2(-1.0f, 1.0f);
                int t = i * 6;
                triangles[t] = at;
                triangles[t + 1] = at + 2;
                triangles[t + 2] = at + 1;
                triangles[t + 3] = at;
                triangles[t + 4] = at + 3;
                triangles[t + 5] = at + 2;
            }
            var mesh = new Mesh { name = "SnowFlakes", hideFlags = HideFlags.DontSave };
            mesh.indexFormat = seeds.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = seeds;
            mesh.uv = corners;
            mesh.SetUVs(1, kinds);
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * EverywhereMetres);
            return mesh;
        }

        private static float Value(System.Random random) => (float)random.NextDouble();
    }
}
