using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The skyline tier (SkylineStreamer): a group of chunks far from the viewer as a plain floor following the land (water as water) and every
    /// building of some height as one box in its wall's colour (a round tower as its prism), a little inside its walls so the real chunk that may
    /// be drawn over it covers it. Chunks near the viewer are left out (their real version has rooms a box would fill).
    /// </summary>
    public static partial class CityMeshBuilder
    {
        /// <summary>How far a skyline box stands inside its building's walls, so the building's own shell, when it is drawn, covers it.</summary>
        public const float SkylineInset = 0.35f;

        /// <summary>How far under the ground the skyline floor lies: under the real ground wherever that is drawn, unseen from a kilometre off.</summary>
        public const float SkylineFloorDrop = 2.0f;

        /// <summary>Buildings lower than this are left out of the skyline: behind the others at a kilometre, they are not seen.</summary>
        public const float SkylineMinHeight = 9.0f;

        private static readonly Color SkylineGround = new Color(0.33f, 0.33f, 0.34f);
        private static readonly Color SkylineWater = new Color(0.16f, 0.20f, 0.24f);

        /// <summary>
        /// The skyline of the <paramref name="chunks"/> x <paramref name="chunks"/> chunks from chunk (<paramref name="chunkX"/>, <paramref name="chunkY"/>),
        /// relative to <paramref name="origin"/>: the floor into <paramref name="ground"/>, the buildings into <paramref name="structures"/>. Chunks within
        /// <paramref name="excludeRadius"/> (Chebyshev) of <paramref name="around"/> get neither.
        /// </summary>
        public static void BuildSkyline(CityWorld world, int chunkX, int chunkY, int chunks, Vector3 origin, MeshBuffer ground, MeshBuffer structures,
            Vector2Int around, int excludeRadius)
        {
            PlanWorld plan = world.Plan;
            float size = CityLayout.ChunkSize;
            Color32 floor = SurfaceLook.Pack(SkylineGround, SurfaceLook.Asphalt, 0.4f);
            Color32 water = SurfaceLook.Pack(SkylineWater, SurfaceLook.Asphalt, 0.0f);

            // The floor: a quad a chunk at the land's height less the drop, grey, water's colour on water.
            var corner = new float[chunks + 1, chunks + 1];
            for (int j = 0; j <= chunks; j++)
            {
                for (int i = 0; i <= chunks; i++)
                {
                    double x = (chunkX + i) * size, z = (chunkY + j) * size;
                    corner[i, j] = (float)plan.GroundHeight(x, z) - SkylineFloorDrop;
                }
            }
            for (int j = 0; j < chunks; j++)
            {
                for (int i = 0; i < chunks; i++)
                {
                    if (Excluded(chunkX + i, chunkY + j, around, excludeRadius))
                    {
                        continue;
                    }
                    double mx = (chunkX + i + 0.5) * size, mz = (chunkY + j + 0.5) * size;
                    ground.Look = plan.Fields.IsWater(mx, mz) ? water : floor;
                    float x0 = (chunkX + i) * size - origin.x, z0 = (chunkY + j) * size - origin.z;
                    float x1 = x0 + size, z1 = z0 + size;
                    var a = new Vector3(x0, corner[i, j] - origin.y, z0);
                    var b = new Vector3(x1, corner[i + 1, j] - origin.y, z0);
                    var c = new Vector3(x1, corner[i + 1, j + 1] - origin.y, z1);
                    var d = new Vector3(x0, corner[i, j + 1] - origin.y, z1);
                    ground.AddQuad(a, b, c, d, Vector3.up, Uv(a, Vector3.up), Uv(b, Vector3.up), Uv(c, Vector3.up), Uv(d, Vector3.up));
                }
            }

            // The buildings: a box each, in its wall's colour, a little inside its walls, from under the floor to just under its roof.
            for (int j = 0; j < chunks; j++)
            {
                for (int i = 0; i < chunks; i++)
                {
                    if (Excluded(chunkX + i, chunkY + j, around, excludeRadius))
                    {
                        continue;
                    }
                    List<int> owned = world.BuildingsOwnedBy(chunkX + i, chunkY + j);
                    for (int k = 0; k < owned.Count; k++)
                    {
                        AddSkylineBuilding(world, world.Building(owned[k]), origin, structures);
                    }
                }
            }
            ground.Look = SurfaceLook.Default;
            structures.Look = SurfaceLook.Default;
        }

        private static bool Excluded(int chunkX, int chunkY, Vector2Int around, int excludeRadius)
        {
            return Mathf.Max(Mathf.Abs(chunkX - around.x), Mathf.Abs(chunkY - around.y)) <= excludeRadius;
        }

        /// <summary>Whether the skyline draws a building at all (tall enough, built, not open ground).</summary>
        public static bool InSkyline(BuildingSite site) => site.Exists && !site.IsOpenGround && site.Params.Height >= SkylineMinHeight;

        private static void AddSkylineBuilding(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer structures)
        {
            if (!InSkyline(site))
            {
                return;
            }
            SiteLooks looks = SiteLooks.For(world, site, WindowPlan.For(world, site).Style);
            var offset = new Vector3((float)(site.CentreX - origin.x), -origin.y, (float)(site.CentreZ - origin.z));
            structures.SetFrame(offset, site.AxisX, site.AxisZ);
            structures.Look = looks.Wall;
            float bottom = Mathf.Min(site.PodiumBase, site.BaseHeight) - SkylineFloorDrop - 0.5f;   // down under the floor: no gap seen from the street
            float top = site.Top - SkylineInset;
            if (site.Sides >= 3)
            {
                // A round tower as the prism of its outline (RoundFootprint: corners at 90 + (k + 0.5) * 360 / sides degrees from +x toward +z).
                float radius = Mathf.Max(0.5f, site.Params.Width * 0.5f - SkylineInset);
                AddSkylinePrism(structures, site.Sides, radius, bottom, top);
            }
            else
            {
                float hw = Mathf.Max(0.5f, site.Params.Width * 0.5f - SkylineInset);
                float hd = Mathf.Max(0.5f, site.Params.Depth * 0.5f - SkylineInset);
                structures.AddBox(new Vector3(-hw, bottom, -hd), new Vector3(hw, top, hd));
            }
            // A tall tower's top (World/TowerTop) as a stub on the box or prism: the far towers keep their masts, spires and crowns.
            TowerTop tip = TowerTop.For(site);
            if (tip.Exists)
            {
                float square = site.Sides >= 3 ? RoundFootprint.Of(site).InnerHalf * 2.0f : 0.0f;
                AddSkylineTop(structures, tip, top, square > 0.0f ? square : site.Params.Width, square > 0.0f ? square : site.Params.Depth);
            }
            structures.ClearFrame();
        }

        /// <summary>A prism of <paramref name="sides"/> sides round the frame's origin, corner radius <paramref name="radius"/>, with its top.</summary>
        private static void AddSkylinePrism(MeshBuffer buffer, int sides, float radius, float bottom, float top)
        {
            var ring = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                double a = Mathf.PI * 0.5 + (k + 0.5) * 2.0 * Mathf.PI / sides;
                ring[k] = new Vector3(radius * (float)System.Math.Cos(a), 0.0f, radius * (float)System.Math.Sin(a));
            }
            for (int k = 0; k < sides; k++)
            {
                Vector3 p = ring[k], q = ring[(k + 1) % sides];
                Vector3 n = ((p + q) * 0.5f).normalized;
                Vector3 a = p + Vector3.up * bottom, b = q + Vector3.up * bottom, c = q + Vector3.up * top, d = p + Vector3.up * top;
                buffer.AddQuad(a, b, c, d, n, Uv(a, n), Uv(b, n), Uv(c, n), Uv(d, n));
            }
            Vector3 centre = Vector3.up * top;
            for (int k = 0; k < sides; k++)
            {
                Vector3 p = ring[k] + Vector3.up * top, q = ring[(k + 1) % sides] + Vector3.up * top;
                buffer.AddTriangle(centre, p, q, Vector3.up, Uv(centre, Vector3.up), Uv(p, Vector3.up), Uv(q, Vector3.up));
            }
        }
    }
}
