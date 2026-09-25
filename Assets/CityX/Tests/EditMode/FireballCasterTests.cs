using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The cooldown/cast-pose bookkeeping around a fireball cast -- driven
    /// directly, the same way JetpackTests drives Jetpack.Tick, since the
    /// actual projectile flight/impact needs real physics frames and is
    /// covered separately by FireballProjectilePlayTests.
    /// </summary>
    public sealed class FireballCasterTests
    {
        private GameObject _caster;
        private FireballCaster _fireball;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _caster = new GameObject("Caster");
            _fireball = _caster.AddComponent<FireballCaster>();
            _vitals = _caster.AddComponent<PlayerVitals>();
            _vitals.Revive();
            // EditMode never calls Awake, and the component caches its own
            // PlayerVitals reference there -- doing it by hand is what
            // makes this testable at all (see FireballCaster.Bind's own
            // doc comment).
            _fireball.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_caster);
        }

        [Test]
        public void AFreshCasterCanCastImmediately()
        {
            Assert.IsTrue(_fireball.CanCast);
        }

        [Test]
        public void CastingStartsTheCooldown()
        {
            Assert.IsTrue(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.IsFalse(_fireball.CanCast, "casting should start a cooldown");
        }

        [Test]
        public void CannotCastAgainBeforeTheCooldownElapses()
        {
            Assert.IsTrue(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
            _fireball.Tick(_fireball.CooldownSeconds * 0.5f);

            Assert.IsFalse(_fireball.TryCast(Vector3.zero, Vector3.forward, null),
                "cast again before the cooldown ran out");
        }

        [Test]
        public void CanCastAgainOnceTheCooldownElapses()
        {
            Assert.IsTrue(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
            _fireball.Tick(_fireball.CooldownSeconds + 0.01f);

            Assert.IsTrue(_fireball.CanCast);
            Assert.IsTrue(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
        }

        [Test]
        public void CastingShowsTheAimPoseForACosmeticWindowThenStops()
        {
            Assert.IsFalse(_fireball.IsCasting, "should not be casting before anything happened");

            _fireball.TryCast(Vector3.zero, Vector3.forward, null);
            Assert.IsTrue(_fireball.IsCasting);

            _fireball.Tick(_fireball.CastPoseSeconds + 0.01f);
            Assert.IsFalse(_fireball.IsCasting, "the aim pose never ended");
        }

        [Test]
        public void CastFractionStartsAtOneAndEasesToZeroWithTheAimPose()
        {
            Assert.AreEqual(0.0f, _fireball.CastFraction, "nothing has been cast yet");

            _fireball.TryCast(Vector3.zero, Vector3.forward, null);
            Assert.AreEqual(1.0f, _fireball.CastFraction, "just cast -- the raise should be at its peak");

            _fireball.Tick(_fireball.CastPoseSeconds * 0.5f);
            Assert.AreEqual(0.5f, _fireball.CastFraction, 0.001f, "halfway through the cosmetic window");

            _fireball.Tick(_fireball.CastPoseSeconds * 0.5f + 0.01f);
            Assert.AreEqual(0.0f, _fireball.CastFraction, "the cosmetic window ended");
        }

        [Test]
        public void AZeroDirectionRefusesToCast()
        {
            Assert.IsFalse(_fireball.TryCast(Vector3.zero, Vector3.zero, null),
                "casting with no direction at all should be refused rather than launching straight down");
        }

        [Test]
        public void ADeadCasterCannotCastEvenOffCooldown()
        {
            _vitals.TakeDamage(10000.0f);
            Assert.IsTrue(_vitals.IsDead);

            Assert.IsFalse(_fireball.CanCast);
            Assert.IsFalse(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
        }
    }
}
