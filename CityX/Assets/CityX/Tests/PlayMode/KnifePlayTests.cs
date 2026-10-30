using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The knife fought (docs/KNIFE.md), stepped by hand as SoulsSwordCombatPlayTests are: its light lands without staggering the body it
    /// hits where a sword's does; from behind the same blow is a backstab -- three times the damage, a stagger, a critical number; its swing
    /// is over a third sooner; and the kitchen knife's own prefab is a knife.
    /// </summary>
    public sealed class KnifePlayTests
    {
        private sealed class Blade : ISwordBlade
        {
            public Transform Body;
            public bool Knife;

            public Vector3 BladeBaseWorld => Body.position + Vector3.up * 1.4f + Body.forward * 0.4f;

            public Vector3 BladeTipWorld => BladeBaseWorld + Body.forward * 0.3f;

            public bool IsOneHanded => true;

            public bool IsKnife => Knife;

            public float GetMeleeDamage() => Knife ? KnifeRules.BaseDamage : CombatTuning.PunchDamage;
        }

        private readonly List<GameObject> _made = new List<GameObject>();
        private GameObject _attacker;
        private PlayerVitals _attackerVitals;
        private SoulsSwordCombat _combat;
        private PlayerVitals _target;

        [SetUp]
        public void SetUp()
        {
            _attacker = new GameObject("Attacker");
            _made.Add(_attacker);
            _attackerVitals = _attacker.AddComponent<PlayerVitals>();
            _attackerVitals.Revive();
            _combat = _attacker.AddComponent<SoulsSwordCombat>();
            _combat.HitStopSeconds = 0.0f;

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Target";
            target.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            _made.Add(target);
            _target = target.AddComponent<PlayerVitals>();
            _target.Roll = () => 1.0f;   // no chance rolls (block, stability): the same every run
            _target.Revive();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private void Hold(bool knife)
        {
            _combat.Equip(new Blade { Body = _attacker.transform, Knife = knife });
        }

        /// <summary>The target faces the attacker (front) or away from it (its back to the blade).</summary>
        private void TargetFaces(bool towardAttacker)
        {
            _target.transform.rotation = Quaternion.Euler(0.0f, towardAttacker ? 180.0f : 0.0f, 0.0f);
            Physics.SyncTransforms();
        }

        /// <summary>One light swung through its live frames; the damage it did (a few blows in all, well within the target's health).</summary>
        private float SwingOnce()
        {
            float before = _target.Health;
            _combat.RequestLight(0.0f);
            SoulsAttack light = _combat.CurrentAttack;
            float seconds = (light.Startup + light.Active) / _combat.BladeSwingSpeed + 0.03f;
            for (float t = 0.0f; t < seconds; t += 1.0f / 60.0f)
            {
                _combat.Tick(1.0f / 60.0f);
            }
            return before - _target.Health;
        }

        private void Settle()
        {
            for (float t = 0.0f; t < 2.0f; t += 1.0f / 60.0f)
            {
                _combat.Tick(1.0f / 60.0f);
                _target.TickHitstun(1.0f / 60.0f);
            }
        }

        [Test]
        public void AKnifesLightLandsWithoutStaggeringWhereASwordsDoes()
        {
            TargetFaces(true);
            Hold(true);
            float knifeDamage = SwingOnce();
            Assert.Greater(knifeDamage, 0.0f, "the knife's light missed a body in front of it");
            Assert.IsFalse(_target.IsStunned, "a knife's light staggered the body it hit");
            Settle();

            Hold(false);
            float swordDamage = SwingOnce();
            Assert.Greater(swordDamage, 0.0f);
            Assert.IsTrue(_target.IsStunned, "a sword's light no longer staggers");
            Assert.Less(knifeDamage, swordDamage, "a knife's blow should be lighter than a sword's");
        }

        [Test]
        public void FromBehindTheSameBlowIsABackstabThreeTimesTheDamageAStaggerAndACriticalNumber()
        {
            Hold(true);
            TargetFaces(true);
            float front = SwingOnce();
            Settle();

            TargetFaces(false);
            int backstabs = 0;
            bool critical = false;
            _combat.Backstabbed += _ => backstabs++;
            System.Action<DamageDealt> landed = hit => critical |= hit.Critical && ReferenceEquals(hit.Target, _target);
            DamageDealt.Landed += landed;
            float back;
            try
            {
                back = SwingOnce();
            }
            finally
            {
                DamageDealt.Landed -= landed;
            }
            Assert.AreEqual(1, backstabs, "a blow on the body's back was not a backstab");
            Assert.AreEqual(front * Backstab.KnifeMultiplier, back, 0.05f, "a knife's backstab is three times the blow");
            Assert.IsTrue(critical, "the backstab's number is not a critical one");
            Settle();

            Hold(false);
            TargetFaces(true);
            float swordFront = SwingOnce();
            Settle();
            TargetFaces(false);
            float swordBack = SwingOnce();
            Assert.AreEqual(swordFront * Backstab.BladeMultiplier, swordBack, 0.05f, "a sword backstabs for less than a knife");
        }

        [Test]
        public void AKnifesSwingIsOverAThirdSoonerThanTheSwords()
        {
            float SecondsToFinish(bool knife)
            {
                Hold(knife);
                _target.transform.position = new Vector3(0.0f, 1.0f, 20.0f);   // out of reach: nothing holds the swing
                Physics.SyncTransforms();
                _combat.RequestLight(0.0f);
                float t = 0.0f;
                while (_combat.Phase != SoulsPhase.Idle && t < 3.0f)
                {
                    _combat.Tick(1.0f / 240.0f);
                    t += 1.0f / 240.0f;
                }
                return t;
            }
            float sword = SecondsToFinish(false);
            float knife = SecondsToFinish(true);
            Assert.AreEqual(sword / KnifeRules.SwingSpeed, knife, 0.02f, $"the knife's swing took {knife:0.000} s against the sword's {sword:0.000} s");
        }

        [Test]
        public void TheKitchenKnifeIsAnEdgedOneHandedKnife()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab");
            Assert.IsNotNull(prefab, "no KitchenKnife.prefab -- rebuild the scene");
            var knife = prefab.GetComponent<SwordWeapon>();
            Assert.IsNotNull(knife);
            Assert.IsTrue(knife.IsKnife && knife.IsOneHanded, "the kitchen knife is not fought as a knife");
            Assert.IsTrue(knife.IsEdged, "a knife cuts (docs/DISMEMBERMENT.md)");
            Assert.AreEqual(ItemRegistry.KitchenKnife, knife.ItemId);
            Assert.AreEqual(KnifeRules.BaseDamage, knife.GetMeleeDamage());
#endif
        }
    }
}
