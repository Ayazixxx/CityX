using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Fire Enhancement's numbers and its one shot of fire, with no scene (docs/FIRE_ENHANCEMENT.md).</summary>
    public sealed class FireEnhancementRulesTests
    {
        /// <summary>A staff in hand, for the enhancement to leave alone.</summary>
        private sealed class FakeStaff : MonoBehaviour, IEquippableStaff
        {
            public void Tick(bool triggerHeld, Vector3 eye, Vector3 look, GameObject wielder, float deltaTime)
            {
            }

            public void CancelCharge(GameObject wielder)
            {
            }
        }

        [Test]
        public void ABurningBlowBurnsAtLeastAsLongAsSteppingOutOfAFire()
        {
            Assert.GreaterOrEqual(FireEnhancementRules.BurnSeconds, FireRules.AfterburnSeconds);
            Assert.Greater(FireEnhancementRules.FireDamage, 0.0f);
            Assert.Less(FireEnhancementRules.CooldownSeconds, FireEnhancementRules.ArmedSeconds, "it would go out before it could be cast again");
            Assert.Less(FireEnhancementRules.ManaCost, PlayerVitals.BaseMaxMana);
        }

        [Test]
        public void OneShotTakesTheFireAndTheNextDoesNot()
        {
            var body = new GameObject("Body");
            try
            {
                var fire = body.AddComponent<FireEnhancementAbility>();
                fire.Bind();
                Assert.IsFalse(FireEnhancementAbility.TakeShot(body), "a shot took fire that was never cast");
                Assert.IsTrue(fire.TryCast());
                Assert.IsTrue(fire.IsArmed);
                Assert.IsTrue(FireEnhancementAbility.TakeShot(body), "the shot did not take the fire");
                Assert.IsFalse(fire.IsArmed);
                Assert.IsFalse(FireEnhancementAbility.TakeShot(body), "the next shot took fire too");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void AStaffInHandNeverTakesIt()
        {
            var body = new GameObject("Body");
            try
            {
                var fire = body.AddComponent<FireEnhancementAbility>();
                fire.Bind();
                var staff = new GameObject("Staff");
                staff.transform.SetParent(body.transform, false);
                staff.AddComponent<FakeStaff>();
                Assert.IsTrue(fire.TryCast());
                Assert.IsFalse(FireEnhancementAbility.TakeShot(body), "the staff took the fire");
                Assert.IsTrue(fire.IsArmed, "the fire went to the staff");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ItGoesOutWhenItsTimeIsUp()
        {
            var body = new GameObject("Body");
            try
            {
                var fire = body.AddComponent<FireEnhancementAbility>();
                fire.Bind();
                int outs = 0;
                fire.Fired += evt => outs += evt.Kind == FireEnhancementAbility.Out ? 1 : 0;
                Assert.IsTrue(fire.TryCast());
                fire.Tick(FireEnhancementRules.ArmedSeconds + 0.1f);
                Assert.IsFalse(fire.IsArmed, "it never went out");
                Assert.AreEqual(1, outs, "other players were not told it went out");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
