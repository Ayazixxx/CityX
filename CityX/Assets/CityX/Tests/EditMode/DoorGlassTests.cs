using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The glass in the doors (World/DoorGlass; owner, 2026-10-16: "can you make the sliding glass doors fracturable like the glass windows"): every
    /// sliding door and pair has two sheets inside its doorway; the glass the city draws is exactly the glass a blow finds, shut and open; a cracked
    /// sheet is drawn cracked with its holes and leaves a trigger box, a broken one leaves its frame empty; and the messages that share broken glass
    /// between players (Character.GlassDamageNetwork) carry a pane and its holes, and merging one never takes a pane backwards.
    /// </summary>
    public sealed class DoorGlassTests
    {
        private const int Seed = 95998845;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [TearDown]
        public void TearDown()
        {
            BalconyDoorState.Shared.Clear();
        }

        private static List<(BuildingSite site, BalconyDoor door)> GlazedDoors(int most)
        {
            var found = new List<(BuildingSite, BalconyDoor)>();
            var doors = new List<BalconyDoor>();
            bool sliding = false, pair = false, bridge = false;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                if (site.Sides >= 3 || site.Shell) continue;
                WindowPlan plan = WindowPlan.For(World, site);
                doors.Clear();
                if (ApartmentFront.For(site, plan).Balconies) BalconyDoors.For(site, plan, doors);
                Footbridges.DoorsOf(World, site, plan, doors);
                foreach (BalconyDoor door in doors)
                {
                    if (!DoorGlass.Glazed(door)) continue;
                    bool isBridge = door.Unit >= Footbridges.DoorUnitBase;
                    // A few of each: sliding doors, pairs onto balconies, and bridges' doors.
                    if (isBridge ? bridge : (door.Kind == BalconyDoorKind.Sliding ? sliding : pair))
                    {
                        if (found.Count >= most) continue;
                    }
                    found.Add((site, door));
                    if (isBridge) bridge = true;
                    else if (door.Kind == BalconyDoorKind.Sliding) sliding = true;
                    else pair = true;
                    if (found.Count >= most && sliding && pair && bridge) return found;
                }
            }
            return found;
        }

        private static Vector3 WorldOf(BuildingSite site, Vector3 framePoint)
        {
            site.LocalToWorld(framePoint.x, framePoint.z, out double wx, out double wz);
            return new Vector3((float)wx, site.BaseHeight + framePoint.y, (float)wz);
        }

        private static Vector3 Middle(in DoorPane pane, float alongShare = 0.5f, float upShare = 0.5f)
        {
            float u = Mathf.Lerp(pane.U0, pane.U1, alongShare), y = Mathf.Lerp(pane.Y0, pane.Y1, upShare);
            return pane.RunsInZ ? new Vector3(pane.Plane, y, u) : new Vector3(u, y, pane.Plane);
        }

        [Test]
        public void EveryGlazedDoorHasTwoSheetsInsideItsDoorwayShutAndOpen()
        {
            var panes = new DoorPane[2];
            List<(BuildingSite site, BalconyDoor door)> doors = GlazedDoors(60);
            Assert.That(doors.Count, Is.GreaterThan(10), "too few glazed doors near the spawn");
            foreach ((BuildingSite site, BalconyDoor door) in doors)
            {
                foreach (bool open in new[] { false, true })
                {
                    Assert.AreEqual(2, DoorGlass.Panes(site, door, open, panes));
                    for (int i = 0; i < 2; i++)
                    {
                        DoorPane p = panes[i];
                        string what = $"building {site.Id} {door.Kind} door on the {door.Wall} wall, {(open ? "open" : "shut")}, sheet {i}";
                        Assert.AreEqual(i, DoorGlass.PanelOf(p.Key), what);
                        Assert.AreEqual(door.Unit, DoorGlass.UnitOf(p.Key), what);
                        Assert.That(p.Width, Is.GreaterThan(0.3f), what);
                        Assert.That(p.Y0, Is.GreaterThanOrEqualTo(door.Y0), what);
                        Assert.That(p.Y1, Is.LessThanOrEqualTo(door.Y1), what);
                        // In the building's frame: within the doorway's width (shut, or a sliding panel open) and within a leaf's reach of the wall.
                        Vector3 m = Middle(p);
                        float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
                        float inFromWall = door.Wall == WindowGrid.Wall.North ? hd - m.z : door.Wall == WindowGrid.Wall.South ? m.z + hd
                            : door.Wall == WindowGrid.Wall.East ? hw - m.x : m.x + hw;
                        Assert.That(inFromWall, Is.InRange(0.0f, DoorGlass.Reach), $"{what}: {inFromWall:F2} m in from the wall");
                        float along = door.Wall == WindowGrid.Wall.North || door.Wall == WindowGrid.Wall.South ? m.x : m.z;
                        Assert.That(along, Is.InRange(door.U0, door.U1), $"{what}: not in the doorway");
                    }
                }
            }
        }

        [Test]
        public void ABlowOnADoorsGlassFindsThatSheetShutAndOpen()
        {
            var panes = new DoorPane[2];
            int found = 0;
            foreach ((BuildingSite site, BalconyDoor door) in GlazedDoors(30))
            {
                foreach (bool open in new[] { false, true })
                {
                    if (open && !door.Opens) continue;
                    BalconyDoorState.Shared.Clear();
                    if (open) BalconyDoorState.Shared.Use(site.Id, door.Unit, door.Row, true);
                    DoorGlass.Panes(site, door, open, panes);
                    for (int i = 0; i < 2; i++)
                    {
                        // Off the middle of the sheet (the other sheet of a shut sliding door overlaps it at the meeting stiles).
                        Vector3 point = WorldOf(site, Middle(panes[i], i == 0 ? 0.35f : 0.65f, 0.4f));
                        string what = $"building {site.Id} {door.Kind} door on the {door.Wall} wall, {(open ? "open" : "shut")}, sheet {i}";
                        Assert.IsTrue(GlassDamage.TryPaneAt(World, site, point, out PaneKey key), $"{what}: a blow on its glass found no pane");
                        Assert.AreEqual(panes[i].Key, key, $"{what}: a blow on its glass found another pane");
                        Vector2 uv = GlassDamage.FractionWithinPane(World, site, key, point);
                        Assert.That(uv.x, Is.EqualTo(i == 0 ? 0.35f : 0.65f).Within(0.02f), what);
                        Assert.That(uv.y, Is.EqualTo(0.4f).Within(0.02f), what);
                        Assert.IsTrue(GlassPaneFrame.TryFrame(World, site, key, point, out PaneFrame frame), $"{what}: no frame to break it in");
                        Assert.That(frame.Width, Is.EqualTo(panes[i].Width).Within(0.01f), what);
                        Assert.That(frame.Height, Is.EqualTo(panes[i].Height).Within(0.01f), what);
                        Assert.That(Vector3.Distance(frame.At(frame.Of(point)), point), Is.LessThan(0.01f), $"{what}: the frame is not where the glass is");
                        found++;
                    }
                }
            }
            Assert.That(found, Is.GreaterThan(20));
        }

        [Test]
        public void TheGlassDrawnIsTheGlassABlowFindsAndADamagedSheetIsDrawnAsItsDamage()
        {
            List<(BuildingSite site, BalconyDoor door)> doors = GlazedDoors(6);
            var panes = new DoorPane[2];
            int built = 0;
            foreach ((BuildingSite site, BalconyDoor door) in doors)
            {
                var damage = new GlassDamage();
                DoorGlass.Panes(site, door, false, panes);
                // Sheet 0 shot once (cracked, a hole in its middle), sheet 1 gone.
                damage.Shoot(panes[0].Key, new Vector2(0.5f, 0.5f), out _);
                damage.Hit(panes[1].Key, null, shatterOutright: true);
                var structures = new MeshBuffer();
                var dark = new MeshBuffer();
                var lit = new MeshBuffer();
                var cracked = new MeshBuffer();
                var openings = new List<CityMeshBuilder.CrackedOpening>();
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), lit, dark, cracked, damage, openings, new MeshBuffer(), false);
                string what = $"building {site.Id} {door.Kind} door on the {door.Wall} wall";

                // The cracked sheet: cracked glass across it, a hole at its middle, and a trigger box for it.
                Assert.IsTrue(Covers(cracked, origin, site, panes[0], 0.2f, 0.25f), $"{what}: no cracked glass on the shot sheet");
                Assert.IsFalse(Covers(cracked, origin, site, panes[0], 0.5f, 0.5f), $"{what}: no hole where the bullet went");
                Assert.IsTrue(openings.Exists(o => o.Key.Equals(panes[0].Key)), $"{what}: the cracked sheet left no box for a knock or a bullet");
                Assert.IsFalse(Covers(dark, origin, site, panes[0], 0.2f, 0.25f) || Covers(lit, origin, site, panes[0], 0.2f, 0.25f),
                    $"{what}: the shot sheet is still drawn whole");
                // The broken sheet: no glass at all.
                foreach (MeshBuffer glass in new[] { dark, lit, cracked })
                {
                    Assert.IsFalse(Covers(glass, origin, site, panes[1], 0.5f, 0.5f), $"{what}: the broken sheet is still drawn");
                }
                built++;

                // Whole, the same door's glass is drawn exactly where a blow finds it.
                var whole = new MeshBuffer();
                CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), whole, whole, null, null, null, null, false);
                for (int i = 0; i < 2; i++)
                {
                    Assert.IsTrue(Covers(whole, origin, site, panes[i], 0.5f, 0.5f), $"{what}: sheet {i}'s glass is not drawn where DoorGlass says it is");
                }
            }
            Assert.That(built, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>Whether a buffer has glass on a sheet's plane over the point at <paramref name="along"/> and <paramref name="up"/> of it.</summary>
        private static bool Covers(MeshBuffer buffer, Vector3 origin, BuildingSite site, in DoorPane pane, float along, float up)
        {
            Vector3 target = WorldOf(site, Middle(pane, along, up)) - origin;
            Vector3 normal = new Vector3((float)site.AxisX, 0, (float)site.AxisZ) * pane.Normal.x + new Vector3(-(float)site.AxisZ, 0, (float)site.AxisX) * pane.Normal.z;
            List<Vector3> v = buffer.Vertices;
            List<int> t = buffer.Triangles;
            for (int k = 0; k + 2 < t.Count; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                if (Mathf.Abs(Vector3.Dot(a - target, normal)) > 0.02f || Mathf.Abs(Vector3.Dot(b - target, normal)) > 0.02f
                    || Mathf.Abs(Vector3.Dot(c - target, normal)) > 0.02f)
                {
                    continue;   // not on this plane
                }
                // In the plane: barycentric on the two axes that span it (along and up).
                Vector3 across = Vector3.Cross(Vector3.up, normal);
                Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p, across), p.y);
                Vector2 pa = P(a), pb = P(b), pc = P(c), pt = P(target);
                float d = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float wa = ((pb.y - pc.y) * (pt.x - pc.x) + (pc.x - pb.x) * (pt.y - pc.y)) / d;
                float wb = ((pc.y - pa.y) * (pt.x - pc.x) + (pa.x - pc.x) * (pt.y - pc.y)) / d;
                if (wa >= -1e-4f && wb >= -1e-4f && wa + wb <= 1.0001f) return true;
            }
            return false;
        }

        [Test]
        public void TheSharedMessagesCarryAPaneAndItsHolesAndAMergeNeverTakesAPaneBack()
        {
            var key = new PaneKey(12, WindowGrid.Wall.South, DoorGlass.PaneColumnBase + 7 * 2 + 1, 4);
            var change = new PaneChange(key, PaneState.Cracked, new[] { new Vector2(0.2f, 0.3f), new Vector2(0.7f, 0.6f) }, new Vector3(1, 2, 3),
                Vector3.forward, BlowStrength.Bullet);
            GlassPaneMessage message = GlassDamageNetwork.ToMessage(change);
            Assert.IsTrue(GlassDamageNetwork.Acceptable(message));
            PaneChange back = GlassDamageNetwork.ToChange(message);
            Assert.AreEqual(key, back.Key);
            Assert.AreEqual(PaneState.Cracked, back.State);
            Assert.AreEqual(2, back.Impacts.Length);
            Assert.AreEqual(new Vector2(0.7f, 0.6f), back.Impacts[1]);
            Assert.AreEqual(new Vector3(1, 2, 3), back.Point);

            // Nonsense is turned away.
            GlassPaneMessage bad = message;
            bad.State = (int)PaneState.Intact;
            Assert.IsFalse(GlassDamageNetwork.Acceptable(bad), "a message that mends a pane");
            bad = message;
            bad.HoleU = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
            bad.HoleV = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
            Assert.IsFalse(GlassDamageNetwork.Acceptable(bad), "more holes than a pane takes");
            bad = message;
            bad.HoleU = new[] { float.NaN, 0.5f };
            Assert.IsFalse(GlassDamageNetwork.Acceptable(bad), "a hole that is not on the pane");

            // Merging: forward only.
            var record = new GlassDamage();
            record.Merge(change, out bool changed);
            Assert.IsTrue(changed);
            Assert.AreEqual(2, record.HoleCount(key));
            var older = new PaneChange(key, PaneState.Cracked, new[] { new Vector2(0.2f, 0.3f) }, Vector3.zero, Vector3.zero, BlowStrength.Bullet);
            record.Merge(older, out changed);
            Assert.IsFalse(changed, "an older message took a hole away");
            Assert.AreEqual(2, record.HoleCount(key));
            var gone = new PaneChange(key, PaneState.Shattered, new Vector2[0], Vector3.zero, Vector3.zero, BlowStrength.Light);
            record.Merge(gone, out changed);
            Assert.IsTrue(changed);
            record.Merge(change, out changed);
            Assert.IsFalse(changed, "a late message put a broken pane back up");
            Assert.AreEqual(PaneState.Shattered, record.StateOf(key));

            // A joining player's state: every broken pane and its holes, both ways.
            var other = new PaneKey(13, WindowGrid.Wall.North, 3, 2);
            record.Merge(new PaneChange(other, PaneState.Cracked, new[] { new Vector2(0.5f, 0.5f) }, Vector3.zero, Vector3.zero, BlowStrength.Bullet), out _);
            List<PaneChange> state = GlassDamageNetwork.FromState(GlassDamageNetwork.ToState(record.Snapshot()));
            Assert.AreEqual(2, state.Count);
            var joiner = new GlassDamage();
            foreach (PaneChange p in state) joiner.Merge(p, out _);
            Assert.AreEqual(PaneState.Shattered, joiner.StateOf(key));
            Assert.AreEqual(PaneState.Cracked, joiner.StateOf(other));
            Assert.AreEqual(1, joiner.HoleCount(other));
        }
    }
}
