using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The shard in flight, on the real prefab: it sticks in a wall and stays 30 seconds; it sticks in a character, hurts it, rides with it,
    /// and stays 10; it melts at the end; it ignores its caster; it can be fired from inside a wall. Stepped by hand (Tick) so a minute of
    /// game time takes no time.
    /// </summary>
    public sealed class IceShardPlayTests
    {
        private sealed class TestTarget : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public int Hits;
            public float Damage;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                Damage += amount;
                return HitResponse.Landed;
            }
        }

        private const string PrefabPath = "Assets/CityX/Weapons/IceShard.prefab";
        private const float Step = 1.0f / 60.0f;
        private GameObject _prefab;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(_prefab, "the ice shard prefab is missing -- run CityX/Rebuild Ice Shard");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            foreach (IceShardProjectile shard in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None))
            {
                Object.Destroy(shard.gameObject);
            }
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
            _made.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private TestTarget Target(Vector3 position)
        {
            var go = new GameObject("Target");
            go.transform.position = position;
            var body = go.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.4f;
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<TestTarget>();
        }

        private IceShardProjectile Fire(Vector3 from, Vector3 velocity, Collider[] ignore = null)
        {
            IceShardProjectile shard = IceShardProjectile.Spawn(_prefab, from, velocity, 0, ignore);
            Assert.IsNotNull(shard);
            return shard;
        }

        /// <summary>Ticks until it has stuck (or a few seconds pass).</summary>
        private static void FlyUntilStuck(IceShardProjectile shard)
        {
            for (int i = 0; i < 600 && shard != null && shard.CurrentPhase == IceShardProjectile.Phase.Flying; i++)
            {
                shard.Tick(Step);
            }
        }

        private static void Run(IceShardProjectile shard, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                shard.Tick(Step);
            }
        }

        [UnityTest]
        public IEnumerator AShardThatHitsAWallSticksInItAndStaysThirtySeconds()
        {
            GameObject wall = Box("Wall", new Vector3(0.0f, 2.0f, 10.0f), new Vector3(20.0f, 8.0f, 0.5f));   // its near face is at z = 9.75
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.5f, 0.0f), new Vector3(0.0f, 0.0f, 50.0f));
            FlyUntilStuck(shard);

            Assert.AreEqual(IceShardProjectile.Phase.StuckInWorld, shard.CurrentPhase);
            Assert.AreEqual(9.75f + IceShardProjectile.WorldEmbedMetres, shard.transform.position.z, 0.08f, "the tip should be in the wall, not short of it or through it");
            Assert.AreEqual(1.5f, shard.transform.position.y, 0.1f);
            Assert.AreSame(wall.transform, shard.StuckTo, "it should ride on what it hit");
            Assert.AreEqual(IceShardProjectile.WorldStickSeconds, shard.StuckSecondsRemaining, 0.1f);

            Run(shard, 29.0f);
            Assert.IsTrue(shard != null, "gone before thirty seconds");
            Assert.AreEqual(IceShardProjectile.Phase.StuckInWorld, shard.CurrentPhase);

            Run(shard, 1.2f);
            yield return null;
            Assert.IsTrue(shard == null, "still there after thirty seconds");
        }

        [UnityTest]
        public IEnumerator AShardThatHitsACharacterHurtsItSticksAndStaysTenSeconds()
        {
            TestTarget target = Target(new Vector3(0.0f, 1.0f, 8.0f));
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 50.0f));
            FlyUntilStuck(shard);

            Assert.AreEqual(IceShardProjectile.Phase.StuckInCharacter, shard.CurrentPhase);
            Assert.AreEqual(1, target.Hits, "one shard, one hit");
            Assert.AreEqual(IceShardProjectile.BaseDamage, target.Damage, 0.01f);
            Assert.AreSame(target.transform, shard.StuckTo);
            Assert.AreEqual(IceShardProjectile.CharacterStickSeconds, shard.StuckSecondsRemaining, 0.1f);

            // It goes where they go.
            Vector3 before = shard.transform.position;
            target.transform.position += new Vector3(3.0f, 0.0f, 0.0f);
            Assert.AreEqual(before.x + 3.0f, shard.transform.position.x, 0.01f, "the shard did not travel with the character");

            Run(shard, 9.0f);
            Assert.IsTrue(shard != null, "gone before ten seconds");
            Run(shard, 1.2f);
            yield return null;
            Assert.IsTrue(shard == null, "a shard in a character should not last the thirty seconds a wall's does");
            Assert.AreEqual(1, target.Hits, "a stuck shard went on hurting");
        }

        [UnityTest]
        public IEnumerator AStuckShardMeltsAwayOverItsLastMoments()
        {
            Box("Wall", new Vector3(0.0f, 2.0f, 10.0f), new Vector3(20.0f, 8.0f, 0.5f));
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.5f, 0.0f), new Vector3(0.0f, 0.0f, 50.0f));
            FlyUntilStuck(shard);
            Vector3 full = shard.transform.localScale;

            Run(shard, IceShardProjectile.WorldStickSeconds - IceShardProjectile.MeltSeconds - 1.0f);
            Assert.AreEqual(full.x, shard.transform.localScale.x, 1e-4f, "it started melting early");

            Run(shard, 1.0f + IceShardProjectile.MeltSeconds * 0.5f);
            Assert.Less(shard.transform.localScale.x, full.x * 0.95f, "it should be shrinking by now");
            Assert.Greater(shard.transform.localScale.x, 0.0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AShardIgnoresTheBodyThatFiredItAndFliesOn()
        {
            Box("Wall", new Vector3(0.0f, 2.0f, 20.0f), new Vector3(20.0f, 8.0f, 0.5f));
            TestTarget caster = Target(new Vector3(0.0f, 1.0f, 0.0f));
            // Fired from inside the caster's own capsule.
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 50.0f), caster.GetComponents<Collider>());
            FlyUntilStuck(shard);

            Assert.AreEqual(0, caster.Hits, "it hit the one who fired it");
            Assert.AreEqual(IceShardProjectile.Phase.StuckInWorld, shard.CurrentPhase);
            Assert.Greater(shard.transform.position.z, 19.0f, "it should have gone on to the wall");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AShardFiredFromInsideAWallStillSticks()
        {
            GameObject wall = Box("Wall", new Vector3(0.0f, 2.0f, 5.0f), new Vector3(20.0f, 8.0f, 4.0f));   // z from 3 to 7
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.5f, 4.0f), new Vector3(0.0f, 0.0f, 50.0f));
            shard.Tick(Step);
            Assert.AreEqual(IceShardProjectile.Phase.StuckInWorld, shard.CurrentPhase, "it flew on through the wall it started in");
            Assert.AreSame(wall.transform, shard.StuckTo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AShardThatHitsNothingGoesAfterThreeSeconds()
        {
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1000.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f) * 20.0f);
            Run(shard, IceShardProjectile.FlightSeconds - 0.3f);
            Assert.IsTrue(shard != null);
            Run(shard, 0.6f);
            yield return null;
            Assert.IsTrue(shard == null, "a shard in the sky should not live for ever");
        }

        [UnityTest]
        public IEnumerator AShardPointsTheWayItFlewWhenItSticks()
        {
            Box("Wall", new Vector3(0.0f, 2.0f, 10.0f), new Vector3(20.0f, 8.0f, 0.5f));
            IceShardProjectile shard = Fire(new Vector3(0.0f, 1.5f, 0.0f), new Vector3(0.0f, 0.0f, 50.0f));
            FlyUntilStuck(shard);
            Assert.Less(Vector3.Angle(shard.transform.forward, Vector3.forward), 8.0f, "it should be pointing into the wall, give or take its little tilt");
            yield return null;
        }
    }
}
