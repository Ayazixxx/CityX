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
    /// A kill pays whoever struck it, whatever it was struck with. Each weapon used to pay for itself, and most spells had never been told
    /// to: a kill by a speck, a boomerang, a sword wave or acid rain paid nothing.
    /// </summary>
    public sealed class KillsPayPlayTests
    {
        private const int Reward = 37;

        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Mortal : MonoBehaviour, IDamageable, IXpReward
        {
            public bool IsAlive { get; private set; } = true;

            public int XpReward => Reward;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                if (!IsAlive)
                {
                    return HitResponse.Ignored;
                }
                IsAlive = false;
                return HitResponse.Landed;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private PlayerVitals Killer()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _made.Add(ground);
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            return body.GetComponent<PlayerVitals>();
#else
            return null;
#endif
        }

        private Mortal MortalAt(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            _made.Add(go);
            return go.AddComponent<Mortal>();
        }

        [UnityTest]
        public IEnumerator AKillPaysWhoeverStruckItOnceAndNoMoreForTheCorpse()
        {
            PlayerVitals killer = Killer();
            Mortal mortal = MortalAt(new Vector3(5.0f, 1.0f, 0.0f));
            yield return null;
            int before = killer.Experience;
            DamageContext.Strike(mortal, 10.0f, Vector3.zero, 0.0f, DamageKind.Magic, 0.0f, null, killer.gameObject);
            Assert.AreEqual(before + Reward, killer.Experience, "a kill paid nothing to whoever struck it");
            DamageContext.Strike(mortal, 10.0f, Vector3.zero, 0.0f, DamageKind.Magic, 0.0f, null, killer.gameObject);
            Assert.AreEqual(before + Reward, killer.Experience, "a blow on the corpse was paid for again");
        }

        [UnityTest]
        public IEnumerator ASpeckFromAWandPaysItsCaster()
        {
            PlayerVitals caster = Killer();
            Vector3 from = new Vector3(0.0f, 1.5f, 4.0f);
            Mortal mortal = MortalAt(from + Vector3.forward * 3.0f);
            yield return null;
            Physics.SyncTransforms();
            int before = caster.Experience;
            MagicSpeck.Spawn(from, Vector3.forward * 20.0f, 0.3f, 10.0f, 0.0f, caster.gameObject, new Collider[0]);
            for (int i = 0; i < 120 && mortal.IsAlive; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.IsFalse(mortal.IsAlive, "setup: the speck never reached it");
            Assert.AreEqual(before + Reward, caster.Experience, "a kill by a speck paid its caster nothing");
        }

        /// <summary>The weapons that paid for themselves are paid once, not twice, now the blow itself pays.</summary>
        [UnityTest]
        public IEnumerator AFistsKillIsPaidOnce()
        {
            PlayerVitals killer = Killer();
            var melee = killer.GetComponent<MeleeAttacker>();
            Assert.IsNotNull(melee, "setup: the body has no fists");
            Mortal mortal = MortalAt(new Vector3(0.0f, 1.0f, 1.3f));
            yield return null;
            Physics.SyncTransforms();
            int before = killer.Experience;
            Assert.IsTrue(melee.TryAttack(heavy: false), "setup: no punch was thrown");
            for (float t = 0.0f; t < 1.2f && mortal.IsAlive; t += 1.0f / 60.0f)
            {
                melee.Tick(1.0f / 60.0f);
            }
            Assert.IsFalse(mortal.IsAlive, "setup: the punch never reached it");
            Assert.AreEqual(before + Reward, killer.Experience, "a kill by a punch was paid wrongly");
        }
    }
}
