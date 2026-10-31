using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A door's glass breaks like a window's in a real streamed city (owner, 2026-10-16: "can you make the sliding glass doors fracturable like the
    /// glass windows"): a shot from outside reaches the glass and finds its sheet, every bullet leaves its own hole and the fifth brings the sheet
    /// down into its own pieces, and walking into a cracked sheet breaks it into its pieces too.
    /// </summary>
    public sealed class DoorGlassPlayTests
    {
        private const int Seed = 61256619;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            BalconyDoorState.Shared.Clear();
        }

        /// <summary>A shut sliding door or pair onto a balcony near the spawn, and its building.</summary>
        private static bool FindDoor(CityWorld world, out BuildingSite site, out BalconyDoor door)
        {
            var doors = new List<BalconyDoor>();
            foreach (BuildingSite s in TestSites.NearSpawn(world, 30))
            {
                if (s.Sides >= 3 || s.Shell) continue;
                WindowPlan plan = WindowPlan.For(world, s);
                if (!ApartmentFront.For(s, plan).Balconies) continue;
                doors.Clear();
                BalconyDoors.For(s, plan, doors);
                foreach (BalconyDoor d in doors)
                {
                    if (!DoorGlass.Glazed(d) || d.Row < 2) continue;
                    site = s;
                    door = d;
                    return true;
                }
            }
            site = default;
            door = default;
            return false;
        }

        private CityStreamer City(Vector3 around, out GlassBreaker breaker)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;   // a broken pane rebuilds its chunk at once
            streamer.SpawnPreStreamBudget = 0;
            breaker = go.AddComponent<GlassBreaker>();
            var shardLook = new GameObject("ShardLook");
            _made.Add(shardLook);
            shardLook.SetActive(false);
            shardLook.AddComponent<MeshFilter>();
#if UNITY_EDITOR
            shardLook.AddComponent<MeshRenderer>().sharedMaterial =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/GlassShards.mat");
#endif
            breaker.ShardPrefab = shardLook;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
            return streamer;
        }

        private static Vector3 WorldOf(BuildingSite site, in DoorPane pane, float along, float up)
        {
            float u = Mathf.Lerp(pane.U0, pane.U1, along), y = Mathf.Lerp(pane.Y0, pane.Y1, up);
            float lx = pane.RunsInZ ? pane.Plane : u, lz = pane.RunsInZ ? u : pane.Plane;
            site.LocalToWorld(lx, lz, out double wx, out double wz);
            return new Vector3((float)wx, site.BaseHeight + y, (float)wz);
        }

        [UnityTest]
        public IEnumerator AShotReachesADoorsGlassEveryBulletLeavesAHoleAndTheFifthBringsItDownInPieces()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoor(world, out BuildingSite site, out BalconyDoor door), "no glazed balcony door near the spawn");
            var panes = new DoorPane[2];
            DoorGlass.Panes(site, door, false, panes);
            DoorPane pane = panes[0];
            BalconyDoors.InWorld(site, door, out _, out _, out Vector3 outward);
            Vector3 onGlass = WorldOf(site, pane, 0.4f, 0.45f);
            CityStreamer streamer = City(onGlass, out GlassBreaker breaker);
            yield return null;
            Physics.SyncTransforms();

            // A shot from the balcony, 1 m out (on it: a balcony is 1.5-2.1 m deep, and from 2 m out the shot at a shallow one met its 1.35 m parapet,
            // seed 63428175): the ray meets something that takes the blow, and that is this sheet of this door.
            Vector3 muzzle = onGlass + outward * 1.0f;
            Assert.IsTrue(Physics.Raycast(muzzle, -outward, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore), "the shot met nothing");
            var breakable = hit.collider.GetComponentInParent<IBreakableSurface>();
            Assert.IsNotNull(breakable, $"the shot met '{hit.collider.name}', which no blow breaks");
            Assert.IsTrue(GlassDamage.TryPaneAt(world, site, hit.point - outward * 0.02f, out PaneKey key), $"the shot at {hit.point} found no pane");
            Debug.Log($"CityX door glass shot: building {site.Id} {door.Kind} door (row {door.Row}), the ray met '{hit.collider.name}' " +
                $"{hit.distance:F2} m out, the pane {key}");
            Assert.IsTrue(DoorGlass.IsDoorPane(key), "the shot at a door's glass found a window");

            int livesBefore = GlassFractureBurst.LiveShards;
            for (int shot = 1; shot <= GlassDamage.MaxBulletHoles; shot++)
            {
                Assert.IsTrue(breakable.TakeBlow(WorldOf(site, pane, 0.2f + 0.12f * shot, 0.4f), -outward, BlowStrength.Bullet), $"bullet {shot} broke nothing");
                yield return null;
                Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(pane.Key), $"after bullet {shot}");
                Assert.AreEqual(shot, streamer.Damage.HoleCount(pane.Key), $"bullet {shot} left no hole of its own");
            }
            // The cracked sheet is still something a bullet finds (its box), and the fifth brings it down into its pieces.
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(muzzle, -outward, out RaycastHit second, 3.0f, ~0, QueryTriggerInteraction.Collide), "the cracked glass is gone");
            Assert.IsNotNull(second.collider.GetComponentInParent<IBreakableSurface>(), $"the shot at the cracked glass met '{second.collider.name}'");
            Assert.IsTrue(breaker.TakeBlow(WorldOf(site, pane, 0.5f, 0.5f), -outward, BlowStrength.Bullet));
            yield return null;
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(pane.Key), "the fifth bullet did not bring the door's glass down");
            Assert.Greater(GlassFractureBurst.LiveShards, livesBefore, "the door's glass vanished instead of breaking into its pieces");
            Assert.AreEqual(PaneState.Intact, streamer.Damage.StateOf(panes[1].Key), "the other sheet broke too");
        }

        [UnityTest]
        public IEnumerator WalkingIntoACrackedSheetOfADoorBreaksItIntoItsPieces()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoor(world, out BuildingSite site, out BalconyDoor door), "no glazed balcony door near the spawn");
            var panes = new DoorPane[2];
            DoorGlass.Panes(site, door, false, panes);
            BalconyDoors.InWorld(site, door, out _, out _, out Vector3 outward);
            // The sheet a body walking in from the balcony meets first (the outer one of a sliding door), shot once.
            DoorPane pane = panes[0];
            Vector3 middle = WorldOf(site, pane, 0.5f, 0.0f);
            CityStreamer streamer = City(middle, out GlassBreaker breaker);
            yield return null;
            streamer.Damage.Shoot(pane.Key, new Vector2(0.5f, 0.6f), out _);
            yield return null;
            Physics.SyncTransforms();
            Assert.IsNotNull(Object.FindFirstObjectByType<CrackedPane>(), "the cracked sheet left no box for a knock");

            // A small body (as CrackedPaneKnockPlayTests' -- it has to fit the sheet) walks off the balcony into it.
            var player = new GameObject("Player");
            _made.Add(player);
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.2f;
            controller.height = 1.4f;
            controller.stepOffset = 0.05f;
            controller.minMoveDistance = 0.0f;
            controller.center = new Vector3(0.0f, 0.75f, 0.0f);
            player.transform.position = middle + outward * 0.7f + Vector3.up * 0.05f;
            Physics.SyncTransforms();
            int livesBefore = GlassFractureBurst.LiveShards;
            for (float t = 0.0f; t < 1.2f && streamer.Damage.StateOf(pane.Key) == PaneState.Cracked; t += Time.deltaTime)
            {
                controller.Move(-outward * (2.5f * Time.deltaTime));
                yield return null;
            }
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(pane.Key), "walking into the cracked door glass did not break it");
            Assert.Greater(GlassFractureBurst.LiveShards, livesBefore, "the door's glass vanished instead of breaking into its pieces");
        }
    }
}
