using CityX.Character;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What rain does to play, on the real components with real colliders and
    /// the shared <see cref="CurrentWeather"/> set by hand: flames weaken in
    /// the rain, wet walls and ladders cost more and slip, and NONE of it
    /// happens on a dry day or under cover. The dry-day half matters as much
    /// as the wet: every other test in the project runs with no weather.
    /// </summary>
    public sealed class RainGameplayTests
    {
        private sealed class FakeCover : IRainCover
        {
            public float Value;
            public float Shelter(Vector3 worldPoint) => Value;
        }

        private sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public float TotalDamage { get; private set; }

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                TotalDamage += amount;
                return HitResponse.Landed;
            }
        }

        private GameObject _flame;
        private GameObject _wielder;
        private GameObject _player;
        private GameObject _wall;
        private GameObject _ground;

        [SetUp]
        public void SetUp() => CurrentWeather.Clear();

        [TearDown]
        public void TearDown()
        {
            CurrentWeather.Clear();
            if (_flame != null) Object.DestroyImmediate(_flame);
            if (_wielder != null) Object.DestroyImmediate(_wielder);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_wall != null) Object.DestroyImmediate(_wall);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        private static void Downpour(float coverValue)
        {
            CurrentWeather.Cover = new FakeCover { Value = coverValue };
            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;
        }

        // --- the flamethrower ---

        private float BurnAt(float distance)
        {
            _flame = new GameObject("Flamethrower");
            Flamethrower flamethrower = _flame.AddComponent<Flamethrower>();
            _wielder = new GameObject("Wielder");
            _wielder.AddComponent<PlayerVitals>().Revive();

            var dummy = new GameObject("Dummy");
            dummy.transform.position = new Vector3(0.0f, 0.0f, distance);
            dummy.AddComponent<SphereCollider>().radius = 0.5f;
            var target = dummy.AddComponent<TestDamageable>();
            try
            {
                flamethrower.Tick(true, Vector3.zero, Vector3.forward, _wielder, 1.0f / 60.0f);
                return target.TotalDamage;
            }
            finally
            {
                Object.DestroyImmediate(dummy);
            }
        }

        [Test]
        public void RainWeakensAFlameInTheOpen()
        {
            float dry = BurnAt(3.0f);
            TearDown();
            Downpour(0.0f);
            float wet = BurnAt(3.0f);

            Assert.Greater(dry, 0.0f);
            Assert.AreEqual(dry * Weather.FlameStrength(1.0f), wet, dry * 0.02f, "a downpour should take about half the bite out of a flame");
        }

        [Test]
        public void ARoofSavesTheFlameFromTheRain()
        {
            float dry = BurnAt(3.0f);
            TearDown();
            Downpour(1.0f);   // the whole street is under a roof
            float sheltered = BurnAt(3.0f);

            Assert.AreEqual(dry, sheltered, dry * 0.001f, "a flamethrower under cover burns exactly as it does on a dry day");
        }

        [Test]
        public void RainShortensAFlamesReachToo()
        {
            float nearEdge = Flamethrower.Range - 0.8f;   // 6.2m: inside a dry flame's reach, outside a soaked one's
            Assert.Greater(BurnAt(nearEdge), 0.0f, "setup: a dry flame should reach this far");
            TearDown();

            Downpour(0.0f);
            Assert.AreEqual(0.0f, BurnAt(nearEdge), "a flame in a downpour should fall short of its dry reach");
        }

        [Test]
        public void ADrizzleBarelyTroublesAFlame()
        {
            float dry = BurnAt(3.0f);
            TearDown();
            CurrentWeather.Intensity = Weather.LightRain;
            CurrentWeather.Wetness = 0.4f;
            float drizzle = BurnAt(3.0f);

            Assert.Greater(drizzle, dry * 0.85f);
            Assert.Less(drizzle, dry);
        }

        // --- wall grip ---

        private void MakeWall()
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3(0.0f, 15.0f, 0.7f);
            _wall.AddComponent<BoxCollider>().size = new Vector3(10.0f, 30.0f, 1.0f);
        }

        private WallStick StickToTheWall(out PlayerVitals vitals)
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            vitals = _player.AddComponent<PlayerVitals>();
            WallStick stick = _player.AddComponent<WallStick>();
            stick.Bind();
            _player.transform.position = new Vector3(0.0f, 3.0f, 0.0f);
            MakeWall();
            Physics.SyncTransforms();
            Assert.IsTrue(stick.TryStart(true, false, vitals.Stamina), "setup: could not grip the wall");
            return stick;
        }

        private float StaminaSpentClimbing(int ticks)
        {
            WallStick stick = StickToTheWall(out PlayerVitals vitals);
            float before = vitals.Stamina;
            for (int i = 0; i < ticks; i++)
            {
                stick.Tick(false, new Vector2(0.0f, 1.0f), vitals, 1.0f / 60.0f);
            }
            return before - vitals.Stamina;
        }

        private float HeightLostHanging(int ticks)
        {
            WallStick stick = StickToTheWall(out PlayerVitals vitals);
            float before = _player.transform.position.y;
            for (int i = 0; i < ticks; i++)
            {
                stick.Tick(false, Vector2.zero, vitals, 1.0f / 60.0f);
            }
            return before - _player.transform.position.y;
        }

        [Test]
        public void ClimbingAWetWallCostsMoreStaminaThanADryOne()
        {
            float dry = StaminaSpentClimbing(60);
            TearDown();
            Downpour(0.0f);
            float wet = StaminaSpentClimbing(60);

            Assert.Greater(dry, 0.0f);
            Assert.AreEqual(dry * Weather.WallStaminaDrain(1.0f), wet, dry * 0.05f, "a soaked wall should double the stamina cost");
        }

        [Test]
        public void ACoveredWallIsMostlyDry()
        {
            float dry = StaminaSpentClimbing(60);
            TearDown();
            Downpour(0.0f);
            float open = StaminaSpentClimbing(60);
            TearDown();
            Downpour(1.0f);
            float covered = StaminaSpentClimbing(60);

            Assert.Less(covered, open, "a wall under a roof is drier than one in the open");
            Assert.Greater(covered, dry - dry * 0.001f, "but the storm still leaves it a little damp");
        }

        [Test]
        public void AWetWallLetsAHangingBodySlideAndADryOneHoldsIt()
        {
            float dry = HeightLostHanging(60);
            TearDown();
            Downpour(0.0f);
            float wet = HeightLostHanging(60);

            Assert.AreEqual(0.0f, dry, 0.05f, "a dry wall should hold a body still");
            Assert.AreEqual(Weather.WetWallSlipSpeed, wet, 0.25f, "a soaked wall should let a body slide about a metre a second");
        }

        [Test]
        public void ClimbingUpAWetWallStillMakesProgress()
        {
            WallStick stick = StickToTheWall(out PlayerVitals vitals);
            Downpour(0.0f);
            float before = _player.transform.position.y;
            for (int i = 0; i < 60; i++)
            {
                stick.Tick(false, new Vector2(0.0f, 1.0f), vitals, 1.0f / 60.0f);
            }

            Assert.Greater(_player.transform.position.y - before, 1.5f, "slippery, not impossible");
        }

        // --- ladder grip ---

        private float LadderStaminaSpent(int ticks)
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            PlayerVitals vitals = _player.AddComponent<PlayerVitals>();
            PlayerClimbing climbing = _player.AddComponent<PlayerClimbing>();
            climbing.Bind();
            _player.transform.position = Vector3.zero;
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3(0.0f, 15.0f, 0.7f);
            _wall.AddComponent<BoxCollider>().size = new Vector3(10.0f, 30.0f, 1.0f);
            Physics.SyncTransforms();
            Assert.IsTrue(climbing.TryStart(true, true, vitals.Stamina), "setup: could not grab the wall");

            float before = vitals.Stamina;
            for (int i = 0; i < ticks; i++)
            {
                climbing.Tick(false, true, false, vitals, 1.0f / 60.0f);
            }
            return before - vitals.Stamina;
        }

        [Test]
        public void ClimbingAWetLadderCostsMoreStaminaThanADryOne()
        {
            float dry = LadderStaminaSpent(60);
            TearDown();
            Downpour(0.0f);
            float wet = LadderStaminaSpent(60);

            Assert.Greater(dry, 0.0f);
            Assert.AreEqual(dry * Weather.WallStaminaDrain(1.0f), wet, dry * 0.05f);
        }

        [Test]
        public void ADryDayLeavesGripExactlyAsItWas()
        {
            // Nothing published: the shipped, no-weather state every other test runs in.
            float a = StaminaSpentClimbing(60);
            TearDown();
            float b = StaminaSpentClimbing(60);

            Assert.AreEqual(a, b, 1e-4f);
            TearDown();
            Assert.AreEqual(0.0f, HeightLostHanging(60), 0.05f);
        }
    }
}
