using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sidewalk as an American one (World/Sidewalks, docs/AMERICAN_STREETS.md): concrete scored into flags whose joints run square to
    /// the sidewalk's own street, the first a kerb stone's width in from the edge and the last at the back of the walk; and the street the
    /// chunks build is laid that way.
    /// </summary>
    public sealed class SidewalkPavingTests
    {
        private const int ReachChunks = 3;

        [Test]
        public void ASidewalksFirstJointIsAKerbStoneInAndItsLastIsTheBackOfTheWalk()
        {
            PlanWorld plan = SpawnStreets.Plan;
            var seen = new HashSet<int>();
            int sides = 0, laid = 0;
            foreach ((int x, int y) in SpawnStreets.Chunks(ReachChunks))
            {
                foreach (int index in plan.BlocksIn(x, y))
                {
                    PlanBlock block = plan.Data.Blocks[index];
                    if (!seen.Add(index) || block.Inner == null)
                    {
                        continue;
                    }
                    for (int side = 0; side < block.Inner.Count; side++)
                    {
                        sides++;
                        if (!Sidewalks.TryAlong(block, side, out Vec2[] strip, out PavingFrame frame))
                        {
                            continue;
                        }
                        laid++;
                        Vec2 kerb = strip[0], kerbTo = strip[1], lot = strip[3];
                        string where = $"block {index} side {side}";
                        Vec2 along = (kerbTo - kerb).Normalized;
                        double width = Vec2.Dot(lot - kerb, frame.Inward);
                        Assert.Greater(width, Sidewalks.NarrowestLaid - 1e-9, where);
                        Assert.AreEqual(1.0, Vec2.Dot(frame.Along, along), 1e-9, $"{where}: its rows do not run along its kerb");
                        Assert.AreEqual(0.0, Vec2.Dot(frame.Along, frame.Inward), 1e-9, where);

                        // Along the kerb a metre of street is a metre of pattern: the joints across the walk are a flag apart.
                        frame.At(kerb + along * 7.0, out double seven, out _);
                        Assert.AreEqual(7.0, seven, 1e-9, where);

                        // Across it: the first joint a kerb stone in, the last at the back, whole rows between, each about a flag deep.
                        Vec2 middle = kerb + along * ((kerbTo - kerb).Length * 0.5);
                        frame.At(middle + frame.Inward * Sidewalks.KerbStoneWidth, out _, out double first);
                        frame.At(middle + frame.Inward * width, out _, out double last);
                        frame.At(middle, out _, out double atTheKerb);
                        Assert.AreEqual(0.0, first, 1e-9, $"{where}: the first joint is not a kerb stone in from the edge");
                        Assert.Less(atTheKerb, 0.0, where);
                        int rows = Sidewalks.Rows(width);
                        Assert.AreEqual(rows * Sidewalks.FlagMetres, last, 1e-6, $"{where}: the back of the walk is not on a joint");
                        double deep = (width - Sidewalks.KerbStoneWidth) / rows;
                        Assert.That(deep, Is.InRange(Sidewalks.FlagMetres * 0.5, Sidewalks.FlagMetres * 1.5), $"{where}: its rows are {deep:0.00} m deep, nothing like a flag");
                    }
                }
            }
            TestContext.WriteLine($"{laid} of {sides} sides of the blocks round the spawn have a sidewalk laid in its street's frame");
            Assert.Greater(laid, sides * 0.9, "many sides of blocks have no sidewalk of their own");
        }

        /// <summary>The paving a chunk built over a point of the world: its texture coordinates there. False where none lies at the pavement's height.</summary>
        private static bool PavingAt(MeshBuffer pavement, Vector3 origin, Vec2 at, float height, out Vector2 texture)
        {
            texture = default;
            var p = new Vector2((float)at.X - origin.x, (float)at.Y - origin.z);
            for (int t = 0; t + 2 < pavement.Triangles.Count; t += 3)
            {
                int ia = pavement.Triangles[t], ib = pavement.Triangles[t + 1], ic = pavement.Triangles[t + 2];
                Vector3 a = pavement.Vertices[ia], b = pavement.Vertices[ib], c = pavement.Vertices[ic];
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.5f)
                {
                    continue;
                }
                SurfaceLook.Unpack(pavement.Colors[ia], out _, out int slice, out _);
                if (slice != SurfaceLook.Paving)
                {
                    continue;
                }
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f)
                {
                    continue;
                }
                float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / d;
                float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / d;
                float w = 1.0f - u - v;
                if (u < 0.0f || v < 0.0f || w < 0.0f || Mathf.Abs(u * a.y + v * b.y + w * c.y - height) > 0.05f)
                {
                    continue;
                }
                texture = u * pavement.Uvs[ia] + v * pavement.Uvs[ib] + w * pavement.Uvs[ic];
                return true;
            }
            return false;
        }

        [Test]
        public void TheBuiltSidewalkIsLaidInItsStreetsFrameAndWhatLiesBehindItInTheBlocks()
        {
            PlanWorld plan = SpawnStreets.Plan;
            int onTheWalks = 0, behindThem = 0, chunks = 0;
            foreach ((int x, int y) in SpawnStreets.Chunks(ReachChunks))
            {
                if (chunks++ % 4 != 0)
                {
                    continue;   // a spread of chunks, not every one: each is a whole build
                }
                double size = PlanWorld.ChunkSize;
                SpawnStreets.Surface built = SpawnStreets.Build(x, y);
                foreach (int index in plan.BlocksIn(x, y))
                {
                    PlanBlock block = plan.Data.Blocks[index];
                    if (block.Inner == null || block.Outer == null || block.Plaza || block.Mall)
                    {
                        continue;
                    }
                    for (int side = 0; side < block.Inner.Count; side++)
                    {
                        if (!Sidewalks.TryAlong(block, side, out Vec2[] strip, out PavingFrame frame))
                        {
                            continue;
                        }
                        double length = (strip[1] - strip[0]).Length;
                        double width = Vec2.Dot(strip[3] - strip[0], frame.Inward);
                        // Down the middle of the walk and a little way behind it, well clear of the corners at its ends (theirs is the next side's).
                        for (double metres = 7.0; metres < length - 7.0; metres += 4.0)
                        {
                            foreach (double inFromTheKerb in new[] { width * 0.5, width + 0.6 })
                            {
                                Vec2 at = strip[0] + frame.Along * metres + frame.Inward * inFromTheKerb;
                                bool behind = inFromTheKerb > width;
                                if (at.X < x * size + 0.5 || at.X > (x + 1) * size - 0.5 || at.Y < y * size + 0.5 || at.Y > (y + 1) * size - 0.5
                                    || (behind && !PlanMath.PointInPolygon(at, block.Outer)) || (!behind && !PlanMath.PointInPolygon(at, strip)))
                                {
                                    continue;
                                }
                                if (!PavingAt(built.Pavement, built.Origin, at, SpawnStreets.PavementHeight(at), out Vector2 texture))
                                {
                                    continue;   // cut away here (a ramp's foot, a basement, a stairway), or under a building's own floor
                                }
                                PavingFrame want = behind ? Sidewalks.Behind(block) : frame;
                                want.At(at, out double wantAlong, out double wantAcross);
                                string where = $"block {index} side {side}, {(behind ? "behind the walk" : "on the walk")} at {at.X:0.0},{at.Y:0.0}";
                                Assert.AreEqual(wantAlong, texture.x, 0.01, $"{where}: the paving is not laid along its frame");
                                Assert.AreEqual(wantAcross, texture.y, 0.01, $"{where}: the paving is not laid across its frame");
                                if (behind) behindThem++; else onTheWalks++;
                            }
                        }
                    }
                }
            }
            TestContext.WriteLine($"{onTheWalks} places on sidewalks and {behindThem} behind them looked at");
            Assert.Greater(onTheWalks, 40, "hardly any sidewalk was built to be looked at");
            Assert.Greater(behindThem, 10, "hardly any paving behind a sidewalk was built to be looked at");
        }

        [Test]
        public void ThePavingsTileIsFlagsWithAJointRoundEachOne()
        {
            Assert.AreEqual(Sidewalks.FlagMetres * 2.0, FacadeTextures.TileMetresX[SurfaceLook.Paving], 1e-6, "the tile is not two flags across");
            Assert.AreEqual(Sidewalks.FlagMetres * 2.0, FacadeTextures.TileMetresY[SurfaceLook.Paving], 1e-6, "the tile is not two flags up");
            const int size = 256;
            FacadeTextures.Tile tile = FacadeTextures.Generate(SurfaceLook.Paving, size);
            // The joints: the tile's own edge and its middle, both ways. A flag's middle is a quarter of the way in.
            foreach (int joint in new[] { 0, size / 2 })
            {
                float alongTheJoint = 0.0f, acrossTheFlag = 0.0f, upTheJoint = 0.0f, upTheFlag = 0.0f;
                for (int i = 0; i < size; i++)
                {
                    alongTheJoint += tile.R[joint * size + i];
                    acrossTheFlag += tile.R[(joint + size / 4) * size + i];
                    upTheJoint += tile.R[i * size + joint];
                    upTheFlag += tile.R[i * size + joint + size / 4];
                }
                Assert.Less(alongTheJoint, acrossTheFlag - 0.15f * size, $"no joint along the walk at row {joint}");
                Assert.Less(upTheJoint, upTheFlag - 0.15f * size, $"no joint across the walk at column {joint}");
            }
        }
    }
}
