using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE STATS ON A REAL BODY (owner, 2026-10-14: "make them actually effect the character as the stat describes"): Movement Speed covers
    /// more ground, Melee Speed throws punches faster, a stagger cuts a charging spin short unless Stability shrugs it off, a block that stops
    /// a blow outright throws sparks here and on another player's copy, Spell Power (not the melee stat) makes the fireball hurt more, and
    /// Cooldown Reduction shortens its cooldown.
    /// </summary>
    public sealed class StatEffectsPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private GameObject Make(GameObject go)
        {
            _made.Add(go);
            return go;
        }

        private void Ground()
        {
            GameObject ground = Make(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        private GameObject Player(Vector3 at)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Make(Object.Instantiate(prefab, at, Quaternion.identity));
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator MovementSpeedCoversMoreGround()
        {
            Time.captureFramerate = 60;
            Ground();
            GameObject plain = Player(new Vector3(-5.0f, 0.1f, 0.0f));
            GameObject quick = Player(new Vector3(5.0f, 0.1f, 0.0f));
            quick.GetComponent<PlayerVitals>().Stats[StatId.MovementSpeed] = 20;   // +20%
            var plainBody = plain.GetComponent<PlayerLocomotion>();
            var quickBody = quick.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 30; i++)
            {
                plainBody.Tick(new LocomotionInput(), 1.0f / 60.0f);
                quickBody.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 plainStart = plain.transform.position;
            Vector3 quickStart = quick.transform.position;
            var forward = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
            for (int i = 0; i < 90; i++)
            {
                plainBody.Tick(forward, 1.0f / 60.0f);
                quickBody.Tick(forward, 1.0f / 60.0f);
                yield return null;
            }
            float plainDistance = Vector3.Distance(plainStart, plain.transform.position);
            float quickDistance = Vector3.Distance(quickStart, quick.transform.position);
            Debug.Log($"CityX movement speed: {plainDistance:0.00} m plain, {quickDistance:0.00} m at +20%");
            Assert.Greater(plainDistance, 2.0f, "setup: the plain body should have walked");
            Assert.AreEqual(1.2f, quickDistance / plainDistance, 0.06f, "Movement Speed +20% should cover a fifth more ground");
        }

        [UnityTest]
        public IEnumerator MeleeSpeedThrowsPunchesFaster()
        {
            Time.captureFramerate = 60;
            Ground();
            GameObject plain = Player(new Vector3(-5.0f, 0.1f, 0.0f));
            GameObject quick = Player(new Vector3(5.0f, 0.1f, 0.0f));
            quick.GetComponent<PlayerVitals>().Stats[StatId.MeleeSpeed] = 40;   // the cap, +60%
            yield return null;
            var plainFists = plain.GetComponent<MeleeAttacker>();
            var quickFists = quick.GetComponent<MeleeAttacker>();
            Assert.IsTrue(plainFists.TryAttack() && quickFists.TryAttack(), "setup: both should punch");
            int plainFrames = 0;
            int quickFrames = 0;
            for (int i = 0; i < 300 && (plainFists.IsSwinging || quickFists.IsSwinging); i++)
            {
                if (plainFists.IsSwinging) plainFrames++;
                if (quickFists.IsSwinging) quickFrames++;
                plainFists.Tick(1.0f / 60.0f);
                quickFists.Tick(1.0f / 60.0f);
                yield return null;
            }
            Debug.Log($"CityX melee speed: a jab took {plainFrames} frames plain, {quickFrames} at +60%");
            Assert.AreEqual(1.6f, plainFrames / (float)quickFrames, 0.15f, "Melee Speed +60% should finish the punch in five eighths of the time");
        }

        [UnityTest]
        public IEnumerator AStaggerCutsAChargingSpinShortUnlessStabilityShrugsItOff()
        {
#if UNITY_EDITOR
            Ground();
            GameObject player = Player(new Vector3(0.0f, 0.1f, 0.0f));
            var vitals = player.GetComponent<PlayerVitals>();
            var melee = player.GetComponent<MeleeAttacker>();
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab");
            GameObject sword = Make(Object.Instantiate(swordPrefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity));
            sword.GetComponent<SwordWeapon>().OnEquip(player, CityX.Items.ItemInstanceData.None);
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            vitals.Stats[StatId.Stability] = 25;   // 75%
            vitals.Roll = () => 0.5f;              // under 75%: shrugged off
            Assert.IsTrue(melee.TrySpinAttack(charge: true), "setup: the spin should start charging");
            DamageContext.Strike(vitals, 5.0f, player.transform.position + player.transform.forward, 0.4f, DamageKind.Physical, 0.0f);
            Assert.IsTrue(melee.IsChargingSpin, "Stability should have shrugged the blow off and kept the charge going");
            Assert.IsFalse(vitals.IsStunned);

            vitals.Roll = () => 0.9f;              // over 75%: staggered
            DamageContext.Strike(vitals, 5.0f, player.transform.position + player.transform.forward, 0.4f, DamageKind.Physical, 0.0f);
            Assert.IsTrue(vitals.IsStunned);
            Assert.IsFalse(melee.IsChargingSpin, "a stagger should cut the charging spin short");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABlockThatStopsABlowThrowsSparksHereAndOnAnotherPlayersCopy()
        {
            Ground();
            GameObject player = Player(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            var vitals = player.GetComponent<PlayerVitals>();
            var effect = player.GetComponent<BlockDeflectEffect>();
            Assert.IsNotNull(effect, "the input reader should give every body its deflect effect");

            vitals.Roll = () => 0.0f;
            vitals.StartBlock();
            float before = vitals.Health;
            DamageContext.Strike(vitals, 20.0f, player.transform.position + player.transform.forward * 5.0f, 0.0f, DamageKind.Bullet, 0.0f);
            Assert.AreEqual(before, vitals.Health, 0.01f, "the bullet should have glanced off the block");
            Assert.AreEqual(1, effect.Shown, "the deflection shows its sparks");
            Assert.IsNotNull(GameObject.Find("DeflectSparks"), "the sparks are in the world");

            // Another player's deflection arrives by name (what NetworkedAnimatorSync hands a copy).
            player.GetComponent<PlayerAnimationDriver>().PlayRemoteState(BlockDeflectEffect.StateName, 1.0f, 0.0f);
            Assert.AreEqual(2, effect.Shown, "a copy shows another player's deflection too");
            Assert.IsNotNull(BlockDeflectEffect.Ting(), "the ting is made");
            Assert.AreEqual(0, effect.ClashesShown, "a bullet glancing off is the ting, not a sword clash");

            // A sword guard meeting a blade, on a copy (the owner, 2026-10-17: one of their three recordings at random).
            player.GetComponent<PlayerAnimationDriver>().PlayRemoteState(BlockDeflectEffect.ClashStateName, 1.0f, 0.0f);
            Assert.AreEqual(1, effect.ClashesShown, "a copy shows another player's sword clash");
            Assert.IsNotNull(effect.LastClashClip, "the clash played no recording");
            CollectionAssert.Contains(BlockDeflectEffect.Clashes(), effect.LastClashClip);
        }

        [UnityTest]
        public IEnumerator SpellPowerNotMeleeDamageMakesTheFireballHurtMoreAndCooldownReductionShortensIt()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            Ground();
            GameObject player = Player(new Vector3(0.0f, 0.1f, 0.0f));
            var vitals = player.GetComponent<PlayerVitals>();
            var caster = player.AddComponent<FireballCaster>();
            caster.FireballPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            Assert.IsNotNull(caster.FireballPrefab, "no fireball prefab");
            GameObject target = Make(GameObject.CreatePrimitive(PrimitiveType.Cube));
            target.transform.position = new Vector3(0.0f, 1.5f, 6.0f);
            target.transform.localScale = new Vector3(2.0f, 3.0f, 0.5f);
            var dummy = target.AddComponent<SparringDummy>();
            dummy.MaxHealth = 10000.0f;
            dummy.Reset();
            Physics.SyncTransforms();
            yield return null;

            Collider[] own = player.GetComponentsInChildren<Collider>();
            Vector3 origin = new Vector3(0.0f, 1.5f, 0.8f);

            vitals.Stats[StatId.MeleeDamage] = 50;   // the melee stat: no use to a spell
            Assert.IsTrue(caster.TryCast(origin, Vector3.forward, own));
            yield return WaitForHit(dummy, 1);
            Assert.AreEqual(FireballProjectile.BaseDamage, dummy.TotalDamageTaken, 0.01f, "Melee Damage should not make a spell hurt more");

            // The cooldown: 1.5 s plain; half that with 50% Cooldown Reduction.
            vitals.Stats[StatId.CooldownReduction] = 50;
            vitals.Stats[StatId.SpellPower] = 50;      // +100%
            for (int i = 0; i < 120 && !caster.CanCast; i++)
            {
                caster.Tick(1.0f / 60.0f);
                yield return null;
            }
            Assert.IsTrue(caster.CanCast, "setup: the first cooldown should be over");
            Assert.IsTrue(caster.TryCast(origin, Vector3.forward, own));
            float waited = 0.0f;
            while (!caster.CanCast && waited < 3.0f)
            {
                caster.Tick(1.0f / 60.0f);
                waited += 1.0f / 60.0f;
                yield return null;
            }
            Assert.AreEqual(caster.CooldownSeconds * 0.5f, waited, 0.05f, "Cooldown Reduction 50% should halve the cooldown");
            yield return WaitForHit(dummy, 2);
            Assert.AreEqual(FireballProjectile.BaseDamage * 3.0f, dummy.TotalDamageTaken, 0.01f, "Spell Power +100% should double the second fireball");
#else
            yield break;
#endif
        }

        private static IEnumerator WaitForHit(SparringDummy dummy, int hits)
        {
            for (int i = 0; i < 240 && dummy.TimesHit < hits; i++)
            {
                yield return null;
            }
            Assert.AreEqual(hits, dummy.TimesHit, "the fireball did not reach the target");
        }
    }
}
