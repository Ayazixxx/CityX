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
        private const int Seed = 48398181;

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
        private static bool FindTarget(CityWorld world, out Target target, bool round = false)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world, round ? 40 : 12))   // round towers are rare: none within 12 chunks on seed 61256619
            {
                // A box building's street wall (the window is found from MinX and MaxZ), or (round) a round tower's front side, which faces +Z
                // like a box's north wall with its columns laid from -half to +half of the side (GlassDamage.RoundAlong); not a sealed block or
                // one on a roof.
                if (site.Params.Width < 8.0f || (site.Sides >= 3) != round || site.Shell || site.OnRoof)
                {
                    continue;
                }

                // The building's front (+Z) wall faces the street, so there is clear air in front of it.
                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                float sideLength = round ? RoundFootprint.Of(site).SideLength : site.Params.Width;
                float frontZ = round ? (float)site.CentreZ + RoundFootprint.Of(site).Apothem : (float)site.MaxZ;
                float u0 = round ? (float)site.CentreX - sideLength * 0.5f : (float)site.MinX;
                CellLayout columns = plan.ColumnsFor(sideLength);
                for (int row = 1; row < Mathf.Min(rows.Count, 4); row++)
                {
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                        {
                            continue;
                        }
                        Vector3 centre = TestSites.At(site,
                            u0 + (column + 0.5f) * columns.Size,
                            site.BaseHeight + (row + 0.5f) * rows.Size,
                            frontZ);
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

        /// <summary>
        /// Slides the aim along the window, once the city is in, to where a shot from six metres out meets this window's glass first: some
        /// facades have vertical fins every metre standing out from the glass (seed 74102958: 0.22 to 0.37 m), and the middle of a window can be
        /// right behind one. The hit must be at the glass's own depth (BuildingInterior.GlassSetBack in from the wall's face): seed 52817364's
        /// first window had a fin 0.19 m proud of the wall, close enough to the face to pass for the glass.
        /// </summary>
        private static Target AimAtTheGlass(CityWorld world, Target target)
        {
            BuildingSite site = world.Building(target.Key.Building);
            Vector3 along = Vector3.Cross(Vector3.up, target.Outward).normalized;
            foreach (float side in new[] { 0.0f, 0.25f, -0.25f, 0.5f, -0.5f, 0.75f, -0.75f })
            {
                Vector3 centre = target.Centre + along * side;
                if (Physics.Raycast(centre + target.Outward * 6.0f, -target.Outward, out RaycastHit hit, 20.0f, ~0, QueryTriggerInteraction.Ignore) &&
                    Mathf.Abs(hit.distance - (6.0f + BuildingInterior.GlassSetBack)) <= 0.08f &&
                    GlassDamage.TryPaneAt(world, site, hit.point - target.Outward * 0.02f, out PaneKey key) && key.Equals(target.Key))
                {
                    target.Centre = centre;
                    return target;
                }
            }
            return target;
        }

        /// <summary>
        /// Until a hit window's block is back on screen with its collider (at most five seconds): on seed 48398181 the cooking took longer than
        /// the shots' cooldown, and the next bullet found no glass at all, the block's collider still on its way, and "flew through".
        /// </summary>
        private static IEnumerator Rebuilt(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 5.0f;
            while (streamer.WindowRefreshPending && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        /// <summary>
        /// A point on the target pane's glass, a hand's width from every shot already made there: nearest the aim first, on a ring of points
        /// round it, each checked with a ray from where the shooter stands meeting this pane's glass first, as the game's window breaker sees it.
        /// </summary>
        private static Vector3 FreshGlass(Target target, Vector3 standing, List<Vector3> shots, GlassBreaker breaker)
        {
            Vector3 across = Vector3.Cross(Vector3.up, target.Outward).normalized;
            // Holes are cut to the pane's size: a box's wide pane wants a hand's width between shots, a round tower's narrow one holds four only closer.
            foreach (float apart in new[] { 0.15f, 0.1f })
            {
                for (float r = 0.0f; r <= 0.6f; r += 0.05f)
                {
                    for (int k = 0; k < (r == 0.0f ? 1 : 12); k++)
                    {
                        float angle = k * Mathf.PI / 6.0f;
                        Vector3 at = target.Centre + across * (Mathf.Cos(angle) * r) + Vector3.up * (Mathf.Sin(angle) * r);
                        if (shots.Exists(s => (s - at).magnitude < apart)) continue;
                        if (FirstABulletMeets(standing, (at - standing).normalized, out RaycastHit hit) && hit.distance <= Vector3.Distance(standing, at) + 0.6f &&   // the window, not the room behind a hole
                            breaker.IsGlassAt(hit.point) && breaker.TryPaneKeyAt(hit.point, out PaneKey key) && key.Equals(target.Key))   // the game's own glass, not a fin
                        {
                            return at;
                        }
                    }
                }
            }
            Assert.Fail("no glass left on the window a hand's width from the shots made");
            return target.Centre;
        }

        /// <summary>The first thing a bullet stops on along a line, as the pistol looks: a cracked window is a trigger box it hits (IShootableTrigger).</summary>
        private static bool FirstABulletMeets(Vector3 from, Vector3 direction, out RaycastHit first)
        {
            first = default;
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(from, direction, 20.0f, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.isTrigger && hit.collider.GetComponent<IShootableTrigger>() == null) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    first = hit;
                }
            }
            return best < float.MaxValue;
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
            return ShootOneWindowFourTimesThenDownWithTheFifth(false);
        }

        /// <summary>Owner, 2026-10-15: "the windows on the round buildings are not fracturing in the new improved way like the square buildings".</summary>
        [UnityTest]
        public IEnumerator ARoundTowersWindowTakesBulletHolesLikeABoxBuildings()
        {
            return ShootOneWindowFourTimesThenDownWithTheFifth(true);
        }

        private IEnumerator ShootOneWindowFourTimesThenDownWithTheFifth(bool round)
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindTarget(world, out Target target, round), round ? "no round tower's window to shoot at near the origin" : "no window to shoot at near the origin");

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
            target = AimAtTheGlass(world, target);
            standing = target.Centre + target.Outward * 6.0f;

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

            var shots = new List<Vector3>();
            for (int shot = 1; shot <= GlassDamage.MaxBulletHoles; shot++)
            {
                // Each on this pane's glass and a hand's width from the shots before: the aim can sit near the pane's edge where a fin stood
                // in front of its middle (AimAtTheGlass), and a fixed pattern of shots round it put one on the frame (seed 48398181).
                // A bullet through a hole already cut rightly makes none; one that does is tried again at another spot (the holes are cut to
                // the pane's size, and an aim a hand's width off one could still be in it).
                for (int attempt = 0; attempt < 3 && streamer.Damage.HoleCount(key) < shot; attempt++)
                {
                    Vector3 at = FreshGlass(target, standing, shots, go.GetComponent<GlassBreaker>());
                    shots.Add(at);
                    Assert.IsTrue(pistol.Fire(standing, (at - standing).normalized, true, null), $"shot {shot} did not fire");
                    yield return Wait(PistolWeapon.FireCooldownSeconds + 0.05f);   // a rebuilt chunk needs its frame; so does the cooldown
                    yield return Rebuilt(streamer);
                    Physics.SyncTransforms();
                }

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

        private sealed class Dummy : MonoBehaviour, CityX.Character.IDamageable
        {
            public bool IsAlive => true;
            public readonly List<float> Hits = new List<float>();

            public CityX.Character.HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits.Add(amount);
                return CityX.Character.HitResponse.Landed;
            }
        }

        /// <summary>
        /// Owner, 2026-10-26: "make it so the bullet travels through the window but does a little less damage" (10% less, GlassPenetration): a
        /// bullet at a real window cracks it and hurts somebody standing in the room behind it; the next one goes through the cracked window too.
        /// </summary>
        [UnityTest]
        public IEnumerator ABulletGoesThroughARealWindowIntoTheRoomBehindIt()
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
            yield return null;
            Physics.SyncTransforms();
            target = AimAtTheGlass(world, target);
            standing = target.Centre + target.Outward * 6.0f;

            // Somebody a metre into the room, behind the glass.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(body);
            body.transform.position = target.Centre - target.Outward * (BuildingInterior.GlassSetBack + 1.0f);
            body.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
            Dummy dummy = body.AddComponent<Dummy>();
            Physics.SyncTransforms();

            var pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            GameObject pistolObject = Object.Instantiate(pistolPrefab, new Vector3(0.0f, -50.0f, 0.0f), Quaternion.identity);
            _made.Add(pistolObject);
            var pistol = pistolObject.GetComponent<PistolWeapon>();
            float throughOne = CityX.Character.CombatTuning.DamageFor(pistol.Profile.Damage, 0) * GlassPenetration.DamageKeptPerPane;

            Assert.IsTrue(pistol.Fire(standing, (target.Centre - standing).normalized, true, null));
            Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(target.Key), "the bullet did not crack the window");
            Assert.AreEqual(1, dummy.Hits.Count, "the bullet stopped in the window");
            Assert.AreEqual(throughOne, dummy.Hits[0], 1e-3f, "through a window a bullet does 10% less");

            yield return Wait(PistolWeapon.FireCooldownSeconds + 0.05f);   // the cracked window's chunk is rebuilt
            yield return Rebuilt(streamer);
            Physics.SyncTransforms();
            // A hand's width off the first: at the same spot it goes through the first hole, as it should, and makes none.
            Vector3 second = FreshGlass(target, standing, new List<Vector3> { target.Centre }, go.GetComponent<GlassBreaker>());
            Assert.IsTrue(pistol.Fire(standing, (second - standing).normalized, true, null));
            Assert.AreEqual(2, streamer.Damage.HoleCount(target.Key), "the second bullet made no hole in the cracked window");
            Assert.AreEqual(2, dummy.Hits.Count, "the second bullet stopped in the cracked window");
            Assert.AreEqual(throughOne, dummy.Hits[1], 1e-3f);
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
            target = AimAtTheGlass(world, target);
            standing = target.Centre + target.Outward * 6.0f;

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
