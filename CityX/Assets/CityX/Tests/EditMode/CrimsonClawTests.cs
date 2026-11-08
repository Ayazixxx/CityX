using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// CrimsonClaw's state machine, damage, and movement -- all pure per-
    /// frame logic driven through Tick(deltaTime) directly, the same
    /// "public Tick, separate from Update" shape WallStickTests already
    /// exercises. HoverCorrect/NpcSteering both call Physics.Raycast
    /// internally, which finds nothing in an EditMode test with no real
    /// physics scene -- both fall back to their own safe "nothing found"
    /// path (hover holds still, steering returns the desired direction
    /// unmodified), so none of that needs a PlayMode test to check the
    /// logic actually under test here.
    /// </summary>
    public sealed class CrimsonClawTests
    {
        /// <summary>
        /// EditMode tests share one scene across the whole run with no
        /// reload between them -- a GameObject created in one test and never
        /// destroyed is still there for the next one to trip over. Confirmed
        /// live: a leftover Player from an earlier test was the FIRST result
        /// FindFirstObjectByType&lt;PlayerVitals&gt; returned in a later one,
        /// silently substituting a stale, wrongly-positioned target. Every
        /// root this file creates -- including the new Rigidbody debris
        /// Die() spawns, which are orphaned scene roots with no parent to
        /// clean them up via -- is tracked here and destroyed in TearDown.
        /// </summary>
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();

            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (debris != null && debris.name == "CrimsonClawDebris")
                {
                    Object.DestroyImmediate(debris);
                }
            }
        }

        private CrimsonClaw CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot)
        {
            var go = new GameObject("CrimsonClaw");
            _spawned.Add(go);
            leftPivot = new GameObject("LeftClawPivot").transform;
            leftPivot.SetParent(go.transform);
            rightPivot = new GameObject("RightClawPivot").transform;
            rightPivot.SetParent(go.transform);

            CrimsonClaw claw = go.AddComponent<CrimsonClaw>();
            claw.LeftClawPivot = leftPivot;
            claw.RightClawPivot = rightPivot;
            return claw;
        }

        private PlayerVitals CreatePlayer(Vector3 position)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.transform.position = position;
            return go.AddComponent<PlayerVitals>();
        }

        /// <summary>Ticks through a full windup+swing+recover cycle, in small steps so CheckClawHit gets several chances to see the target, matching how a real Update loop would call it every frame.</summary>
        private static void TickSeconds(CrimsonClaw claw, float seconds, float step = 0.02f)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += step)
            {
                claw.Tick(step);
            }
        }

        [Test]
        public void StartsAtFullHealthAndAlive()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            Assert.IsTrue(claw.IsAlive);
            Assert.AreEqual(CrimsonClaw.MaxHealth, claw.Health, 0.001f);
        }

        [Test]
        public void TakingDamageReducesHealthAndReportsLanded()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            HitResponse response = ((IDamageable)claw).ApplyHit(40.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Landed, response);
            Assert.AreEqual(CrimsonClaw.MaxHealth - 40.0f, claw.Health, 0.001f);
            Assert.IsTrue(claw.IsAlive);
        }

        [Test]
        public void LethalDamageKillsItAndFurtherHitsAreIgnored()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            HitResponse killingBlow = ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth + 50.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Landed, killingBlow);
            Assert.IsFalse(claw.IsAlive);
            Assert.AreEqual(0.0f, claw.Health, 0.001f);

            HitResponse secondHit = ((IDamageable)claw).ApplyHit(10.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Ignored, secondHit);
            Assert.AreEqual(0.0f, claw.Health, 0.001f);
        }

        [Test]
        public void DyingDetachesBothClawPivotsOntoTheirOwnRigidbodies()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            Transform root = claw.transform;

            ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, Vector3.zero, 0.0f);

            Assert.AreNotEqual(root, leftPivot.parent, "left claw never detached from the corpse root");
            Assert.AreNotEqual(root, rightPivot.parent, "right claw never detached from the corpse root");
            Assert.IsNotNull(leftPivot.GetComponentInParent<Rigidbody>(), "left claw debris has no Rigidbody");
            Assert.IsNotNull(rightPivot.GetComponentInParent<Rigidbody>(), "right claw debris has no Rigidbody");
            Assert.AreNotSame(
                leftPivot.GetComponentInParent<Rigidbody>(), rightPivot.GetComponentInParent<Rigidbody>(),
                "both claws landed on the SAME debris body -- each should be independent");
        }

        [Test]
        public void DyingDisablesTheMainCollision()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            claw.MainCollision = claw.gameObject.AddComponent<BoxCollider>();

            ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, Vector3.zero, 0.0f);

            Assert.IsFalse(claw.MainCollision.enabled);
        }

        [Test]
        public void NoNearbyTargetNeverStartsAnAttack()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out _);
            // Alone in the scene -- FindFirstObjectByType<PlayerVitals> finds nothing.
            TickSeconds(claw, 2.0f);
            Assert.AreEqual(Quaternion.identity, leftPivot.localRotation,
                "a claw pivot moved with no target anywhere to chase or attack");
        }

        [Test]
        public void ATargetBeyondAttackRangeIsChasedButNeverAttacked()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 10.0f)); // within DetectionRange, past AttackRange
            Vector3 start = claw.transform.position;

            TickSeconds(claw, 1.0f);

            Assert.Greater(Vector3.Distance(claw.transform.position, start), 0.0f, "never moved toward a reachable target");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, player.Health, 0.001f, "took damage despite never entering attack range");
            Assert.AreEqual(Quaternion.identity, leftPivot.localRotation);
            Assert.AreEqual(Quaternion.identity, rightPivot.localRotation);
        }

        [Test]
        public void ATargetFarBeyondDetectionRangeIsIgnoredEntirely()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            CreatePlayer(new Vector3(0.0f, 0.0f, CrimsonClaw.DetectionRange * 4.0f));
            Vector3 start = claw.transform.position;

            TickSeconds(claw, 1.0f);

            Assert.AreEqual(start.x, claw.transform.position.x, 0.01f, "moved toward a target far outside detection range");
            Assert.AreEqual(start.z, claw.transform.position.z, 0.01f);
        }

        [Test]
        public void AReachableTargetGetsHitExactlyOnceOverAFullAttackCycle()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            // Well within AttackRange from the very first tick, so it attacks in place.
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));

            TickSeconds(claw, CrimsonClaw.AttackSeconds + 0.1f);

            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, player.Health, 0.001f,
                "the swing should have landed exactly one hit for AttackDamage");
        }

        [Test]
        public void AttackCooldownBlocksAnImmediateSecondSwing()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));

            float oneFullCycle = CrimsonClaw.AttackSeconds;
            TickSeconds(claw, oneFullCycle + 0.05f);
            float healthAfterFirstAttack = player.Health;
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, healthAfterFirstAttack, 0.001f);

            // Comfortably inside the cooldown window -- no second swing should even start.
            TickSeconds(claw, CrimsonClaw.AttackCooldownSeconds * 0.5f);
            Assert.AreEqual(healthAfterFirstAttack, player.Health, 0.001f,
                "a second swing landed before AttackCooldownSeconds elapsed");
        }

        [Test]
        public void SuccessiveAttacksAlternateHands()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));
            bool startedLeft = claw.NextAttackHandIsLeft;

            // Margin bigger than AttackCooldownBlocksAnImmediateSecondSwing's
            // own +0.05f -- that test only needs to reach partway through
            // Swing to see damage land, but this one needs the FULL windup
            // -> swing -> recover -> idle transition, which can lose up to
            // one tick step's worth of overshoot at EACH of 3 phase
            // boundaries (TickAttack resets the timer to exactly 0 on
            // transition rather than carrying the overshoot forward) --
            // worst case ~3 * TickSeconds' own step, comfortably covered here.
            float oneFullCycle = CrimsonClaw.AttackSeconds;
            TickSeconds(claw, oneFullCycle + 0.15f);

            Assert.AreNotEqual(startedLeft, claw.NextAttackHandIsLeft, "the next attack should swing the OTHER hand");
        }

        /// <summary>
        /// Owner, 2026-10-24: "the telegraph of the enemies attack is either very short or non existent". After the wind-up the claw holds drawn
        /// back for its tell, glinting, and only then swipes.
        /// </summary>
        [Test]
        public void ItsClawHoldsDrawnBackAndGlintsBeforeItSwipes()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.5f));
            Transform pivot = claw.NextAttackHandIsLeft ? leftPivot : rightPivot;
            int glints = AttackGlint.Shown;

            TickSeconds(claw, CrimsonClaw.WindupSeconds + 0.05f, 0.01f);
            Assert.AreEqual(glints + 1, AttackGlint.Shown, "no glint when the claw was drawn back");
            Quaternion drawnBack = pivot.localRotation;
            Assert.Greater(Quaternion.Angle(Quaternion.identity, drawnBack), CrimsonClaw.WindupAngleDegrees * 0.9f, "the claw is not drawn back");

            // Through the tell it stays drawn back, coiling tighter (2026-11-07: a held frame looked robotic), never on its way.
            for (float t = 0.0f; t < CrimsonClaw.TellSeconds - 0.1f; t += 0.01f)
            {
                TickSeconds(claw, 0.01f, 0.01f);
                Assert.Greater(Quaternion.Angle(Quaternion.identity, pivot.localRotation), CrimsonClaw.WindupAngleDegrees * 0.9f,
                    "the claw did not stay drawn back through its tell");
            }
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, player.Health, 0.001f, "it struck during its tell");

            TickSeconds(claw, 0.1f + CrimsonClaw.SwingSeconds + 0.05f, 0.01f);
            Assert.Less(player.Health, PlayerVitals.BaseMaxHealth, "after its tell it never swiped");
        }

        /// <summary>
        /// Owner, 2026-10-24: "the dash feels pretty useless ... because the enemies just auto lock onto you anyways". Dashed round it at the end
        /// of the tell, the target is not followed (it turns only slowly winding up, not at all swiping) and the swipe, which reaches only what is
        /// in front of it, misses; standing there, it is hit.
        /// </summary>
        [Test]
        public void ItsSwipeMissesWhatDashedRoundItAtTheEndOfTheTell()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.5f));
            TickSeconds(claw, CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds - 0.06f, 0.01f);
            player.transform.position = new Vector3(1.5f, 0.0f, 0.0f);   // round to its side, still well within its reach
            TickSeconds(claw, 0.06f + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds, 0.01f);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, player.Health, 0.001f, "the swipe followed a target that dashed round it");
        }

        /// <summary>
        /// Owner, 2026-11-08: "he was swinging right next to me and should have been hitting but it wasnt reaching my hitbox". It swung from 3 m
        /// but hit only within 2.5; now a swipe lands on whatever its claws reach, the edge of a body included, and not beyond.
        /// </summary>
        [Test]
        public void ItsSwipeLandsOnWhatItsClawsReachAndNotBeyond()
        {
            Assert.LessOrEqual(CrimsonClaw.AttackRange, CrimsonClaw.ClawHitRadius - 0.3f, "it swings at what it cannot reach");
            foreach ((float distance, bool lands) in new[] { (CrimsonClaw.ClawHitRadius - 0.1f, true), (CrimsonClaw.ClawHitRadius + 0.3f, false) })
            {
                CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
                PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, CrimsonClaw.AttackRange - 0.2f));
                TickSeconds(claw, CrimsonClaw.WindupSeconds, 0.01f);
                Assert.IsTrue(claw.IsAttacking, "setup: it did not swing at a target in range");
                player.transform.position = new Vector3(0.0f, 0.0f, distance);   // stepped back during the tell
                TickSeconds(claw, CrimsonClaw.TellSeconds + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds, 0.01f);
                Assert.AreEqual(lands, player.Health < PlayerVitals.BaseMaxHealth, lands
                    ? $"a target {distance:0.00} m away, within its claws' reach, was missed"
                    : $"a target {distance:0.00} m away, beyond its claws' reach, was hit");
                TearDown();
            }
        }

        /// <summary>
        /// Owner, 2026-11-08: "it looks right and left super rapidly ... it just is a glitch". It turns toward what it chases at a steady rate,
        /// never snapping round, and ends up facing it.
        /// </summary>
        [Test]
        public void ItTurnsSmoothlyToFaceWhatItChases()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            CreatePlayer(new Vector3(0.0f, 0.0f, -10.0f));   // behind it
            Quaternion before = claw.transform.rotation;
            for (int i = 0; i < 100; i++)
            {
                claw.Tick(0.01f);
                Assert.LessOrEqual(Quaternion.Angle(before, claw.transform.rotation), CrimsonClaw.TurnDegreesPerSecond * 0.01f + 0.01f, "it snapped round");
                before = claw.transform.rotation;
            }
            Vector3 toPlayer = new Vector3(0.0f, 0.0f, -10.0f) - claw.transform.position;
            toPlayer.y = 0.0f;
            Assert.Less(Vector3.Angle(claw.transform.forward, toPlayer), 2.0f, "it never came to face what it chases");
        }

        /// <summary>Its claws, swiping, reach as far in front of it as <see cref="CrimsonClaw.ClawReachMetres"/> says (the model, not a guess).</summary>
        [Test]
        public void ItsClawsReachAsFarAsItSays()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab");
            GameObject go = Object.Instantiate(prefab);
            _spawned.Add(go);
            var claw = go.GetComponent<CrimsonClaw>();
            claw.Bind();
            CreatePlayer(new Vector3(0.0f, 0.0f, 2.0f));
            Renderer striking = (claw.NextAttackHandIsLeft ? claw.LeftClawPivot : claw.RightClawPivot).GetComponentInChildren<Renderer>();
            for (int i = 0; i < 300 && !claw.IsAttacking; i++)
            {
                claw.Tick(0.01f);
            }
            Assert.IsTrue(claw.IsAttacking, "setup: it never swiped");
            float reach = 0.0f;
            for (float t = 0.0f; t < CrimsonClaw.AttackSeconds; t += 0.01f)
            {
                claw.Tick(0.01f);
                reach = Mathf.Max(reach, striking.bounds.max.z - go.transform.position.z);   // it faces +Z, at the origin
            }
            Assert.AreEqual(CrimsonClaw.ClawReachMetres, reach, 0.35f, "its claws do not reach as far as it says");
        }

        /// <summary>A target with no ragdoll to throw is only ever swiped at, every attack.</summary>
        [Test]
        public void WhatCannotBeThrownIsNeverGrabbed()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));
            claw.GrabNext();
            TickSeconds(claw, CrimsonClaw.AttackSeconds + 0.1f);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, player.Health, 0.001f, "it did not swipe");
            Assert.IsFalse(claw.IsHolding);
        }

        /// <summary>After a swipe every claw and the body are back exactly where they rest.</summary>
        [Test]
        public void AfterASwipeEverythingIsBackAtRest()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            Transform body = new GameObject("Body").transform;
            Transform rig = new GameObject("FacingFlip").transform;
            rig.SetParent(claw.transform, false);
            body.SetParent(rig, false);
            claw.BodyVisual = body;
            Vector3 leftAt = leftPivot.localPosition;
            CreatePlayer(new Vector3(0.0f, 0.0f, 1.5f));

            TickSeconds(claw, CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds - 0.03f, 0.01f);   // coiled, just before it swipes
            Assert.Greater(Quaternion.Angle(Quaternion.identity, rig.localRotation), 3.0f, "the body took no part in the swipe");
            Assert.Greater(Quaternion.Angle(Quaternion.identity, rightPivot.localRotation), 1.0f, "the other claw took no part in it");

            TickSeconds(claw, 0.03f + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds, 0.01f);
            Assert.AreEqual(Quaternion.identity, leftPivot.localRotation);
            Assert.AreEqual(Quaternion.identity, rightPivot.localRotation);
            Assert.AreEqual(Quaternion.identity, rig.localRotation);
            Assert.AreEqual(Vector3.zero, rig.localPosition);
            Assert.AreEqual(leftAt, leftPivot.localPosition);
        }

        /// <summary>
        /// Owner, 2026-11-07: "make it glow red from within the head only visible through the eyes, the glow fades away upon death". Its eyes
        /// glow until it dies, then go out over EyesFadeSeconds, on its own copy of their material: another Claw's stay lit.
        /// </summary>
        [Test]
        public void ItsEyesGlowUntilItDiesThenGoOut()
        {
            Assert.AreEqual(1.0f, CrimsonClaw.EyesGlowAfterDeath(0.0f), 1e-5f);
            Assert.AreEqual(0.0f, CrimsonClaw.EyesGlowAfterDeath(CrimsonClaw.EyesFadeSeconds), 1e-5f);

            Shader lit = CityX.Core.LitShader.Find();
            var skin = new Material(lit);
            var eyes = new Material(lit);
            eyes.SetColor("_EmissionColor", CrimsonClaw.EyesGlow);
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            var body = new GameObject("Body");
            body.transform.SetParent(claw.transform, false);
            body.AddComponent<MeshFilter>();
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { skin, eyes };
            claw.BodyVisual = body.transform;
            claw.EyesMaterialSlot = 1;
            try
            {
                TickSeconds(claw, 0.5f);
                Assert.AreEqual(1.0f, claw.EyesBrightness, "its eyes dimmed while it lived");
                Assert.AreSame(eyes, renderer.sharedMaterials[1], "a living one's eyes left the shared material");

                ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, Vector3.zero, 0.0f);
                TickSeconds(claw, CrimsonClaw.EyesFadeSeconds * 0.5f);
                Material own = renderer.sharedMaterials[1];
                Assert.AreNotSame(eyes, own, "it put out every Claw's eyes, not its own");
                float halfway = own.GetColor("_EmissionColor").r;
                Assert.Less(halfway, CrimsonClaw.EyesGlow.r * 0.8f, "its eyes did not dim");
                Assert.Greater(halfway, CrimsonClaw.EyesGlow.r * 0.2f, "its eyes went out at once, not fading");
                Assert.AreEqual(CrimsonClaw.EyesGlow, eyes.GetColor("_EmissionColor"), "the shared eyes dimmed");

                TickSeconds(claw, CrimsonClaw.EyesFadeSeconds);
                Assert.AreEqual(0.0f, own.GetColor("_EmissionColor").maxColorComponent, 1e-4f, "its eyes never went out");
            }
            finally
            {
                Object.DestroyImmediate(renderer.sharedMaterials[1]);
                Object.DestroyImmediate(skin);
                Object.DestroyImmediate(eyes);
            }
        }

        /// <summary>
        /// The built Crimson Claw's swipe cuts in across its front, at what it swipes at. Swung by the claws' names it swept the claw out to
        /// its side and back, away from its target (the model's LeftClaw sits on its right).
        /// </summary>
        [Test]
        public void ItsSwipeCutsInAcrossItsFront()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab");
            GameObject go = Object.Instantiate(prefab);
            _spawned.Add(go);
            var claw = go.GetComponent<CrimsonClaw>();
            claw.Bind();
            CreatePlayer(new Vector3(0.0f, 0.0f, 2.0f));
            foreach (bool left in new[] { true, false })
            {
                Assert.AreEqual(left, claw.NextAttackHandIsLeft);
                Transform pivot = left ? claw.LeftClawPivot : claw.RightClawPivot;
                Renderer striking = pivot.GetComponentInChildren<Renderer>();
                float side = Mathf.Sign(go.transform.InverseTransformPoint(striking.bounds.center).x);
                Assert.AreEqual(side, Mathf.Sign(go.transform.InverseTransformPoint(pivot.position).x), "the claw turns about a point on the other side");
                Assert.Less(Vector3.Distance(pivot.position, striking.bounds.center), striking.bounds.extents.magnitude,
                    "the claw turns about a point off it, not its wrist");
                for (int i = 0; i < 300 && !claw.IsAttacking; i++)
                {
                    claw.Tick(0.01f);
                }
                Assert.IsTrue(claw.IsAttacking, "it never swiped at the target in front of it");

                TickSeconds(claw, CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds - 0.03f, 0.01f);
                Vector3 coiled = go.transform.InverseTransformPoint(striking.bounds.center);
                TickSeconds(claw, 0.03f + CrimsonClaw.SwingSeconds, 0.01f);
                Vector3 struck = go.transform.InverseTransformPoint(striking.bounds.center);

                Assert.Less((struck.x - coiled.x) * side, -0.3f, $"the {(left ? "left" : "right")} claw did not cut in across its front");
                Assert.Greater(struck.z, coiled.z - 0.1f, $"the {(left ? "left" : "right")} claw swept back, away from its target");
                for (int i = 0; i < 300 && claw.IsAttacking; i++)
                {
                    claw.Tick(0.01f);
                }
            }
        }

        /// <summary>The built Crimson Claw: the eyes in its head glow, and nothing else on it does.</summary>
        [Test]
        public void TheBuiltClawsEyesAloneGlow()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab");
            Assert.IsNotNull(prefab, "no Crimson Claw prefab: run CrimsonClawBuilder.Build");
            var claw = prefab.GetComponent<CrimsonClaw>();
            Assert.GreaterOrEqual(claw.EyesMaterialSlot, 0, "its head has no eyes");
            MeshRenderer body = claw.BodyVisual.GetComponent<MeshRenderer>();
            Mesh mesh = claw.BodyVisual.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.GetTriangles(claw.EyesMaterialSlot).Length / 3, 20, "its eyes are not in the mesh");
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>())
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material material = renderer.sharedMaterials[i];
                    bool eyes = renderer == body && i == claw.EyesMaterialSlot;
                    bool glows = material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                    Assert.AreEqual(eyes, glows, eyes ? "its eyes do not glow" : $"{renderer.name}'s {material.name} glows");
                }
            }
            Assert.AreEqual(0, prefab.GetComponentsInChildren<Light>().Length, "a light shines out of more than its eyes");
        }
    }
}
