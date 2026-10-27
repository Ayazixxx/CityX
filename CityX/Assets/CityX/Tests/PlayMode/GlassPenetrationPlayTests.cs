using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A bullet goes on through a window, ten per cent weaker for each pane (owner, 2026-10-26: "make it so the bullet travels through the window
    /// but does a little less damage", "lets make it 10% less damage for all guns for now"); a wall still stops it, and so does a second window
    /// ("a bullet that has gone through 1 window cant go through another it just stops at whatever surface it hits unless its a player/mob/npc").
    /// The glass here is a stand-in that records the bullets it took.
    /// </summary>
    public sealed class GlassPenetrationPlayTests
    {
        private sealed class FakeGlass : MonoBehaviour, IBreakableSurface
        {
            public bool IsGlass = true;
            public int Bullets;

            public bool TakePunch(Vector3 point) => IsGlass;

            public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
            {
                if (IsGlass && strength == BlowStrength.Bullet)
                {
                    Bullets++;
                }
                return IsGlass;
            }

            public bool IsGlassAt(Vector3 point) => IsGlass;
        }

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public float Damage;
            public int Hits;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Damage += amount;
                Hits++;
                return HitResponse.Landed;
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            foreach (BulletTracer tracer in Object.FindObjectsByType<BulletTracer>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tracer.gameObject);
            }
        }

        private T Box<T>(string name, float z, float depth) where T : Component
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = new Vector3(0.0f, 0.0f, z);
            box.transform.localScale = new Vector3(3.0f, 3.0f, depth);
            _made.Add(box);
            return box.AddComponent<T>();
        }

        private FakeGlass Pane(float z) => Box<FakeGlass>($"Pane{z}", z, 0.02f);

        /// <summary>One shot straight down +Z from the origin, from a fresh gun (so no fire cooldown stands in the way).</summary>
        private void Shoot()
        {
            var gunObject = new GameObject("Gun");
            _made.Add(gunObject);
            var gun = gunObject.AddComponent<PistolWeapon>();
            Physics.SyncTransforms();
            Assert.IsTrue(gun.Fire(Vector3.zero, Vector3.forward, aiming: true, wielder: null), "the gun did not fire");
        }

        private float OpenShotDamage()
        {
            Target target = Box<Target>("OpenTarget", 10.0f, 0.5f);
            Shoot();
            float damage = target.Damage;
            Object.DestroyImmediate(target.gameObject);
            Assert.Greater(damage, 0.0f, "setup: an open shot hurt nothing");
            return damage;
        }

        [Test]
        public void EachPaneTakesTenPerCentOff()
        {
            Assert.AreEqual(1.0f, GlassPenetration.DamageShare(0), 1e-6f);
            Assert.AreEqual(0.9f, GlassPenetration.DamageShare(1), 1e-6f);
            Assert.AreEqual(0.81f, GlassPenetration.DamageShare(2), 1e-6f);
        }

        [Test]
        public void ABulletGoesThroughAWindowBreakingItAndHurtsWhatIsBehindTenPerCentLess()
        {
            float open = OpenShotDamage();
            FakeGlass pane = Pane(4.0f);
            Target target = Box<Target>("Target", 10.0f, 0.5f);

            Shoot();

            Assert.AreEqual(1, pane.Bullets, "the window did not take the bullet");
            Assert.AreEqual(1, target.Hits, "the bullet stopped in the window");
            Assert.AreEqual(open * 0.9f, target.Damage, 1e-3f, "through one window the bullet should do 10% less");
            LineRenderer trail = Object.FindFirstObjectByType<BulletTracer>().GetComponent<LineRenderer>();
            Assert.Greater(trail.GetPosition(1).z, 9.0f, "the trail stops at the window, not at what the bullet hit");
        }

        [Test]
        public void ASecondWindowTakesTheBulletAndStopsIt()
        {
            FakeGlass first = Pane(3.0f);
            FakeGlass second = Pane(6.0f);
            Target target = Box<Target>("Target", 10.0f, 0.5f);

            Shoot();

            Assert.AreEqual(1, first.Bullets, "the first window did not take the bullet");
            Assert.AreEqual(1, second.Bullets, "the second window was not hit (the bullet stops in it)");
            Assert.AreEqual(0, target.Hits, "a bullet went through two windows");
            LineRenderer trail = Object.FindFirstObjectByType<BulletTracer>().GetComponent<LineRenderer>();
            Assert.AreEqual(6.0f, trail.GetPosition(1).z, 0.05f, "the trail does not stop at the second window");
        }

        [Test]
        public void AfterAWindowAWallStillStopsABullet()
        {
            Pane(3.0f);
            FakeGlass wall = Box<FakeGlass>("Wall", 6.0f, 0.3f);
            wall.IsGlass = false;
            Target target = Box<Target>("Target", 10.0f, 0.5f);

            Shoot();

            Assert.AreEqual(0, target.Hits, "a bullet went through a window and then a wall");
        }

        [Test]
        public void AWallStillStopsABullet()
        {
            FakeGlass wall = Box<FakeGlass>("Wall", 4.0f, 0.3f);
            wall.IsGlass = false;
            Target target = Box<Target>("Target", 10.0f, 0.5f);

            Shoot();

            Assert.AreEqual(0, target.Hits, "a bullet went through a wall");
        }

        [Test]
        public void AnotherPlayersCopyOfTheShotOnlyLooksForTheGlass()
        {
            // Another player's copy draws its trail on through the windows without breaking them (the shooter's machine broke them).
            FakeGlass pane = Pane(4.0f);
            Target target = Box<Target>("Target", 10.0f, 0.5f);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(Vector3.zero, Vector3.forward, out RaycastHit hit, 50.0f));

            bool onSomething = GlassPenetration.Follow(ref hit, Vector3.forward, 50.0f - hit.distance, false,
                (Vector3 from, float distance, out RaycastHit next) => Physics.Raycast(from, Vector3.forward, out next, distance),
                out int panes, out Vector3 end);

            Assert.IsTrue(onSomething);
            Assert.AreEqual(1, panes);
            Assert.AreEqual(0, pane.Bullets, "a copy of the shot broke the glass");
            Assert.AreSame(target.GetComponent<Collider>(), hit.collider);
            Assert.AreEqual(9.75f, end.z, 0.01f);
        }
    }
}
