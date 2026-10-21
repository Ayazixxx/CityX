using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Adjusting how a weapon is held in the Unity editor: the held weapon carries a draggable left-hand
    /// marker, a saved hold replaces the derived one on the next equip, and the torso amount is read live
    /// from the settings. (The Inspector buttons are thin wrappers over WeaponGripSettings.Save/Reset,
    /// which is what these exercise.)
    /// </summary>
    public sealed class WeaponGripSettingsPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _weaponObject;
        private GameObject _light;
        private PlayerLocomotion _locomotion;
        private WeaponGripSettings _realSettings;
        private WeaponGripSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _realSettings = WeaponGripSettings.Current;
            _settings = ScriptableObject.CreateInstance<WeaponGripSettings>();
            WeaponGripSettings.Current = _settings;
        }

        [TearDown]
        public void TearDown()
        {
            WeaponGripSettings.Current = _realSettings;
            if (_player != null) Object.Destroy(_player);
            if (_weaponObject != null) Object.Destroy(_weaponObject);
            if (_ground != null) Object.Destroy(_ground);
            if (_light != null) Object.Destroy(_light);
        }

        private void BuildPlayer()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _light = new GameObject("Light");
            _light.AddComponent<Light>().type = LightType.Directional;
#endif
        }

        private SwordWeapon EquipSword()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab");
            Assert.IsNotNull(prefab, "Sword.prefab is missing");
            _weaponObject = Object.Instantiate(prefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            var sword = _weaponObject.GetComponent<SwordWeapon>();
            sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            return sword;
#else
            return null;
#endif
        }

        private PistolWeapon EquipPistol()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(prefab, "Pistol.prefab is missing");
            _weaponObject = Object.Instantiate(prefab, new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            var pistol = _weaponObject.GetComponent<PistolWeapon>();
            pistol.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            return pistol;
#else
            return null;
#endif
        }

        private IEnumerator Frames(float seconds, bool swordEquipped = true)
        {
            // The sword combat is ticked too: a swing only ENDS through its Tick (its recovery is longer than a measurement window now), and
            // an unfinished swing keeps the torso following the clip into whatever is measured next.
            var combat = _player != null ? _player.GetComponent<SoulsSwordCombat>() : null;
            var vitals = _player != null ? _player.GetComponent<PlayerVitals>() : null;
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                combat?.Tick(dt);
                vitals?.TickStamina(false, dt);
                _locomotion.Tick(new LocomotionInput { SwordEquipped = swordEquipped }, dt);
                yield return null;
                elapsed += dt;
            }
        }

        private static float PositionGap(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        [UnityTest]
        public IEnumerator WithNothingSavedTheSwordSitsAtTheDerivedHoldWithAMarkerOnTheHandle()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            yield return Frames(0.8f);

            Assert.IsFalse(_settings.SwordTuned);
            Assert.Less(PositionGap(sword.transform.localPosition, SwordWeapon.DefaultGripPosition), 1e-4f, "the sword is not at the derived hold");
            Assert.Less(Quaternion.Angle(sword.transform.localRotation, SwordWeapon.DefaultGripRotation), 0.01f);
            Assert.IsNotNull(sword.LeftHandGrip, "there is no left-hand marker to drag");
            Assert.AreEqual(sword.transform, sword.LeftHandGrip.parent, "the marker should ride on the sword");
            Assert.Less(PositionGap(sword.LeftHandGrip.localPosition, SwordWeapon.DefaultSupportPoint), 1e-4f);
            Assert.Less(_locomotion.SupportHandError, 0.03f, "the left hand is not on its marker");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DraggingTheMarkerMovesTheLeftHandAndDraggingTheSwordMovesTheSword()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            yield return Frames(0.8f);

            // Drag the marker 8cm up the handle: the left palm should follow it.
            Vector3 palmBefore;
            Assert.IsTrue(_locomotion.TryGetSupportPalm(out palmBefore));
            sword.LeftHandGrip.localPosition += new Vector3(0.0f, 0.08f, 0.0f);
            yield return Frames(0.8f);
            Vector3 palmAfter;
            _locomotion.TryGetSupportPalm(out palmAfter);
            Assert.Greater(Vector3.Distance(palmBefore, palmAfter), 0.04f, "the hand did not follow the marker");
            Assert.Less(_locomotion.SupportHandError, 0.03f, "the hand did not reach the dragged marker");

            // Drag the sword itself along the hand: it stays where it is put (nothing re-applies the hold every frame).
            Vector3 dragged = sword.transform.localPosition + new Vector3(0.05f, 0.0f, 0.0f);
            sword.transform.localPosition = dragged;
            yield return Frames(0.8f);
            Assert.Less(PositionGap(sword.transform.localPosition, dragged), 1e-5f, "the sword was snapped back while being dragged");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SavingKeepsWhatTheSwordIsAtAndTheNextEquipUsesIt()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            yield return Frames(0.5f);

            sword.transform.localPosition = new Vector3(0.21f, -0.03f, 0.05f);
            sword.transform.localEulerAngles = new Vector3(30.0f, 200.0f, 350.0f);
            sword.LeftHandGrip.localPosition = new Vector3(0.0f, -0.28f, 0.01f);
            _settings.SaveSword(sword.transform, sword.LeftHandGrip);

            Assert.IsTrue(_settings.SwordTuned);
            Assert.Less(PositionGap(_settings.SwordPosition, new Vector3(0.21f, -0.03f, 0.05f)), 1e-4f);
            Assert.Less(PositionGap(_settings.SwordLeftHand, new Vector3(0.0f, -0.28f, 0.01f)), 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(_settings.SwordEuler.y), 180.0f, "angles should be wrapped to -180..180");

            // Put it away and pick up a fresh sword: it comes out at the saved hold.
            sword.OnUnequip(_player);
            _weaponObject = null;
            yield return null;
            SwordWeapon second = EquipSword();
            yield return Frames(0.5f);

            Assert.Less(PositionGap(second.transform.localPosition, new Vector3(0.21f, -0.03f, 0.05f)), 1e-4f, "the saved sword position was not used");
            Assert.Less(Quaternion.Angle(second.transform.localRotation, Quaternion.Euler(30.0f, 200.0f, 350.0f)), 0.05f, "the saved sword rotation was not used");
            Assert.Less(PositionGap(second.LeftHandGrip.localPosition, new Vector3(0.0f, -0.28f, 0.01f)), 1e-4f, "the saved left-hand point was not used");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GoingBackToTheDerivedHoldPutsTheSwordBack()
        {
#if UNITY_EDITOR
            BuildPlayer();
            SwordWeapon sword = EquipSword();
            yield return Frames(0.5f);
            sword.transform.localPosition += new Vector3(0.1f, 0.1f, 0.0f);
            _settings.SaveSword(sword.transform, sword.LeftHandGrip);

            _settings.ResetSword();
            sword.ApplyGrip();

            Assert.IsFalse(_settings.SwordTuned);
            Assert.Less(PositionGap(sword.transform.localPosition, SwordWeapon.DefaultGripPosition), 1e-4f);
            Assert.Less(PositionGap(sword.LeftHandGrip.localPosition, SwordWeapon.DefaultSupportPoint), 1e-4f);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePistolsFreeHandFollowsItsMarkerWhileAiming()
        {
#if UNITY_EDITOR
            BuildPlayer();
            PistolWeapon pistol = EquipPistol();
            Assert.IsNotNull(pistol.LeftHandGrip, "the pistol has no left-hand marker");
            Assert.Less(PositionGap(pistol.LeftHandGrip.localPosition, PistolWeapon.DefaultSupportGripLocal), 1e-4f);

            Vector3 chest = _player.transform.position + Vector3.up * 1.4f;
            System.Func<float, IEnumerator> aim = seconds => AimFrames(seconds, chest);
            yield return aim(0.9f);
            Assert.Less(_locomotion.SupportHandError, 0.03f, "the free hand is not on the pistol's marker while aiming");

            Vector3 palmBefore;
            _locomotion.TryGetSupportPalm(out palmBefore);
            pistol.LeftHandGrip.localPosition += new Vector3(-0.05f, 0.0f, 0.0f);   // 5 cm to the shooter's left (the Glock's frame: x is the right)
            yield return aim(0.7f);
            Vector3 palmAfter;
            _locomotion.TryGetSupportPalm(out palmAfter);
            Assert.Greater(Vector3.Distance(palmBefore, palmAfter), 0.02f, "the free hand did not follow the dragged marker");

            _settings.SavePistol(pistol.LeftHandGrip);
            Assert.IsTrue(_settings.PistolTuned);
            Assert.AreEqual(pistol.LeftHandGrip.localPosition.y, _settings.PistolLeftHand.y, 1e-5f);
#else
            yield break;
#endif
        }

        private IEnumerator AimFrames(float seconds, Vector3 chest)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(new LocomotionInput
                {
                    Aiming = true,
                    GunEquipped = true,
                    AimCameraPosition = chest + Vector3.right * 0.65f - Vector3.forward * 4.0f,
                    AimCameraForward = Vector3.forward,
                }, Time.deltaTime);
                yield return null;
                elapsed += dt;
            }
        }

        /// <summary>A light swing in real time; returns the most the spine turns from upright during it.</summary>
        private IEnumerator LeanDuringASwing(SoulsSwordCombat combat, Transform spine, System.Action<float> result)
        {
            combat.RequestLight(0.0f);
            float most = 0.0f;
            float elapsed = 0.0f;
            while (elapsed < 0.7f)
            {
                float dt = Time.deltaTime;
                combat.Tick(dt);
                _player.GetComponent<PlayerVitals>()?.TickStamina(false, dt);
                _locomotion.Tick(new LocomotionInput { SwordEquipped = true }, dt);
                yield return null;
                elapsed += dt;
                most = Mathf.Max(most, Quaternion.Angle(spine.localRotation, Quaternion.identity));
            }
            result(most);
        }

        [UnityTest]
        public IEnumerator TheTorsoAmountIsReadLiveFromTheSettings()
        {
#if UNITY_EDITOR
            BuildPlayer();
            EquipSword();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            Transform spine = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Spine);
            yield return Frames(0.8f);

            float full = 0.0f, none = 0.0f;
            _settings.TorsoFollowStrength = 1.0f;
            yield return LeanDuringASwing(combat, spine, v => full = v);
            yield return Frames(1.5f);   // settle, and let the stamina come back

            _settings.TorsoFollowStrength = 0.0f;
            yield return LeanDuringASwing(combat, spine, v => none = v);

            Debug.Log($"CityX torso setting: spine turned {full:0.0} deg at strength 1, {none:0.0} at strength 0");
            Assert.Greater(full, 5.0f, "at full strength a swing should use the torso");
            Assert.Less(none, 0.5f, "with the torso amount at 0 the spine should stay upright through a swing");
            Assert.AreEqual(0.0f, PlayerLocomotion.TorsoFollowStrength, 1e-6f, "the locomotion is not reading the settings asset");
#else
            yield break;
#endif
        }
    }
}
