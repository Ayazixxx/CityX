using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sewers as the chunks build them (docs/SEWERS.md): an open manhole is a real hole in its junction's road and a shut one is not,
    /// every face of a tunnel looks into the tunnel (nothing sees or strikes a face from behind), and is marked under the ground, where the
    /// sky and the street's lamps light nothing.
    /// </summary>
    public sealed class SewerMeshTests
    {
        private const int Seed = 95998845;

        private sealed class Chunk
        {
            public readonly MeshBuffer Ground = new MeshBuffer(), Pavement = new MeshBuffer(), Structures = new MeshBuffer();
            public Vector3 Origin;
        }

        private readonly Dictionary<(int, int), Chunk> _built = new Dictionary<(int, int), Chunk>();
        private CityWorld _world;

        private CityWorld World => _world ??= new CityWorld(Seed);

        private Chunk At(double x, double z)
        {
            int cx = PlanWorld.ChunkOf(x), cy = PlanWorld.ChunkOf(z);
            if (!_built.TryGetValue((cx, cy), out Chunk chunk))
            {
                chunk = new Chunk { Origin = new Vector3(cx * CityLayout.ChunkSize, 0, cy * CityLayout.ChunkSize) };
                CitySurfaceBuilder.Build(World, cx, cy, chunk.Origin, chunk.Ground, chunk.Pavement, chunk.Structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), false);
                _built[(cx, cy)] = chunk;
            }
            return chunk;
        }

        /// <summary>Whether a buffer has a face over a point of the plan, between two heights.</summary>
        private static bool Covers(MeshBuffer buffer, Vector3 origin, double x, double z, double low, double high)
        {
            var p = new Vector2((float)(x - origin.x), (float)(z - origin.z));
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]], b = buffer.Vertices[buffer.Triangles[t + 1]], c = buffer.Vertices[buffer.Triangles[t + 2]];
                var a2 = new Vector2(a.x, a.z);
                Vector2 v0 = new Vector2(b.x, b.z) - a2, v1 = new Vector2(c.x, c.z) - a2, v2 = p - a2;
                float det = v0.x * v1.y - v1.x * v0.y;
                if (Mathf.Abs(det) < 1e-7f) continue;
                float u = (v2.x * v1.y - v1.x * v2.y) / det, v = (v0.x * v2.y - v2.x * v0.y) / det;
                if (u < 0.0f || v < 0.0f || u + v > 1.0f) continue;
                float y = a.y + (b.y - a.y) * u + (c.y - a.y) * v + origin.y;
                if (y >= low && y <= high) return true;
            }
            return false;
        }

        [Test]
        public void AnOpenManholeIsAHoleInItsJunctionsRoadAndAShutOneIsNot()
        {
            PlanWorld plan = World.Plan;
            plan.TryFindSpawn(out double sx, out double sz);
            var rooms = new List<SewerRoom>();
            foreach (SewerRoom room in SewerPlan.For(plan).Rooms)
            {
                if (room.Shaft != null && (room.Node.P - new Vec2(sx, sz)).Length < 700.0) rooms.Add(room);
            }
            int open = 0, shut = 0;
            foreach (SewerRoom room in rooms)
            {
                if ((room.Shaft.Open ? open : shut) >= 6) continue;
                Vec2 c = room.Node.P;
                double road = plan.GroundHeight(c.X, c.Y);
                Chunk chunk = At(c.X, c.Y);
                bool covered = Covers(chunk.Ground, chunk.Origin, c.X, c.Y, road - 0.5, road + 0.5);
                // A step out from the hole, the road is whole either way.
                Vec2 beside = c + room.Shaft.Normal.Perp * (SewerPlan.NeckHalfWidth * 2.2);
                Chunk other = At(beside.X, beside.Y);
                Assert.IsTrue(Covers(other.Ground, other.Origin, beside.X, beside.Y, road - 0.8, road + 0.8), $"junction {room.Node.Id}: no road beside its manhole");
                if (room.Shaft.Open)
                {
                    open++;
                    Assert.IsFalse(covered, $"junction {room.Node.Id}: its manhole's lid is off and the road is whole over it");
                }
                else
                {
                    shut++;
                    Assert.IsTrue(covered, $"junction {room.Node.Id}: its manhole is shut and there is a hole in the road");
                }
                // Under it, the room: a floor at the ladder's foot.
                Assert.IsTrue(Covers(chunk.Structures, chunk.Origin, beside.X, beside.Y, room.CentreFloor - 0.6, room.CentreFloor + 0.6), $"junction {room.Node.Id}: no room's floor under it");
            }
            Assert.Greater(open, 2, "too few open manholes near the spawn to tell");
            Assert.Greater(shut, 2, "too few shut manholes near the spawn to tell");
        }

        /// <summary>One face of a tunnel as a chunk built it: its first corner in the chunk's structures, its middle, and the tunnel's axis beside it.</summary>
        private struct TunnelFace
        {
            public MeshBuffer Buffer;
            public int Corner, Road;
            public Vector3 Middle, Axis;
            public string Where;
        }

        /// <summary>The faces of a dozen tunnels near the spawn, a slice of each every five metres, away from the rooms at their ends.</summary>
        private IEnumerable<TunnelFace> TunnelFaces()
        {
            PlanWorld plan = World.Plan;
            SewerPlan sewers = SewerPlan.For(plan);
            plan.TryFindSpawn(out double sx, out double sz);
            int tunnels = 0;
            foreach (RoadEdge edge in plan.Data.Edges)
            {
                SewerTube tube = sewers.Tube(edge.Id);
                if (tube == null || tube.End - tube.Start < 20.0 || (tube.At(tube.Start) - new Vec2(sx, sz)).Length > 500.0) continue;
                if (++tunnels > 12) break;
                SewerSection section = tube.Section;
                for (double along = tube.Start + 3.0; along < tube.End - 3.0; along += 5.0)
                {
                    Vec2 at = tube.At(along);
                    Chunk chunk = At(at.X, at.Y);
                    MeshBuffer s = chunk.Structures;
                    double floor = tube.FloorAt(along);
                    var axis = new Vector3((float)(at.X - chunk.Origin.x), (float)(floor + section.Crown * 0.45), (float)(at.Y - chunk.Origin.z));
                    for (int t = 0; t + 2 < s.Triangles.Count; t += 3)
                    {
                        Vector3 middle = (s.Vertices[s.Triangles[t]] + s.Vertices[s.Triangles[t + 1]] + s.Vertices[s.Triangles[t + 2]]) / 3.0f;
                        var flat = new Vec2(middle.x + chunk.Origin.x - at.X, middle.z + chunk.Origin.z - at.Y);
                        if (System.Math.Abs(Vec2.Dot(flat, tube.Dir)) > 2.0 || System.Math.Abs(Vec2.Cross(tube.Dir, flat)) > section.HalfWidth + 0.05) continue;
                        if (middle.y < floor - section.ChannelDepth - 0.05 || middle.y > floor + section.Crown + 0.6) continue;
                        yield return new TunnelFace
                        {
                            Buffer = s, Corner = t, Road = edge.Id, Middle = middle, Axis = axis,
                            Where = $"the tunnel under road {edge.Id} ({section.Kind} {section.Style}), {along:F0} m along",
                        };
                    }
                }
            }
        }

        [Test]
        public void EveryFaceOfATunnelLooksIntoIt()
        {
            int faces = 0;
            var tunnels = new HashSet<int>();
            foreach (TunnelFace face in TunnelFaces())
            {
                faces++;
                tunnels.Add(face.Road);
                Vector3 normal = face.Buffer.Normals[face.Buffer.Triangles[face.Corner]];
                Assert.Greater(Vector3.Dot(normal, (face.Axis - face.Middle).normalized), -0.05f, $"{face.Where}: a face looks out of it");
            }
            Assert.Greater(tunnels.Count, 5);
            Assert.Greater(faces, 2000, "the tunnels' faces were not found in the chunks");
        }

        [Test]
        public void ATunnelIsMarkedUnderTheGroundAndTheStreetOverItIsNot()
        {
            // The mark is what keeps the sun, the moon and the street's lamps off a tunnel's walls (SurfaceLook.PackUnderground): without it
            // a sewer at noon is lit like a cellar with the door open.
            int faces = 0;
            foreach (TunnelFace face in TunnelFaces())
            {
                faces++;
                for (int corner = 0; corner < 3; corner++)
                {
                    Assert.IsTrue(SurfaceLook.IsUnderground(face.Buffer.Colors[face.Buffer.Triangles[face.Corner + corner]]),
                        $"{face.Where}: a face the sky lights");
                }
            }
            Assert.Greater(faces, 2000, "the tunnels' faces were not found in the chunks");
            foreach (Chunk chunk in _built.Values)
            {
                foreach (MeshBuffer street in new[] { chunk.Ground, chunk.Pavement })
                {
                    foreach (Color32 look in street.Colors)
                    {
                        Assert.IsFalse(SurfaceLook.IsUnderground(look), "a piece of the street is marked under the ground: nothing would light it");
                    }
                }
            }
        }
    }
}
