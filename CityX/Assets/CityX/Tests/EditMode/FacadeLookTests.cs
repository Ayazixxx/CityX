using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The look of a building's outside: what a vertex carries (SurfaceLook), what a building is made of (BuildingLook), and the eight
    /// procedural wall textures (FacadeTextures).
    /// </summary>
    public sealed class FacadeLookTests
    {
        private const int Seed = 45678679;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        // ---- SurfaceLook: the vertex colour ----

        [Test]
        public void ALookSurvivesBeingPackedIntoAVertexColour()
        {
            for (int slice = 0; slice < FacadeTextures.Columns * FacadeTextures.Rows; slice++)
            {
                foreach (float wear in new[] { 0.0f, 0.4f, 1.0f })
                {
                    Color32 packed = SurfaceLook.Pack(new Color32(200, 120, 40, 255), slice, wear);
                    SurfaceLook.Unpack(packed, out Color32 tint, out int gotSlice, out float gotWear);
                    Assert.AreEqual(slice, gotSlice);
                    Assert.AreEqual(wear, gotWear, 1.0f / (SurfaceLook.WearLevels - 1), $"slice {slice}");
                    Assert.AreEqual(200, tint.r);
                    Assert.AreEqual(120, tint.g);
                    Assert.AreEqual(40, tint.b);
                }
            }
        }

        [Test]
        public void ANeverStyledVertexDecodesAsPlainCleanMaterial()
        {
            // Unity gives a mesh with no vertex colours opaque white; that has to mean slice 0, no wear, no tint.
            SurfaceLook.Unpack(new Color32(255, 255, 255, 255), out Color32 tint, out int slice, out float wear);
            Assert.AreEqual(0, slice);
            Assert.AreEqual(0.0f, wear);
            Assert.AreEqual(255, tint.r);
            Assert.AreEqual(new Color32(255, 255, 255, 255), SurfaceLook.Default);
        }

        [Test]
        public void EveryVertexCarriesTheLookThatWasSetWhenItWasWritten()
        {
            var buffer = new MeshBuffer();
            Color32 first = SurfaceLook.Pack(new Color32(10, 20, 30, 255), 3, 0.5f);
            Color32 second = SurfaceLook.Pack(new Color32(90, 80, 70, 255), 5, 0.1f);

            buffer.Look = first;
            buffer.AddBox(Vector3.zero, Vector3.one);
            int afterFirst = buffer.Vertices.Count;
            buffer.Look = second;
            buffer.AddQuad(Vector3.zero, Vector3.right, new Vector3(1, 1, 0), Vector3.up, Vector3.back,
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            buffer.AddTriangle(Vector3.zero, Vector3.right, Vector3.up, Vector3.back, Vector2.zero, Vector2.right, Vector2.up);

            Assert.AreEqual(buffer.Vertices.Count, buffer.Colors.Count, "one colour per vertex, always");
            for (int i = 0; i < afterFirst; i++)
            {
                Assert.AreEqual(first, buffer.Colors[i]);
            }
            for (int i = afterFirst; i < buffer.Colors.Count; i++)
            {
                Assert.AreEqual(second, buffer.Colors[i]);
            }
        }

        [Test]
        public void AnUvOffsetMovesEveryUvAndNothingElse()
        {
            var plain = new MeshBuffer();
            var moved = new MeshBuffer { UvOffset = new Vector2(3.5f, -2.0f) };
            plain.AddBox(Vector3.zero, new Vector3(2, 3, 4));
            moved.AddBox(Vector3.zero, new Vector3(2, 3, 4));
            for (int i = 0; i < plain.Uvs.Count; i++)
            {
                Assert.AreEqual(plain.Uvs[i].x + 3.5f, moved.Uvs[i].x, 1e-5f);
                Assert.AreEqual(plain.Uvs[i].y - 2.0f, moved.Uvs[i].y, 1e-5f);
                Assert.AreEqual(plain.Vertices[i], moved.Vertices[i]);
            }
        }

        // ---- BuildingLook ----

        [Test]
        public void ABuildingsLookIsTheSameEveryTimeItIsAsked()
        {
            BuildingSite site = default;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 3))
            {
                site = s;
                break;
            }
            BuildingLook a = BuildingLook.For(site, FacadeStyle.Punched);
            BuildingLook b = BuildingLook.For(site, FacadeStyle.Punched);
            Assert.AreEqual(a.Wall, b.Wall);
            Assert.AreEqual(a.WallTint, b.WallTint);
            Assert.AreEqual(a.WallWear, b.WallWear);
            Assert.AreEqual(a.UvOffset, b.UvOffset);
        }

        [Test]
        public void TheCityIsMadeOfManyMaterialsNotOne()
        {
            var counts = new Dictionary<WallMaterial, int>();
            int total = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 20))
            {
                BuildingLook look = BuildingLook.For(site, WindowPlan.For(World, site).Style);
                counts.TryGetValue(look.Wall, out int n);
                counts[look.Wall] = n + 1;
                total++;
            }
            Assert.Greater(total, 200, "need a real sample of the city");
            Assert.GreaterOrEqual(counts.Count, 5, "at least five different wall materials in a stretch of city");
            foreach (KeyValuePair<WallMaterial, int> pair in counts)
            {
                Assert.Less(pair.Value, total * 0.6f, $"{pair.Key} is more than 60% of the buildings: that is one material again");
            }
        }

        [Test]
        public void AGlassTowerIsMadeOfGlassyOrMetalPanelsNeverBrick()
        {
            for (int i = 0; i < 300; i++)
            {
                BuildingLook look = BuildingLook.For(1000 + i, ZoneKind.Core, BuildingMass.Tower, FacadeStyle.Curtain);
                Assert.That(look.Wall, Is.EqualTo(WallMaterial.Spandrel).Or.EqualTo(WallMaterial.Metal).Or.EqualTo(WallMaterial.Concrete));
            }
        }

        [Test]
        public void ADistrictThatIsKeptUpWearsLessThanOneThatIsNot()
        {
            double core = 0.0, industrial = 0.0;
            const int n = 400;
            for (int i = 0; i < n; i++)
            {
                core += BuildingLook.For(i, ZoneKind.Core, BuildingMass.MidRise, FacadeStyle.Punched).WallWear;
                industrial += BuildingLook.For(i, ZoneKind.Industrial, BuildingMass.Warehouse, FacadeStyle.HighBand).WallWear;
            }
            Assert.Less(core / n, industrial / n - 0.2);
        }

        [Test]
        public void EveryTintIsAQuietColourNotAShout()
        {
            // Restraint is the brief: dense, but not overwhelming. No wall tint is highly saturated.
            for (int i = 0; i < 600; i++)
            {
                foreach (ZoneKind zone in System.Enum.GetValues(typeof(ZoneKind)))
                {
                    BuildingLook look = BuildingLook.For(i, zone, BuildingMass.MidRise, FacadeStyle.Punched);
                    Color.RGBToHSV(look.WallTint, out _, out float saturation, out _);
                    Assert.Less(saturation, 0.5f, $"seed {i} {zone} {look.Wall}");
                }
            }
        }

        // ---- the geometry carries the look ----

        [Test]
        public void ABuildingsRoofIsRoofingAndItsWallsAreWallsInTheSameBuffer()
        {
            BuildingSite site = default;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 4))
            {
                if (s.Params.Height > 20.0f)
                {
                    site = s;
                    break;
                }
            }
            Assert.IsTrue(site.Exists, "need a building");

            var structures = new MeshBuffer();
            var concrete = new MeshBuffer();
            Vector3 origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, structures, concrete, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                null, null, new MeshBuffer(), far: false);

            var slices = new HashSet<int>();
            int roofVertices = 0;
            foreach (Color32 look in structures.Colors)
            {
                SurfaceLook.Unpack(look, out _, out int slice, out _);
                slices.Add(slice);
                roofVertices += slice == (int)WallMaterial.Roof ? 1 : 0;
            }
            Assert.IsTrue(slices.Contains((int)WallMaterial.Roof), "the roof is roofing");
            Assert.Greater(slices.Count, 1, "and the walls are something else");
            Assert.Greater(roofVertices, 0);
            Assert.AreEqual(structures.Vertices.Count, structures.Colors.Count);
        }

        [Test]
        public void EveryWindowsGlassCarriesALookOfItsOwn()
        {
            BuildingSite site = default;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 4))
            {
                if (s.Params.Height > 20.0f && s.Params.Width > 14.0f)
                {
                    site = s;
                    break;
                }
            }
            Assert.IsTrue(site.Exists, "need a building");
            Vector3 origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            var lit = new MeshBuffer();
            var dark = new MeshBuffer();
            CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), lit, dark, new MeshBuffer(), null, null, new MeshBuffer(), false);

            var seen = new HashSet<uint>();
            int styled = 0, total = 0;
            foreach (MeshBuffer glass in new[] { lit, dark })
            {
                Assert.AreEqual(glass.Vertices.Count, glass.Colors.Count);
                foreach (Color32 look in glass.Colors)
                {
                    total++;
                    if (!(look.b == 255 && look.a == 255))
                    {
                        styled++;
                    }
                    seen.Add((uint)(look.b << 8 | look.a));
                }
            }
            Assert.Greater(total, 20, "a building has windows");
            Assert.AreEqual(total, styled, "every window's glass is styled, not left as the unstyled default");
            Assert.Greater(seen.Count, 5, "and they are not all the same look");
        }

        [Test]
        public void ADistantBuildingWearsTheSameMaterialsAsTheNearOne()
        {
            BuildingSite site = default;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 4))
            {
                if (s.Params.Height > 20.0f)
                {
                    site = s;
                    break;
                }
            }
            Assert.IsTrue(site.Exists, "need a building");
            Vector3 origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);

            HashSet<int> Slices(bool far)
            {
                var structures = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                    new MeshBuffer(), null, null, new MeshBuffer(), far);
                var set = new HashSet<int>();
                foreach (Color32 look in structures.Colors)
                {
                    SurfaceLook.Unpack(look, out _, out int slice, out _);
                    set.Add(slice);
                }
                return set;
            }

            // The near building also has trim (slice 0) the shell does not; every material the shell shows, the near one shows too. (Seed 61839025
            // picks a tower standing on its podium, whose shell used to paint a street row in the base material the near tower never has.)
            HashSet<int> near = Slices(false);
            HashSet<int> far = Slices(true);
            foreach (int slice in far)
            {
                Assert.IsTrue(near.Contains(slice) || slice == (int)WallMaterial.Concrete,
                    $"far uses material {slice} that the near building ({WindowPlan.For(World, site).Style}, on a roof: {site.OnRoof}) does not");
            }
            Assert.IsTrue(far.Contains((int)WallMaterial.Roof));
        }

        // ---- FacadeTextures ----

        [Test]
        public void EveryWallTextureTilesWithoutASeam()
        {
            const int size = 128;
            for (int slice = 0; slice < FacadeTextures.Columns * FacadeTextures.Rows; slice++)
            {
                FacadeTextures.Tile tile = FacadeTextures.Generate(slice, size);
                // How different the pixels either side of the wrap are, against how different neighbouring pixels are anywhere else in the
                // tile: a seam would be a cliff, an ordinary joint or grain is not.
                double across = 0.0, within = 0.0, up = 0.0, withinUp = 0.0;
                for (int y = 0; y < size; y++)
                {
                    across += Mathf.Abs(Luma(tile, 0, y) - Luma(tile, size - 1, y));
                    for (int x = 0; x < size - 1; x++)
                    {
                        within += Mathf.Abs(Luma(tile, x, y) - Luma(tile, x + 1, y));
                    }
                }
                for (int x = 0; x < size; x++)
                {
                    up += Mathf.Abs(Luma(tile, x, 0) - Luma(tile, x, size - 1));
                    for (int y = 0; y < size - 1; y++)
                    {
                        withinUp += Mathf.Abs(Luma(tile, x, y) - Luma(tile, x, y + 1));
                    }
                }
                within /= size - 1;
                withinUp /= size - 1;
                // Either as smooth as the rest of the tile, or a small absolute step: a tile of separate ceramic squares steps between
                // two of them anywhere, and that is not a seam. A real one is a cliff of 0.1 and more.
                Assert.That(across < within * 2.5 + 0.5 || across / size < 0.06, $"{FacadeTextures.SliceNames[slice]}: horizontal seam ({across / size:F3} per row)");
                Assert.That(up < withinUp * 2.5 + 0.5 || up / size < 0.06, $"{FacadeTextures.SliceNames[slice]}: vertical seam ({up / size:F3} per column)");
            }
        }

        [Test]
        public void EveryWallTextureHasSomethingInItAndIsNotBlownOut()
        {
            for (int slice = 0; slice < FacadeTextures.Columns * FacadeTextures.Rows; slice++)
            {
                FacadeTextures.Tile tile = FacadeTextures.Generate(slice, 128);
                float min = 1.0f, max = 0.0f, sum = 0.0f;
                for (int i = 0; i < tile.R.Length; i++)
                {
                    float l = (tile.R[i] + tile.G[i] + tile.B[i]) / 3.0f;
                    min = Mathf.Min(min, l);
                    max = Mathf.Max(max, l);
                    sum += l;
                }
                Assert.Greater(max - min, 0.02f, $"{FacadeTextures.SliceNames[slice]} is flat");
                Assert.Less(sum / tile.R.Length, 0.95f, $"{FacadeTextures.SliceNames[slice]} is blown out white");
            }
        }

        [Test]
        public void TheTexturesAreDrawnTheSameEveryTime()
        {
            FacadeTextures.Tile a = FacadeTextures.Generate(1, 64);
            FacadeTextures.Tile b = FacadeTextures.Generate(1, 64);
            CollectionAssert.AreEqual(a.R, b.R);
            CollectionAssert.AreEqual(a.Height, b.Height);
        }

        [Test]
        public void TheShaderKnowsTheTileSizeOfEveryTexture()
        {
            // The shader keeps its own table of how many metres a tile covers; it has to say what the generator says.
            string path = System.IO.Path.Combine(Application.dataPath, "CityX/View/Shaders/FacadeForwardPass.hlsl");
            string text = System.IO.File.ReadAllText(path);
            int start = text.IndexOf("kTileMetres");
            int end = text.IndexOf("};", start);
            System.Text.RegularExpressions.MatchCollection entries = System.Text.RegularExpressions.Regex.Matches(
                text.Substring(start, end - start), @"float2\(([0-9.]+), ([0-9.]+)\)");
            Assert.AreEqual(FacadeTextures.Columns * FacadeTextures.Rows, entries.Count, "one entry per slice");
            for (int i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(FacadeTextures.TileMetresX[i], float.Parse(entries[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f, FacadeTextures.SliceNames[i]);
                Assert.AreEqual(FacadeTextures.TileMetresY[i], float.Parse(entries[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f, FacadeTextures.SliceNames[i]);
            }
        }

        private static float Luma(FacadeTextures.Tile tile, int x, int y)
        {
            int i = y * tile.Size + x;
            return tile.R[i] * 0.3f + tile.G[i] * 0.59f + tile.B[i] * 0.11f;
        }
    }
}
