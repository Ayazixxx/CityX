using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What a blow that lands does to the body (HitImpactEffects, PlayerLocomotion.Impact.cs, BloodBurst, BloodSplats): the freeze, the shove,
    /// the flash, the blood and the splats, a copy told from the network, and a killing cut's fling (owner, 2026-10-17: "the hits dont feel
    /// impactful at all ... make the blood effects better too").
    /// </summary>
    public sealed class HitImpactPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _body;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private HitImpactEffects _effects;
        private GameObject _extra;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0.0f, 0.1f, 0.0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2.6f;
            controller.center = new Vector3(0.0f, 1.3f, 0.0f);
            controller.radius = 0.35f;
            _locomotion = _body.AddComponent<PlayerLocomotion>();
            _vitals = _body.AddComponent<PlayerVitals>();
            _vitals.Roll = () => 1.0f;
            _vitals.Revive();
            _effects = _body.AddComponent<HitImpactEffects>();
            BloodSplats.Clear();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            BloodSplats.Clear();
            if (_extra != null) Object.DestroyImmediate(_extra);
            if (_body != null) Object.DestroyImmediate(_body);
            if (_ground != null) Object.DestroyImmediate(_ground);
            foreach (GameObject burst in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (burst != null && burst.name == "BloodBurst")
                {
                    Object.DestroyImmediate(burst);
                }
            }
        }

        private IEnumerator Settle()
        {
            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(default, Dt);
                yield return null;
            }
        }

        /// <summary>A blow from a striker standing in front of the body, with its contact point on the chest.</summary>
        private HitResponse StrikeFromTheFront(float hitstunSeconds, float damage = 10.0f)
        {
            Vector3 striker = _body.transform.position + _body.transform.forward * 1.6f;
            Vector3 chest = _body.transform.position + Vector3.up * 1.6f + _body.transform.forward * 0.3f;
            return DamageContext.Strike(_vitals, damage, striker, hitstunSeconds, DamageKind.Physical, 0.0f, chest);
        }

        [UnityTest]
        public IEnumerator ALandedBlowHoldsTheBodyStillThenThrowsItBack()
        {
            yield return Settle();
            Vector3 before = _body.transform.position;
            int burstsBefore = BloodBurst.Spawned;

            Assert.AreEqual(HitResponse.Landed, StrikeFromTheFront(0.6f));

            Assert.AreEqual(1, _effects.Shown, "the wound was not shown");
            Assert.IsTrue(_locomotion.IsImpactFrozen, "a landed blow should hold the body still for an instant");
            Assert.AreEqual(burstsBefore + 1, BloodBurst.Spawned, "no blood burst out of the wound");

            // Held: nothing moves through the freeze.
            for (int i = 0; i < 3; i++)
            {
                _locomotion.Tick(default, Dt);
                yield return null;
            }
            Assert.Less(Vector3.Distance(before, _body.transform.position), 0.01f, "the body moved during its own impact freeze");

            // Then thrown back, away from the striker (who stood in front): along -forward.
            for (int i = 0; i < 24; i++)
            {
                _locomotion.Tick(default, Dt);
                yield return null;
            }
            Assert.IsFalse(_locomotion.IsImpactFrozen, "the freeze never let go");
            float back = Vector3.Dot(_body.transform.position - before, -_body.transform.forward);
            Assert.Greater(back, 0.25f, $"the body was thrown {back:0.00} m back; a light blow (0.6 s of stun) should throw it about 0.39 m");
            Assert.Less(back, 0.6f, "thrown too far for a light blow");
        }

        [UnityTest]
        public IEnumerator AHeavyBlowHoldsLongerAndThrowsFurther()
        {
            yield return Settle();
            Vector3 before = _body.transform.position;
            StrikeFromTheFront(1.0f);
            int frozenFrames = 0;
            for (int i = 0; i < 40; i++)
            {
                if (_locomotion.IsImpactFrozen) frozenFrames++;
                _locomotion.Tick(default, Dt);
                yield return null;
            }
            float back = Vector3.Dot(_body.transform.position - before, -_body.transform.forward);
            Assert.GreaterOrEqual(frozenFrames, 6, $"a heavy blow held the body for only {frozenFrames} frames (0.12 s is 7)");
            Assert.Greater(back, 0.5f, $"a heavy blow threw the body only {back:0.00} m");
        }

        [UnityTest]
        public IEnumerator ABlowSoakedByAHeldGuardShowsNoWound()
        {
            yield return Settle();
            _vitals.BlockDrainsStamina = true;
            _vitals.StartBlock();
            int burstsBefore = BloodBurst.Spawned;

            HitResponse response = StrikeFromTheFront(0.6f);

            Assert.AreEqual(HitResponse.Guarded, response);
            Assert.AreEqual(0, _effects.Shown, "a guard that held should not bleed");
            Assert.AreEqual(burstsBefore, BloodBurst.Spawned);
            Assert.IsFalse(_locomotion.IsImpactFrozen, "a blow on the guard should not freeze the defender");
            Assert.AreEqual(0, BloodSplats.Count, "a blow on the guard should leave nothing on the floor");
        }

        [UnityTest]
        public IEnumerator TheBloodLandsOnTheFloorAndOnAWallBehind()
        {
            _extra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extra.name = "Wall";
            _extra.transform.position = _body.transform.position - _body.transform.forward * 1.0f + Vector3.up * 1.5f;
            _extra.transform.localScale = new Vector3(6.0f, 3.0f, 0.3f);
            Physics.SyncTransforms();
            yield return Settle();

            StrikeFromTheFront(0.6f);

            Assert.GreaterOrEqual(BloodSplats.Count, 2, "a wound on a floor with a wall behind should leave at least the floor's splat and the wall's");
            bool onFloor = false, onWall = false;
            foreach (BloodSplat splat in Object.FindObjectsByType<BloodSplat>(FindObjectsSortMode.None))
            {
                float up = Vector3.Dot(splat.transform.up, Vector3.up);
                if (up > 0.9f) onFloor = true;
                if (Mathf.Abs(up) < 0.2f) onWall = true;
                Assert.Less(splat.transform.position.y, 2.9f, "a splat floated above the wall");
            }
            Assert.IsTrue(onFloor, "no splat on the floor");
            Assert.IsTrue(onWall, "no splat on the wall behind the body");
            yield return null;
            Assert.IsNotNull(GameObject.Find("BloodBurst"), "the burst was not in the world a frame after the blow");
        }

        [Test]
        public void TheSplatsNeverExceedTheirCap()
        {
            Vector3 point = _body.transform.position + Vector3.up * 1.4f;
            for (int i = 0; i < 40; i++)
            {
                BloodSplats.Mark(point + Vector3.right * (i * 0.1f), Vector3.forward, 1.0f, _body.transform);
            }
            Assert.LessOrEqual(BloodSplats.Count, BloodSplats.MaxSplats);
            Assert.Greater(BloodSplats.Count, BloodSplats.MaxSplats - 3, "the pool should be full after forty wounds");
        }

        [UnityTest]
        public IEnumerator TheFlashBrightensTheBodyForAnInstantThenLetsGo()
        {
            var skin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            skin.transform.SetParent(_body.transform, false);
            skin.transform.localPosition = Vector3.up * 1.3f;
            Object.DestroyImmediate(skin.GetComponent<Collider>());
            var renderer = skin.GetComponent<MeshRenderer>();
            // A known skin colour, as CharacterIdentity gives a body (CityX/Lit, its own material instance).
            var skinColour = new Color(0.6f, 0.5f, 0.45f, 1.0f);
            Shader lit = CityX.Core.LitShader.Find();
            renderer.material = new Material(lit != null ? lit : Shader.Find("Standard"));
            renderer.material.SetColor("_BaseColor", skinColour);
            renderer.material.SetColor("_Color", skinColour);
            yield return Settle();

            StrikeFromTheFront(0.6f);

            Assert.IsTrue(_effects.IsFlashing, "no flash on the blow");
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Color flashed = block.GetColor("_BaseColor");
            Assert.Greater(flashed.r, skinColour.r * 1.5f,
                $"the flash should brighten the body (its own colour times {1.0f + HitImpactEffects.FlashBrightness:0.0} at the top); got {flashed} over {skinColour}");
            Assert.AreEqual(skinColour.g / skinColour.r, flashed.g / flashed.r, 0.01f, "the flash changed the skin's hue");

            for (int i = 0; i < 12; i++)
            {
                yield return null;
            }
            Assert.IsFalse(_effects.IsFlashing, "the flash outstayed its 0.09 s");
            Assert.IsFalse(renderer.HasPropertyBlock(), "the flash left its colour on the body");
        }

        [Test]
        public void AWoundsNameSaysItsSideAndWeightAndReadsBack()
        {
            foreach (HitDirection direction in System.Enum.GetValues(typeof(HitDirection)))
            {
                foreach (bool heavy in new[] { false, true })
                {
                    string name = HitImpactEffects.NameFor(direction, heavy);
                    Assert.IsTrue(name.StartsWith(HitImpactEffects.StatePrefix), name);
                    Assert.IsTrue(HitImpactEffects.TryParse(name, out HitDirection back, out bool backHeavy), name);
                    Assert.AreEqual(direction, back, name);
                    Assert.AreEqual(heavy, backHeavy, name);
                }
            }
            Assert.IsFalse(HitImpactEffects.TryParse("hit_front", out _, out _));
            Assert.IsFalse(HitImpactEffects.TryParse("wounded_front", out _, out _));
            Assert.IsFalse(HitImpactEffects.TryParse("wounded_up_heavy", out _, out _));
            Assert.IsFalse(HitImpactEffects.TryParse(null, out _, out _));
        }

        [UnityTest]
        public IEnumerator AWoundToldFromTheNetworkIsShownOnTheCopyAndItsFreezeLetsGoWithoutATick()
        {
            // A copy of another player's body: the real prefab, never Ticked (nothing drives it), told the wound by name as the network would.
            _extra = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(6.0f, 0.1f, 0.0f), Quaternion.identity);
            _extra.GetComponent<PlayerInputReader>().enabled = false;
            var driver = _extra.GetComponent<PlayerAnimationDriver>();
            var copyEffects = _extra.GetComponent<HitImpactEffects>();
            var copyLocomotion = _extra.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(copyEffects, "PlayerInputReader.Awake should give every body the wound effects");
            yield return null;
            int burstsBefore = BloodBurst.Spawned;

            driver.PlayRemoteState(HitImpactEffects.NameFor(HitDirection.Left, heavy: true));

            Assert.AreEqual(1, copyEffects.RemoteShown, "the copy did not show the wound it was told");
            Assert.AreEqual(burstsBefore + 1, BloodBurst.Spawned);
            Assert.IsTrue(copyLocomotion.IsImpactFrozen, "the copy should be held still like the real body");
            for (int i = 0; i < 12; i++)
            {
                yield return null;   // no Tick: LateUpdate counts the freeze down on a copy
            }
            Assert.IsFalse(copyLocomotion.IsImpactFrozen, "a copy's freeze never let go (nothing Ticks a copy)");
        }

        [UnityTest]
        public IEnumerator TheAttackersHitStopHoldsTheirWholeBodyNotJustTheArm()
        {
            _extra = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.1f, 8.0f), Quaternion.identity);
            _extra.GetComponent<PlayerInputReader>().enabled = false;
            var attackerLocomotion = _extra.GetComponent<PlayerLocomotion>();
            var attackerVitals = _extra.GetComponent<PlayerVitals>();
            attackerVitals.InfiniteStamina = true;
            var combat = _extra.AddComponent<SoulsSwordCombat>();
            combat.Equip(null);   // no sword object: a blade held out in front at chest height
            combat.HitStopSeconds = 0.10f;
            var animator = _extra.GetComponentInChildren<Animator>();
            // Something to hit, 1.5 m ahead.
            _body.transform.position = _extra.transform.position + _extra.transform.forward * 1.5f;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++)
            {
                attackerLocomotion.Tick(default, Dt);
                yield return null;
            }

            combat.RequestLight(_extra.transform.eulerAngles.y);
            float elapsed = 0.0f;
            while (!combat.IsInHitStop && elapsed < 2.0f)
            {
                combat.Tick(Dt);
                attackerLocomotion.Tick(default, Dt);
                attackerVitals.TickHitstun(Dt);
                elapsed += Dt;
                yield return null;
            }
            Assert.IsTrue(combat.IsInHitStop, "the blade never connected");
            Assert.IsTrue(attackerLocomotion.IsImpactFrozen, "the hit-stop should hold the attacker's whole body, not only the sword arm");
            Assert.AreEqual(0.0f, animator.speed, 1e-4f, "the attacker's Animator should be stopped through the hit-stop");

            for (int i = 0; i < 12; i++)
            {
                combat.Tick(Dt);
                attackerLocomotion.Tick(default, Dt);
                yield return null;
            }
            Assert.IsFalse(combat.IsInHitStop);
            Assert.IsFalse(attackerLocomotion.IsImpactFrozen, "the attacker stayed frozen after the hit-stop");
            Assert.Greater(animator.speed, 0.5f, "the attacker's Animator was not let go");
        }

        [UnityTest]
        public IEnumerator AKillingCutFlingsTheCorpseAwayFromTheStriker()
        {
            _extra = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.1f, 10.0f), Quaternion.identity);
            _extra.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = _extra.GetComponent<PlayerVitals>();
            var ragdoll = _extra.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "the player prefab has no ragdoll");
            vitals.Revive();
            var locomotion = _extra.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 10; i++)
            {
                locomotion.Tick(default, Dt);
                yield return null;
            }
            vitals.TakeDamage(vitals.MaxHealth - 1.0f);
            Assert.IsTrue(vitals.IsAlive);

            Vector3 striker = _extra.transform.position + _extra.transform.forward * 1.6f;
            Vector3 torsoStart = ragdoll.Torso.position;
            DamageContext.Strike(vitals, 50.0f, striker, 0.6f, DamageKind.Physical, 0.0f, null);

            Assert.IsTrue(vitals.IsDead);
            Assert.IsTrue(ragdoll.IsLimp, "a killing cut should drop the body limp");
            // The fling is given to the limp body the moment it dies (Ragdoll.OnDied): read it there, before the joints and the floor have
            // had a physics step to do what they will with it. Then it really travels: within half a second the torso is further from the
            // striker than where it died.
            float away = Vector3.Dot(ragdoll.Torso.linearVelocity, -_extra.transform.forward);
            Assert.Greater(away, 1.5f, $"the corpse should be flung away from the striker; its torso leaves at {away:0.0} m/s that way");
            float travelled = 0.0f;
            for (int i = 0; i < 30 && travelled < 0.15f; i++)
            {
                yield return null;
                travelled = Vector3.Dot(ragdoll.Torso.position - torsoStart, -_extra.transform.forward);
            }
            Assert.Greater(travelled, 0.15f, $"the corpse never moved away from the striker ({travelled:0.00} m in half a second)");
        }
    }
}
