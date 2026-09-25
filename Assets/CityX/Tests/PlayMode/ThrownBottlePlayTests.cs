using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A thrown bottle's impact, which only ever fires from a real physics
    /// collision (OnCollisionEnter) -- needs actual frames, not an EditMode
    /// test.
    /// </summary>
    public sealed class ThrownBottlePlayTests
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

        private GameObject _bottlePrefab;

        [SetUp]
        public void SetUp()
        {
            _bottlePrefab = new GameObject("ThrownBottlePrefab");
            _bottlePrefab.AddComponent<Rigidbody>();
            var collider = _bottlePrefab.AddComponent<SphereCollider>();
            collider.radius = 0.05f;
            _bottlePrefab.AddComponent<ThrownBottle>();
            // ACTIVE, NOT DEACTIVATED: Instantiate() copies the source's
            // own active state onto the clone, and an inactive clone never
            // fires OnCollisionEnter at all. Parked far below the test area
            // instead, so this template object itself can't physically
            // interfere with what it spawns.
            _bottlePrefab.transform.position = new Vector3(0.0f, -500.0f, 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bottlePrefab);
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

            ThrownBottle thrown = ThrownBottle.Spawn(_bottlePrefab, Vector3.zero,
                new Vector3(0.0f, 0.0f, 16.0f), throwerStrength: 0, ignoreColliders: null);
            GameObject thrownObject = thrown.gameObject;

            yield return WaitUntilDestroyedOrTimeout(thrownObject, 2.0f);

            Assert.IsTrue(thrownObject == null, "the thrown bottle should have destroyed itself on impact");
            Assert.Greater(damageable.LastDamage, 0.0f, "a hit on a damageable target should have applied real damage");
            Assert.AreEqual(CombatTuning.DamageFor(ThrownBottle.ThrowDamage, 0), damageable.LastDamage,
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

            ThrownBottle thrown = ThrownBottle.Spawn(_bottlePrefab, Vector3.zero,
                new Vector3(0.0f, 0.0f, 16.0f), throwerStrength: 0, ignoreColliders: null);
            GameObject thrownObject = thrown.gameObject;

            yield return WaitUntilDestroyedOrTimeout(thrownObject, 2.0f);

            Assert.IsTrue(thrownObject == null, "the thrown bottle should have destroyed itself on impact with terrain too");

            Object.DestroyImmediate(terrainObject);
        }
    }
}
