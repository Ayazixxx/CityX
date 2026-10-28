using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The water staff on the real body (docs/STAFFS.md): held on a body in its line it ticks 3 damage every third of a second and nothing off
    /// the line; held on a player it throws them limp and pushes them along; the staff is thrust out while it sprays and the jet stops when it
    /// is let go, or when the mana runs out; another player's copy shows the jet on its staff, pointed where the wearer points it.
    /// </summary>
    public sealed class WaterStaffPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;
            public int Hits;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
                Hits++;
                return HitResponse.Landed;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (WaterJet jet in Object.FindObjectsByType<WaterJet>(FindObjectsSortMode.None)) Object.Destroy(jet.gameObject);
        }

        private GameObject Body(Vector3 at, float yawDegrees = 0.0f)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.Euler(0.0f, yawDegrees, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            if (player.GetComponent<WaterJetCaster>() == null)
            {
                player.AddComponent<WaterJetCaster>();
            }
            return player;
#else
            return null;
#endif
        }

        private StaffItem Staff()
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/WaterStaff.prefab"));
            Assert.IsNotNull(model, "no WaterStaff.prefab -- rebuild the scene");
            _made.Add(model);
            return model.GetComponent<StaffItem>();
#else
            return null;
#endif
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            go.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            _made.Add(go);
            return go.AddComponent<Dummy>();
        }

        /// <summary>Holds (or not) the staff's click for <paramref name="seconds"/>, the crosshair straight ahead of the body.</summary>
        private static IEnumerator Hold(GameObject player, IEquippableStaff staff, bool held, float seconds, System.Action each = null)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.4f - player.transform.forward * 4.0f;
                staff.Tick(held, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                each?.Invoke();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HeldOnABodyInItsLineItTicksThreeDamageAndNothingOffTheLine()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var vitals = player.GetComponent<PlayerVitals>();
            // On the jet's line: the staff's head is held out at about chest height and the crosshair, straight ahead, is level with it.
            Dummy ahead = Target(new Vector3(0.0f, 2.2f, 7.0f));
            Dummy beside = Target(new Vector3(3.0f, 2.2f, 7.0f));
            yield return Hold(player, staff, false, 0.3f);
            float mana = vitals.Mana;
            yield return Hold(player, staff, true, 1.0f);
            Assert.AreEqual(WaterJetRules.TicksIn(1.0f - Step), ahead.Hits, 1, "the body in the jet should be hit every third of a second");
            Assert.AreEqual(ahead.Hits * WaterJetRules.DamagePerTick, ahead.Taken, 0.01f, "each tick is 3");
            Assert.AreEqual(0, beside.Hits, "the body beside the line should be untouched");
            Assert.Less(vitals.Mana, mana - WaterJetRules.ManaPerSecond * 0.8f, "the jet should drink mana");
        }

        [UnityTest]
        public IEnumerator HeldOnAPlayerItThrowsThemLimpAndPushesThemAlong()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            GameObject victim = Body(new Vector3(0.0f, 0.0f, 6.0f), 180.0f);
            var ragdoll = victim.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "setup: the body has no ragdoll");
            yield return Hold(player, staff, false, 0.5f);
            float zBefore = victim.transform.position.z;
            float healthBefore = victim.GetComponent<PlayerVitals>().Health;
            yield return Hold(player, staff, true, 0.5f);
            Assert.IsTrue(ragdoll.IsLimp, "the jet should throw a player limp");
            yield return Hold(player, staff, true, 1.5f);
            Assert.Greater(ragdoll.Torso.position.z, zBefore + 1.0f, "the jet should push the body along while it is down");
            Assert.Less(victim.GetComponent<PlayerVitals>().Health, healthBefore, "and hurt a little");
        }

        [UnityTest]
        public IEnumerator TheStaffIsThrustOutWhileSprayingAndTheJetStopsWhenLetGoOrOutOfMana()
        {
            GameObject player = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(player, ItemInstanceData.None);
            var caster = player.GetComponent<WaterJetCaster>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            yield return Hold(player, staff, true, 0.5f);
            Assert.IsTrue(caster.IsSpraying);
            Assert.IsTrue(locomotion.StaffCasting, "the staff should be held out while it sprays");
            Assert.Greater(locomotion.StaffRaise, 0.9f);
            WaterJet jet = Object.FindObjectsByType<WaterJet>(FindObjectsSortMode.None)[0];
            Assert.IsTrue(jet.IsRunning, "the jet should be drawn");
            yield return Hold(player, staff, false, 0.3f);
            Assert.IsFalse(caster.IsSpraying, "letting go should stop the jet");
            Assert.IsFalse(jet.IsRunning);
            Assert.Less(locomotion.StaffRaise, 0.1f, "and the staff should come back down");

            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(vitals.TrySpendMana(vitals.Mana - 1.0f));
            yield return Hold(player, staff, true, 0.1f);
            Assert.IsTrue(caster.IsSpraying, "with a little mana it sprays");
            yield return Hold(player, staff, true, 0.5f);
            Assert.IsFalse(caster.IsSpraying, "out of mana, the jet should stop");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyShowsTheJetOnItsStaffPointedWhereTheWearerPointsIt()
        {
            GameObject owner = Body(Vector3.zero);
            StaffItem staff = Staff();
            staff.OnEquip(owner, ItemInstanceData.None);
            GameObject copy = Body(new Vector3(20.0f, 0.0f, 0.0f));
            StaffItem copyStaff = Staff();
            copyStaff.MountCosmetic(copy);
            var ownerCaster = owner.GetComponent<WaterJetCaster>();
            var copyCaster = copy.GetComponent<WaterJetCaster>();
            var sent = new List<AbilityEvent>();
            ownerCaster.Fired += evt =>
            {
                sent.Add(evt);
                copyCaster.PlayRemote(evt);
            };
            yield return Hold(owner, staff, true, 0.5f);
            Assert.IsTrue(sent.Exists(e => e.Kind == WaterJetCaster.JetStarted), "the copy was never told the jet started");
            Assert.Greater(sent.FindAll(e => e.Kind == WaterJetCaster.JetAimed).Count, 2, "the copy should be told where the jet points, again and again");
            WaterJet copyJet = null;
            foreach (WaterJet jet in Object.FindObjectsByType<WaterJet>(FindObjectsSortMode.None))
            {
                if (jet.transform.IsChildOf(copy.transform)) copyJet = jet;
            }
            Assert.IsNotNull(copyJet, "no jet on the copy's staff");
            Assert.IsTrue(copyJet.IsRunning, "the copy's jet should run");
            Assert.IsTrue(copy.GetComponent<PlayerLocomotion>().StaffCasting, "the copy's staff should be held out");
            yield return Hold(owner, staff, false, 0.2f);
            Assert.IsTrue(sent.Exists(e => e.Kind == WaterJetCaster.JetStopped));
            Assert.IsFalse(copyJet.IsRunning, "the copy's jet should stop with the wearer's");
        }
    }
}
