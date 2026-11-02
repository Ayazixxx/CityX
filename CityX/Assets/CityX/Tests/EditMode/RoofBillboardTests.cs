using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Billboards on the roofs (World/RoofBillboards, View/Buildings/CityMeshBuilder.Billboards.cs, World Gacha phase 4): on flat roofs of mid-rise buildings,
    /// most downtown, facing the street, inside the roof's edges and clear of the stair's hatch, high enough to walk under, and drawn where they
    /// are said to be.
    /// </summary>
    public sealed class RoofBillboardTests
    {
        private const int Seed = 95998845;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [Test]
        public void BillboardsStandOnFlatMidRiseRoofsMostlyDowntown()
        {
            var could = new Dictionary<ZoneKind, int>();
            var have = new Dictionary<ZoneKind, int>();
            int boards = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                RoofBillboard b = RoofBillboards.For(site);
                bool eligible = site.Exists && !site.Shell && !site.Params.IsSpecial && site.Sides < 3 && !site.HasRoofObstacle && !site.RoofSealed
                    && site.Params.Height >= RoofBillboards.MinBuildingHeight && site.Params.Height <= RoofBillboards.MaxBuildingHeight
                    && !RoofShape.For(site).Pitched;
                if (b.Exists)
                {
                    boards++;
                    string what = $"building {site.Id} ({site.Zone}, {site.Params.Width:F1} x {site.Params.Depth:F1} x {site.Params.Height:F1})";
                    Assert.IsTrue(eligible, $"{what}: a billboard where none can stand");
                    Assert.That(b.Width, Is.InRange(RoofBillboards.MinWidth, RoofBillboards.MaxWidth), what);
                    Assert.That(b.Height, Is.InRange(RoofBillboards.MinHeight, RoofBillboards.MaxHeight), what);
                    Assert.That(Mathf.Abs(b.Offset) + b.Width * 0.5f, Is.LessThanOrEqualTo(site.Params.Width * 0.5f - 1.5f + 1e-3f), $"{what}: past the roof's end");
                    Assert.That(b.Lift - RoofBillboards.CatwalkBelow, Is.GreaterThan(BodyHeightWithSkin + 0.2f), $"{what}: a body walking on the roof hits its catwalk");
                    Assert.AreNotEqual(b.Background, b.Accent, $"{what}: an ad in one colour");
                }
                if (!eligible) continue;
                could[site.Zone] = (could.TryGetValue(site.Zone, out int c) ? c : 0) + 1;
                have[site.Zone] = (have.TryGetValue(site.Zone, out int h) ? h : 0) + (b.Exists ? 1 : 0);
            }
            float Share(ZoneKind z) => could.TryGetValue(z, out int n) && n > 0 ? have[z] / (float)n : -1.0f;
            Debug.Log($"[Billboards] {boards} boards; core {Share(ZoneKind.Core):F2}, urban {Share(ZoneKind.Urban):F2}, old town {Share(ZoneKind.OldTown):F2}, " +
                $"residential {Share(ZoneKind.Residential):F2}");
            Assert.That(boards, Is.GreaterThan(30), "need billboards to check");
            Assert.That(Share(ZoneKind.Core), Is.GreaterThan(Share(ZoneKind.Residential)), "more downtown than among the homes");
        }

        private static float BodyHeightWithSkin => CityX.Character.BodyRig.StandingHeight + 2.0f * 0.03f;

        [Test]
        public void ABillboardIsDrawnWhereItStandsFacingTheStreet()
        {
            int drawn = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                RoofBillboard b = RoofBillboards.For(site);
                if (!b.Exists || site.Contacts != null && site.Contacts.Length > 0) continue;
                // Never on the stair's hatch: RoofBillboards.For moves it off (or leaves it out), so the builder draws every one it says there is.
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                StairPlan roofStair = stairs.NoRoofStair ? null : stairs.Floors[stairs.FloorCount - 1];
                if (roofStair != null)
                {
                    Rect h = roofStair.Hole;
                    Assert.IsFalse(Rect.MinMaxRect(h.xMin - 1.2f, h.yMin - 1.2f, h.xMax + 1.2f, h.yMax + 1.2f).Overlaps(RoofBillboards.Ground(site, b.Width, b.Offset)),
                        $"building {site.Id}: a billboard on the stair's hatch");
                }
                var structures = new MeshBuffer();
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), null, null,
                    new MeshBuffer(), false);
                // The board's middle: over the roof by its lift and half its height, along the street wall by its offset, set back from the wall.
                float roofY = site.Top;
                site.LocalToWorld(b.Offset, site.Params.Depth * 0.5f - RoofBillboards.SetBack - RoofBillboards.Thickness * 0.5f, out double mx, out double mz);
                var middle = new Vector3((float)mx, roofY + b.Lift + b.Height * 0.5f, (float)mz) - origin;
                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                bool hit = false;
                for (int t = 0; t + 2 < structures.Triangles.Count && !hit; t += 3)
                {
                    hit = SegmentHits(middle + outward * 2.0f, middle - outward * 0.05f, structures.Vertices[structures.Triangles[t]],
                        structures.Vertices[structures.Triangles[t + 1]], structures.Vertices[structures.Triangles[t + 2]]);
                }
                Assert.IsTrue(hit, $"building {site.Id}: no board facing the street where its billboard stands (unless its stair's hatch moved it off)");
                if (++drawn >= 12) break;
            }
            Assert.That(drawn, Is.GreaterThan(3), "need billboards to check");
        }

        [Test]
        public void AFloodlitBoardGlowsAtNightOnlyOnABuildingWithPowerOverItsOwnFace()
        {
            // The floodlit face's look is told from every glass look the shader knows (SurfaceLook.PackFloodlit).
            for (int tint = 0; tint < 8; tint++)
            {
                for (int light = 0; light < 8; light++)
                {
                    Assert.IsFalse(SurfaceLook.IsFloodlit(SurfaceLook.PackGlass(tint, light, 1.0f, true)), $"glass {tint}/{light} reads as floodlit");
                }
            }
            Assert.IsTrue(SurfaceLook.IsFloodlit(SurfaceLook.PackFloodlit(new Color(1.0f, 1.0f, 1.0f))));

            int powered = 0, dead = 0, far = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                RoofBillboard b = RoofBillboards.For(site);
                if (!b.Exists) continue;
                bool on = PowerGrid.IsOn(site);
                if (!on && dead >= 10) continue;
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                var lit = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), lit, new MeshBuffer(), new MeshBuffer(), null, null,
                    new MeshBuffer(), false);
                string what = $"building {site.Id} ({site.Zone}, powered {on}, floodlit {b.Floodlit})";
                int faces = CheckFloodlitFaces(site, b, origin, lit, what);
                if (!on || !b.Floodlit)
                {
                    Assert.AreEqual(0, faces, $"{what}: a board lit with nothing to light it");
                    if (!on) dead++;
                    continue;
                }
                Assert.That(faces, Is.GreaterThanOrEqualTo(4), $"{what}: its face is not lit (the background, the accent, two lines of lettering)");
                powered++;

                // From afar too: a lit board is seen from across the city.
                var farLit = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), farLit, new MeshBuffer(), null, null, null, null, true);
                if (CheckFloodlitFaces(site, b, origin, farLit, what + " far away") == faces) far++;
            }
            Debug.Log($"[Billboards] {powered} lit boards on powered buildings ({far} lit from afar), {dead} dead ones dark");
            Assert.That(powered, Is.GreaterThan(3), "need lit boards to check");
            Assert.AreEqual(powered, far, "a lit board is dark from afar");
            Assert.That(dead, Is.GreaterThan(3), "need dead boards to check");
        }

        /// <summary>How many floodlit quads a lit buffer holds, each checked to lie on the board's face, facing the street, UV'd as the shader reads it.</summary>
        private static int CheckFloodlitFaces(BuildingSite site, in RoofBillboard b, Vector3 origin, MeshBuffer lit, string what)
        {
            int corners = 0;
            Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            float bottom = site.Top + b.Lift, top = bottom + b.Height;
            float face = site.Params.Depth * 0.5f - RoofBillboards.SetBack;
            for (int i = 0; i < lit.Vertices.Count; i++)
            {
                if (!SurfaceLook.IsFloodlit(lit.Colors[i])) continue;
                corners++;
                Vector3 v = lit.Vertices[i] + origin;
                site.WorldToLocal(v.x, v.z, out double lx, out double lz);
                Assert.That(v.y, Is.InRange(bottom - 0.01f, top + 0.01f), $"{what}: a floodlit face above or below its board");
                Assert.That((float)lx, Is.InRange(b.Offset - b.Width * 0.5f - 0.01f, b.Offset + b.Width * 0.5f + 0.01f), $"{what}: a floodlit face past its board's end");
                Assert.That((float)lz, Is.InRange(face, face + 0.04f), $"{what}: a floodlit face off its board's face");
                Assert.That(Vector3.Dot(lit.Normals[i], outward), Is.GreaterThan(0.99f), $"{what}: a floodlit face not facing the street");
                Assert.That(lit.Uvs[i].y, Is.InRange(-1e-3f, 1.0f + 1e-3f), $"{what}: a floodlit face UV'd past its board's foot or top");
            }
            return corners / 4;
        }

        private static bool SegmentHits(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 dir = p1 - p0, e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1.0f / det;
            Vector3 s = p0 - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0.0f || u > 1.0f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0.0f || u + v > 1.0f) return false;
            float t = Vector3.Dot(e2, q) * inv;
            return t >= 0.0f && t <= 1.0f;
        }
    }
}
