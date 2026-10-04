using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The ice shards volley's bookkeeping: four shards one after another, the cooldown, the cast pose, the shards following the aim. Driven
    /// by hand like FireballCasterTests; the flight and the sticking are IceShardPlayTests.
    /// </summary>
    public sealed class IceShardCasterTests
    {
        private GameObject _caster;
        private IceShardCaster _ice;
        private PlayerVitals _vitals;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("ShardPrefab");
            _prefab.AddComponent<IceShardProjectile>();
            _caster = new GameObject("Caster");
            _ice = _caster.AddComponent<IceShardCaster>();
            _ice.ShardPrefab = _prefab;
            _vitals = _caster.AddComponent<PlayerVitals>();
            _vitals.Revive();
            _ice.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IceShardProjectile shard in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(shard.gameObject);
            }
            Object.DestroyImmediate(_caster);
        }

        private static IceShardProjectile[] Shards()
        {
            return Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None);
        }

        /// <summary>Shards in the world not counting the template the caster copies.</summary>
        private int Fired()
        {
            int n = 0;
            foreach (IceShardProjectile shard in Shards())
            {
                if (shard.gameObject != _prefab)
                {
                    n++;
                }
            }
            return n;
        }

        [Test]
        public void TheFirstShardLeavesAtOnceAndFourFollowOneAfterAnother()
        {
            Assert.IsTrue(_ice.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(1, Fired(), "the first shard leaves at the press");
            Assert.IsTrue(_ice.IsVolleying);

            _ice.Tick(_ice.ShardIntervalSeconds * 0.5f);
            Assert.AreEqual(1, Fired(), "the second should wait for its moment");
            _ice.Tick(_ice.ShardIntervalSeconds * 0.6f);
            Assert.AreEqual(2, Fired());
            _ice.Tick(_ice.ShardIntervalSeconds);
            Assert.AreEqual(3, Fired());
            _ice.Tick(_ice.ShardIntervalSeconds);
            Assert.AreEqual(4, Fired());
            Assert.IsFalse(_ice.IsVolleying, "four is the whole volley");

            _ice.Tick(5.0f);
            Assert.AreEqual(4, Fired(), "it fired more than four");
        }

        [Test]
        public void ALongFrameDoesNotSwallowShards()
        {
            _ice.TryCast(Vector3.zero, Vector3.forward, null);
            _ice.Tick(1.0f);
            Assert.AreEqual(4, Fired(), "a slow frame should still put out every shard that was due");
        }

        [Test]
        public void ItCannotBeCastAgainDuringTheVolleyOrTheCooldown()
        {
            Assert.IsTrue(_ice.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.IsFalse(_ice.TryCast(Vector3.zero, Vector3.forward, null), "cast again mid-volley");

            _ice.Tick(1.0f);   // the volley is over, the cooldown is not
            Assert.IsFalse(_ice.IsVolleying);
            Assert.IsFalse(_ice.CanCast);
            Assert.IsFalse(_ice.TryCast(Vector3.zero, Vector3.forward, null), "cast again inside the cooldown");

            _ice.Tick(_ice.CooldownSeconds);
            Assert.IsTrue(_ice.CanCast);
            Assert.IsTrue(_ice.TryCast(Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(5, Fired(), "the second volley's first shard");
        }

        [Test]
        public void TheRestOfTheVolleyFollowsTheAim()
        {
            _ice.TryCast(Vector3.zero, Vector3.forward, null);
            _ice.Aim(Vector3.zero, Vector3.right);
            _ice.Tick(_ice.ShardIntervalSeconds + 0.001f);

            Assert.AreEqual(2, _ice.LastVolley.Count);
            Vector3 first = _ice.LastVolley[0].transform.position;
            Vector3 second = _ice.LastVolley[1].transform.position;
            Assert.Greater(first.z, 0.5f, "the first shard went where the aim was at the press");
            Assert.Greater(second.x, 0.5f, "the second should have gone where the aim had moved to");
        }

        [Test]
        public void EachShardIsNudgedOffTheExactLineByLessThanADegreeOrSo()
        {
            _ice.TryCast(Vector3.zero, Vector3.forward, null);
            _ice.Tick(1.0f);

            var positions = new System.Collections.Generic.List<Vector3>();
            foreach (IceShardProjectile shard in _ice.LastVolley)
            {
                positions.Add(shard.transform.position);
            }
            Assert.AreEqual(4, positions.Count);
            Assert.AreEqual(0.0f, positions[0].x, 1e-4f, "the first goes dead centre");
            for (int i = 1; i < positions.Count; i++)
            {
                float off = Vector3.Distance(positions[i], new Vector3(0.0f, 0.0f, IceShardCaster.SpawnForwardOffset));
                Assert.Greater(off, 0.002f, $"shard {i} sits exactly on the aim line, on top of the first");
                Assert.Less(off, 0.03f, $"shard {i} is nudged too far");
            }
        }

        [Test]
        public void TheCastPoseHoldsThroughTheVolleyAndEasesOutAfter()
        {
            Assert.IsFalse(_ice.IsCasting);
            _ice.TryCast(Vector3.zero, Vector3.forward, null);
            Assert.IsTrue(_ice.IsCasting);
            Assert.AreEqual(1.0f, _ice.CastFraction, 1e-4f);

            _ice.Tick(_ice.ShardIntervalSeconds * 2.0f);
            Assert.AreEqual(1.0f, _ice.CastFraction, 1e-4f, "the hand should stay up while shards are still going out");

            _ice.Tick(_ice.ShardIntervalSeconds * 2.0f + _ice.FollowThroughSeconds * 0.5f);
            Assert.Greater(_ice.CastFraction, 0.0f);
            Assert.Less(_ice.CastFraction, 1.0f, "it should be easing out");

            _ice.Tick(_ice.FollowThroughSeconds);
            Assert.IsFalse(_ice.IsCasting);
        }

        [Test]
        public void ADeadCasterFiresNoMoreShards()
        {
            _ice.TryCast(Vector3.zero, Vector3.forward, null);
            _vitals.TakeDamage(10000.0f);
            Assert.IsTrue(_vitals.IsDead);
            _ice.Tick(1.0f);
            Assert.AreEqual(1, Fired(), "a corpse went on firing");
            Assert.IsFalse(_ice.TryCast(Vector3.zero, Vector3.forward, null));
        }

        [Test]
        public void ADirectionOfZeroDoesNothing()
        {
            Assert.IsFalse(_ice.TryCast(Vector3.zero, Vector3.zero, null));
            Assert.AreEqual(0, Fired());
        }
    }
}
