using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The sword wave's two shapes: the whole crescent hurts enemies, and only its centre meets the world (the ground clipping its lower
    /// edge does not end it). Uses the real prefab.
    /// </summary>
    public sealed class SwordWaveProjectilePlayTests
    {
        private sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public int Hits;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                return HitResponse.Landed;
            }
        }

        private const string PrefabPath = "Assets/CityX/Weapons/SwordWave.prefab";
        private GameObject _prefab;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(_prefab, "the sword wave prefab is missing -- run CityX/Rebuild Sword Wave");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, bool damageable = false)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
            if (damageable)
            {
                go.AddComponent<TestDamageable>();
            }
            _made.Add(go);
            return go;
        }

        private SwordWaveProjectile Throw(Vector3 from)
        {
            SwordWaveProjectile wave = SwordWaveProjectile.Spawn(_prefab, from, new Vector3(0.0f, 0.0f, 30.0f), 0, null);
            _made.Add(wave.gameObject);
            return wave;
        }

        private static IEnumerator Fixed(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator TheGroundClippingTheLowerEdgeDoesNotEndIt()
        {
            // The ground is a big slab whose top is at y = 0. The wave flies at 0.9 m, so the lower tip of the crescent (about 1.8 m below its
            // centre) is well under the ground the whole way -- but its centre is clear.
            Box("Ground", new Vector3(0.0f, -1.0f, 20.0f), new Vector3(40.0f, 2.0f, 80.0f));
            SwordWaveProjectile wave = Throw(new Vector3(0.0f, 0.9f, 0.0f));
            GameObject waveObject = wave.gameObject;

            yield return Fixed(30);   // 0.6 s: 18 m
            Assert.IsTrue(waveObject != null, "the wave died on the ground with only its edge touching");
            Assert.Greater(waveObject.transform.position.z, 10.0f, "and it flew on");
        }

        [UnityTest]
        public IEnumerator TheCentreMeetingTheGroundEndsIt()
        {
            Box("Ground", new Vector3(0.0f, -1.0f, 20.0f), new Vector3(40.0f, 2.0f, 80.0f));
            SwordWaveProjectile wave = Throw(new Vector3(0.0f, 0.15f, 0.0f));   // its centre is practically on the ground
            GameObject waveObject = wave.gameObject;

            yield return Fixed(10);
            Assert.IsTrue(waveObject == null, "a wave whose centre is in the ground should be gone");
        }

        [UnityTest]
        public IEnumerator AWallInFrontOfItsCentreStopsIt()
        {
            Box("Wall", new Vector3(0.0f, 2.0f, 8.0f), new Vector3(10.0f, 6.0f, 0.5f));
            SwordWaveProjectile wave = Throw(new Vector3(0.0f, 1.8f, 0.0f));
            GameObject waveObject = wave.gameObject;

            yield return Fixed(25);
            Assert.IsTrue(waveObject == null, "the wall should have stopped it");
        }

        [UnityTest]
        public IEnumerator EveryPartOfTheCrescentHurtsAnEnemyAndTheWavePassesThrough()
        {
            // One enemy dead centre, one up where only the top tip of the crescent reaches, one down at the bottom tip, one off to the side
            // at a depth the tail covers; and a wall far beyond them all.
            TestDamageable centre = Box("Centre", new Vector3(0.0f, 1.8f, 6.0f), new Vector3(0.6f, 0.6f, 0.6f), true).GetComponent<TestDamageable>();
            TestDamageable top = Box("Top", new Vector3(-0.5f, 3.3f, 6.0f), new Vector3(0.4f, 0.4f, 0.4f), true).GetComponent<TestDamageable>();
            TestDamageable bottom = Box("Bottom", new Vector3(0.5f, 0.3f, 6.0f), new Vector3(0.4f, 0.4f, 0.4f), true).GetComponent<TestDamageable>();
            SwordWaveProjectile wave = Throw(new Vector3(0.0f, 1.8f, 0.0f));
            GameObject waveObject = wave.gameObject;

            yield return Fixed(20);   // 0.4 s: 12 m, past all of them

            Assert.AreEqual(1, centre.Hits, "the middle of the crescent");
            Assert.AreEqual(1, top.Hits, "the top tip of the crescent");
            Assert.AreEqual(1, bottom.Hits, "the bottom tip of the crescent");
            Assert.IsTrue(waveObject != null, "enemies do not stop it");
        }
    }
}
