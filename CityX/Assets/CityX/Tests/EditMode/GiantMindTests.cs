using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>What the giant does, with no scene (docs/GIANT.md): it strides at a player, crouches (the tell), leaps, slams where it lands, stands, and waits before the next leap.</summary>
    public sealed class GiantMindTests
    {
        private const float Step = 1.0f / 60.0f;

        private static GiantMind.Intent Tick(GiantMind mind, float distance, bool grounded = true, bool target = true)
        {
            return mind.Tick(new GiantMind.Senses { HasTarget = target, Distance = distance, Grounded = grounded }, Step);
        }

        [Test]
        public void FarOffItStridesAtThePlayer()
        {
            var mind = new GiantMind();
            Tick(mind, 80.0f);
            GiantMind.Intent intent = Tick(mind, 80.0f);
            Assert.AreEqual(GiantMind.Phase.Approach, mind.Current);
            Assert.AreEqual(1.0f, intent.Move, 1e-4f, "far off, it should walk at the player");
            Assert.IsFalse(intent.Jump);
        }

        [Test]
        public void NearItCrouchesForTheTellThenLeapsThenSlamsWhereItLands()
        {
            var mind = new GiantMind();
            Tick(mind, 20.0f);
            GiantMind.Intent intent = Tick(mind, 20.0f);
            Assert.AreEqual(GiantMind.Phase.Crouch, mind.Current, "near enough, it did not crouch");
            Assert.IsTrue(intent.Crouch);
            float crouched = 0.0f;
            while (!intent.Jump && crouched < 5.0f)
            {
                intent = Tick(mind, 20.0f);
                crouched += Step;
            }
            Assert.IsTrue(intent.Jump, "it never leapt");
            Assert.AreEqual(GiantRules.CrouchSeconds, crouched, 2.0f * Step, "the tell is not as long as it should be");

            intent = Tick(mind, 15.0f, grounded: false);
            Assert.IsFalse(intent.Slam, "it slammed in the air");
            intent = Tick(mind, 5.0f, grounded: true);
            Assert.IsTrue(intent.Slam, "it came down and did not slam");
            Assert.AreEqual(GiantMind.Phase.Recover, mind.Current);
        }

        [Test]
        public void AfterASlamItWaitsBeforeTheNextLeap()
        {
            var mind = new GiantMind();
            Tick(mind, 20.0f);
            Tick(mind, 20.0f);
            for (float t = 0.0f; t < GiantRules.CrouchSeconds + Step; t += Step) Tick(mind, 20.0f);
            Tick(mind, 20.0f, grounded: false);
            Tick(mind, 20.0f, grounded: true);
            for (float t = 0.0f; t < GiantRules.RecoverSeconds + Step; t += Step) Tick(mind, 20.0f);
            Assert.AreEqual(GiantMind.Phase.Approach, mind.Current, "after standing it does not go after the player again");
            GiantMind.Intent intent = Tick(mind, 20.0f);
            Assert.IsFalse(intent.Crouch, "it leapt again before its cooldown");
        }

        [Test]
        public void KnockedOutOfItsLeapItStandsAMomentAndDoesNotSlamOnLanding()
        {
            var mind = new GiantMind();
            Tick(mind, 20.0f);
            Tick(mind, 20.0f);
            for (float t = 0.0f; t < GiantRules.CrouchSeconds + Step; t += Step) Tick(mind, 20.0f);
            Tick(mind, 20.0f, grounded: false);
            Assert.AreEqual(GiantMind.Phase.Airborne, mind.Current, "setup: it is not in its leap");

            mind.KnockedDown();
            GiantMind.Intent intent = Tick(mind, 20.0f, grounded: true);
            Assert.IsFalse(intent.Slam, "knocked out of the air, it slammed as it stood up");
            Assert.AreEqual(GiantMind.Phase.Recover, mind.Current);
            for (float t = 0.0f; t < GiantRules.RecoverSeconds + Step; t += Step) intent = Tick(mind, 20.0f);
            intent = Tick(mind, 20.0f);
            Assert.AreEqual(GiantMind.Phase.Crouch, mind.Current, "it does not go for the player again: crouch first, then leap");

            // Thrown down while walking, it walks on.
            var walking = new GiantMind();
            Tick(walking, 80.0f);
            walking.KnockedDown();
            Assert.AreEqual(GiantMind.Phase.Approach, walking.Current);
        }

        [Test]
        public void TheSlamHurtsMostAtItsMiddleAndNothingPastItsRim()
        {
            Assert.AreEqual(1.0f, GiantRules.SlamShare(0.0f), 1e-4f);
            Assert.AreEqual(0.5f, GiantRules.SlamShare(GiantRules.SlamRadius), 1e-4f);
            Assert.AreEqual(0.0f, GiantRules.SlamShare(GiantRules.SlamRadius + 0.1f), 1e-4f);
            Assert.AreEqual(10.0f, GiantRules.Scale, 1e-4f, "the giant is the Grow spell's size");
        }

        /// <summary>/spawn giant asks a session's server for it: further off than the server allows and nothing comes.</summary>
        [Test]
        public void TheConsolePutsTheGiantWhereASessionLetsAPlayerSpawnOne()
        {
            Assert.Less(CityX.UI.DevConsoleView.GiantSpawnDistance, CityX.Character.EnemyNetwork.SpawnReachMetres);
            Assert.Greater(CityX.UI.DevConsoleView.GiantSpawnDistance, GiantRules.LeapFromMetres, "it spawns close enough to leap at once");
        }
    }
}
