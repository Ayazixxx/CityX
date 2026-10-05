using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The multiplayer contract of the abilities (IReplicatedAbility) without a network: each one announces what it does when the OWNER uses it,
    /// showing another player's event never announces anything back, and the wire's numbers are checked before they are shown. The real
    /// sockets are NetworkedAbilitySyncPlayTests.
    /// </summary>
    public sealed class AbilityReplicationTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _made.Clear();
            foreach (IceShardProjectile shard in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(shard.gameObject);
            }
        }

        private GameObject Body()
        {
            var go = new GameObject("Body");
            _made.Add(go);
            return go;
        }

        private static List<AbilityEvent> Listen(IReplicatedAbility ability)
        {
            var heard = new List<AbilityEvent>();
            ability.Fired += heard.Add;
            return heard;
        }

        // ---- ids and the wire ----

        [Test]
        public void TheHashOfAnIdIsStableAndIdsDoNotCollide()
        {
            // Pinned values: the hash must be the same in every process and every build, or two machines would disagree about which ability a packet means.
            Assert.AreEqual(AbilityIds.Hash("fireball"), AbilityIds.Hash("fireball"));
            Assert.AreEqual(unchecked((int)0xE40C292Cu), AbilityIds.Hash("a"), "FNV-1a 32 of \"a\"");
            var seen = new HashSet<int>();
            foreach (string id in new[] { FireballCaster.Id, IceShardCaster.Id, PoisonBreathCaster.Id, SwordWaveCaster.Id, BloodrageAbility.Id, MeleeAttacker.SpinAbilityId })
            {
                Assert.IsTrue(seen.Add(AbilityIds.Hash(id)), $"ability id '{id}' collides with another");
            }
        }

        [Test]
        public void AnEventOffTheWireIsOnlyShownIfItsNumbersAreSane()
        {
            Vector3 caster = new Vector3(100.0f, 5.0f, 100.0f);
            Vector3 near = caster + new Vector3(1.0f, 1.0f, 0.5f);
            Assert.IsTrue(NetworkedAbilitySync.IsPlausible(near, Vector3.forward, 20.0f, 0.4f, 2, caster));
            Assert.IsTrue(NetworkedAbilitySync.IsPlausible(near, Vector3.zero, 0.0f, 0.0f, 0, caster), "an event with no direction (Bloodrage) is fine");

            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(new Vector3(float.NaN, 0, 0), Vector3.forward, 1, 1, 0, caster), "NaN origin");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, new Vector3(0, float.PositiveInfinity, 0), 1, 1, 0, caster), "infinite direction");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, Vector3.forward, float.NaN, 1, 0, caster), "NaN number");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, Vector3.forward, 1e9f, 1, 0, caster), "absurd speed");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, Vector3.forward * 40.0f, 1, 1, 0, caster), "not a direction");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, Vector3.forward, 1, 1, -1, caster), "negative count");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(near, Vector3.forward, 1, 1, 100000, caster), "huge count");
            Assert.IsFalse(NetworkedAbilitySync.IsPlausible(caster + new Vector3(500.0f, 0, 0), Vector3.forward, 1, 1, 0, caster), "spawned across the map from the caster");
        }

        // ---- the owner announces what it does ----

        [Test]
        public void TheFireballAnnouncesItselfWhereItLeavesTheHand()
        {
            var caster = Body().AddComponent<FireballCaster>();
            var heard = Listen(caster);

            Assert.IsTrue(caster.TryCast(new Vector3(1.0f, 2.0f, 3.0f), Vector3.forward * 5.0f, null));

            Assert.AreEqual(1, heard.Count, "a cast is one event");
            Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f + FireballCaster.SpawnForwardOffset).z, heard[0].Origin.z, 1e-4f, "it leaves from just in front of the hand");
            Assert.AreEqual(Vector3.forward, heard[0].Direction);
            Assert.AreEqual(FireballCaster.FireballSpeed, heard[0].A, 1e-4f);

            Assert.IsFalse(caster.TryCast(Vector3.zero, Vector3.forward, null), "a refused cast announces nothing");
            Assert.AreEqual(1, heard.Count);
        }

        [Test]
        public void TheIceShardsAnnounceEachShardAsItLeaves()
        {
            GameObject body = Body();
            var prefab = new GameObject("ShardPrefab");
            _made.Add(prefab);
            prefab.AddComponent<IceShardProjectile>();
            var caster = body.AddComponent<IceShardCaster>();
            caster.ShardPrefab = prefab;
            var heard = Listen(caster);

            caster.TryCast(Vector3.zero, Vector3.forward, null);
            Assert.AreEqual(1, heard.Count, "the first shard leaves at the press");
            caster.Tick(1.0f);
            Assert.AreEqual(4, heard.Count, "four shards, four events");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(i, heard[i].N, "N is which of the volley it is");
                Assert.AreEqual(caster.ShardSpeed, heard[i].A, 1e-4f);
                Assert.Greater(heard[i].B, caster.FollowThroughSeconds, "B carries how long the hand should stay up in all");
            }
        }

        [Test]
        public void ThePoisonGasAnnouncesItselfWhereItLeavesTheMouthAndHasACooldown()
        {
            var caster = Body().AddComponent<PoisonBreathCaster>();
            var heard = Listen(caster);

            Assert.IsTrue(caster.TryCast(new Vector3(1.0f, 2.0f, 3.0f), Vector3.forward, null));
            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(3.0f + PoisonBreathCaster.SpawnForwardOffset, heard[0].Origin.z, 1e-4f);
            Assert.AreEqual(caster.CloudSpeed, heard[0].A, 1e-4f);

            Assert.IsFalse(caster.TryCast(Vector3.zero, Vector3.forward, null), "cooldown");
            Assert.AreEqual(1, heard.Count);
            caster.Tick(caster.CooldownSeconds + 0.1f);
            Assert.IsTrue(caster.CanCast);
            Assert.IsTrue(caster.IsCasting == false, "the pose should have eased out");
        }

        [Test]
        public void BloodrageAnnouncesItsStartAndHowLongItLasts()
        {
            var rage = Body().AddComponent<BloodrageAbility>();
            var heard = Listen(rage);

            Assert.IsTrue(rage.TryActivate());
            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(BloodrageAbility.DurationSeconds, heard[0].A, 1e-4f);

            Assert.IsFalse(rage.TryActivate(), "already running");
            Assert.AreEqual(1, heard.Count, "a refused activation announces nothing");
        }

        // ---- showing another player's event never announces anything back ----

        [Test]
        public void ShowingAnotherPlayersAbilityNeverAnnouncesItAgain()
        {
            GameObject body = Body();
            var fireball = body.AddComponent<FireballCaster>();
            var ice = body.AddComponent<IceShardCaster>();
            var gas = body.AddComponent<PoisonBreathCaster>();
            var wave = body.AddComponent<SwordWaveCaster>();
            var rage = body.AddComponent<BloodrageAbility>();
            var melee = body.AddComponent<MeleeAttacker>();
            var all = new IReplicatedAbility[] { fireball, ice, gas, wave, rage, melee };
            var echoes = new List<AbilityEvent>();
            foreach (IReplicatedAbility ability in all)
            {
                ability.Fired += echoes.Add;
            }

            var evt = new AbilityEvent(0, Vector3.up, Vector3.forward, 20.0f, 0.5f, 0);
            foreach (IReplicatedAbility ability in all)
            {
                ability.PlayRemote(evt);
                ability.PlayRemote(new AbilityEvent(MeleeAttacker.SpinEventTurning, Vector3.up, Vector3.forward, 1.0f, 0.0f));
                ability.PlayRemote(new AbilityEvent(MeleeAttacker.SpinEventEnded, Vector3.up, Vector3.forward));
            }

            Assert.AreEqual(0, echoes.Count, "a replayed ability raised Fired, which would bounce it across the network for ever");
        }

        [Test]
        public void GarbageOffTheWireDoesNothingBadToAnyAbility()
        {
            GameObject body = Body();
            var all = new IReplicatedAbility[]
            {
                body.AddComponent<FireballCaster>(), body.AddComponent<IceShardCaster>(), body.AddComponent<PoisonBreathCaster>(), body.AddComponent<SwordWaveCaster>(),
                body.AddComponent<BloodrageAbility>(), body.AddComponent<MeleeAttacker>(),
            };
            foreach (IReplicatedAbility ability in all)
            {
                Assert.DoesNotThrow(() => ability.PlayRemote(new AbilityEvent(0, Vector3.zero, Vector3.zero)), $"{ability.AbilityId}: zero direction");
                Assert.DoesNotThrow(() => ability.PlayRemote(new AbilityEvent(99, Vector3.zero, Vector3.forward, -5.0f, -5.0f, -1)), $"{ability.AbilityId}: odd kind and numbers");
            }
        }

        [Test]
        public void TheRemoteCastPoseHoldsThenEasesOutThenSwitchesItselfOff()
        {
            GameObject body = Body();
            RemoteAbilityPose.Show(body, 0.3f, 0.4f);
            var pose = body.GetComponent<RemoteAbilityPose>();
            Assert.IsNotNull(pose);

            pose.Step(0.1f);
            Assert.AreEqual(1.0f, pose.Fraction, 1e-4f, "held");
            pose.Step(0.3f);   // 0.4 s in: 0.3 s left, easing
            Assert.Greater(pose.Fraction, 0.0f);
            Assert.Less(pose.Fraction, 1.0f);
            pose.Step(1.0f);
            Assert.AreEqual(0.0f, pose.Fraction, 1e-4f, "gone");
            Assert.IsFalse(pose.enabled, "it should switch itself off when idle");
        }
    }
}
