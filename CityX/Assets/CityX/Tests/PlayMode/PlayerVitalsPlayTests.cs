using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Health, stamina and fall damage.
    ///
    /// Deliberately NOT [UnityTest] coroutines: every rule here is a function
    /// of a delta, so it can be stepped by hand at whatever timestep the case
    /// needs. Waiting real seconds for stamina to refill would make the suite
    /// slow AND flaky, and would prove less.
    /// </summary>
    public class PlayerVitalsPlayTests
    {
        private GameObject _object;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Vitals");
            _vitals = _object.AddComponent<PlayerVitals>();
            // Awake has not run for a bare AddComponent outside play, so put it
            // into the state it would start a life in.
            _vitals.Revive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
            {
                Object.DestroyImmediate(_object);
            }
        }

        /// <summary>Steps stamina for a number of simulated seconds.</summary>
        private void RunStamina(bool sprinting, float seconds, float step = 1.0f / 60.0f)
        {
            for (float t = 0.0f; t < seconds; t += step)
            {
                _vitals.TickStamina(sprinting, step);
            }
        }

        [Test]
        public void StartsAtFullHealthAndStamina()
        {
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, _vitals.Health, 0.001f);
            Assert.AreEqual(PlayerVitals.BaseMaxStamina, _vitals.Stamina, 0.001f);
            Assert.IsFalse(_vitals.IsDead);
        }

        [Test]
        public void StatPointsRaiseTheCeilings()
        {
            _vitals.Vitality = 5;
            _vitals.Endurance = 3;
            _vitals.Revive();

            Assert.AreEqual(PlayerVitals.BaseMaxHealth + 50.0f, _vitals.MaxHealth, 0.001f);
            Assert.AreEqual(PlayerVitals.BaseMaxStamina + 30.0f, _vitals.MaxStamina, 0.001f);
            Assert.AreEqual(_vitals.MaxHealth, _vitals.Health, 0.001f,
                "reviving should fill to the NEW ceiling, not the base one");
        }

        [Test]
        public void SprintingDrainsStaminaAtTheStatedRate()
        {
            RunStamina(sprinting: true, seconds: 1.0f);
            Assert.AreEqual(PlayerVitals.BaseMaxStamina - PlayerVitals.StaminaDrainPerSecond,
                _vitals.Stamina, 0.5f);
        }

        [Test]
        public void StandingStillRefillsStaminaButNeverPastTheCeiling()
        {
            RunStamina(sprinting: true, seconds: 3.0f);
            Assert.Less(_vitals.Stamina, PlayerVitals.BaseMaxStamina);

            RunStamina(sprinting: false, seconds: 60.0f);
            Assert.AreEqual(_vitals.MaxStamina, _vitals.Stamina, 0.001f,
                "stamina overflowed its ceiling");
        }

        /// <summary>
        /// The exhaustion latch. Without it, sprint flickers on and off one
        /// frame at a time at zero stamina -- you get a frame of stamina, spend
        /// it, and repeat, which reads as the controls breaking.
        /// </summary>
        [Test]
        public void RunningOutOfStaminaLocksSprintUntilItHasPartlyRecovered()
        {
            // Drain until it bottoms out, and stop THERE. Holding sprint past
            // that point does not sit at zero -- the latch lets stamina climb
            // back to the recovery fraction and grants another burst, which is
            // the whole purpose of the latch. Sprinting for a flat 20 seconds
            // just leaves it oscillating around 20%.
            const float step = 1.0f / 60.0f;
            for (int i = 0; i < 10000 && !_vitals.IsExhausted; i++)
            {
                _vitals.TickStamina(true, step);
            }

            Assert.AreEqual(0.0f, _vitals.Stamina, 0.001f, "should have bottomed out");
            Assert.IsTrue(_vitals.IsExhausted);

            Assert.IsFalse(_vitals.TickStamina(true, 1.0f / 60.0f),
                "sprint was allowed again the instant a scrap of stamina appeared");

            // Just under the recovery threshold: still locked.
            float threshold = _vitals.MaxStamina * PlayerVitals.ExhaustedRecoverFraction;
            while (_vitals.Stamina < threshold - 1.0f)
            {
                _vitals.TickStamina(false, 1.0f / 60.0f);
            }
            Assert.IsTrue(_vitals.IsExhausted, "unlocked before reaching the recovery fraction");

            RunStamina(sprinting: false, seconds: 1.0f);
            Assert.IsFalse(_vitals.IsExhausted, "never unlocked despite recovering past the fraction");
            Assert.IsTrue(_vitals.TickStamina(true, 1.0f / 60.0f), "sprint still refused after recovery");
        }

        [Test]
        public void DamageReducesHealthAndDeathFiresOnceAtZero()
        {
            int deaths = 0;
            _vitals.Died += () => deaths++;

            _vitals.TakeDamage(30.0f);
            Assert.AreEqual(70.0f, _vitals.Health, 0.001f);
            Assert.AreEqual(0, deaths);

            _vitals.TakeDamage(1000.0f);
            Assert.AreEqual(0.0f, _vitals.Health, 0.001f, "health went negative");
            Assert.IsTrue(_vitals.IsDead);
            Assert.AreEqual(1, deaths);

            // Hitting a corpse must not fire death again -- anything listening
            // would run its death handling twice.
            _vitals.TakeDamage(50.0f);
            Assert.AreEqual(1, deaths, "death fired more than once");
        }

        [Test]
        public void BlockingLetsAQuarterOfTheDamageThrough()
        {
            _vitals.TakeDamage(40.0f, blocking: true);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - 10.0f, _vitals.Health, 0.001f,
                "blocking should absorb 75%, not all of it");
        }

        [Test]
        public void AShortDropDoesNoDamage()
        {
            _vitals.TickFallDamage(grounded: false, currentY: 3.0f);
            _vitals.TickFallDamage(grounded: false, currentY: 2.0f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 3.0f - 1.0f);

            Assert.AreEqual(0.0f, damage, 0.001f);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, _vitals.Health, 0.001f);
        }

        [Test]
        public void ALongDropHurtsInProportionToTheDistancePastSafe()
        {
            const float startY = 40.0f;
            _vitals.TickFallDamage(grounded: false, currentY: startY);
            _vitals.TickFallDamage(grounded: false, currentY: 20.0f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            float expected = (startY - PlayerVitals.SafeFallDistance)
                             * (PlayerVitals.BaseMaxHealth * PlayerVitals.FallDamagePerMetreFraction);
            Assert.AreEqual(expected, damage, 0.01f);
            Assert.Less(_vitals.Health, PlayerVitals.BaseMaxHealth);
        }

        /// <summary>
        /// Fall damage tracks the PEAK, not the landing speed. A body that rises
        /// after leaving the ground -- a jump, a bridge, a lift -- must be
        /// measured from the top of its arc, not from where it took off.
        /// </summary>
        [Test]
        public void FallDamageMeasuresFromTheTopOfTheArcNotTheTakeoff()
        {
            _vitals.TickFallDamage(grounded: false, currentY: 0.0f);
            _vitals.TickFallDamage(grounded: false, currentY: 30.0f);   // rose
            _vitals.TickFallDamage(grounded: false, currentY: 15.0f);
            float damage = _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            float expected = (30.0f - PlayerVitals.SafeFallDistance)
                             * (PlayerVitals.BaseMaxHealth * PlayerVitals.FallDamagePerMetreFraction);
            Assert.AreEqual(expected, damage, 0.01f,
                "measured from takeoff instead of from the apex");
        }

        [Test]
        public void StayingOnTheGroundNeverAccruesFallDamage()
        {
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0.0f, _vitals.TickFallDamage(grounded: true, currentY: 0.0f), 0.001f);
            }
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, _vitals.Health, 0.001f);
        }

        /// <summary>
        /// A fall from a mid-rise roof has to be lethal, or the whole vertical
        /// city carries no risk and the climbing has no weight.
        /// </summary>
        [Test]
        public void FallingOffAMidRiseIsFatal()
        {
            const float roof = 40.0f;
            _vitals.TickFallDamage(grounded: false, currentY: roof);
            _vitals.TickFallDamage(grounded: true, currentY: 0.0f);

            Assert.IsTrue(_vitals.IsDead,
                $"survived a {roof}m fall with {_vitals.Health:0} health left");
        }

        [Test]
        public void SpendingStaminaFailsWhenThereIsNotEnough()
        {
            Assert.IsTrue(_vitals.TrySpendStamina(30.0f));
            Assert.AreEqual(PlayerVitals.BaseMaxStamina - 30.0f, _vitals.Stamina, 0.001f);

            Assert.IsFalse(_vitals.TrySpendStamina(10000.0f), "spent stamina it did not have");
            Assert.AreEqual(PlayerVitals.BaseMaxStamina - 30.0f, _vitals.Stamina, 0.001f,
                "a failed spend still took stamina");
        }

        [Test]
        public void RevivingClearsDeathAndExhaustion()
        {
            _vitals.TakeDamage(1000.0f);
            RunStamina(sprinting: true, seconds: 20.0f);
            Assert.IsTrue(_vitals.IsDead);
            Assert.IsTrue(_vitals.IsExhausted);

            _vitals.Revive();
            Assert.IsFalse(_vitals.IsDead);
            Assert.IsFalse(_vitals.IsExhausted);
            Assert.AreEqual(_vitals.MaxHealth, _vitals.Health, 0.001f);
            Assert.AreEqual(_vitals.MaxStamina, _vitals.Stamina, 0.001f);
        }
    }
}
