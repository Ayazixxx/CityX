using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE RPG (owner, 2026-11-19: "make a basic rpg rocket launcher that is animated"; docs/RPG.md): the rocket's flight (slow out of the muzzle,
    /// the motor lighting a few metres out and driving it on, nearly straight), the back-blast (close behind, not beside or in front), the
    /// launcher's profile and reload motion, and a stranger's packet tolerated.
    /// </summary>
    public sealed class RPGTests
    {
        [Test]
        public void TheRoundLeavesSlowThenTheMotorLightsAndDrivesItToItsTopSpeed()
        {
            Assert.AreEqual(RocketRules.LaunchSpeed, RocketRules.SpeedAt(0.0f));
            Assert.AreEqual(RocketRules.LaunchSpeed, RocketRules.SpeedAt(RocketRules.IgniteSeconds * 0.9f), "it sped up before the motor lit");
            Assert.Greater(RocketRules.SpeedAt(RocketRules.IgniteSeconds + 0.1f), RocketRules.LaunchSpeed);
            Assert.AreEqual(RocketRules.TopSpeed, RocketRules.SpeedAt(5.0f));
            float lit = RocketRules.LaunchSpeed * RocketRules.IgniteSeconds;
            Assert.That(lit, Is.InRange(4.0f, 12.0f), "the motor lights a few metres out, as the RPG-7's does");
        }

        [Test]
        public void ItFliesNearlyStraightAndCanBeWatchedAllTheWay()
        {
            Vector3 position = Vector3.zero, velocity = Vector3.forward * RocketRules.LaunchSpeed;
            float t = 0.0f;
            while (position.z < 100.0f && t < 10.0f)
            {
                RocketRules.Step(ref position, ref velocity, t, 1.0f / 60.0f);
                t += 1.0f / 60.0f;
            }
            Assert.That(t, Is.InRange(1.0f, 3.0f), $"100 m took {t:0.00} s: too fast to see, or too slow to be a rocket");
            Assert.That(position.y, Is.InRange(-2.0f, 0.0f), $"it fell {-position.y:0.00} m over 100 m");
            Assert.AreEqual(0.0f, position.x, 1e-4f, "it wandered off its line");
        }

        [Test]
        public void TheBackBlastHurtsCloseBehindNotBesideNorInFrontNorFarOff()
        {
            Vector3 forward = Vector3.forward;
            Assert.AreEqual(1.0f, RocketRules.BackblastShare(Vector3.zero, forward), 1e-4f);
            Assert.Greater(RocketRules.BackblastShare(new Vector3(0.0f, 0.0f, -2.0f), forward), 0.5f, "two metres straight behind");
            Assert.AreEqual(0.0f, RocketRules.BackblastShare(new Vector3(2.0f, 0.0f, 0.0f), forward), "beside the shooter");
            Assert.AreEqual(0.0f, RocketRules.BackblastShare(new Vector3(0.0f, 0.0f, 2.0f), forward), "in front");
            Assert.AreEqual(0.0f, RocketRules.BackblastShare(new Vector3(0.0f, 0.0f, -RocketRules.BackblastMetres - 0.5f), forward), "past its reach");
        }

        [Test]
        public void TheLauncherHoldsOneRoundFiresARocketAndReloadsAtOnce()
        {
            FirearmProfile rpg = FirearmProfile.RPG();
            Assert.AreEqual(ItemRegistry.RPG, rpg.ItemId);
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.RPG, out ItemDefinition definition));
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            Assert.AreEqual(Rarity.VeryRare, ItemRarity.Of(ItemRegistry.RPG), "the owner's list: RPG under Very Rare");
            Assert.AreEqual(1, rpg.MagazineSize);
            Assert.IsTrue(rpg.FiresRocket);
            Assert.IsTrue(rpg.ReloadsAfterEveryShot, "a single shot reloads the moment it fires");
            Assert.IsTrue(rpg.IsRifle, "it is shouldered as a rifle is");
            Assert.AreEqual(0.0f, rpg.CasingLength, "a rocket throws no case");
            Assert.Less(rpg.BackblastLocal.z, rpg.ButtLocal.z, "the back-blast leaves behind the shoulder");
        }

        [Test]
        public void ItsReloadIsOneMotionWithTheRoundSeatedThenTheHammerCocked()
        {
            BakedMotion motion = BakedMotion.Load(FirearmProfile.RPG().MotionResource);
            Assert.IsNotNull(motion, "no RPG reload: run SourceArt/RPG/rpg_build_all.py");
            Assert.IsTrue(motion.HasSegment(GunReload.EmptySegment) && motion.HasSegment(GunReload.TacticalSegment));
            float seat = -1.0f, rack = -1.0f;
            foreach (System.Collections.Generic.KeyValuePair<string, float> e in motion.EventsIn(GunReload.EmptySegment))
            {
                if (GunReload.EventKind(e.Key) == GunReload.SeatEvent) seat = e.Value;
                if (GunReload.EventKind(e.Key) == GunReload.RackEvent) rack = e.Value;
            }
            Assert.Greater(seat, 0.0f, "the round is never seated");
            Assert.Greater(rack, seat, "the hammer is cocked before the round is in");
            Assert.That(motion.SegmentSeconds(GunReload.EmptySegment), Is.InRange(1.8f, 3.5f));
        }

        [Test]
        public void AStrangersPacketIsTolerated()
        {
            var body = new GameObject("Copy");
            try
            {
                var launcher = body.AddComponent<RocketLauncher>();
                launcher.PlayRemote(new AbilityEvent(RocketLauncher.Launched, new Vector3(float.NaN, 0.0f, 0.0f), Vector3.forward));
                launcher.PlayRemote(new AbilityEvent(RocketLauncher.Launched, Vector3.zero, Vector3.zero));
                launcher.PlayRemote(new AbilityEvent(99, Vector3.zero, Vector3.forward));
                Assert.IsNull(Object.FindFirstObjectByType<FlyingRocket>(), "a rocket flew from a broken packet");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }
    }
}
