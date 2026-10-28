using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The three staffs on the real body (docs/STAFFS.md): each held in the right hand, its fingers closed on it, and on another player's copy;
    /// thrust out to shoot; the Arcane Staff's click one orb and its hold two and three, paid by the orb; the Fireball Staff's hold growing the
    /// ball and its blast for up to a second and a half; the Ice Shard Staff shooting on while held at its pace; and a copy showing a charge.
    /// </summary>
    public sealed class StaffPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public float Taken;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Taken += amount;
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
            foreach (ArcaneOrbProjectile b in Object.FindObjectsByType<ArcaneOrbProjectile>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            foreach (FireballProjectile f in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            foreach (IceShardProjectile s in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
            foreach (CursedOrbs c in Object.FindObjectsByType<CursedOrbs>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
                _made.Add(ground);
            }
            // The plain body (a networked one would want a session), with the three spells the staffs shoot, as PlayerAbilityWiring gives them.
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<FireballCaster>().FireballPrefab =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            player.AddComponent<IceShardCaster>().ShardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/IceShard.prefab");
            player.AddComponent<ArcaneOrbCaster>();
            return player;
#else
            return null;
#endif
        }

        private StaffItem Equip(GameObject player, string staff)
        {
#if UNITY_EDITOR
            string path = staff == ItemRegistry.ArcaneStaff ? "Assets/CityX/Weapons/ArcaneStaff.prefab"
                : staff == ItemRegistry.FireballStaff ? "Assets/CityX/Weapons/FireballStaff.prefab"
                : "Assets/CityX/Weapons/IceShardStaff.prefab";
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Assert.IsNotNull(model, $"no {path} -- rebuild the scene");
            _made.Add(model);
            var held = model.GetComponent<StaffItem>();
            held.OnEquip(player, ItemInstanceData.None);
            return held;
#else
            return null;
#endif
        }

        /// <summary>Holds the click (or not) for <paramref name="seconds"/>, the body standing still, the camera behind it looking ahead.</summary>
        private static IEnumerator Click(GameObject player, IEquippableStaff staff, bool held, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.6f - player.transform.forward * 4.0f;
                staff.Tick(held, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
        }

        private static T[] Live<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [UnityTest]
        public IEnumerator EachStaffIsHeldInTheRightHandAndByACopy()
        {
            foreach (string id in new[] { ItemRegistry.ArcaneStaff, ItemRegistry.FireballStaff, ItemRegistry.IceShardStaff })
            {
                GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
                StaffItem staff = Equip(player, id);
                Transform hand = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                Assert.AreEqual(hand, staff.transform.parent, $"{id}: in the right hand");
                Assert.AreEqual(staff.transform, player.GetComponent<PlayerLocomotion>().Staff, $"{id}: the body poses it");
                Assert.IsTrue(ReferenceEquals(player.GetComponent<PlayerInputReader>().EquippedStaff, staff), $"{id}: the click is the staff's");
                Assert.IsNotNull(staff.Tip, $"{id}: shots need a head to leave from");

                GameObject copy = Body(new Vector3(4.0f, 0.05f, 0.0f));
                GameObject model = Object.Instantiate(staff.gameObject);
                _made.Add(model);
                model.GetComponent<ICosmeticMountable>().MountCosmetic(copy);
                Transform copyHand = copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                Assert.AreEqual(copyHand, model.transform.parent, $"{id}: a copy holds it in its right hand too");
                Assert.AreEqual(model.transform, copy.GetComponent<PlayerLocomotion>().Staff);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ShootingThrustsTheStaffOutBeforeTheChest()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Equip(player, ItemRegistry.ArcaneStaff);
            yield return Click(player, staff, false, 0.5f);
            Transform hand = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            float carried = Vector3.Dot(hand.position - player.transform.position, player.transform.forward);
            yield return Click(player, staff, true, 0.4f);
            Assert.Greater(player.GetComponent<PlayerLocomotion>().StaffRaise, 0.99f, "charging, the staff is up");
            float casting = Vector3.Dot(hand.position - player.transform.position, player.transform.forward);
            Assert.Greater(casting - carried, 0.25f, "the hand goes out before the chest");
            float lean = Vector3.Angle(staff.transform.up, Vector3.up);
            Assert.AreEqual(PlayerLocomotion.StaffCastLeanDegrees, lean, 8.0f, "the head tipped forward");
        }

        [UnityTest]
        public IEnumerator TheArcaneStaffsClickPlacesOneOrbAndItsHoldTwoAndThreePaidByTheOrb()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Equip(player, ItemRegistry.ArcaneStaff);
            PlayerVitals vitals = player.GetComponent<PlayerVitals>();
            int[] expected = { 1, 2, 3 };
            float[] held = { Step, 0.35f, 0.6f };
            for (int i = 0; i < 3; i++)
            {
                float mana = vitals.Mana;
                yield return Click(player, staff, true, held[i]);
                yield return Click(player, staff, false, 0.25f);
                ArcaneOrbProjectile[] balls = Live<ArcaneOrbProjectile>().Where(b => !b.Cosmetic).ToArray();
                Assert.AreEqual(1, balls.Length, $"one shot for hold {held[i]} s");
                Assert.AreEqual(expected[i], balls[0].Orbs, $"held {held[i]} s");
                Assert.AreEqual(mana - ArcaneOrbRules.StaffManaFor(expected[i]), vitals.Mana, 0.5f, "paid by the orb");
                Object.Destroy(balls[0].gameObject);
                yield return Click(player, staff, false, 0.6f);
            }
        }

        [UnityTest]
        public IEnumerator TheFireballStaffsHoldGrowsTheBallAndItsBlastForASecondAndAHalf()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Equip(player, ItemRegistry.FireballStaff);
            PlayerVitals vitals = player.GetComponent<PlayerVitals>();

            float mana = vitals.Mana;
            yield return Click(player, staff, true, Step);
            yield return Click(player, staff, false, 0.2f);
            FireballProjectile plain = Live<FireballProjectile>().Single(f => !f.Cosmetic);
            Assert.AreEqual(1.0f, plain.Size, 1e-3f, "a click: a plain fireball");
            Assert.AreEqual(0.0f, plain.BlastRadius, 1e-3f);
            Assert.AreEqual(mana - FireballCaster.StaffLeastMana, vitals.Mana, 0.5f);
            Object.Destroy(plain.gameObject);
            yield return Click(player, staff, false, 0.6f);

            mana = vitals.Mana;
            yield return Click(player, staff, true, 3.0f);
            yield return Click(player, staff, false, 0.2f);
            FireballProjectile full = Live<FireballProjectile>().Single(f => !f.Cosmetic);
            Assert.AreEqual(FireballCaster.StaffMostSize, full.Size, 0.01f, "full after 1.5 s, and no bigger for holding on");
            Assert.AreEqual(FireballCaster.StaffMostBlastRadius, full.BlastRadius, 0.01f);
            Assert.AreEqual(mana - FireballCaster.StaffMostMana, vitals.Mana, 0.5f);
        }

        [UnityTest]
        public IEnumerator AChargedFireballsBlastBurnsWhoeverStandsNearWhereItLands()
        {
#if UNITY_EDITOR
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 1.0f, 8.0f);
            _made.Add(wall);
            var near = new GameObject("Near");
            near.transform.position = new Vector3(2.0f, 1.0f, 7.5f);
            near.AddComponent<BoxCollider>();
            Dummy nearDummy = near.AddComponent<Dummy>();
            _made.Add(near);
            var far = new GameObject("Far");
            far.transform.position = new Vector3(6.0f, 1.0f, 7.5f);
            far.AddComponent<BoxCollider>();
            Dummy farDummy = far.AddComponent<Dummy>();
            _made.Add(far);
            Physics.SyncTransforms();
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            FireballProjectile.Spawn(prefab, new Vector3(0.0f, 1.0f, 0.0f), Vector3.forward * FireballCaster.FireballSpeed, 0, null,
                size: FireballCaster.StaffMostSize, blastRadius: FireballCaster.StaffMostBlastRadius);
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Assert.Greater(nearDummy.Taken, 0.0f, "two metres from the landing, inside the blast");
            Assert.AreEqual(0.0f, farDummy.Taken, "six metres off, outside it");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheIceShardStaffShootsOnWhileHeldAtItsPaceAndOnceForAClick()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Equip(player, ItemRegistry.IceShardStaff);
            PlayerVitals vitals = player.GetComponent<PlayerVitals>();
            float mana = vitals.Mana;
            int before = Live<IceShardProjectile>().Length;
            yield return Click(player, staff, true, Step);
            yield return Click(player, staff, false, 0.3f);
            Assert.AreEqual(before + 1, Live<IceShardProjectile>().Length, "a click: one shard");

            before = Live<IceShardProjectile>().Length;
            yield return Click(player, staff, true, 1.0f);
            int shot = Live<IceShardProjectile>().Length - before;
            Assert.AreEqual(4, shot, 1, "held a second: one every quarter second");
            Assert.AreEqual(mana - IceShardCaster.StaffManaPerShard * (shot + 1), vitals.Mana, 0.5f, "each shard paid for");
        }

        [UnityTest]
        public IEnumerator ACopyShowsTheChargeGlowingAtTheStaffsHead()
        {
            GameObject copy = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Equip(copy, ItemRegistry.ArcaneStaff);
            var caster = copy.GetComponent<ArcaneOrbCaster>();
            caster.PlayRemote(new AbilityEvent(ArcaneOrbCaster.ChargeStarted, copy.transform.position, Vector3.zero));
            for (int i = 0; i < 36; i++)
            {
                copy.GetComponent<PlayerLocomotion>().Tick(new LocomotionInput(), Step);
                yield return null;
            }
            StaffChargeGlow glow = staff.Tip.GetComponentInChildren<StaffChargeGlow>();
            Assert.IsNotNull(glow, "the charge glows at the head");
            Assert.Greater(glow.Strength, 0.5f);
            Assert.AreEqual(3, glow.LitSatellites, "0.6 s in: three orbs circle it");
            Assert.IsTrue(copy.GetComponent<PlayerLocomotion>().StaffCasting, "and the staff is held out");
            caster.PlayRemote(new AbilityEvent(ArcaneOrbCaster.ChargeStopped, copy.transform.position, Vector3.zero));
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0.0f, glow.Strength, 1e-3f, "let go without a shot, it goes out");
        }
    }
}
