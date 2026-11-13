using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The sewers' water drawn (docs/SEWERS.md): one sheet over every tunnel and room near a point, at the height <see cref="SewerPlan"/>
    /// pours it to. A tunnel's sheet reaches a little into its walls (they hide the rest) and, where its water is under the walkways, lies
    /// under them, so only the channel shows. Each vertex carries the way its water runs (TEXCOORD1: x, z metres a second), downhill along
    /// its tunnel, still in a room, for the ripples of CityX/Water.
    /// </summary>
    public static class SewerWaterMesh
    {
        /// <summary>How far past a wall the sheet reaches, metres: the wall hides it, and no crack shows at the edge.</summary>
        public const float IntoTheWalls = 0.08f;

        /// <summary>How fast a tunnel's water looks to run, metres a second.</summary>
        public const float RunSpeed = 0.45f;

        /// <summary>The water of every tunnel and room within <paramref name="radius"/> of <paramref name="centre"/> (x, z), in world space.</summary>
        public static void Build(PlanWorld plan, Vector2 centre, float radius, List<Vector3> vertices, List<Vector2> flows, List<int> triangles)
        {
            vertices.Clear();
            flows.Clear();
            triangles.Clear();
            SewerPlan sewers = SewerPlan.For(plan);
            var edges = new HashSet<int>();
            var nodes = new HashSet<int>();
            int x0 = PlanWorld.ChunkOf(centre.x - radius), x1 = PlanWorld.ChunkOf(centre.x + radius);
            int y0 = PlanWorld.ChunkOf(centre.y - radius), y1 = PlanWorld.ChunkOf(centre.y + radius);
            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cy = y0; cy <= y1; cy++)
                {
                    edges.UnionWith(plan.EdgesIn(cx, cy));
                    nodes.UnionWith(plan.JunctionsIn(cx, cy));
                }
            }
            var near = new Vec2(centre.x, centre.y);
            foreach (int edgeId in edges)
            {
                AddTube(sewers.Tube(edgeId), near, radius, vertices, flows, triangles);
                foreach (SewerOutfall outfall in sewers.Outfalls(edgeId))
                {
                    AddTube(outfall.Tube, near, radius, vertices, flows, triangles);
                }
            }
            foreach (int nodeId in nodes)
            {
                SewerRoom room = sewers.Room(nodeId);
                if (room != null && (room.Node.P - near).Length <= radius)
                {
                    AddRoom(room, vertices, flows, triangles);
                }
            }
        }

        private static void AddTube(SewerTube tube, Vec2 near, float radius, List<Vector3> vertices, List<Vector2> flows, List<int> triangles)
        {
            if (tube == null) return;
            if (PlanMath.DistanceToSegment(near, tube.At(tube.Start), tube.At(tube.End), out _) > radius) return;
            SewerSection section = tube.Section;
            // Downhill along the tunnel; a level one runs the way its distances do.
            double fall = tube.Floors[0] - tube.Floors[tube.Floors.Length - 1];
            Vec2 run = tube.Dir * (fall < 0.0 ? -1.0 : 1.0);
            var flow = new Vector2((float)run.X * RunSpeed, (float)run.Y * RunSpeed);
            Vec2 right = -tube.Dir.Perp;
            int first = vertices.Count;
            for (int i = 0; i < tube.Stations.Length; i++)
            {
                double along = tube.Stations[i];
                double water = SewerPlan.WaterAlong(tube, along);
                double half = section.HalfWidthAt(Math.Max(water, 0.01)) + IntoTheWalls;
                float surface = (float)(tube.Floors[i] + water);
                Vec2 middle = tube.At(along);
                Vec2 l = middle - right * half, r = middle + right * half;
                vertices.Add(new Vector3((float)l.X, surface, (float)l.Y));
                vertices.Add(new Vector3((float)r.X, surface, (float)r.Y));
                flows.Add(flow);
                flows.Add(flow);
            }
            for (int i = 0; i + 1 < tube.Stations.Length; i++)
            {
                int a = first + i * 2;
                AddFacingUp(vertices, triangles, a, a + 1, a + 3);
                AddFacingUp(vertices, triangles, a, a + 3, a + 2);
            }
        }

        private static void AddRoom(SewerRoom room, List<Vector3> vertices, List<Vector2> flows, List<int> triangles)
        {
            int hub = vertices.Count;
            Vec2 centre = room.Node.P;
            vertices.Add(new Vector3((float)centre.X, (float)(room.CentreFloor + room.Water), (float)centre.Y));
            flows.Add(Vector2.zero);
            int n = room.Plate.Length;
            for (int i = 0; i < n; i++)
            {
                Vec2 p = room.Plate[i];
                vertices.Add(new Vector3((float)p.X, (float)(room.Floor[i] + room.Water), (float)p.Y));
                flows.Add(Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                AddFacingUp(vertices, triangles, hub, hub + 1 + i, hub + 1 + (i + 1) % n);
            }
        }

        /// <summary>A triangle wound so its front faces up (the shader draws the underside as seen from below the surface).</summary>
        private static void AddFacingUp(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Mathf.Abs(normal.y) < 1e-6f) return;   // no area seen from above: two of its corners in one place
            triangles.Add(a);
            if (normal.y >= 0.0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
