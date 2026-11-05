using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The poison gas: a cloud that poisons whatever is in it for ten seconds (3 health a second, 30 in all, in half-second ticks), tops it up rather
    /// than stacking, never poisons whoever blew it, stops at walls, thins away; a cosmetic copy (another player's machine) poisons nothing; a player takes
    /// the poison quietly (no flinch). Stepped by hand (Tick) so ten seconds of game time takes no time.
    /// </summary>
    public sealed class PoisonGasPlayTests
    {
        private const string PrefabPath = "Assets/CityX/Weapons/PoisonGasCloud.prefab";
        private const float Step = 1.0f / 30.0f;
        private GameObject _prefab;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(_prefab, "the poison gas prefab is missing -- run CityX/Rebuild Poison Gas");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            foreach (PoisonGasCloud c in Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
        }

        private SparringDummy Dummy(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;
            _made.Add(go);
            Physics.SyncTransforms();
            var dummy = go.AddComponent<SparringDummy>();
            dummy.MaxHealth = 1000.0f;
            dummy.Reset();
            return dummy;
        }

        private PoisonGasCloud Cloud(Vector3 at, bool cosmetic = false, Collider[] ignore = null)
        {
            PoisonGasCloud cloud = PoisonGasCloud.Spawn(_prefab, at, Vector3.zero, 0, ignore, cosmetic);
            Assert.IsNotNull(cloud);
            return cloud;
        }

        private static void Run(PoisonGasCloud cloud, float seconds)
        {
            for (float t = 0.0f; t < seconds && cloud != null; t += Step)
            {
                cloud.Tick(Step);
            }
        }

        private static void Run(PoisonEffect effect, float seconds)
        {
            for (float t = 0.0f; t < seconds && effect != null; t += Step)
            {
                effect.Tick(Step);
            }
        }

        [UnityTest]
        public IEnumerator ABodyInTheCloudIsPoisonedForTenSecondsAndLosesThirty()
        {
            SparringDummy victim = Dummy(new Vector3(0.5f, 1.0f, 0.0f));
            PoisonGasCloud cloud = Cloud(new Vector3(0.0f, 1.0f, 0.0f));
            Run(cloud, 0.5f);

            var poison = victim.GetComponent<PoisonEffect>();
            Assert.IsNotNull(poison, "standing in the cloud did not poison it");
            Assert.AreEqual(PoisonEffect.DefaultSeconds, poison.Seconds, 1e-4f);

            // It keeps the cloud's top-up going for as long as it stands there, so step the poison alone after taking it away.
            Object.Destroy(cloud.gameObject);
            yield return null;
            Run(poison, 20.0f);
            yield return null;
            Assert.AreEqual(PoisonEffect.DefaultDamagePerSecond * PoisonEffect.DefaultSeconds, victim.TotalDamageTaken, 0.01f,
                "ten seconds at 3 a second should be exactly 30");
            Assert.IsNull(victim.GetComponent<PoisonEffect>(), "the poison should end by itself");
        }

        [UnityTest]
        public IEnumerator PoisonLandsInHalfSecondTicksNotAllAtOnce()
        {
            SparringDummy victim = Dummy(new Vector3(0.0f, 1.0f, 0.0f));
            PoisonEffect poison = PoisonEffect.Apply(victim.gameObject, 3.0f, 10.0f);
            Assert.IsNotNull(poison);
            poison.Tick(0.2f);
            Assert.AreEqual(0.0f, victim.TotalDamageTaken, 1e-4f, "nothing before the first tick");
            poison.Tick(0.4f);   // 0.6 s: one tick
            Assert.AreEqual(1.5f, victim.TotalDamageTaken, 1e-4f);
            poison.Tick(0.4f);   // 1.0 s: two ticks
            Assert.AreEqual(3.0f, victim.TotalDamageTaken, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeingPoisonedAgainRestartsTheClockInsteadOfStacking()
        {
            SparringDummy victim = Dummy(new Vector3(0.0f, 1.0f, 0.0f));
            PoisonEffect first = PoisonEffect.Apply(victim.gameObject, 3.0f, 10.0f);
            first.Tick(6.0f);
            Assert.AreEqual(6.0f * 3.0f, victim.TotalDamageTaken, 0.01f);

            PoisonEffect second = PoisonEffect.Apply(victim.gameObject, 3.0f, 10.0f);
            Assert.AreSame(first, second, "a second dose made a second effect");
            Assert.AreEqual(10.0f, second.Remaining, 1e-3f, "the clock should have restarted");
            second.Tick(20.0f);
            yield return null;
            Assert.AreEqual(6.0f * 3.0f + 30.0f, victim.TotalDamageTaken, 0.01f, "the second dose is another ten seconds, not double speed");
        }

        /// <summary>
        /// The gas hurts while you stand in it. The cloud doses whoever is in it five times a second, and a dose that started the half-second
        /// tick over meant none ever landed until the body left the cloud.
        /// </summary>
        [UnityTest]
        public IEnumerator StandingInTheCloudHurtsFromTheStartNotOnlyAfterLeavingIt()
        {
            SparringDummy victim = Dummy(new Vector3(0.5f, 1.0f, 0.0f));
            PoisonGasCloud cloud = Cloud(new Vector3(0.0f, 1.0f, 0.0f));
            const float Stood = 3.0f;
            for (float t = 0.0f; t < Stood; t += Step)
            {
                cloud.Tick(Step);
                var poisoned = victim.GetComponent<PoisonEffect>();
                if (poisoned != null)
                {
                    poisoned.Tick(Step);
                }
            }
            float tick = PoisonEffect.DefaultDamagePerSecond * PoisonEffect.TickSeconds;
            Assert.AreEqual(PoisonEffect.DefaultDamagePerSecond * Stood, victim.TotalDamageTaken, tick * 1.5f,
                "three seconds in the gas should have cost three seconds of poison");

            // And it is still a full dose from the last breath: ten seconds more once out of the gas, no more and no less.
            var poison = victim.GetComponent<PoisonEffect>();
            Assert.AreEqual(PoisonEffect.DefaultSeconds, poison.Remaining, PoisonGasCloud.CheckSeconds + Step, "the dose is not kept topped up while it stands in the gas");
            float atLeaving = victim.TotalDamageTaken;
            float owed = poison.Remaining;
            Object.Destroy(cloud.gameObject);
            yield return null;
            Run(poison, 20.0f);
            yield return null;
            Assert.AreEqual(PoisonEffect.DefaultDamagePerSecond * owed, victim.TotalDamageTaken - atLeaving, tick * 1.5f, "out of the gas, what was left of the dose should run its course");
            Assert.IsNull(victim.GetComponent<PoisonEffect>(), "the poison should end by itself");
        }

        [UnityTest]
        public IEnumerator TheCloudNeverPoisonsWhoeverBlewIt()
        {
            SparringDummy caster = Dummy(new Vector3(0.0f, 1.0f, 0.0f));
            SparringDummy other = Dummy(new Vector3(1.0f, 1.0f, 0.0f));
            PoisonGasCloud cloud = Cloud(new Vector3(0.5f, 1.0f, 0.0f), false, caster.GetComponents<Collider>());
            Run(cloud, 1.0f);

            Assert.IsNull(caster.GetComponent<PoisonEffect>(), "the gas poisoned the one who blew it");
            Assert.IsNotNull(other.GetComponent<PoisonEffect>(), "but it should poison anyone else in it");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ACosmeticCloudPoisonsNothing()
        {
            SparringDummy victim = Dummy(new Vector3(0.5f, 1.0f, 0.0f));
            PoisonGasCloud cloud = Cloud(new Vector3(0.0f, 1.0f, 0.0f), cosmetic: true);
            Run(cloud, 3.0f);
            Assert.IsNull(victim.GetComponent<PoisonEffect>(), "another player's cloud, shown on this machine, poisoned something");
            Assert.AreEqual(0.0f, victim.TotalDamageTaken, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCloudSwellsHangsAndThinsAwayHarmless()
        {
            PoisonGasCloud cloud = Cloud(new Vector3(0.0f, 1.0f, 0.0f));
            Assert.AreEqual(PoisonGasCloud.StartRadius, cloud.Radius, 0.01f);
            Run(cloud, PoisonGasCloud.ExpandSeconds + 0.2f);
            Assert.AreEqual(PoisonGasCloud.MaxRadius, cloud.Radius, 0.1f, "full size after it has swollen");
            Assert.IsTrue(cloud.IsHarmful);

            Run(cloud, PoisonGasCloud.LifeSeconds - PoisonGasCloud.FadeSeconds - cloud.Age + 0.1f);
            Assert.IsFalse(cloud.IsHarmful, "in its last moments it is too thin to poison");

            Run(cloud, 2.5f);
            yield return null;
            Assert.IsTrue(cloud == null, "the cloud never went away");
        }

        [UnityTest]
        public IEnumerator ABlownCloudStopsAtAWallButNotAtAPerson()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 8.0f);
            wall.transform.localScale = new Vector3(20.0f, 6.0f, 0.5f);
            _made.Add(wall);
            Dummy(new Vector3(0.0f, 1.0f, 2.0f));
            Physics.SyncTransforms();

            PoisonGasCloud cloud = PoisonGasCloud.Spawn(_prefab, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 30.0f), 0, null);
            Run(cloud, 3.0f);
            Assert.Less(cloud.transform.position.z, 8.0f - 0.25f, "the cloud went through the wall");
            Assert.Greater(cloud.transform.position.z, 3.0f, "and it should have drifted past the person on its way");
            yield return null;
        }

        [UnityTest]
        public IEnumerator APlayerTakesPoisonQuietlyWithNoFlinchAndPoisonCanKill()
        {
            var go = new GameObject("Player");
            go.AddComponent<CapsuleCollider>();
            var vitals = go.AddComponent<PlayerVitals>();
            vitals.Revive();
            _made.Add(go);
            float before = vitals.Health;

            PoisonEffect poison = PoisonEffect.Apply(go, 3.0f, 10.0f);
            Assert.IsNotNull(poison);
            poison.Tick(1.0f);
            Assert.AreEqual(before - 3.0f, vitals.Health, 0.01f);
            Assert.AreEqual(0.0f, vitals.HitstunRemaining, 1e-4f, "poison made the player flinch");

            // Poison does not care about a block or invulnerability frames.
            vitals.GrantInvulnerability(5.0f);
            poison.Tick(1.0f);
            Assert.AreEqual(before - 6.0f, vitals.Health, 0.01f, "i-frames stopped the poison");

            // And it kills when it has to: ten seconds of a much stronger dose at low health.
            vitals.TakeDamage(vitals.Health - 5.0f);
            PoisonEffect strong = PoisonEffect.Apply(go, 10.0f, 10.0f);
            strong.Tick(3.0f);
            Assert.IsTrue(vitals.IsDead, "poison cannot kill");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheVictimShowsPurpleBubblesWhilePoisonedAndTheyGoWhenItEnds()
        {
            SparringDummy victim = Dummy(new Vector3(0.0f, 1.0f, 0.0f));
            var cloud = _prefab.GetComponent<PoisonGasCloud>();
            Assert.IsNotNull(cloud.VictimFxPrefab, "the cloud has no victim effect");
            PoisonEffect poison = PoisonEffect.Apply(victim.gameObject, 3.0f, 2.0f, cloud.VictimFxPrefab);
            Assert.IsNotNull(victim.GetComponentInChildren<ParticleSystem>(), "no bubbles on the poisoned body");
            Run(poison, 3.0f);
            yield return new WaitForSeconds(2.2f);
            Assert.IsNull(victim.GetComponentInChildren<ParticleSystem>(), "the bubbles outlasted the poison");
        }
    }
}
