using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The riot shield (owner, 2026-11-18; docs/SHIELD.md) on the real player body and from the real mouse: held in the left hand and raised before
    /// the body; a bullet at the chest stops on it, one at the feet of a standing body does not, crouched it does; the body is not slowed; beside a
    /// pistol right-click raises it and nothing aims, a click shoots from the hip and takes it out of its guard for a moment; beside a rifle, a
    /// staff or a two-handed sword it is not raised; beside a wand or a one-handed sword it is.
    /// </summary>
    public sealed class RiotShieldPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();
        private Scene _scene;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private (PlayerLocomotion body, PlayerVitals vitals, Transform shield) Shielded()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            GameObject shield = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RiotShield.prefab"));
            Assert.IsNotNull(shield, "no RiotShield.prefab -- rebuild the scene");
            shield.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            return (player.GetComponent<PlayerLocomotion>(), player.GetComponent<PlayerVitals>(), shield.transform);
#else
            return (null, null, null);
#endif
        }

        private static IEnumerator Hold(PlayerLocomotion body, LocomotionInput input, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                body.Tick(input, Step);
                yield return null;
            }
        }

        private static IEnumerator Until(float seconds, System.Func<bool> done)
        {
            for (float t = 0.0f; t < seconds && !done(); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ItIsHeldInTheLeftHandOffTheGroundAndRaisedItStandsBeforeTheBodyFromTheShinsToTheHead()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            Animator animator = body.GetComponentInChildren<Animator>();
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.LeftHand), shield.parent, "the shield is not in the left hand");
            Assert.AreSame(ShieldTraits.Riot, vitals.ShieldTraits);
            Assert.AreSame(ShieldTraits.Riot, body.ShieldTraits);

            yield return Hold(body, default, 0.5f);
            Bounds lowered = shield.GetComponentInChildren<Renderer>().bounds;
            TestContext.WriteLine($"lowered: {lowered.min.y:0.00} to {lowered.max.y:0.00}");
            Assert.Greater(lowered.min.y, 0.0f, "lowered, the shield is in the ground");

            yield return Hold(body, new LocomotionInput { ShieldRaised = true }, 0.5f);
            Assert.AreEqual(1.0f, body.ShieldRaise, 1e-3f);
            Assert.Less(Vector3.Angle(shield.forward, body.transform.forward), 5.0f, "raised, the shield does not face forward");
            Assert.Less(Vector3.Angle(shield.up, Vector3.up), 5.0f, "raised, the shield does not stand upright");
            Bounds raised = shield.GetComponentInChildren<Renderer>().bounds;
            TestContext.WriteLine($"raised: {raised.min.y:0.00} to {raised.max.y:0.00}, {body.transform.InverseTransformPoint(shield.position)} in the body's frame");
            Assert.Greater(body.transform.InverseTransformPoint(shield.position).z, 0.4f, "the raised shield is not out in front");
            Assert.Less(raised.min.y, 1.0f, "the raised shield does not reach down past the hips");
            Assert.Greater(raised.min.y, 0.3f, "the raised shield covers the feet: they are meant to show");
            Assert.Greater(raised.max.y, 2.4f, "the raised shield does not reach up over the head");
        }

        [UnityTest]
        public IEnumerator ABulletAtTheChestStopsOnItAndOneAtTheFeetOnlyWhenCrouched()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            yield return Hold(body, default, 0.3f);
            vitals.ShieldRaised = true;
            Vector3 front = body.transform.position + body.transform.forward * 10.0f + Vector3.up * 1.5f;
            Vector3 chest = body.transform.position + Vector3.up * 1.6f, feet = body.transform.position + Vector3.up * 0.2f;
            float health = vitals.Health;
            Assert.AreEqual(HitResponse.Blocked, DamageContext.Strike(vitals, 10.0f, front, 0.0f, DamageKind.Bullet, 0.0f, chest));
            Assert.AreEqual(health, vitals.Health, 0.01f, "a bullet at the chest came through the raised riot shield");
            Assert.AreNotEqual(HitResponse.Blocked, DamageContext.Strike(vitals, 10.0f, front, 0.0f, DamageKind.Bullet, 0.0f, feet));
            Assert.AreEqual(health - 10.0f, vitals.Health, 0.01f, "a bullet at a standing body's feet was stopped: they show under the shield");

            yield return Hold(body, new LocomotionInput { Crouch = true }, 0.6f);
            Assert.IsTrue(body.IsCrouching, "setup: the body did not crouch");
            health = vitals.Health;
            feet = body.transform.position + Vector3.up * 0.2f;
            Assert.AreEqual(HitResponse.Blocked, DamageContext.Strike(vitals, 10.0f, front, 0.0f, DamageKind.Bullet, 0.0f, feet));
            Assert.AreEqual(health, vitals.Health, 0.01f, "crouched behind it, a bullet at the feet came through");

            Vector3 behind = body.transform.position - body.transform.forward * 10.0f + Vector3.up * 1.5f;
            DamageContext.Strike(vitals, 10.0f, behind, 0.0f, DamageKind.Bullet, 0.0f, chest);
            Assert.AreEqual(health - 10.0f, vitals.Health, 0.01f, "a bullet from behind was stopped by the shield");
        }

        [UnityTest]
        public IEnumerator WithItUpTheBodyWalksAtItsOwnSpeed()
        {
            (PlayerLocomotion body, PlayerVitals vitals, Transform shield) = Shielded();
            yield return Hold(body, new LocomotionInput { Move = Vector2.up, ShieldRaised = true }, 1.0f);
            Assert.AreEqual(body.WalkSpeed, body.CurrentSpeed, 0.05f, "the riot shield slowed the body");
        }

        private IEnumerator InTheGameWith(string weaponId, System.Action<PlayerVitals, PlayerInputReader> ready)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(vitals, "no player in the scene");
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            yield return Until(6.0f, () => locomotion.IsGrounded);
            var inventory = vitals.GetComponent<Inventory>();
            foreach (string itemId in new[] { ItemRegistry.RiotShield, weaponId })
            {
                inventory.AddItem(itemId, 1);
                Assert.IsTrue(inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData data));
                Assert.IsTrue(inventory.EquipItemId(itemId, vitals.gameObject, data), $"could not equip {itemId}");
            }
            yield return Until(0.5f, () => false);
            Assert.IsNotNull(vitals.Shield, "the riot shield is not on the body");
            ready(vitals, vitals.GetComponent<PlayerInputReader>());
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner: "there is no aim in with the pistol with riot shield in other hand, instead when you hold right click, he brings the shield forward and
        /// blocks with it and if you want to shoot you have to left click with no aim in and you have to move the riot shield from its guarded state".
        /// </summary>
        [UnityTest]
        public IEnumerator BesideAPistolRightClickRaisesItNothingAimsAndAShotTakesItOutOfItsGuard()
        {
            PlayerVitals vitals = null;
            PlayerInputReader reader = null;
            yield return InTheGameWith(ItemRegistry.Pistol, (v, r) => { vitals = v; reader = r; });
            var gun = reader.EquippedGun as PistolWeapon;
            Assert.IsNotNull(gun, "setup: no pistol in hand");
            var animator = vitals.GetComponentInChildren<Animator>();
            int shots = 0;
            gun.ShotResolved += _ => shots++;

            Press(_mouse.rightButton);
            yield return Until(0.4f, () => false);
            Assert.IsTrue(vitals.ShieldRaised, "right-click did not raise the riot shield beside the pistol");
            Assert.IsFalse(animator.GetBool("Aiming"), "the pistol aimed in with the riot shield up");
            Assert.AreEqual(1.0f, vitals.GetComponent<PlayerLocomotion>().ShieldRaise, 0.01f, "the shield did not come up");

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            yield return null;
            Assert.AreEqual(1, shots, "a click did not shoot from behind the shield");
            Assert.IsFalse(vitals.ShieldRaised, "the shield stayed in its guard through the shot");
            yield return Until(ShieldTraits.Riot.OpenToAttackSeconds * 0.6f, () => false);
            Assert.IsFalse(vitals.ShieldRaised, "the shield was back in its guard at once");
            yield return Until(ShieldTraits.Riot.OpenToAttackSeconds, () => vitals.ShieldRaised);
            Assert.IsTrue(vitals.ShieldRaised, "right-click still held, the shield never came back up after the shot");
            Release(_mouse.rightButton);
            yield return null;
            yield return null;
            Assert.IsFalse(vitals.ShieldRaised, "right-click let go, the shield stayed up");
        }

        [UnityTest]
        public IEnumerator BesideARifleAStaffOrATwoHandedSwordItIsNotRaisedAndBesideAWandOrAOneHandedSwordItIs(
            [Values(ItemRegistry.AK47, ItemRegistry.FireballStaff, ItemRegistry.MagicWand, ItemRegistry.CombatSword, ItemRegistry.SpikeSword)] string weaponId)
        {
            PlayerVitals vitals = null;
            yield return InTheGameWith(weaponId, (v, r) => vitals = v);
            var melee = vitals.GetComponent<MeleeAttacker>();
            bool oneHandedSword = melee.EquippedWeapon is ISwordBlade blade && blade.IsOneHanded;
            Assert.AreEqual(weaponId == ItemRegistry.CombatSword, oneHandedSword, "setup: which sword is one-handed changed");
            bool goes = weaponId == ItemRegistry.MagicWand || oneHandedSword;

            Press(_mouse.rightButton);
            yield return Until(0.4f, () => false);
            Assert.AreEqual(goes, vitals.ShieldRaised, goes ? $"right-click did not raise the riot shield beside {weaponId}" : $"the riot shield was raised beside {weaponId}");
            if (weaponId == ItemRegistry.AK47)
            {
                Assert.IsTrue(vitals.GetComponentInChildren<Animator>().GetBool("Aiming"), "a rifle no longer aims with right-click when a riot shield is carried");
            }
            Release(_mouse.rightButton);
            yield return null;
        }
    }
}
