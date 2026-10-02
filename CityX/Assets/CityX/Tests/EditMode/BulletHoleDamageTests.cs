using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A pistol round leaves a hole and the window stays up, hole after hole, for good -- only a knock brings it down;
    /// a fist or a blade still does what it always did.
    /// </summary>
    public sealed class BulletHoleDamageTests
    {
        private static readonly PaneKey Pane = new PaneKey(3, WindowGrid.Wall.North, 2, 3);

        [Test]
        public void EachBulletAddsAHoleAndTheWindowStaysStanding()
        {
            var damage = new GlassDamage();
            for (int i = 1; i <= GlassDamage.MaxBulletHoles; i++)
            {
                PaneState state = damage.Shoot(Pane, new Vector2(0.1f * i, 0.5f), out bool changed);
                Assert.IsTrue(changed);
                Assert.AreEqual(PaneState.Cracked, state, $"bullet {i} took the window down");
                Assert.AreEqual(i, damage.HoleCount(Pane));
            }

            Assert.IsTrue(damage.TryGetImpacts(Pane, out Vector2[] holes));
            Assert.AreEqual(GlassDamage.MaxBulletHoles, holes.Length);
            Assert.AreEqual(0.1f, holes[0].x, 1e-5f, "the holes are kept in the order they were shot");
        }

        [Test]
        public void BulletsNeverBringTheWindowDownAndTheOldestHoleMakesWay()
        {
            var damage = new GlassDamage();
            for (int i = 0; i < GlassDamage.MaxBulletHoles * 3; i++)
            {
                PaneState state = damage.Shoot(Pane, new Vector2(0.02f * (i % 40), 0.5f), out bool changed);
                Assert.AreEqual(PaneState.Cracked, state, $"bullet {i + 1} brought the window down: only a knock may");
                Assert.IsTrue(changed);
            }

            Assert.AreEqual(GlassDamage.MaxBulletHoles, damage.HoleCount(Pane), "a shot-up window keeps a fixed number of holes");
            Assert.IsTrue(damage.TryGetImpact(Pane, out Vector2 newest));
            Assert.AreEqual(0.02f * ((GlassDamage.MaxBulletHoles * 3 - 1) % 40), newest.x, 1e-5f, "the newest hole is the one kept");
        }

        [Test]
        public void ShootingAWindowThatIsAlreadyGoneDoesNothing()
        {
            var damage = new GlassDamage();
            damage.Hit(Pane, null, shatterOutright: true);
            int announced = 0;
            damage.Changed += (_, __) => announced++;

            PaneState state = damage.Shoot(Pane, new Vector2(0.5f, 0.5f), out bool changed);

            Assert.AreEqual(PaneState.Shattered, state);
            Assert.IsFalse(changed);
            Assert.AreEqual(0, announced, "a bullet into thin air made the chunk rebuild");
        }

        [Test]
        public void EveryNewHoleAnnouncesItselfSoTheChunkIsRebuiltWithIt()
        {
            var damage = new GlassDamage();
            int announced = 0;
            damage.Changed += (_, __) => announced++;

            damage.Shoot(Pane, new Vector2(0.3f, 0.3f), out _);
            damage.Shoot(Pane, new Vector2(0.6f, 0.6f), out _);
            damage.Shoot(Pane, new Vector2(0.4f, 0.8f), out _);

            Assert.AreEqual(3, announced, "the pane's state stayed Cracked, but what it looks like changed each time");
        }

        [Test]
        public void AFistOnAShotUpWindowStillFinishesIt()
        {
            var damage = new GlassDamage();
            damage.Shoot(Pane, new Vector2(0.3f, 0.3f), out _);
            damage.Shoot(Pane, new Vector2(0.6f, 0.6f), out _);

            PaneState state = damage.Hit(Pane, new Vector2(0.5f, 0.5f));

            Assert.AreEqual(PaneState.Shattered, state);
            Assert.AreEqual(0, damage.HoleCount(Pane));
        }

        [Test]
        public void ABladeTakesAFreshWindowOutAtOnceWhateverBulletsDo()
        {
            var damage = new GlassDamage();
            Assert.AreEqual(PaneState.Shattered, damage.Hit(Pane, null, shatterOutright: true));
            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Intact, BlowStrength.Heavy));
            Assert.AreEqual(PaneState.Cracked, GlassDamage.NextState(PaneState.Intact, BlowStrength.Bullet));
            Assert.AreEqual(PaneState.Cracked, GlassDamage.NextState(PaneState.Cracked, BlowStrength.Bullet));
        }

        [Test]
        public void APunchThatCracksAPaneStillRecordsItsHole()
        {
            var damage = new GlassDamage();
            damage.Hit(Pane, new Vector2(0.4f, 0.6f));

            Assert.AreEqual(1, damage.HoleCount(Pane));
            Assert.IsTrue(damage.TryGetImpact(Pane, out Vector2 uv));
            Assert.AreEqual(0.4f, uv.x, 1e-5f);
        }

        [Test]
        public void MendingAPaneForgetsItsHoles()
        {
            var damage = new GlassDamage();
            damage.Shoot(Pane, new Vector2(0.4f, 0.6f), out _);

            damage.Set(Pane, PaneState.Intact);

            Assert.AreEqual(0, damage.HoleCount(Pane));
            Assert.AreEqual(PaneState.Intact, damage.StateOf(Pane));
        }
    }
}
