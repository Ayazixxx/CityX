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

        [Test]
        public void ACastPaysManaAndWithoutEnoughNoneLeaves()
        {
            // Owner, 2026-10-30: "make fireball cost some mana that was unintended".
            float full = _vitals.Mana;
            Assert.IsTrue(_fireball.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(full - FireballCaster.SpellManaCost, _vitals.Mana, 1e-3f);

            _fireball.Tick(_fireball.CooldownSeconds + 0.01f);
            Assert.IsTrue(_vitals.TrySpendMana(_vitals.Mana - FireballCaster.SpellManaCost * 0.5f));
            Assert.IsFalse(_fireball.TryCast(Vector3.zero, Vector3.forward, null), "a fireball cast with half its mana");
            Assert.AreEqual(FireballCaster.SpellManaCost * 0.5f, _vitals.Mana, 1e-3f, "a refused cast took mana");
            Assert.IsTrue(_fireball.CanCast, "a refused cast started the cooldown");
        }

        [Test]
        public void AFireballGlowsInTheDark()
        {
            // Owner, 2026-10-10: "make the fireball have a light effect so it glows in the dark". The light is on the prefab every machine spawns.
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            Assert.IsNotNull(prefab);
            Light glow = prefab.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(glow, "a fireball with no light of its own");
            Assert.AreEqual(LightType.Point, glow.type);
            Assert.IsTrue(glow.enabled);
            Assert.AreEqual(LightShadows.None, glow.shadows, "a moving point light's shadow costs six renders a frame");
            Assert.GreaterOrEqual(glow.range, 8.0f, "too small a glow to light the street it flies over");
            Assert.Greater(glow.color.r, glow.color.b * 2.0f, "fire is warm");
        }

        [Test]
        public void AFireballsCoreIsMoltenFireThatCastsNoShadowAndItsWakeIsTheFiresOwn()
        {
            // docs/FIRE.md, "The fire spells": the core's seams heat as it flies; the wake, the embers and the smoke are the shared fire's.
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            Assert.IsNotNull(prefab);
            var fireball = prefab.GetComponent<FireballProjectile>();
            Assert.IsNotNull(fireball.Core, "no molten core");
            Assert.AreEqual("CityX/FireballCore", fireball.Core.sharedMaterial.shader.name);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, fireball.Core.shadowCastingMode, "a ball of fire casts a shadow");
            Assert.IsNull(prefab.GetComponentInChildren<ParticleSystem>(true), "the fireball still carries particles of its own");
            Assert.IsNotNull(fireball.BurstPrefab, "no burst where it lands");
            Assert.IsNull(fireball.BurstPrefab.GetComponentInChildren<ParticleSystem>(true), "the burst still carries particles of its own");
            Assert.IsNotNull(fireball.BurstPrefab.GetComponentInChildren<Light>(true), "the burst has no flash");
        }
    }
}
