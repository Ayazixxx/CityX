using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How hard a blow to glass is: a fist or a bullet only cracks a whole pane, while a blade, a thrown
    /// bottle or a body at speed takes it out at once (the melee-versus-thrown-brick difference in The Last
    /// of Us Part II's windows), and the shards fly the way the blow went.
    /// </summary>
    public sealed class GlassBlowTests
    {
        private static readonly PaneKey Pane = new PaneKey(3, 4, WindowGrid.Wall.North, 2, 1);

        [Test]
        public void ALightBlowTakesAPaneOneStepAndAHeavyBlowTakesItAllTheWay()
        {
            Assert.AreEqual(PaneState.Cracked, GlassDamage.NextState(PaneState.Intact, BlowStrength.Light));
            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Cracked, BlowStrength.Light));
            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Shattered, BlowStrength.Light));

            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Intact, BlowStrength.Heavy));
            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Cracked, BlowStrength.Heavy));
            Assert.AreEqual(PaneState.Shattered, GlassDamage.NextState(PaneState.Shattered, BlowStrength.Heavy));
        }

        [Test]
        public void AHeavyHitOnAWholePaneShattersItInOneStepAndTellsTheChunkOnce()
        {
            var damage = new GlassDamage();
            int changes = 0;
            PaneState last = PaneState.Intact;
            damage.Changed += (key, state) => { changes++; last = state; };

            PaneState result = damage.Hit(Pane, new Vector2(0.5f, 0.5f), shatterOutright: true);

            Assert.AreEqual(PaneState.Shattered, result);
            Assert.AreEqual(PaneState.Shattered, damage.StateOf(Pane));
            Assert.AreEqual(1, changes, "one blow is one change for the chunk to rebuild on");
            Assert.AreEqual(PaneState.Shattered, last);
        }

        [Test]
        public void ALightHitStillCracksFirstAsItAlwaysDid()
        {
            var damage = new GlassDamage();
            Assert.AreEqual(PaneState.Cracked, damage.Hit(Pane, new Vector2(0.4f, 0.6f)));
            Assert.AreEqual(PaneState.Shattered, damage.Hit(Pane));
        }

        [Test]
        public void HittingAnAlreadyGonePaneChangesNothing()
        {
            var damage = new GlassDamage();
            damage.Hit(Pane, null, shatterOutright: true);
            int changes = 0;
            damage.Changed += (key, state) => changes++;

            damage.Hit(Pane, null, shatterOutright: true);

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void TheShardsFlyTheWayTheBlowWent()
        {
            Vector3 outward = Vector3.forward;   // the wall faces the street

            Assert.AreEqual(-outward, GlassBreaker.ShardDirection(outward, Vector3.back), "hit from the street: the glass goes into the building");
            Assert.AreEqual(outward, GlassBreaker.ShardDirection(outward, Vector3.forward), "hit from inside: the glass goes out over the street");
            Assert.AreEqual(-outward, GlassBreaker.ShardDirection(outward, new Vector3(0.6f, -0.2f, -0.7f)), "a slanting blow still goes through the pane");
            Assert.AreEqual(outward, GlassBreaker.ShardDirection(outward, Vector3.zero), "with no known direction it is the wall's outward normal, as before");
        }

        private sealed class PunchOnly : IBreakableSurface
        {
            public int Punches;

            public bool TakePunch(Vector3 point)
            {
                Punches++;
                return true;
            }
        }

        [Test]
        public void ASurfaceThatOnlyKnowsPunchesStillTakesABlowAsAPunch()
        {
            var surface = new PunchOnly();
            IBreakableSurface asInterface = surface;

            Assert.IsTrue(asInterface.TakeBlow(Vector3.zero, Vector3.forward, BlowStrength.Heavy));

            Assert.AreEqual(1, surface.Punches, "existing implementers must not have to change");
        }
    }
}
