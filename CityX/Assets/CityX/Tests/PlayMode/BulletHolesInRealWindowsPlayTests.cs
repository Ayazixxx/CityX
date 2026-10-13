using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The real pistol against a real window in a real streamed chunk. Reported: "the windows when shot, only 1
    /// bullet hole appears even if you shoot more than 1. each bullet hole should appear."
    ///
    /// A cracked pane is drawn without a solid collider (it has to be walkable at once), so the second bullet at
    /// it used to fly through the first bullet's hole and never reach the glass at all. Every shot here goes
    /// through PistolWeapon.Fire -- the traces, the breakable lookup and all -- and each one has to register; the fifth shatters the window.
    /// </summary>
    public sealed class BulletHolesInRealWindowsPlayTests
    {
        private const int Seed = 74920318;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private struct Target
        {
            public PaneKey Key;
            public Vector3 Centre;
            public Vector3 Outward;
        }

        /// <summary>A pane on a building's door wall with clear air in front of it: the first thing a ray from six metres out hits is that pane.</summary>
        private static bool FindTarget(CityWorld world, out Target target)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.Params.Width < 8.0f)
                {
                    continue;
                }

                // The building's front (+Z) wall faces the street, so there is clear air in front of it.
                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int row = 1; row < Mathf.Min(rows.Count, 4); row++)
                {
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                        {
                            continue;
                        }
                        Vector3 centre = TestSites.At(site,
                            site.MinX + (column + 0.5f) * columns.Size,
                            site.BaseHeight + (row + 0.5f) * rows.Size,
                            site.MaxZ);
                        if (GlassDamage.TryPaneAt(world, site, centre + outward * 0.02f, out PaneKey key))
                        {
                            target = new Target { Key = key, Centre = centre, Outward = outward };
                            return true;
                        }
                    }
                }
            }
            target = default;
            return false;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EveryBulletAtTheSameWindowMakesItsOwnHoleAndTheWindowStaysUp()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindTarget(world, out Target target), "no window to shoot at near the origin");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.AddComponent<GlassBreaker>();
            go.SetActive(true);

            Vector3 standing = target.Centre + target.Outward * 6.0f;
            streamer.StreamAround(standing, int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            var pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            GameObject pistolObject = Object.Instantiate(pistolPrefab, new Vector3(0.0f, -50.0f, 0.0f), Quaternion.identity);
            _made.Add(pistolObject);
            var pistol = pistolObject.GetComponent<PistolWeapon>();

            // The pane must really be there to shoot: the first thing the crosshair ray finds is glass.
            Vector3 aim = target.Centre;
            Assert.IsTrue(Physics.Raycast(standing, -target.Outward, out RaycastHit first, 20.0f, ~0, QueryTriggerInteraction.Ignore),
                "the chosen window has nothing solid where the glass is");
            Assert.AreEqual(6.0f, first.distance, 0.3f, "something stands between the shooter and the window");

            PaneKey key = target.Key;
            Assert.AreEqual(PaneState.Intact, streamer.Damage.StateOf(key));

            for (int shot = 1; shot <= GlassDamage.MaxBulletHoles; shot++)
            {
                // A shade different each time, so the holes are not one on top of another.
                Vector3 at = aim + Vector3.Cross(Vector3.up, target.Outward) * (0.04f * (shot % 3 - 1)) + Vector3.up * (0.05f * (shot % 2));
                Assert.IsTrue(pistol.Fire(standing, (at - standing).normalized, true, null), $"shot {shot} did not fire");
                yield return Wait(PistolWeapon.FireCooldownSeconds + 0.05f);   // a rebuilt chunk needs its frame; so does the cooldown
                Physics.SyncTransforms();

                Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(key), $"after {shot} bullets the window is {streamer.Damage.StateOf(key)}");
                Assert.AreEqual(shot, streamer.Damage.HoleCount(key),
                    $"bullet {shot} did not make its own hole ({streamer.Damage.HoleCount(key)} holes) -- it flew through the first one");
            }

            // The next bullet (the fifth) brings the window down.
            yield return Wait(PistolWeapon.FireCooldownSeconds + 0.05f);
            Assert.IsTrue(pistol.Fire(standing, (aim - standing).normalized, true, null));
            yield return Wait(0.3f);
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(key), $"bullet {GlassDamage.ShatteringBullet} left the window standing");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ACrackedWindowIsStillSomethingABulletCanFindEvenThoughItIsWalkable()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindTarget(world, out Target target));

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.AddComponent<GlassBreaker>();
            go.SetActive(true);
            Vector3 standing = target.Centre + target.Outward * 6.0f;
            streamer.StreamAround(standing, int.MaxValue);
            yield return null;
            Physics.SyncTransforms();

            // A crack somebody made earlier: a cracked pane has no solid collider, but a shot must still find it.
            streamer.Damage.Shoot(target.Key, new Vector2(0.5f, 0.5f), out _);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            bool solid = Physics.Raycast(standing, -target.Outward, out RaycastHit hit, 20.0f, ~0, QueryTriggerInteraction.Ignore);
            bool sawTheOpening = false;
            RaycastHit[] all = Physics.RaycastAll(standing, -target.Outward, 20.0f, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit h in all)
            {
                if (h.collider.GetComponent<IShootableTrigger>() != null && h.distance < 7.0f)
                {
                    sawTheOpening = true;
                }
            }
            Assert.IsTrue(sawTheOpening, "the cracked window's opening is not something a bullet can find");
            Assert.IsTrue(!solid || hit.distance > 6.2f, "a cracked window is solid to a walking body: it is meant to be walkable");
#else
            yield break;
#endif
        }
    }
}
