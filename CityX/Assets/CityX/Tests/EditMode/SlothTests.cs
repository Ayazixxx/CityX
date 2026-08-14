using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sloth. Built on Wolf's own shape, so what's worth pinning here is
    /// the same kind of thing WolfTests pins -- health/death bookkeeping and
    /// the shape of its body -- plus the one real difference: no XP reward,
    /// because Godot's own sloth.gd has none either.
    /// </summary>
    public sealed class SlothTests
    {
        private GameObject _object;
        private Sloth _sloth;
        private IDamageable _target;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Sloth");
            _object.AddComponent<CharacterController>();
            _sloth = _object.AddComponent<Sloth>();
            _target = _sloth;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ItStartsAliveOnFullHealth()
        {
            Assert.AreEqual(Sloth.MaxHealth, _sloth.Health, 0.001f);
            Assert.IsTrue(_sloth.IsAlive);
        }

        [Test]
        public void EnoughDamageFellsIt()
        {
            _target.ApplyHit(Sloth.MaxHealth * 0.5f, Vector3.zero, 0.5f);
            Assert.IsTrue(_sloth.IsAlive, "half its health killed it");

            _target.ApplyHit(Sloth.MaxHealth, Vector3.zero, 0.5f);
            Assert.IsTrue(_sloth.IsDead);
            Assert.AreEqual(0.0f, _sloth.Health, 0.001f, "health went negative");
        }

        [Test]
        public void EveryHitOnALivingSlothLands()
        {
            Assert.AreEqual(HitResponse.Landed, _target.ApplyHit(5.0f, Vector3.zero, 0.5f));
        }

        [Test]
        public void ACorpseTakesNoFurtherHits()
        {
            _target.ApplyHit(Sloth.MaxHealth, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Ignored, _target.ApplyHit(5.0f, Vector3.zero, 0.5f));
        }

        [Test]
        public void FellingItIsAnnouncedExactlyOnce()
        {
            int announced = 0;
            _sloth.Felled += () => announced++;

            _target.ApplyHit(Sloth.MaxHealth, Vector3.zero, 0.5f);
            _target.ApplyHit(Sloth.MaxHealth, Vector3.zero, 0.5f);

            Assert.AreEqual(1, announced);
        }

        /// <summary>
        /// Its own reach has to cover the ground its slower chase actually
        /// closes, or it either never gets to attack or attacks from too far
        /// away to read as a hit.
        /// </summary>
        [Test]
        public void ItCanReachWhatItChases()
        {
            Assert.Less(Sloth.AttackRange, Sloth.DetectionRange);
            Assert.Greater(Sloth.ChaseSpeed, 0.0f);
        }

        /// <summary>Below the player's own sprint speed -- a real, deliberate escape option for a boss-tier threat.</summary>
        [Test]
        public void ASprintingPlayerCanOutrunIt()
        {
            const float sprintSpeed = 7.5f; // player.gd's own SPRINT_SPEED
            Assert.Less(Sloth.ChaseSpeed, sprintSpeed);
        }
    }
}
