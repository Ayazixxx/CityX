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
    /// The sword's crescent wave leaves the blade a moment after the key (the middle of the swing). A caster staggered, thrown down or killed
    /// in that moment still sent it: the crescent flew from a body lying on the ground.
    /// </summary>
    public sealed class SwordWaveInterruptedPlayTests
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
        }

        private SwordWaveCaster Caster(out PlayerVitals vitals, out int[] fired)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
                _made.Add(ground);
            }
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(_made.Count * 4.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(body);
            vitals = body.GetComponent<PlayerVitals>();
            SwordWaveCaster caster = body.GetComponent<SwordWaveCaster>();
            if (caster == null)
            {
                caster = body.AddComponent<SwordWaveCaster>();
            }
            caster.WavePrefab = null;   // nothing flies here: what counts is whether it was let go
            var count = new int[1];
            caster.Fired += _ => count[0]++;
            fired = count;
            return caster;
#else
            vitals = null;
            fired = null;
            return null;
#endif
        }

        private static void Through(SwordWaveCaster caster, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += 1.0f / 60.0f)
            {
                caster.Tick(1.0f / 60.0f);
            }
        }

        [UnityTest]
        public IEnumerator ACasterStaggeredKnockedDownOrKilledBeforeTheBladeComesThroughSendsNoCrescent()
        {
            SwordWaveCaster plain = Caster(out _, out int[] plainFired);
            SwordWaveCaster staggered = Caster(out PlayerVitals staggeredVitals, out int[] staggeredFired);
            SwordWaveCaster thrown = Caster(out PlayerVitals thrownVitals, out int[] thrownFired);
            SwordWaveCaster killed = Caster(out PlayerVitals killedVitals, out int[] killedFired);
            yield return null;
            yield return null;

            foreach (SwordWaveCaster caster in new[] { plain, staggered, thrown, killed })
            {
                Assert.IsTrue(caster.TryCast(1.2f, Vector3.forward, null), "setup: the wave would not start");
            }
            staggeredVitals.Stun(0.5f);
            thrownVitals.TriggerLaunchKnockdown(Vector3.up * 2.0f);
            killedVitals.TakeDamage(100000.0f);
            yield return null;

            foreach (SwordWaveCaster caster in new[] { plain, staggered, thrown, killed })
            {
                Through(caster, plain.ReleaseDelaySeconds + 0.1f);
            }
            Assert.AreEqual(1, plainFired[0], "setup: an untouched caster's crescent never left the blade");
            Assert.AreEqual(0, staggeredFired[0], "a caster staggered before the blade came through still sent the crescent");
            Assert.AreEqual(0, thrownFired[0], "a caster thrown to the ground before the blade came through still sent the crescent");
            Assert.AreEqual(0, killedFired[0], "a caster killed before the blade came through still sent the crescent");
        }
    }
}
