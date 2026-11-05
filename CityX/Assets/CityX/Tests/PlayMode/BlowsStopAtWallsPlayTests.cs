using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Melee blows stop at walls. A punch and a sword's cut found whoever was in reach, wall or no wall between: a thin wall, a closed
    /// door, a window (fights through the walls of a room).
    /// </summary>
    public sealed class BlowsStopAtWallsPlayTests
    {
        private const float Slice = 1.0f / 60.0f;

        private GameObject _attackerObject;
        private GameObject _dummyObject;
        private SparringDummy _dummy;
        private GameObject _wall;

        [SetUp]
        public void SetUp()
        {
            _attackerObject = new GameObject("Attacker");
            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);   // its near side a metre off, straight ahead
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null) Object.DestroyImmediate(_attackerObject);
            if (_dummyObject != null) Object.DestroyImmediate(_dummyObject);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        /// <summary>A wall 20 cm thick across the way, from the floor to <paramref name="height"/>, halfway between the two.</summary>
        private void Wall(float height)
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0.0f, height * 0.5f, 0.6f);
            _wall.transform.localScale = new Vector3(4.0f, height, 0.2f);
            Physics.SyncTransforms();
        }

        private void NoWall()
        {
            Object.DestroyImmediate(_wall);
            Physics.SyncTransforms();
        }

        private static void Step(System.Action<float> tick, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Slice)
            {
                tick(Slice);
            }
        }

        [Test]
        public void APunchDoesNotLandThroughAWall()
        {
            var attacker = _attackerObject.AddComponent<MeleeAttacker>();
            Wall(3.0f);
            Assert.IsTrue(attacker.TryAttack());
            Step(attacker.Tick, 1.2f);
            Assert.AreEqual(0, _dummy.TimesHit, "the punch landed on a body the other side of a wall");
            Assert.AreEqual(0, attacker.TargetsInReach().Count, "a body behind a wall counts as in reach");

            NoWall();
            Assert.IsTrue(attacker.TryAttack());
            Step(attacker.Tick, 1.2f);
            Assert.AreEqual(1, _dummy.TimesHit, "with the wall gone the punch still does not land");
        }

        [Test]
        public void APunchOverALowWallStillLands()
        {
            var attacker = _attackerObject.AddComponent<MeleeAttacker>();
            Wall(0.6f);   // a kerb, a bench, a car's bonnet: the blow goes over it
            Assert.IsTrue(attacker.TryAttack());
            Step(attacker.Tick, 1.2f);
            Assert.AreEqual(1, _dummy.TimesHit, "something knee-high between them stopped the punch");
        }

        [Test]
        public void ThePunchDoesNotTurnToSomeoneBehindAWall()
        {
            var attacker = _attackerObject.AddComponent<MeleeAttacker>();
            Assert.IsTrue(attacker.TryFindMagnetYaw(20.0f, out _), "setup: the punch does not turn to a body in the open");
            Wall(3.0f);
            Assert.IsFalse(attacker.TryFindMagnetYaw(20.0f, out _), "the punch is pulled toward a body the other side of a wall");
        }

        [Test]
        public void ASwordDoesNotCutThroughAWall()
        {
            var vitals = _attackerObject.AddComponent<PlayerVitals>();
            vitals.Roll = () => 1.0f;
            vitals.Revive();
            var combat = _attackerObject.AddComponent<SoulsSwordCombat>();
            combat.Equip(null);
            combat.HitStopSeconds = 0.0f;
            System.Action<float> tick = dt =>
            {
                combat.Tick(dt);
                vitals.TickStamina(false, dt);
            };

            Wall(3.0f);
            combat.RequestLight(0.0f);
            Step(tick, combat.Light1.Startup + combat.Light1.Active + combat.Light1.Recovery + 0.2f);
            Assert.AreEqual(0, _dummy.TimesHit, "the cut landed on a body the other side of a wall");

            NoWall();
            Step(tick, 1.0f);
            combat.RequestLight(0.0f);
            Step(tick, combat.Light1.Startup + combat.Light1.Active + combat.Light1.Recovery + 0.2f);
            Assert.AreEqual(1, _dummy.TimesHit, "with the wall gone the cut still does not land");
        }
    }
}
