using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Healing Aura on real bodies (docs/HEALING.md): cast, a green circle lies on the ground round the caster and follows them; it slowly
    /// heals the caster and a player standing in it, not one outside; it ends after its time and waits out its cooldown; another player's copy
    /// shows the circle and heals nobody.
    /// </summary>
    public sealed class HealingAuraPlayTests
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
            foreach (HealingAuraCircle circle in Object.FindObjectsByType<HealingAuraCircle>(FindObjectsSortMode.None)) Object.Destroy(circle.gameObject);
        }

        /// <summary>A body at <paramref name="at"/>; a player's (its input reader on: no keyboard here, so it does nothing) when <paramref name="player"/>.</summary>
        private GameObject Body(Vector3 at, bool player = true)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = player;
            if (body.GetComponent<HealingAuraAbility>() == null)
            {
                body.AddComponent<HealingAuraAbility>();
            }
            return body;
#else
            return null;
#endif
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CastTheCircleLiesOnTheGroundRoundTheCasterAndGoesWithThem()
        {
            GameObject caster = Body(Vector3.zero, player: false);
            var aura = caster.GetComponent<HealingAuraAbility>();
            yield return null;
            Assert.IsTrue(aura.TryCast(), "the aura did not cast");
            yield return Wait(0.6f);
            Assert.IsNotNull(aura.Circle, "no circle");
            Assert.IsTrue(aura.Circle.IsShowing);
            Assert.Greater(aura.Circle.Strength, 0.9f, "the circle did not grow in");
            Assert.AreEqual(0.0f, aura.Circle.transform.position.y, 0.1f, "the circle is not on the ground");
            var controller = caster.GetComponent<CharacterController>();
            controller.enabled = false;
            caster.transform.position = new Vector3(3.0f, 0.0f, 2.0f);
            controller.enabled = true;
            yield return null;
            yield return null;
            Vector3 flat = aura.Circle.transform.position - caster.transform.position;
            flat.y = 0.0f;
            Assert.Less(flat.magnitude, 0.05f, "the circle did not go with the caster");
        }

        [UnityTest]
        public IEnumerator ItSlowlyHealsTheCasterAndAPlayerInItButNotOneOutside()
        {
            GameObject caster = Body(Vector3.zero, player: false);
            GameObject friend = Body(new Vector3(2.5f, 0.0f, 0.0f));
            GameObject outsider = Body(new Vector3(HealingAuraRules.Radius + 4.0f, 0.0f, 0.0f));
            foreach (GameObject body in new[] { caster, friend, outsider })
            {
                body.GetComponent<PlayerVitals>().TakeDamage(50.0f);
            }
            yield return null;
            float casterBefore = caster.GetComponent<PlayerVitals>().Health;
            float friendBefore = friend.GetComponent<PlayerVitals>().Health;
            float outsiderBefore = outsider.GetComponent<PlayerVitals>().Health;
            Assert.IsTrue(caster.GetComponent<HealingAuraAbility>().TryCast());
            yield return Wait(4.0f);
            // The outsider heals on its own (health comes back by itself after a while); what the aura gives is over and above that.
            float natural = outsider.GetComponent<PlayerVitals>().Health - outsiderBefore;
            float friendAura = friend.GetComponent<PlayerVitals>().Health - friendBefore - natural;
            float casterAura = caster.GetComponent<PlayerVitals>().Health - casterBefore - natural;
            float expected = HealingAuraRules.HealPerSecond * 4.0f;
            Assert.AreEqual(expected, friendAura, HealingAuraRules.HealPerTick * 1.5f, "the player in the circle was not healed about 5 a second");
            Assert.AreEqual(expected, casterAura, HealingAuraRules.HealPerTick * 1.5f, "the caster was not healed about 5 a second");
        }

        [UnityTest]
        public IEnumerator ItEndsAfterItsTimeAndWaitsOutItsCooldown()
        {
            GameObject caster = Body(Vector3.zero, player: false);
            var aura = caster.GetComponent<HealingAuraAbility>();
            aura.enabled = false;   // stepped by hand
            yield return null;
            Assert.IsTrue(aura.TryCast());
            Assert.IsFalse(aura.TryCast(), "cast again while it was on");
            aura.Tick(HealingAuraRules.DurationSeconds + 0.1f);
            Assert.IsFalse(aura.IsActive, "the aura outlasted its time");
            Assert.IsFalse(aura.TryCast(), "cast again before its cooldown was out");
            aura.Tick(HealingAuraRules.CooldownSeconds);
            Assert.IsTrue(aura.TryCast(), "the cooldown never ran out");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheCircleAndHealsNobody()
        {
            GameObject copy = Body(Vector3.zero, player: false);
            GameObject friend = Body(new Vector3(2.0f, 0.0f, 0.0f));
            var friendVitals = friend.GetComponent<PlayerVitals>();
            friendVitals.TakeDamage(50.0f);
            GameObject outsider = Body(new Vector3(HealingAuraRules.Radius + 4.0f, 0.0f, 0.0f));
            outsider.GetComponent<PlayerVitals>().TakeDamage(50.0f);
            var aura = copy.GetComponent<HealingAuraAbility>();
            yield return null;
            float friendBefore = friendVitals.Health;
            float outsiderBefore = outsider.GetComponent<PlayerVitals>().Health;
            aura.PlayRemote(new AbilityEvent(HealingAuraAbility.Started, copy.transform.position, Vector3.zero, 5.0f));
            yield return Wait(1.0f);
            Assert.IsTrue(aura.Circle != null && aura.Circle.IsShowing, "the copy shows no circle");
            float natural = outsider.GetComponent<PlayerVitals>().Health - outsiderBefore;
            Assert.AreEqual(natural, friendVitals.Health - friendBefore, 0.01f, "a copy's aura healed someone: only the caster's machine heals");
            aura.PlayRemote(new AbilityEvent(HealingAuraAbility.Ended, copy.transform.position, Vector3.zero));
            yield return Wait(1.0f);
            Assert.IsFalse(aura.Circle.IsShowing, "the copy's circle did not fade with its owner's");
        }
    }
}
