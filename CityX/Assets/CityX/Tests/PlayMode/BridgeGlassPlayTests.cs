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
    /// A sky bridge's glass breaks like a window's in a real streamed city (owner, 2026-10-28: "make it so the sky bridges that have windows have
    /// shatterable windows ... right now the windows dont shatter on the skybridges that have them"): a shot from the street side reaches a bay of
    /// an enclosed bridge's window and finds it, every bullet leaves its own hole, the fifth brings the bay down into its own pieces and the next
    /// shot goes through where it was, the bays either side still whole.
    /// </summary>
    public sealed class BridgeGlassPlayTests
    {
        private const int Seed = 95998845;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        /// <summary>A bay in the middle of a level stretch of an enclosed bridge near the spawn, and the bays either side of it.</summary>
        private static bool FindBay(CityWorld world, out BridgePane bay, out BridgePane before, out BridgePane after)
        {
            var panes = new List<BridgePane>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (b.SiteA != site.Id || b.Kind != BridgeKind.Enclosed) continue;
                    panes.Clear();
                    BridgeGlass.Panes(b, panes);
                    for (int i = 1; i + 1 < panes.Count; i++)
                    {
                        if (!panes[i].Level || panes[i - 1].Key.Column + 1 != panes[i].Key.Column || panes[i + 1].Key.Column != panes[i].Key.Column + 1)
                        {
                            continue;
                        }
                        bay = panes[i];
                        before = panes[i - 1];
                        after = panes[i + 1];
                        return true;
                    }
                }
            }
            bay = before = after = default;
            return false;
        }

        private CityStreamer City(Vector3 around, out GlassBreaker breaker)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 3;
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

        private static Vector3 On(in BridgePane pane, float along, float up) =>
            Vector3.Lerp(pane.Start, pane.End, along) + Vector3.up * (pane.Height * up);

        [UnityTest]
        public IEnumerator AShotReachesABaysGlassEveryBulletLeavesAHoleAndTheFifthBringsItDownInPieces()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindBay(world, out BridgePane bay, out BridgePane before, out BridgePane after), "no enclosed bridge near the spawn");
            Vector3 middle = On(bay, 0.5f, 0.5f);
            CityStreamer streamer = City(middle, out GlassBreaker breaker);
            yield return null;
            Physics.SyncTransforms();

            // A shot from 2 m out over the street: the ray meets something that takes the blow, and that is this bay.
            Vector3 muzzle = middle + bay.Normal * 2.0f;
            Assert.IsTrue(Physics.Raycast(muzzle, -bay.Normal, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore), "the shot met nothing");
            var breakable = hit.collider.GetComponentInParent<IBreakableSurface>();
            Assert.IsNotNull(breakable, $"the shot met '{hit.collider.name}', which no blow breaks");
            Assert.IsTrue(breakable.IsGlassAt(hit.point), $"the shot at {hit.point} met no glass");
            Debug.Log($"CityX bridge glass shot: bay {bay.Key}, the ray met '{hit.collider.name}' {hit.distance:F2} m out");

            int livesBefore = GlassFractureBurst.LiveShards;
            for (int shot = 1; shot <= GlassDamage.MaxBulletHoles; shot++)
            {
                Assert.IsTrue(breakable.TakeBlow(On(bay, 0.15f + 0.15f * shot, 0.4f), -bay.Normal, BlowStrength.Bullet), $"bullet {shot} broke nothing");
                yield return null;
                Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(bay.Key), $"after bullet {shot}");
                Assert.AreEqual(shot, streamer.Damage.HoleCount(bay.Key), $"bullet {shot} left no hole of its own");
            }
            // The cracked bay is still something a bullet finds (its box), and the fifth brings it down into its pieces.
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(muzzle, -bay.Normal, out RaycastHit second, 3.0f, ~0, QueryTriggerInteraction.Collide), "the cracked glass is gone");
            Assert.IsNotNull(second.collider.GetComponentInParent<IBreakableSurface>(), $"the shot at the cracked glass met '{second.collider.name}'");
            Assert.IsTrue(breaker.TakeBlow(middle, -bay.Normal, BlowStrength.Bullet));
            yield return null;
            yield return null;
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(bay.Key), "the fifth bullet did not bring the bay down");
            Assert.Greater(GlassFractureBurst.LiveShards, livesBefore, "the bay vanished instead of breaking into its pieces");
            Assert.AreEqual(PaneState.Intact, streamer.Damage.StateOf(before.Key), "the bay before it broke too");
            Assert.AreEqual(PaneState.Intact, streamer.Damage.StateOf(after.Key), "the bay after it broke too");

            // Where it was, a shot goes into the corridor: nothing stops it at the window any more.
            Physics.SyncTransforms();
            bool met = Physics.Raycast(muzzle, -bay.Normal, out RaycastHit third, 2.0f + Footbridges.CorridorWallThickness, ~0, QueryTriggerInteraction.Ignore)
                && third.collider.GetComponentInParent<GlassFractureBurst>() == null;
            Assert.IsFalse(met, $"something still stands where the bay was: '{(met ? third.collider.name : "")}'");
            Assert.IsFalse(breaker.IsGlassAt(middle), "the broken bay still reads as glass");
        }

        [UnityTest]
        public IEnumerator AHeavyBlowTakesABayOutAtOnceAndWalkingIntoACrackedOneBreaksIt()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindBay(world, out BridgePane bay, out BridgePane before, out _), "no enclosed bridge near the spawn");
            CityStreamer streamer = City(On(bay, 0.5f, 0.5f), out GlassBreaker breaker);
            yield return null;
            Assert.IsTrue(breaker.TakeBlow(On(bay, 0.5f, 0.5f), -bay.Normal, BlowStrength.Heavy), "a heavy blow on the bay broke nothing");
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(bay.Key), "a heavy blow did not take the bay out at once");

            // The bay before it, shot once from the corridor, then walked into from inside.
            streamer.Damage.Shoot(before.Key, new Vector2(0.5f, 0.6f), out _);
            yield return null;
            Physics.SyncTransforms();
            var player = new GameObject("Player");
            _made.Add(player);
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.2f;
            controller.height = 1.0f;
            controller.stepOffset = 0.05f;
            controller.minMoveDistance = 0.0f;
            controller.center = new Vector3(0.0f, 0.55f, 0.0f);
            // Standing on the deck just inside the bay, at the sill's height less a little: the body's middle is level with the glass.
            Vector3 inside = On(before, 0.5f, 0.15f) - before.Normal * 0.5f;
            player.transform.position = inside;
            Physics.SyncTransforms();
            int livesBefore = GlassFractureBurst.LiveShards;
            for (float t = 0.0f; t < 1.2f && streamer.Damage.StateOf(before.Key) == PaneState.Cracked; t += Time.deltaTime)
            {
                controller.Move(before.Normal * (2.5f * Time.deltaTime));
                yield return null;
            }
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(before.Key), "walking into the cracked bay did not break it");
            Assert.Greater(GlassFractureBurst.LiveShards, livesBefore, "the bay vanished instead of breaking into its pieces");
        }
    }
}
