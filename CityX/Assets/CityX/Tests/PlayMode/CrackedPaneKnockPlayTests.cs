using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A cracked window breaks when something KNOCKS into it, and not otherwise. Reported: "i keep noticing the
    /// windows shattering by themself after being shot but should only shatter when an object or player knocks
    /// into the already shattered window."
    ///
    /// The cause was the trigger box a cracked pane leaves: ANY rigid body overlapping it shattered the pane --
    /// the kinematic limb colliders of a character standing or aiming beside the glass, glass shards from the
    /// window next door, anything resting there. The classification (<see cref="CrackedPane.IsAKnock"/>) is
    /// checked on real colliders; the physics half puts a real body in a real cracked window's box.
    /// </summary>
    public sealed class CrackedPaneKnockPlayTests
    {
        private const int Seed = 61256619;
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

        private GameObject Made(GameObject go)
        {
            _made.Add(go);
            return go;
        }

        // The North wall faces +Z, so "through the pane" is along Z.

        [UnityTest]
        public IEnumerator ACharacterStandingStillInTheOpeningIsNotAKnock()
        {
            var body = Made(new GameObject("Player"));
            var controller = body.AddComponent<CharacterController>();
            yield return null;
            controller.Move(Vector3.zero);
            yield return null;

            Assert.IsFalse(CrackedPane.IsAKnock(controller, WindowGrid.Wall.North), "standing next to a cracked window broke it");
        }

        [UnityTest]
        public IEnumerator ACharacterWalkingIntoTheWindowIsAKnock()
        {
            var body = Made(new GameObject("Player"));
            var controller = body.AddComponent<CharacterController>();
            yield return null;
            controller.Move(new Vector3(0.0f, 0.0f, 3.0f * Time.deltaTime));   // 3 m/s at the pane

            Assert.IsTrue(CrackedPane.IsAKnock(controller, WindowGrid.Wall.North), "walking into a cracked window does not break it");
        }

        [UnityTest]
        public IEnumerator ACharacterMovingAlongTheGlassIsNotAKnock()
        {
            var body = Made(new GameObject("Player"));
            var controller = body.AddComponent<CharacterController>();
            yield return null;
            controller.Move(new Vector3(3.0f * Time.deltaTime, 0.0f, 0.0f));   // running past it, parallel to the wall

            Assert.IsFalse(CrackedPane.IsAKnock(controller, WindowGrid.Wall.North), "running past a window along its wall broke it");
        }

        [UnityTest]
        public IEnumerator AKinematicLimbColliderIsNeverAKnockHoweverItMoves()
        {
            // A limb of a character standing or aiming beside the glass: a collider on a kinematic rigid body.
            var limb = Made(new GameObject("hand"));
            limb.AddComponent<SphereCollider>().radius = 0.1f;
            var rigidbody = limb.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            yield return null;

            Assert.IsFalse(CrackedPane.IsAKnock(limb.GetComponent<Collider>(), WindowGrid.Wall.North),
                "the arm of somebody aiming at a cracked window broke it");
        }

        [UnityTest]
        public IEnumerator AThrownBodyIsAKnockOnlyIfItIsMovingFastEnoughThroughThePane()
        {
            var thrown = Made(new GameObject("bottle"));
            thrown.AddComponent<SphereCollider>().radius = 0.1f;
            var rigidbody = thrown.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            yield return null;

            rigidbody.linearVelocity = new Vector3(0.0f, 0.0f, 4.0f);
            Assert.IsTrue(CrackedPane.IsAKnock(thrown.GetComponent<Collider>(), WindowGrid.Wall.North));

            rigidbody.linearVelocity = new Vector3(0.0f, 0.0f, 0.4f);
            Assert.IsFalse(CrackedPane.IsAKnock(thrown.GetComponent<Collider>(), WindowGrid.Wall.North), "a body drifting at a cracked window broke it");

            rigidbody.linearVelocity = new Vector3(4.0f, 0.0f, 0.0f);
            Assert.IsFalse(CrackedPane.IsAKnock(thrown.GetComponent<Collider>(), WindowGrid.Wall.North), "a body sliding along the glass broke it");
        }

        [UnityTest]
        public IEnumerator FlyingGlassNeverBreaksAnotherWindow()
        {
            // The shards of a shattered pane are launched fast, as real rigid bodies: they used to shatter every
            // cracked pane they crossed, which threw more shards -- a chain reaction.
            var shard = Made(new GameObject("shard"));
            shard.AddComponent<IgnoredByShots>();
            shard.AddComponent<SphereCollider>().radius = 0.05f;
            var rigidbody = shard.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            yield return null;
            rigidbody.linearVelocity = new Vector3(0.0f, 0.0f, 6.0f);

            Assert.IsFalse(CrackedPane.IsAKnock(shard.GetComponent<Collider>(), WindowGrid.Wall.North), "a glass shard broke a window");
        }

        // ---- the real physics, in a real cracked window ----

        private struct Target
        {
            public PaneKey Key;
            public Vector3 Centre;
            public Vector3 Outward;
        }

        /// <summary>The glazed upper windows of the door walls near spawn, nearest first (up to <paramref name="most"/>).</summary>
        private static List<Target> FindTargets(CityWorld world, int most)
        {
            var found = new List<Target>();
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (found.Count >= most || site.Params.Width < 8.0f)
                {
                    continue;
                }
                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int row = 1; row < Mathf.Min(rows.Count, 3) && found.Count < most; row++)
                {
                    for (int column = 1; column < columns.Count - 1 && found.Count < most; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                        {
                            continue;
                        }
                        Vector3 centre = TestSites.At(site, site.MinX + (column + 0.5f) * columns.Size, site.BaseHeight + (row + 0.5f) * rows.Size, site.MaxZ);
                        if (GlassDamage.TryPaneAt(world, site, centre + outward * 0.02f, out PaneKey key))
                        {
                            found.Add(new Target { Key = key, Centre = centre, Outward = outward });
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Whether nothing stands out of the facade in front of a window's middle: a ray from a metre out meets the glass (at the wall's face or
        /// behind it), not a fin. Some facades have vertical fins every metre, one right through the middle of a window (seed 74102958), and a
        /// body put in front of one is put inside it.
        /// </summary>
        private static bool ClearInFront(Target target)
        {
            return !Physics.Raycast(target.Centre + target.Outward * 1.0f, -target.Outward, out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore) ||
                   hit.distance > 0.97f;
        }

        private static bool FindTarget(CityWorld world, out Target target)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.Params.Width < 8.0f)
                {
                    continue;
                }

                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int row = 1; row < Mathf.Min(rows.Count, 3); row++)
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

        private static IEnumerator WaitPhysics(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator AShotWindowStaysCrackedWhileAPlayerStandsInItsBoxAndBreaksWhenHeWalksIntoIt()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            List<Target> targets = FindTargets(world, 16);
            Assert.IsNotEmpty(targets, "no glazed upper window on a door wall near spawn");

            var streamerObject = Made(new GameObject("Streamer"));
            streamerObject.SetActive(false);
            var streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            // The breaker with the game's own glass for the pieces (GlassFractureBurst), so a knock is seen to break the pane into them.
            var breaker = streamerObject.AddComponent<GlassBreaker>();
            var shardLook = Made(new GameObject("ShardLook"));
            shardLook.SetActive(false);
            shardLook.AddComponent<MeshFilter>();
            shardLook.AddComponent<MeshRenderer>().sharedMaterial =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/GlassShards.mat");
            breaker.ShardPrefab = shardLook;
            streamerObject.SetActive(true);

            // The first window with nothing standing out of the facade in front of its middle.
            Target target = default;
            Vector3 outside = default;
            bool chosen = false;
            foreach (Target candidate in targets)
            {
                outside = candidate.Centre + candidate.Outward * 1.0f;
                streamer.StreamAround(outside, int.MaxValue);
                yield return null;
                Physics.SyncTransforms();
                if (ClearInFront(candidate))
                {
                    target = candidate;
                    chosen = true;
                    break;
                }
            }
            Assert.IsTrue(chosen, "every glazed upper window near spawn has something standing in front of it");

            // The first shot cracks the window, which makes its opening (and the trigger box in it).
            streamer.Damage.Shoot(target.Key, new Vector2(0.5f, 0.5f), out _);
            yield return null;
            Physics.SyncTransforms();
            CrackedPane crackedPane = Object.FindFirstObjectByType<CrackedPane>();
            Assert.IsNotNull(crackedPane, "the shot did not leave a cracked pane behind");
            Vector3 opening = crackedPane.transform.position;

            // A player standing right at the glass, INSIDE its trigger box (it reaches a quarter metre out),
            // small enough to fit in the opening.
            var player = Made(new GameObject("Player"));
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.2f;
            controller.minMoveDistance = 0.0f;   // the default 1 mm silently discards a batch-mode frame's tiny step, so he would never reach the glass
            controller.height = 1.4f;
            controller.stepOffset = 0.05f;   // the default 0.3 m step lifts him into the top of a 1.86 m window before every move, and he could not move at all (seed 74102958)
            // Clear of anything standing out of the facade: some facades have vertical fins every metre, one right through the middle of a
            // window (seed 74102958), and a body placed in one cannot move at all. Slide along the pane to where the little body fits.
            Vector3 alongPane = Vector3.Cross(Vector3.up, target.Outward).normalized;
            Vector3 place = opening + target.Outward * 0.15f;
            foreach (float side in new[] { 0.0f, 0.3f, -0.3f, 0.4f, -0.4f })
            {
                Vector3 p = opening + target.Outward * 0.15f + alongPane * side;
                if (!Physics.CheckCapsule(p + Vector3.up * 0.5f, p - Vector3.up * 0.5f, 0.21f, ~0, QueryTriggerInteraction.Ignore))
                {
                    place = p;
                    break;
                }
            }
            player.transform.position = place;
            Physics.SyncTransforms();
            yield return null;

            // Shot again: the chunk is rebuilt and the opening's box is made afresh around him.
            streamer.Damage.Shoot(target.Key, new Vector2(0.3f, 0.6f), out _);
            yield return null;
            Physics.SyncTransforms();
            yield return WaitPhysics(0.5f);
            Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(target.Key),
                "a window shattered by itself round somebody standing at it, which is what was reported");

            // Now he walks into it.
            int piecesBefore = GlassFractureBurst.LiveShards;
            CollisionFlags lastFlags = CollisionFlags.None;
            CollisionFlags everFlags = CollisionFlags.None;
            float totalRequested = 0.0f;
            Vector3 startPosition = player.transform.position;
            int frames = 0;
            for (float elapsed = 0.0f; elapsed < 1.0f && streamer.Damage.StateOf(target.Key) == PaneState.Cracked; elapsed += Time.deltaTime)
            {
                totalRequested += 3.0f * Time.deltaTime;
                lastFlags = controller.Move(-target.Outward * (3.0f * Time.deltaTime));
                everFlags |= lastFlags;
                frames++;
                yield return null;
            }
            yield return WaitPhysics(0.2f);
            string diag = "";
            if (streamer.Damage.StateOf(target.Key) != PaneState.Shattered)
            {
                CrackedPane[] panes = Object.FindObjectsByType<CrackedPane>(FindObjectsSortMode.None);
                diag = $" DIAG panes={panes.Length} playerPos={player.transform.position} velocity={controller.velocity} " +
                       $"lastFlags={lastFlags} everFlags={everFlags} frames={frames} totalRequested={totalRequested:0.000} " +
                       $"actualMoved={Vector3.Distance(startPosition, player.transform.position):0.000} timeScale={Time.timeScale}";
                foreach (CrackedPane pane in panes)
                {
                    var box = pane.GetComponent<BoxCollider>();
                    diag += $" pane at {pane.transform.position} size {box.size} key {pane.Key} contains={box.bounds.Contains(player.transform.position)}";
                }
                if (Physics.CapsuleCast(
                        player.transform.position + Vector3.up * (controller.height * 0.5f - controller.radius),
                        player.transform.position - Vector3.up * (controller.height * 0.5f - controller.radius),
                        controller.radius, -target.Outward, out RaycastHit blockHit, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    diag += $" BLOCKED BY solid collider '{blockHit.collider.name}' on '{blockHit.collider.gameObject.name}' at {blockHit.point}, distance {blockHit.distance:0.000}";
                }
                else
                {
                    diag += " no solid collider found immediately ahead (CapsuleCast clear)";
                }
            }
            Assert.AreEqual(PaneState.Shattered, streamer.Damage.StateOf(target.Key), "walking into a cracked window did not break it" + diag);
            // ...and it broke the way a blow breaks it: into its own pieces (owner, 2026-10-16: "they only do the procedural fracture when you shoot
            // them a bunch of times but after being 'shattered' they should do the procedural fracture when you walk into them").
            Assert.Greater(GlassFractureBurst.LiveShards, piecesBefore, "walking into a cracked window made it vanish instead of breaking into its pieces");
#else
            yield break;
#endif
        }
    }
}
