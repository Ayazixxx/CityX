using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A fireball's impact, which only ever fires from a real physics
    /// collision (OnCollisionEnter) -- needs actual frames, not an EditMode
    /// test. Same shape as ThrownBottlePlayTests.
    /// </summary>
    public sealed class FireballProjectilePlayTests
    {
        private sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public float LastDamage { get; private set; } = -1.0f;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                LastDamage = amount;
                return HitResponse.Landed;
            }
        }

        private GameObject _fireballPrefab;

        [SetUp]
        public void SetUp()
        {
            _fireballPrefab = new GameObject("FireballPrefab");
            _fireballPrefab.AddComponent<Rigidbody>();
            var collider = _fireballPrefab.AddComponent<SphereCollider>();
            collider.radius = 0.16f;
            _fireballPrefab.AddComponent<FireballProjectile>();
            // ACTIVE, NOT DEACTIVATED -- see ThrownBottlePlayTests' own
            // comment: Instantiate() copies the source's own active state
            // onto the clone, and an inactive clone never fires
            // OnCollisionEnter at all. Parked far below the test area so
            // this template object itself can't physically interfere.
            _fireballPrefab.transform.position = new Vector3(0.0f, -500.0f, 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_fireballPrefab);
        }

        private static IEnumerator WaitUntilDestroyedOrTimeout(GameObject target, float timeoutSeconds)
        {
            float elapsed = 0.0f;
            while (target != null && elapsed < timeoutSeconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator HittingADamageableAppliesStrengthScaledDamageAndDestroysItself()
        {
            var targetObject = new GameObject("Dummy");
            targetObject.transform.position = new Vector3(0.0f, 0.0f, 2.0f);
            var targetCollider = targetObject.AddComponent<BoxCollider>();
            targetCollider.size = Vector3.one;
            var damageable = targetObject.AddComponent<TestDamageable>();

            FireballProjectile fireball = FireballProjectile.Spawn(_fireballPrefab, Vector3.zero,
                new Vector3(0.0f, 0.0f, 20.0f), casterStrength: 0, ignoreColliders: null);
            GameObject fireballObject = fireball.gameObject;

            yield return WaitUntilDestroyedOrTimeout(fireballObject, 2.0f);

            Assert.IsTrue(fireballObject == null, "the fireball should have destroyed itself on impact");
            Assert.Greater(damageable.LastDamage, 0.0f, "a hit on a damageable target should have applied real damage");
            Assert.AreEqual(CombatTuning.DamageFor(FireballProjectile.BaseDamage, 0), damageable.LastDamage,
                "damage should run through the same strength-scaling chokepoint as every other hit");

            Object.DestroyImmediate(targetObject);
        }

        [UnityTest]
        public IEnumerator HittingPlainTerrainDestroysItselfWithoutApplyingDamage()
        {
            var terrainObject = new GameObject("Wall");
            terrainObject.transform.position = new Vector3(0.0f, 0.0f, 2.0f);
            var terrainCollider = terrainObject.AddComponent<BoxCollider>();
            terrainCollider.size = Vector3.one;

            FireballProjectile fireball = FireballProjectile.Spawn(_fireballPrefab, Vector3.zero,
                new Vector3(0.0f, 0.0f, 20.0f), casterStrength: 0, ignoreColliders: null);
            GameObject fireballObject = fireball.gameObject;

            yield return WaitUntilDestroyedOrTimeout(fireballObject, 2.0f);

            Assert.IsTrue(fireballObject == null, "the fireball should have destroyed itself on impact with terrain too");

            Object.DestroyImmediate(terrainObject);
        }

        [UnityTest]
        public IEnumerator StrengthScalesTheDamageUpward()
        {
            var targetObject = new GameObject("Dummy");
            targetObject.transform.position = new Vector3(0.0f, 0.0f, 2.0f);
            var targetCollider = targetObject.AddComponent<BoxCollider>();
            targetCollider.size = Vector3.one;
            var damageable = targetObject.AddComponent<TestDamageable>();

            FireballProjectile fireball = FireballProjectile.Spawn(_fireballPrefab, Vector3.zero,
                new Vector3(0.0f, 0.0f, 20.0f), casterStrength: 50, ignoreColliders: null);

            yield return WaitUntilDestroyedOrTimeout(fireball.gameObject, 2.0f);

            Assert.AreEqual(FireballProjectile.BaseDamage * 2.0f, damageable.LastDamage, 0.05f,
                "50 Strength at 2% a point should double the damage");

            Object.DestroyImmediate(targetObject);
        }
    }
}
