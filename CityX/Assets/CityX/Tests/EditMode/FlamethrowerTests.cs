using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The flamethrower's own continuous stream damage -- driven directly
    /// through the real component the same way JetpackTests drives
    /// Jetpack.Tick, since Physics.OverlapSphere resolves against real
    /// collider positions without needing a played frame (the same reason
    /// MeleeAttacker's own OverlapSphere-based ContactsInReach is testable
    /// in EditMode too).
    /// </summary>
    public sealed class FlamethrowerTests
    {
        private sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive { get; set; } = true;
            public float TotalDamage { get; private set; }
            public int HitCount { get; private set; }

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                TotalDamage += amount;
                HitCount++;
                return HitResponse.Landed;
            }
        }

        private GameObject _flamethrowerObject;
        private Flamethrower _flamethrower;
        private GameObject _wielder;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _flamethrowerObject = new GameObject("Flamethrower");
            _flamethrower = _flamethrowerObject.AddComponent<Flamethrower>();

            _wielder = new GameObject("Wielder");
            _vitals = _wielder.AddComponent<PlayerVitals>();
            _vitals.Revive();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_flamethrowerObject);
            Object.DestroyImmediate(_wielder);
        }

        private static GameObject MakeDummy(Vector3 position, out TestDamageable damageable)
        {
            var dummy = new GameObject("Dummy");
            dummy.transform.position = position;
            var collider = dummy.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
            damageable = dummy.AddComponent<TestDamageable>();
            return dummy;
        }

        [Test]
        public void ATargetDirectlyInFrontTakesDamageWhileTheTriggerIsHeld()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                Assert.Greater(damageable.TotalDamage, 0.0f, "a target in the stream should take damage");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void NoDamageAtAllWhileTheTriggerIsNotHeld()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(false, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                Assert.AreEqual(0.0f, damageable.TotalDamage, "an unfired flamethrower should burn nothing");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void ATargetBehindTheWielderIsOutsideTheConeAndTakesNoDamage()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, -3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                Assert.AreEqual(0.0f, damageable.TotalDamage, "a target behind the stream should never burn");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void ATargetFarBeyondRangeTakesNoDamage()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, Flamethrower.Range + 5.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                Assert.AreEqual(0.0f, damageable.TotalDamage, "a flamethrower's short stream should not reach this far");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void ADeadTargetTakesNoDamage()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            damageable.IsAlive = false;
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                Assert.AreEqual(0.0f, damageable.TotalDamage, "an already-dead target should not be burned again");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void TheWielderNeverBurnsItself()
        {
            var selfDamageable = _wielder.AddComponent<TestDamageable>();
            // The wielder's own collider sits right at the muzzle origin,
            // well inside both Range and the cone -- the self-exclusion
            // check is the only thing standing between this and a real hit.
            _wielder.AddComponent<SphereCollider>().radius = 0.3f;

            _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);

            Assert.AreEqual(0.0f, selfDamageable.TotalDamage, "the flamethrower burned its own wielder");
        }

        [Test]
        public void OneBodyWithSeveralCollidersIsOnlyBurnedOncePerTick()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                // A second collider on the same body, further from the
                // muzzle but still within range/cone -- the real-world
                // shape this guards is a ragdoll with a collider per bone.
                var secondCollider = new GameObject("SecondHitbox");
                secondCollider.transform.SetParent(dummy.transform, false);
                secondCollider.transform.localPosition = new Vector3(0.0f, 0.3f, 0.0f);
                secondCollider.AddComponent<SphereCollider>().radius = 0.3f;

                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);

                Assert.AreEqual(1, damageable.HitCount, "one body with two colliders should only be hit once per tick");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void DamageScalesWithDeltaTime()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f);
                Assert.AreEqual(CombatTuning.DamageFor(Flamethrower.DamagePerSecond, 0), damageable.TotalDamage, 0.01f,
                    "a full one-second tick should deal exactly DamagePerSecond, strength-scaled");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void FirearmDamageScalesTheDamageUpward()
        {
            // The flamethrower is a gun of a kind: Firearm Damage, not Melee Damage (the old Strength), scales it since 2026-10-14.
            _vitals.Stats[StatId.FirearmDamage] = 50;
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f);
                Assert.AreEqual(Flamethrower.DamagePerSecond * 2.0f, damageable.TotalDamage, 0.05f,
                    "50 points of Firearm Damage at 2% a point should double the damage, same chokepoint every other hit uses");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void AZeroDirectionAppliesNoDamage()
        {
            GameObject dummy = MakeDummy(new Vector3(0.0f, 0.0f, 3.0f), out TestDamageable damageable);
            try
            {
                _flamethrower.Tick(true, Vector3.zero, Vector3.zero, _wielder, 1.0f / 60.0f);
                Assert.AreEqual(0.0f, damageable.TotalDamage, "no aim direction means no stream to burn anything with");
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void IsFiringReflectsTheTriggerState()
        {
            _flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
            Assert.IsTrue(_flamethrower.IsFiring);

            _flamethrower.Tick(false, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
            Assert.IsFalse(_flamethrower.IsFiring);
        }

        [Test]
        public void TryPickupRefusesWithoutEPressed()
        {
            Assert.IsFalse(_flamethrower.TryPickup(_wielder.transform, ePressed: false));
        }

        [Test]
        public void TryPickupRefusesTooFarAway()
        {
            _wielder.transform.position = new Vector3(0.0f, 0.0f, Flamethrower.InteractRange + 5.0f);
            Assert.IsFalse(_flamethrower.TryPickup(_wielder.transform, ePressed: true));
        }

        // A SUCCESSFUL TryPickup destroys the flamethrower's own GameObject,
        // and Destroy() defers to end of frame -- see FlamethrowerPlayTests
        // (PlayMode) for that case, matching PistolWeaponPlayTests' own
        // documented reason for why the same test isn't here.
    }
}
