using System.Collections.Generic;
using CityX.View;
using CityX.World;
using UnityEngine;

namespace CityX.UIEditor
{
    /// <summary>
    /// The title screen's row of buildings: plain boxes -- walls and windows,
    /// nothing else -- laid out edge to edge across the whole screen.
    ///
    /// NO INTERIORS, FLOORS, STAIRS, ROOFS DETAIL. An earlier pass baked whole
    /// buildings out of the city generator and you could see the slabs and rooms
    /// through the glass; on a title screen that is noise. These use the city's
    /// own materials (concrete facade, lit and dark glass) and its own window
    /// grid (cell layout, which cells are glazed, which are lit), so they read
    /// as the game's buildings without being the game's buildings.
    /// </summary>
    public static class TitleSkylineBaker
    {
        public const int Seed = 63428175;

        /// <summary>Height of the one tallest tower, in the middle. Everything else is lower.</summary>
        public const float TallestHeight = 110.0f;

        public const float MinWidth = 14.0f;
        public const float MaxWidth = 22.0f;
        public const float MinHeight = 28.0f;
        public const float MaxHeight = 82.0f;
        public const float Depth = 18.0f;
        public const float MinGap = 2.0f;
        public const float MaxGap = 5.0f;

        /// <summary>Windows stand this far off the wall.</summary>
        public const float WindowLift = 0.03f;

        public sealed class Baked
        {
            public Mesh Mesh;
            public int BuildingCount;
            public float TallestTop;
            public readonly List<float> Heights = new List<float>();
        }

        /// <summary>A row of buildings covering x from -halfWidth to +halfWidth, centre tower tallest.</summary>
        public static Baked Bake(float halfWidth)
        {
            var rng = new System.Random(Seed);
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var walls = new MeshBuffer();
            var dark = new MeshBuffer();
            var lit = new MeshBuffer();
            var baked = new Baked();

            // Left to right, from past the left edge to past the right.
            float x = -halfWidth - MaxWidth;
            var slots = new List<(float centre, float width)>();
            while (x < halfWidth + MaxWidth)
            {
                float width = Mathf.Lerp(MinWidth, MaxWidth, (float)rng.NextDouble());
                slots.Add((x + width * 0.5f, width));
                x += width + Mathf.Lerp(MinGap, MaxGap, (float)rng.NextDouble());
            }

            int centre = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (Mathf.Abs(slots[i].centre) < Mathf.Abs(slots[centre].centre))
                {
                    centre = i;
                }
            }

            float previous = 0.0f;
            for (int i = 0; i < slots.Count; i++)
            {
                float height;
                if (i == centre)
                {
                    height = TallestHeight;
                }
                else
                {
                    // Uneven, and never the same as the neighbour.
                    do
                    {
                        height = Mathf.Lerp(MinHeight, MaxHeight, (float)rng.NextDouble());
                    }
                    while (Mathf.Abs(height - previous) < 12.0f);
                }
                previous = height;

                float z = ((float)rng.NextDouble() - 0.5f) * 8.0f;
                AddTower(slots[i].centre, slots[i].width, height, z, i, walls, pavement, dark, lit);
                baked.Heights.Add(height);
            }

            baked.BuildingCount = slots.Count;
            baked.TallestTop = TallestHeight;
            baked.Mesh = ToMesh(new[] { ground, pavement, walls, dark, lit });
            return baked;
        }

        private static void AddTower(float centreX, float width, float height, float frontZ, int index,
            MeshBuffer walls, MeshBuffer roofs, MeshBuffer dark, MeshBuffer lit)
        {
            CellLayout rows = WindowGrid.RowsFor(height);
            WindowPlan plan = WindowGrid.PlanFor(Seed + index * 7919, ZoneKind.Core,
                new BuildingParams(true, width, height, Depth, 0.0f, 0.0f, "", 0, "", false, BuildingMass.Tower));

            // The same look the city gives a building of this kind (material, tint, weathering), so the skyline over the title screen
            // is made of the same stuff as the streets it stands for.
            BuildingLook look = BuildingLook.For(Seed + index * 7919, ZoneKind.Core, BuildingMass.Tower, plan.Style);
            walls.Look = SurfaceLook.Pack(look.WallTint, (int)look.Wall, look.WallWear);
            walls.UvOffset = look.UvOffset;
            walls.AddBox(new Vector3(centreX - width * 0.5f, 0.0f, frontZ),
                new Vector3(centreX + width * 0.5f, height, frontZ + Depth), roofs);
            walls.Look = SurfaceLook.Default;
            walls.UvOffset = Vector2.zero;

            // The camera-facing wall and both sides. The back is never seen.
            AddWall(plan, WindowGrid.Wall.South, new Vector3(centreX, 0.0f, frontZ), Vector3.back,
                Vector3.right, width, width * 0.0f, rows, dark, lit);
            AddWall(plan, WindowGrid.Wall.East, new Vector3(centreX + width * 0.5f, 0.0f, frontZ + Depth * 0.5f),
                Vector3.right, Vector3.forward, Depth, 0.0f, rows, dark, lit);
            AddWall(plan, WindowGrid.Wall.West, new Vector3(centreX - width * 0.5f, 0.0f, frontZ + Depth * 0.5f),
                Vector3.left, Vector3.forward, Depth, 0.0f, rows, dark, lit);
        }

        private static void AddWall(in WindowPlan plan, WindowGrid.Wall wall, Vector3 origin, Vector3 normal, Vector3 along,
            float span, float unused, CellLayout rows, MeshBuffer dark, MeshBuffer lit)
        {
            CellLayout columns = plan.ColumnsFor(span);
            float marginU = columns.Size * CellLayout.WindowMarginRatio;
            float marginV = rows.Size * CellLayout.WindowMarginRatio;
            Vector3 face = origin + normal * WindowLift;

            for (int row = 0; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (!plan.IsGlazed(wall, column, row))
                    {
                        continue;
                    }

                    float u0 = -span * 0.5f + column * columns.Size + marginU;
                    float u1 = -span * 0.5f + (column + 1) * columns.Size - marginU;
                    float y0 = row * rows.Size + marginV;
                    float y1 = (row + 1) * rows.Size - marginV;
                    MeshBuffer target = plan.IsLit(wall, column, row, 0.5) ? lit : dark;
                    target.AddQuad(
                        face + along * u0 + Vector3.up * y0, face + along * u1 + Vector3.up * y0,
                        face + along * u1 + Vector3.up * y1, face + along * u0 + Vector3.up * y1,
                        normal, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            }
        }

        /// <summary>One mesh, a submesh per buffer: ground, pavement (roofs), walls, dark glass, lit glass -- the streamer's order.</summary>
        private static Mesh ToMesh(MeshBuffer[] buffers)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colours = new List<Color32>();
            var offsets = new int[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
            {
                offsets[i] = vertices.Count;
                vertices.AddRange(buffers[i].Vertices);
                normals.AddRange(buffers[i].Normals);
                uvs.AddRange(buffers[i].Uvs);
                colours.AddRange(buffers[i].Colors);
            }

            var mesh = new Mesh { name = "TitleSkyline", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colours);
            mesh.subMeshCount = buffers.Length;
            for (int i = 0; i < buffers.Length; i++)
            {
                var triangles = new int[buffers[i].Triangles.Count];
                for (int t = 0; t < triangles.Length; t++)
                {
                    triangles[t] = buffers[i].Triangles[t] + offsets[i];
                }
                mesh.SetTriangles(triangles, i);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
