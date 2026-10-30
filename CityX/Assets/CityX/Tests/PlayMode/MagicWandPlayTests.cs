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
    /// The Magic Wand and the Magic Staff on the real body (docs/MAGIC_WAND.md): the wand in the right hand, pointed rather than stood upright, the
    /// body's spell focus while held, and on another player's copy; its click a speck from its tip once a second while held, landing on what the
    /// crosshair is on; the staff's speck harder and slower from its head; a spell cast with the wand in hand hurting 5% more for 10% less mana;
    /// and a copy showing another player's speck with its wand pointed. A spell leaving the wand's tip from the real keys is
    /// MagicWandRealInputPlayTests.
    /// </summary>
    public sealed class MagicWandPlayTests
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
            foreach (MagicSpeck s in Object.FindObjectsByType<MagicSpeck>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
            foreach (FireballProjectile f in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
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
            // The plain body (a networked one would want a session), with the speck and the fireball, as PlayerAbilityWiring gives them.
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<MagicSpeckCaster>();
            player.AddComponent<FireballCaster>().FireballPrefab =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            return player;
#else
            return null;
#endif
        }

        private StaffItem Hold(GameObject player, string path, bool copy = false)
        {
#if UNITY_EDITOR
            GameObject model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Assert.IsNotNull(model, $"no {path} -- rebuild the scene");
            _made.Add(model);
            var held = model.GetComponent<StaffItem>();
            if (copy)
            {
                held.MountCosmetic(player);
            }
            else
            {
                held.OnEquip(player, ItemInstanceData.None);
            }
            return held;
#else
            return null;
#endif
        }

        private const string WandPath = "Assets/CityX/Weapons/MagicWand.prefab";
        private const string StaffPath = "Assets/CityX/Weapons/MagicStaff.prefab";

        /// <summary>A wall to shoot at, <paramref name="metres"/> ahead of the body, tall enough for the crosshair's line to meet.</summary>
        private Dummy Wall(float metres)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.5f, metres);
            wall.transform.localScale = new Vector3(4.0f, 5.0f, 0.5f);
            _made.Add(wall);
            Physics.SyncTransforms();
            return wall.AddComponent<Dummy>();
        }

        /// <summary>Holds the click (or not) for <paramref name="seconds"/>, the body standing still, the camera behind it looking ahead.</summary>
        private static IEnumerator Click(GameObject player, IEquippableStaff held, bool down, float seconds)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 eye = player.transform.position + Vector3.up * 2.6f - player.transform.forward * 4.0f;
                held.Tick(down, eye, player.transform.forward, player, Step);
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheWandIsHeldInTheRightHandPointedByTheBodyAndByACopy()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem wand = Hold(player, WandPath);
            Assert.IsInstanceOf<MagicWand>(wand);
            Transform hand = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.AreEqual(hand, wand.transform.parent, "in the right hand");
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Assert.AreEqual(wand.transform, locomotion.Wand, "the body points it");
            Assert.IsNull(locomotion.Staff, "a wand is not stood upright as a staff is");
            Assert.AreEqual(wand.Tip, locomotion.WandTip);
            Assert.IsTrue(ReferenceEquals(player.GetComponent<PlayerInputReader>().EquippedStaff, wand), "the click is the wand's");
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(ReferenceEquals(vitals.SpellFocus, wand), "held, every spell is cast through it");

            GameObject copy = Body(new Vector3(4.0f, 0.05f, 0.0f));
            StaffItem shown = Hold(copy, WandPath, copy: true);
            Assert.AreEqual(shown.transform, copy.GetComponent<PlayerLocomotion>().Wand, "another player's copy holds it too");
            Assert.IsNull(copy.GetComponent<PlayerVitals>().SpellFocus, "a copy's model is no one's focus");
            yield return null;

            wand.OnUnequip(player);
            yield return null;
            Assert.IsNull(locomotion.Wand);
            Assert.IsNull(vitals.SpellFocus, "put away, it boosts nothing");
        }

        [UnityTest]
        public IEnumerator HeldTheWandShootsASpeckFromItsTipOnceASecondAtTheCrosshair()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem wand = Hold(player, WandPath);
            Dummy wall = Wall(12.0f);
            var caster = player.GetComponent<MagicSpeckCaster>();
            var origins = new List<Vector3>();
            System.Action<AbilityEvent> heard = e => origins.Add(e.Origin);
            caster.Fired += heard;
            yield return Click(player, wand, true, Step);
            Assert.AreEqual(1, origins.Count, "the click did not shoot at once");
            yield return null;
            Assert.Less(Vector3.Distance(origins[0], wand.Tip.position), 0.05f, "the speck did not leave the wand's tip");
            Assert.Greater(player.GetComponent<PlayerLocomotion>().WandRaise, 0.99f, "the wand was not snapped out with the shot");

            yield return Click(player, wand, true, 2.2f - 2.0f * Step);
            yield return Click(player, wand, false, 0.6f);
            caster.Fired -= heard;
            Assert.AreEqual(3, origins.Count, "held for 2.2 s, the wand should shoot at 0, 1 and 2 s");
            Assert.AreEqual(3, wall.Hits, "every speck should land on what the crosshair is on");
            int spellPower = player.GetComponent<PlayerVitals>().Stats[StatId.SpellPower];
            Assert.AreEqual(3 * CombatTuning.DamageFor(MagicSpeckRules.WandDamage, spellPower), wall.Taken, 0.01f);
        }

        [UnityTest]
        public IEnumerator TheStaffShootsAHarderSpeckMoreSlowlyFromItsHead()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            StaffItem staff = Hold(player, StaffPath);
            Assert.IsInstanceOf<MagicStaff>(staff);
            Assert.AreEqual(staff.transform, player.GetComponent<PlayerLocomotion>().Staff, "held as every staff is");
            Assert.IsNull(player.GetComponent<PlayerVitals>().SpellFocus, "the staff boosts no spell");
            Dummy wall = Wall(12.0f);
            var caster = player.GetComponent<MagicSpeckCaster>();
            int before = caster.Shots;
            yield return Click(player, staff, true, 2.2f);
            yield return Click(player, staff, false, 0.6f);
            Assert.AreEqual(2, caster.Shots - before, "held for 2.2 s, the staff should shoot at 0 and 1.25 s");
            int spellPower = player.GetComponent<PlayerVitals>().Stats[StatId.SpellPower];
            Assert.AreEqual(2 * CombatTuning.DamageFor(MagicSpeckRules.StaffDamage, spellPower), wall.Taken, 0.01f);
        }

        [UnityTest]
        public IEnumerator ASpellCastWithTheWandInHandHurtsFivePercentMoreForATenthLessMana()
        {
            GameObject player = Body(new Vector3(0.0f, 0.05f, 0.0f));
            Dummy wall = Wall(8.0f);
            var vitals = player.GetComponent<PlayerVitals>();
            var fireball = player.GetComponent<FireballCaster>();
            fireball.Bind();
            Vector3 from = player.transform.position + Vector3.up * 1.8f;

            IEnumerator CastAndLand()
            {
                Assert.IsTrue(fireball.TryCast(from, Vector3.forward, player.GetComponentsInChildren<Collider>()));
                for (int i = 0; i < 90; i++)
                {
                    fireball.Tick(Step);
                    yield return null;
                }
                fireball.Tick(fireball.CooldownSeconds);
            }

            float mana = vitals.Mana;
            yield return CastAndLand();
            float plain = wall.Taken;
            Assert.Greater(plain, 0.0f, "the fireball missed the wall");
            Assert.AreEqual(mana - FireballCaster.SpellManaCost, vitals.Mana, 0.5f);

            Hold(player, WandPath);
            mana = vitals.Mana;
            yield return CastAndLand();
            float boosted = wall.Taken - plain;
            Assert.AreEqual(plain * 1.05f, boosted, 0.01f, "a spell cast with the wand in hand should hurt 5% more");
            Assert.AreEqual(mana - FireballCaster.SpellManaCost * 0.9f, vitals.Mana, 0.5f, "and pay 10% less mana");
        }

        [UnityTest]
        public IEnumerator ACopyShowsAnotherPlayersSpeckWithItsWandPointed()
        {
            GameObject copy = Body(new Vector3(0.0f, 0.05f, 0.0f));
            Hold(copy, WandPath, copy: true);
            Dummy wall = Wall(10.0f);
            var caster = copy.GetComponent<MagicSpeckCaster>();
            int heard = 0;
            caster.Fired += _ => heard++;
            caster.PlayRemote(new AbilityEvent(MagicSpeckRules.WandKind, copy.transform.position + new Vector3(0.3f, 1.9f, 0.6f), Vector3.forward,
                MagicSpeckRules.Speed));
            MagicSpeck[] specks = Object.FindObjectsByType<MagicSpeck>(FindObjectsSortMode.None);
            Assert.AreEqual(1, specks.Length, "the copy showed no speck");
            Assert.IsTrue(specks[0].Cosmetic, "another player's speck must hurt nobody here");
            var locomotion = copy.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 12; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            Assert.Greater(locomotion.WandRaise, 0.9f, "the copy's wand was not pointed");
            for (int i = 0; i < 40; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            Assert.AreEqual(0, wall.Hits, "a shown speck hurt something");
            Assert.AreEqual(0, heard, "a copy must never send a shot back");
        }
    }
}
