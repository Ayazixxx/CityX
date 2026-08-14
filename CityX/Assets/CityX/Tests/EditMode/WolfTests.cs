using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The wolf. Its chase is a PlayMode question -- it needs a controller and
    /// real frames -- but its damage rules and the shape of its body are not,
    /// and those are where a mistake is silent.
    /// </summary>
    public sealed class WolfTests
    {
        private GameObject _object;
        private Wolf _wolf;
        private IDamageable _target;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Wolf");
            _object.AddComponent<CharacterController>();
            _wolf = _object.AddComponent<Wolf>();
            _target = _wolf;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ItStartsAliveOnFullHealth()
        {
            Assert.AreEqual(Wolf.MaxHealth, _wolf.Health, 0.001f);
            Assert.IsTrue(_wolf.IsAlive);
        }

        [Test]
        public void EnoughDamageFellsIt()
        {
            _target.ApplyHit(Wolf.MaxHealth * 0.5f, Vector3.zero, 0.5f);
            Assert.IsTrue(_wolf.IsAlive, "half its health killed it");

            _target.ApplyHit(Wolf.MaxHealth, Vector3.zero, 0.5f);
            Assert.IsTrue(_wolf.IsDead);
            Assert.AreEqual(0.0f, _wolf.Health, 0.001f, "health went negative");
        }

        /// <summary>
        /// A wolf never blocks and never dodges. Anything else would need a
        /// mechanic it does not have, and reporting one would punish an
        /// attacker for a hit that actually landed.
        /// </summary>
        [Test]
        public void EveryHitOnALivingWolfLands()
        {
            Assert.AreEqual(HitResponse.Landed, _target.ApplyHit(5.0f, Vector3.zero, 0.5f));
        }

        [Test]
        public void ACorpseTakesNoFurtherHits()
        {
            _target.ApplyHit(Wolf.MaxHealth, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Ignored, _target.ApplyHit(5.0f, Vector3.zero, 0.5f));
        }

        /// <summary>
        /// Felling one has to announce itself, or nothing can be rewarded for
        /// it -- and it must announce ONCE however many times the corpse is
        /// hit afterwards.
        /// </summary>
        [Test]
        public void FellingItIsAnnouncedExactlyOnce()
        {
            int announced = 0;
            float reward = 0.0f;
            _wolf.Felled += amount =>
            {
                announced++;
                reward = amount;
            };

            _target.ApplyHit(Wolf.MaxHealth, Vector3.zero, 0.5f);
            _target.ApplyHit(Wolf.MaxHealth, Vector3.zero, 0.5f);

            Assert.AreEqual(1, announced);
            Assert.AreEqual(Wolf.ExperienceReward, reward, 0.001f);
        }

        /// <summary>
        /// It has to be able to reach what it can see. A bite range longer than
        /// its detection range would mean a wolf that bites things it never
        /// noticed; the other way round and it chases forever without ever
        /// arriving.
        /// </summary>
        [Test]
        public void ItCanReachWhatItChases()
        {
            Assert.Less(Wolf.BiteRange, Wolf.DetectionRange);
            Assert.Greater(Wolf.ChaseSpeed, 0.0f);
        }
    }
}
