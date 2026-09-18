using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Checks the ragdoll rig on the built prefab.
    ///
    /// A ragdoll goes wrong quietly and then spectacularly: a joint pointing at
    /// the wrong parent, a body with default mass, a limb with no collider.
    /// None of it errors. It just produces a corpse that pings off down the
    /// street, and by then there is no way to tell which of a dozen numbers
    /// caused it.
    /// </summary>
    public class RagdollTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";

        private static Ragdoll LoadRagdoll()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"no player prefab at {PrefabPath}");

            var ragdoll = prefab.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "the player has no Ragdoll -- dying would do nothing");
            return ragdoll;
        }

        /// <summary>
        /// Hips, chest, head, and two bones per limb. Fewer means a limb was
        /// silently skipped, which shows up as an arm that stays rigidly welded
        /// while the rest of the body falls.
        /// </summary>
        [Test]
        public void EveryLimbGotABody()
        {
            Ragdoll ragdoll = LoadRagdoll();
            Assert.AreEqual(11, ragdoll.Bodies.Length,
                "expected hips, chest, head and eight limb bones");

            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsNotNull(body, "a null slot in the ragdoll's body list");
                Assert.IsNotNull(body.GetComponent<Collider>(),
                    $"'{body.name}' has no collider -- it would fall through the world");
            }
        }

        [Test]
        public void TheTorsoIsTheHips()
        {
            Ragdoll ragdoll = LoadRagdoll();
            Assert.IsNotNull(ragdoll.Torso, "no torso -- nothing would carry the initial spin");
            Assert.Contains(ragdoll.Torso, ragdoll.Bodies,
                "the torso is not one of the bodies in the rig");
        }

        /// <summary>
        /// The whole rig must start kinematic. A prefab saved with live bodies
        /// collapses into a heap the instant it is instantiated, before the
        /// player has done anything.
        /// </summary>
        [Test]
        public void TheRigStartsKinematicSoTheAnimatorOwnsThePose()
        {
            Ragdoll ragdoll = LoadRagdoll();
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.IsTrue(body.isKinematic,
                    $"'{body.name}' is live in the prefab -- the player would collapse on spawn");
            }
        }

        /// <summary>
        /// Every bone except the hips hangs off exactly one parent, and the
        /// chain has to reach the hips from anywhere. A joint connected to the
        /// wrong body still simulates -- it just produces an elbow attached to
        /// a thigh.
        /// </summary>
        [Test]
        public void EveryBoneHangsOffAChainThatReachesTheHips()
        {
            Ragdoll ragdoll = LoadRagdoll();

            foreach (Rigidbody body in ragdoll.Bodies)
            {
                var joint = body.GetComponent<CharacterJoint>();

                if (body == ragdoll.Torso)
                {
                    Assert.IsNull(joint, "the hips should hang off nothing");
                    continue;
                }

                Assert.IsNotNull(joint, $"'{body.name}' has no joint -- it would fall off the body");
                Assert.IsNotNull(joint.connectedBody,
                    $"'{body.name}' is jointed to the world rather than to another bone");

                // Walk up to the hips. A cycle or a stray parent shows up as
                // running out of steps rather than as anything visible.
                Rigidbody current = body;
                int steps = 0;
                while (current != null && current != ragdoll.Torso && steps < 16)
                {
                    var up = current.GetComponent<CharacterJoint>();
                    current = up == null ? null : up.connectedBody;
                    steps++;
                }
                Assert.AreEqual(ragdoll.Torso, current,
                    $"'{body.name}' does not connect back to the hips");
            }
        }

        /// <summary>
        /// Mass has to be proportional to size. Unity's default of 1kg per body
        /// makes a forearm as heavy as a torso, and the joint solver then has
        /// no reason to think the torso should lead -- which is exactly what
        /// makes a rig flail around its own extremities instead of falling.
        /// </summary>
        [Test]
        public void TheTorsoOutweighsTheLimbs()
        {
            Ragdoll ragdoll = LoadRagdoll();

            float lightest = float.MaxValue;
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.Greater(body.mass, 0.0f, $"'{body.name}' is weightless");
                if (body != ragdoll.Torso)
                {
                    lightest = Mathf.Min(lightest, body.mass);
                }
            }

            Assert.Greater(ragdoll.Torso.mass, lightest * 2.0f,
                "the torso is not meaningfully heavier than the lightest limb, so the rig " +
                "will be led around by its own extremities");
        }

        /// <summary>
        /// Joint limits must actually limit. A CharacterJoint left at its
        /// defaults lets a knee bend backwards through the thigh.
        /// </summary>
        [Test]
        public void EveryJointIsActuallyLimited()
        {
            Ragdoll ragdoll = LoadRagdoll();

            foreach (Rigidbody body in ragdoll.Bodies)
            {
                var joint = body.GetComponent<CharacterJoint>();
                if (joint == null)
                {
                    continue;
                }

                Assert.Greater(joint.swing1Limit.limit, 0.0f, $"'{body.name}' swing1 is unlimited");
                Assert.LessOrEqual(joint.swing1Limit.limit, 90.0f,
                    $"'{body.name}' can swing past a right angle");
                Assert.Greater(joint.highTwistLimit.limit, 0.0f,
                    $"'{body.name}' can twist freely about its own bone");
                Assert.Less(joint.lowTwistLimit.limit, 0.0f,
                    $"'{body.name}' can only twist one way");
            }
        }

        /// <summary>
        /// Damping is what makes the rig come to rest in a heap rather than
        /// quivering at the joints forever.
        /// </summary>
        [Test]
        public void EveryBodyIsDampedEnoughToSettle()
        {
            Ragdoll ragdoll = LoadRagdoll();
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                Assert.AreEqual(Ragdoll.LinearDamping, body.linearDamping, 0.001f);
                Assert.AreEqual(Ragdoll.AngularDamping, body.angularDamping, 0.001f);
                Assert.Greater(body.angularDamping, body.linearDamping,
                    "rotational jitter is what reads as janky, so it must be damped harder");
            }
        }

        /// <summary>
        /// The REAL rig, dropped on a real floor by the real solver.
        ///
        /// Everything above checks the rig's description. This checks the only
        /// thing that actually matters, which no amount of field-checking can:
        /// that the skeleton's own geometry -- its bone directions, its capsule
        /// overlaps at every joint, the scale the model is displayed at --
        /// produces a body that falls over rather than one that detonates.
        ///
        /// Driven by Physics.Simulate rather than by frames, because edit mode
        /// has no frames. That also makes it exactly reproducible, which a
        /// physics test otherwise is not.
        ///
        /// THE THRESHOLDS ARE MEASURED, NOT GUESSED. As first written they were
        /// loose enough that the test passed with the rig's self-collision
        /// filtering removed entirely, which makes it worth stating what the
        /// numbers actually are: this rig peaks at 4.5 m/s, drifts under a
        /// metre and spreads 0.55m. Removing the collision filtering takes the
        /// peak to 5.4 -- so the thing genuinely holding the rig together is
        /// the capped depenetration velocity, and the filtering is the second
        /// line rather than the first. The limits below sit at roughly twice
        /// what is measured: tight enough to notice a regression, loose enough
        /// not to fail on solver noise.
        /// </summary>
        [Test]
        public void TheRealRigFallsOverInsteadOfExploding()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);

            SimulationMode previousMode = Physics.simulationMode;

            try
            {
                floor.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                floor.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);

                instance.transform.position = new Vector3(0.0f, 1.0f, 0.0f);
                var ragdoll = instance.GetComponent<Ragdoll>();
                var controller = instance.GetComponent<CharacterController>();

                // Awake never runs in edit mode, so the two things it does have
                // to be done by hand.
                Ragdoll.IgnoreCollisionsWithin(ragdoll.Bodies, controller);
                if (controller != null)
                {
                    controller.enabled = false;
                }

                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();

                Vector3 start = ragdoll.Torso.position;
                foreach (Rigidbody body in ragdoll.Bodies)
                {
                    body.isKinematic = false;
                }

                const float step = 1.0f / 60.0f;
                for (int i = 0; i < 240; i++)   // four seconds
                {
                    Physics.Simulate(step);

                    // Checked throughout rather than at the end: an exploding
                    // rig is long gone by the time it settles, and its final
                    // position on its own would look perfectly plausible.
                    foreach (Rigidbody body in ragdoll.Bodies)
                    {
                        Assert.Less(body.linearVelocity.magnitude, 10.0f,
                            $"'{body.name}' reached {body.linearVelocity.magnitude:0.0} m/s at " +
                            $"{(i + 1) * step:0.00}s -- measured peak for this rig is 4.5, so it " +
                            "was flung apart rather than dropped");
                    }
                }

                Vector3 rest = ragdoll.Torso.position;

                Assert.Less(rest.y, start.y - 0.3f,
                    "the rig did not fall");
                Assert.Greater(rest.y, -1.0f,
                    "the rig sank through the floor");

                float drift = Vector3.Distance(
                    new Vector3(start.x, 0.0f, start.z),
                    new Vector3(rest.x, 0.0f, rest.z));
                Assert.Less(drift, 2.5f,
                    $"the corpse wandered {drift:0.0}m sideways from a standing drop " +
                    "(measured: 1.0m)");

                // Every piece stays near the body it belongs to. A stretched
                // joint shows up here as a hand two streets away.
                foreach (Rigidbody body in ragdoll.Bodies)
                {
                    float reach = Vector3.Distance(body.position, rest);
                    Assert.Less(reach, 1.5f,
                        $"'{body.name}' ended up {reach:0.0}m from the torso -- its joint came " +
                        "apart (measured spread: 0.55m)");
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(floor);
            }
        }

        // --- Stand-up recovery: EXPLICIT ASK, "once you are ragdolled from
        // --- one of these events your are able to press space to stand up
        // --- once your on the floor for 2.5 seconds" ---

        /// <summary>
        /// A minimal rig, built by hand rather than loaded from the prefab
        /// -- JetpackTests' own "no Animator, deliberately" reasoning
        /// applies here too: leaving Animator null skips BeginStandingUp's
        /// visual Play("GettingUp") call entirely, which is what makes the
        /// underlying timing/repositioning logic testable without a real
        /// rig or a "GettingUp" state to actually exist.
        /// </summary>
        private static Ragdoll BuildTestRig(out GameObject player, out Rigidbody torso)
        {
            player = new GameObject("Player");
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerLocomotion>();
            player.AddComponent<PlayerVitals>();

            var torsoObject = new GameObject("Torso");
            torsoObject.transform.SetParent(player.transform, false);
            torso = torsoObject.AddComponent<Rigidbody>();
            torsoObject.AddComponent<BoxCollider>();

            var ragdoll = player.AddComponent<Ragdoll>();
            ragdoll.Bodies = new[] { torso };
            ragdoll.Torso = torso;
            ragdoll.Bind();
            return ragdoll;
        }

        [Test]
        public void CanStandUpIsFalseRightAfterActivating()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out _);
            try
            {
                ragdoll.Activate(Vector3.zero);
                Assert.IsFalse(ragdoll.CanStandUp, "no time has passed yet -- nothing to stand up from");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CanStandUpBecomesTrueAfterRestingLongEnough()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            try
            {
                ragdoll.Activate(Vector3.zero);
                torso.linearVelocity = Vector3.zero;   // "at rest" the whole time

                const float step = 1.0f / 60.0f;
                int steps = Mathf.CeilToInt(Ragdoll.StandUpRestSeconds / step) + 5;
                for (int i = 0; i < steps; i++)
                {
                    ragdoll.Tick(step);
                }

                Assert.IsTrue(ragdoll.CanStandUp,
                    $"resting for over {Ragdoll.StandUpRestSeconds}s should have made standing up available");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>
        /// The control case: a ragdoll that never actually stops moving
        /// (still tumbling/falling) must not let the rest timer accumulate
        /// just because real time passed -- otherwise a body sliding down a
        /// long slope could stand up mid-slide.
        /// </summary>
        [Test]
        public void CanStandUpStaysFalseWhileStillMoving()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            try
            {
                ragdoll.Activate(Vector3.zero);

                const float step = 1.0f / 60.0f;
                int steps = Mathf.CeilToInt(Ragdoll.StandUpRestSeconds / step) + 5;
                for (int i = 0; i < steps; i++)
                {
                    // Re-set every tick -- nothing is actually simulating
                    // physics in this test, so a one-time assignment would
                    // otherwise just sit there unchanged either way.
                    torso.linearVelocity = new Vector3(0.0f, 0.0f, Ragdoll.RestVelocityThreshold * 5.0f);
                    ragdoll.Tick(step);
                }

                Assert.IsFalse(ragdoll.CanStandUp,
                    "a torso that never stopped moving should never accumulate rest time");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>
        /// EXPLICIT ASK: "make it so i can press space to get up at any
        /// time after /sleep is used" -- /sleep (PlayerChat.Dispatch) calls
        /// Activate with allowImmediateStandUp:true specifically so it
        /// skips the rest timer a real knockdown still has to sit through.
        /// </summary>
        [Test]
        public void CanStandUpIsTrueImmediatelyWhenAllowedToSkipTheRestTimer()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out _);
            try
            {
                ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
                Assert.IsTrue(ragdoll.CanStandUp,
                    "allowImmediateStandUp should let Space stand the body up with no wait at all");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>The default -- every ordinary knockdown (crash, finisher) -- must still wait out the real timer.</summary>
        [Test]
        public void CanStandUpStillWaitsForTheRestTimerByDefault()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out _);
            try
            {
                ragdoll.Activate(Vector3.zero);
                Assert.IsFalse(ragdoll.CanStandUp,
                    "a real knockdown must not skip the rest timer just because /sleep's own flag exists");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BeginStandingUpDoesNothingBeforeTheRestTimerIsUp()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            try
            {
                ragdoll.Activate(Vector3.zero);
                torso.linearVelocity = Vector3.zero;
                ragdoll.Tick(0.1f);   // nowhere near StandUpRestSeconds

                ragdoll.BeginStandingUp();

                Assert.IsFalse(ragdoll.IsStandingUp, "standing up should not have started yet");
                Assert.IsTrue(ragdoll.IsLimp, "the body should still be limp");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>
        /// Nothing moves the root transform during a ragdoll -- only the
        /// individual Rigidbodies do -- so standing up has to reposition it
        /// to wherever the torso actually ended up, not leave it frozen at
        /// the original Activate() position. No real ground exists in this
        /// test scene, so the downward raycast finds nothing and falls back
        /// to the torso's own height -- still proves the X/Z move happened.
        /// </summary>
        [Test]
        public void BeginStandingUpRepositionsToWhereTheTorsoSettled()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            try
            {
                ragdoll.Activate(Vector3.zero);
                torso.linearVelocity = Vector3.zero;

                const float step = 1.0f / 60.0f;
                int steps = Mathf.CeilToInt(Ragdoll.StandUpRestSeconds / step) + 5;
                for (int i = 0; i < steps; i++)
                {
                    ragdoll.Tick(step);
                }
                Assert.IsTrue(ragdoll.CanStandUp, "setup failed -- never became eligible to stand up");

                // The torso "settled" well away from where Activate() was
                // originally called from.
                torso.transform.position = new Vector3(12.0f, 3.0f, -7.0f);

                ragdoll.BeginStandingUp();

                Assert.AreEqual(12.0f, player.transform.position.x, 0.01f);
                Assert.AreEqual(-7.0f, player.transform.position.z, 0.01f);
                Assert.IsTrue(ragdoll.IsStandingUp);
                Assert.IsTrue(ragdoll.IsLimp, "still limp/kinematic while the GettingUp clip plays");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void FinishesStandingUpAndReturnsControlAfterTheClipDuration()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            var controller = player.GetComponent<CharacterController>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            try
            {
                ragdoll.Activate(Vector3.zero);
                torso.linearVelocity = Vector3.zero;

                const float step = 1.0f / 60.0f;
                int restSteps = Mathf.CeilToInt(Ragdoll.StandUpRestSeconds / step) + 5;
                for (int i = 0; i < restSteps; i++)
                {
                    ragdoll.Tick(step);
                }
                ragdoll.BeginStandingUp();
                Assert.IsTrue(ragdoll.IsStandingUp, "setup failed -- standing up never actually started");

                int clipSteps = Mathf.CeilToInt(Ragdoll.StandUpRecoverySeconds / step) + 5;
                for (int i = 0; i < clipSteps; i++)
                {
                    ragdoll.Tick(step);
                }

                Assert.IsFalse(ragdoll.IsLimp, "the body should be back under control after the clip finished");
                Assert.IsFalse(ragdoll.IsStandingUp);
                Assert.IsTrue(controller.enabled, "the CharacterController was never handed back");
                Assert.IsTrue(locomotion.enabled, "PlayerLocomotion was never handed back");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        // --- Depenetration: EXPLICIT BUG REPORT, "whenever i fly into a
        // --- wall i just get stuck in it" -- still true after a first,
        // --- fixed-distance pull-back attempt (see JetpackWallCrashTests'
        // --- own doc comment for the full history) ---

        [Test]
        public void ActivateClearsTheRigFromGeometryItWasOverlapping()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out Rigidbody torso);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // Deliberately overlapping the torso's own BoxCollider
                // (added by BuildTestRig) by a real amount -- standing in
                // for a limb that tunnelled deep into a wall at full
                // jetpack dive speed.
                wall.transform.position = torso.transform.position;
                wall.transform.localScale = Vector3.one * 3.0f;
                Physics.SyncTransforms();

                Bounds wallBoundsBefore = wall.GetComponent<Collider>().bounds;
                Bounds torsoBoundsBefore = torso.GetComponent<Collider>().bounds;
                Assert.IsTrue(wallBoundsBefore.Intersects(torsoBoundsBefore),
                    "setup failed -- the torso was never actually overlapping the wall");

                ragdoll.Activate(Vector3.zero);
                Physics.SyncTransforms();

                Bounds wallBounds = wall.GetComponent<Collider>().bounds;
                Bounds torsoBounds = torso.GetComponent<Collider>().bounds;
                Assert.IsFalse(wallBounds.Intersects(torsoBounds),
                    "the torso is still overlapping the wall right after Activate -- " +
                    "DepenetrateFromWorld did not clear it");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(wall);
            }
        }

        /// <summary>The common case -- an ordinary fall or finisher with nothing nearby -- must not get nudged for no reason.</summary>
        [Test]
        public void ActivateDoesNotMoveTheRigWhenNothingIsOverlapping()
        {
            Ragdoll ragdoll = BuildTestRig(out GameObject player, out _);
            try
            {
                Vector3 before = player.transform.position;
                ragdoll.Activate(Vector3.zero);
                Assert.AreEqual(before, player.transform.position,
                    "with nothing to clear, Activate should not move the rig at all");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
