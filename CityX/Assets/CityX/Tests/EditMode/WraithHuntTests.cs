using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Wraith's hunt on paper (Character.WraithHunt): it comes on while a back is turned, goes round when it is faced, and bites only from behind.</summary>
    public sealed class WraithHuntTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static WraithHunt.Prey PreyAt(Vector3 position, Vector3 forward, bool evading = false)
        {
            return new WraithHunt.Prey { Exists = true, Position = position, Forward = forward, Evading = evading };
        }

        /// <summary>Runs it, moving by its own velocity, until it reaches a phase or time runs out.</summary>
        private static Vector3 RunUntil(WraithHunt hunt, Vector3 self, in WraithHunt.Prey prey, WraithPhase wanted, float seconds)
        {
            for (float t = 0.0f; t < seconds && hunt.Phase != wanted; t += Dt)
            {
                hunt.Tick(Dt, self, prey, false);
                self += hunt.Velocity * Dt;
            }
            return self;
        }

        [Test]
        public void ItLeavesAPlayerFarOffAloneAndNoticesOneNear()
        {
            var hunt = new WraithHunt();
            hunt.Tick(Dt, Vector3.zero, PreyAt(new Vector3(WraithHunt.NoticeMetres + 5.0f, 0.0f, 0.0f), Vector3.forward), false);
            Assert.AreEqual(WraithPhase.Drifting, hunt.Phase);
            hunt.Tick(Dt, Vector3.zero, PreyAt(new Vector3(WraithHunt.NoticeMetres - 1.0f, 0.0f, 0.0f), Vector3.forward), false);
            Assert.AreEqual(WraithPhase.Stalking, hunt.Phase);
            Assert.IsTrue(hunt.Changed);
        }

        [Test]
        public void BehindABackTurnedItComesOnAndLunges()
        {
            var hunt = new WraithHunt();
            Vector3 self = RunUntil(hunt, new Vector3(0.0f, 0.0f, -12.0f), PreyAt(Vector3.zero, Vector3.forward), WraithPhase.Lunging, 10.0f);
            Assert.AreEqual(WraithPhase.Lunging, hunt.Phase, "it never lunged at a turned back");
            Assert.Less(self.z, 0.0f, "it did not come from behind");
            Assert.LessOrEqual(new Vector3(self.x, 0.0f, self.z).magnitude, WraithHunt.LungeMetres + 0.1f);
        }

        /// <summary>A summon (Bold) goes straight in and lunges at what faces it: an enemy fighting it always turns to it (Wraith.Summon.cs).</summary>
        [Test]
        public void ABoldOneComesStraightOnAtWhatFacesItAndLunges()
        {
            var hunt = new WraithHunt { Bold = true };
            Vector3 self = RunUntil(hunt, new Vector3(0.0f, 0.0f, 10.0f), PreyAt(Vector3.zero, Vector3.forward), WraithPhase.Lunging, 6.0f);
            Assert.AreEqual(WraithPhase.Lunging, hunt.Phase, "it never lunged at what faced it");
            Assert.Greater(self.z, 0.0f, "it went round to the back");
        }

        [Test]
        public void FacedItHangsBackAndGoesRoundInsteadOfComingOn()
        {
            var hunt = new WraithHunt();
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.forward);
            Vector3 self = new Vector3(0.5f, 0.0f, 4.0f);
            hunt.Tick(Dt, self, prey, false);
            Assert.AreEqual(WraithPhase.Stalking, hunt.Phase);
            float closest = float.MaxValue;
            for (float t = 0.0f; t < 1.5f; t += Dt)
            {
                hunt.Tick(Dt, self, prey, false);
                self += hunt.Velocity * Dt;
                closest = Mathf.Min(closest, new Vector3(self.x, 0.0f, self.z).magnitude);
                Assert.AreEqual(WraithPhase.Stalking, hunt.Phase, "it lunged at a body facing it");
            }
            Assert.Greater(closest, 3.5f, "it came on while watched");
            Assert.Greater(Mathf.Abs(self.x), 0.5f, "it did not go round");
        }

        [Test]
        public void AlwaysFacedItNeverBites()
        {
            var hunt = new WraithHunt();
            Vector3 self = new Vector3(0.0f, 0.0f, 6.0f);
            for (float t = 0.0f; t < 30.0f; t += Dt)
            {
                Vector3 toward = new Vector3(self.x, 0.0f, self.z).normalized;   // the player keeps turning to it
                hunt.Tick(Dt, self, PreyAt(Vector3.zero, toward), true);
                self += hunt.Velocity * Dt;
                Assert.AreNotEqual(WraithPhase.Lunging, hunt.Phase, "it lunged at a body that kept facing it");
            }
        }

        [Test]
        public void TheLungeLandsOnANeckAndTheBiteDoesALot()
        {
            var hunt = new WraithHunt();
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.forward);
            RunUntil(hunt, new Vector3(0.0f, 0.0f, -3.0f), prey, WraithPhase.Lunging, 5.0f);
            for (float t = 0.0f; t < WraithHunt.LungeSeconds - 2.0f * Dt; t += Dt)
            {
                hunt.Tick(Dt, Vector3.zero, prey, true);
                Assert.AreEqual(0.0f, hunt.DamageDue, "it bit before its lunge arrived");
            }
            float dealt = 0.0f;
            for (int i = 0; i < 4 && hunt.Phase != WraithPhase.Biting; i++)
            {
                hunt.Tick(Dt, Vector3.zero, prey, true);
                dealt += hunt.DamageDue;
            }
            Assert.AreEqual(WraithPhase.Biting, hunt.Phase);
            Assert.AreEqual(WraithHunt.SinkDamage, dealt, 1e-3f);
            for (float t = 0.0f; t < WraithHunt.BiteSeconds + 0.2f && hunt.Phase == WraithPhase.Biting; t += Dt)
            {
                hunt.Tick(Dt, Vector3.zero, prey, true);
                dealt += hunt.DamageDue;
            }
            Assert.AreEqual(WraithPhase.Thrown, hunt.Phase, "it never lets go");
            Assert.GreaterOrEqual(dealt, 45.0f, "owner: it should do a lot of damage");
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ALungeThatMissesOrIsDashedThroughDoesNothing(bool reached, bool evading)
        {
            var hunt = new WraithHunt();
            RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), PreyAt(Vector3.zero, Vector3.forward), WraithPhase.Lunging, 3.0f);
            float dealt = 0.0f;
            for (float t = 0.0f; t < WraithHunt.LungeSeconds + 0.1f; t += Dt)
            {
                hunt.Tick(Dt, Vector3.zero, PreyAt(Vector3.zero, Vector3.forward, evading), reached);
                dealt += hunt.DamageDue;
            }
            Assert.AreEqual(WraithPhase.Thrown, hunt.Phase);
            Assert.AreEqual(0.0f, dealt);
        }

        [Test]
        public void AfterABiteItLeavesThemAloneAWhile()
        {
            var hunt = new WraithHunt();
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.forward);
            RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), prey, WraithPhase.Lunging, 3.0f);
            RunUntil(hunt, Vector3.zero, prey, WraithPhase.Drifting, 6.0f);
            Assert.AreEqual(WraithPhase.Drifting, hunt.Phase);
            Assert.AreEqual(WraithHunt.CooldownSeconds, hunt.Cooldown, 0.05f);
            hunt.Tick(Dt, new Vector3(0.0f, 0.0f, -2.0f), prey, false);
            Assert.AreEqual(WraithPhase.Drifting, hunt.Phase, "it hunted again at once");
            Assert.IsFalse(hunt.Hunting);
            for (float t = 0.0f; t < WraithHunt.CooldownSeconds + 0.1f; t += Dt)
            {
                hunt.Tick(Dt, new Vector3(0.0f, 0.0f, -2.0f), prey, false);
            }
            Assert.AreNotEqual(WraithPhase.Drifting, hunt.Phase, "it never hunts again");
        }

        [Test]
        public void ItLosesAPlayerWhoGetsAway()
        {
            var hunt = new WraithHunt();
            hunt.Tick(Dt, Vector3.zero, PreyAt(new Vector3(5.0f, 0.0f, 0.0f), Vector3.forward), false);
            Assert.AreEqual(WraithPhase.Stalking, hunt.Phase);
            hunt.Tick(Dt, Vector3.zero, PreyAt(new Vector3(WraithHunt.LoseMetres + 1.0f, 0.0f, 0.0f), Vector3.forward), false);
            Assert.AreEqual(WraithPhase.Drifting, hunt.Phase);
        }

        [Test]
        public void ThePreyDyingLetsGo()
        {
            var hunt = new WraithHunt();
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.forward);
            RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), prey, WraithPhase.Lunging, 3.0f);
            for (float t = 0.0f; t < WraithHunt.LungeSeconds + 0.1f; t += Dt)
            {
                hunt.Tick(Dt, Vector3.zero, prey, true);
            }
            Assert.AreEqual(WraithPhase.Biting, hunt.Phase);
            hunt.Tick(Dt, Vector3.zero, default, true);
            Assert.AreEqual(WraithPhase.Thrown, hunt.Phase, "it went on biting a dead body");
        }

        /// <summary>
        /// Owner, 2026-11-11: "he always uses the bite as his first attack but after that it is 50/50 whether he does the punch or bite, but make
        /// it so if he is fighting a large opponent like crimson claw for example then he only uses the punch".
        /// </summary>
        [Test]
        public void TheFirstAttackIsABiteThenHalfArePunchesAndALargeBodyIsOnlyPunched()
        {
            Assert.AreEqual(WraithAttack.Bite, WraithHunt.Choose(0, false, 0.0), "the first attack is not the bite");
            Assert.AreEqual(WraithAttack.Bite, WraithHunt.Choose(0, false, 0.99));
            Assert.AreEqual(WraithAttack.Punch, WraithHunt.Choose(0, true, 0.99), "a large body was bitten");
            Assert.AreEqual(WraithAttack.Punch, WraithHunt.Choose(3, true, 0.99), "a large body was bitten");
            var dice = new System.Random(7);
            int punches = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (WraithHunt.Choose(1 + i, false, dice.NextDouble()) == WraithAttack.Punch) punches++;
            }
            Assert.That(punches, Is.InRange(450, 550), "after the first, punch or bite is not 50/50");
        }

        [Test]
        public void ALargeBodyIsPunchedFromTheFirstAttackAndThePunchLandsAsTheSwingEnds()
        {
            var hunt = new WraithHunt { Bold = true };
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.back);
            prey.Large = true;
            prey.Radius = 1.0f;
            Vector3 self = RunUntil(hunt, new Vector3(0.0f, 0.0f, -6.0f), prey, WraithPhase.WindingUp, 6.0f);
            Assert.AreEqual(WraithPhase.WindingUp, hunt.Phase, "it never swung at the large body");
            Assert.AreEqual(WraithAttack.Punch, hunt.Attack);
            float damage = 0.0f;
            for (float t = 0.0f; t < WraithHunt.PunchSwingSeconds + 0.1f && hunt.Phase == WraithPhase.WindingUp; t += Dt)
            {
                hunt.Tick(Dt, self, prey, true);
                damage += hunt.DamageDue;
            }
            Assert.AreEqual(WraithPhase.Punching, hunt.Phase, "the swing did not go through");
            Assert.AreEqual(WraithHunt.PunchDamage, damage, 1e-4f, "the blow did not land once, as the swing ended");
            RunUntil(hunt, self, prey, WraithPhase.Drifting, WraithHunt.PunchFollowSeconds + 0.2f);
            Assert.AreEqual(WraithPhase.Drifting, hunt.Phase, "it did not come back to its stance");
            Assert.Greater(hunt.Cooldown, 0.0f, "it rests after a punch as after a bite");
        }

        [Test]
        public void APunchThatMissesDoesNoDamage()
        {
            var hunt = new WraithHunt { Bold = true };
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.back);
            prey.Large = true;
            Vector3 self = RunUntil(hunt, new Vector3(0.0f, 0.0f, -6.0f), prey, WraithPhase.WindingUp, 6.0f);
            float damage = 0.0f;
            for (float t = 0.0f; t < WraithHunt.PunchSwingSeconds + 0.1f; t += Dt)
            {
                hunt.Tick(Dt, self, prey, false);
                damage += hunt.DamageDue;
            }
            Assert.AreEqual(0.0f, damage, "a swing that missed hurt");
        }

        [Test]
        public void ANewPreyIsBittenFirstAgain()
        {
            var hunt = new WraithHunt { Bold = true, RestSeconds = 0.0f, Dice = new System.Random(1) };
            WraithHunt.Prey prey = PreyAt(Vector3.zero, Vector3.back);
            RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), prey, WraithPhase.Lunging, 3.0f);
            Assert.AreEqual(WraithAttack.Bite, hunt.Attack, "the first attack was not a bite");
            for (int attack = 0; attack < 20; attack++)
            {
                RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), prey, WraithPhase.Drifting, 6.0f);
                hunt.NewPrey();
                RunUntil(hunt, new Vector3(0.0f, 0.0f, -2.0f), prey, WraithPhase.Stalking, 2.0f);
                for (float t = 0.0f; t < 2.0f && hunt.Phase == WraithPhase.Stalking; t += Dt)
                {
                    hunt.Tick(Dt, new Vector3(0.0f, 0.0f, -2.0f), prey, false);
                }
                Assert.AreEqual(WraithAttack.Bite, hunt.Attack, "a new prey's first attack was not a bite");
            }
        }
    }
}
