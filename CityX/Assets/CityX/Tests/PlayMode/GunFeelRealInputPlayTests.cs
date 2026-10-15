using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The third-person pistol through the REAL input path -- simulated mouse
    /// and keyboard, the real PlayerInputReader, camera and HUD, the real saved
    /// scene, a pistol equipped the way /equip pistol equips it: the camera
    /// kick and its settling, the hit/kill marker on the real reticle, the
    /// automatic reload, sprint ending on aim, the aim slowdown, the
    /// shoulder swap and the aim sensitivity. The rules themselves are covered
    /// without the scene (PistolThirdPersonPlayTests, GunReticleViewPlayTests).
    /// </summary>
    public sealed class GunFeelRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private ThirdPersonCamera _camera;
        private PistolWeapon _pistol;
        private GunReticleView _reticle;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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

        private IEnumerator LoadSceneWithAPistol()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(_vitals, "no PlayerVitals in the real scene");
            _locomotion = _vitals.GetComponent<PlayerLocomotion>();
            _camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(_camera, "no ThirdPersonCamera in the real scene");
            _vitals.InfiniteHealth = true;
            _vitals.InfiniteStamina = true;

            Inventory inventory = _vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.Pistol, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.Pistol, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Pistol, _vitals.gameObject, data), "could not equip the pistol");
            _pistol = _vitals.GetComponentInChildren<PistolWeapon>();
            Assert.IsNotNull(_pistol, "the equipped pistol is not on the body");
            Assert.IsNotNull(_vitals.GetComponent<GunFeel>(), "equipping the pistol did not give the body its GunFeel");

            _reticle = Object.FindFirstObjectByType<GunReticleView>();
            Assert.IsNotNull(_reticle, "the real HUD has no GunReticleView");

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>A dummy standing exactly on the crosshair's ray, `metres` from the camera.</summary>
        private SparringDummy DummyOnTheCrosshair(float metres, float maxHealth = 100.0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = _camera.transform.position + _camera.transform.forward * metres;
            var dummy = go.AddComponent<SparringDummy>();
            dummy.MaxHealth = maxHealth;
            dummy.Reset();
            _made.Add(go);
            Physics.SyncTransforms();
            return dummy;
        }

        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator Click()
        {
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        /// <summary>
        /// THE GUN STATS (2026-10-14): Firearm Damage makes a shot hurt more, Fire Rate shortens the wait for the next one, Reload Speed
        /// reloads sooner, Recoil Control kicks the camera less -- and a standing first shot still lands exactly on the crosshair.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGunStatsMakeShotsHurtMoreComeFasterReloadSoonerAndKickLess()
        {
            yield return LoadSceneWithAPistol();
            SparringDummy dummy = DummyOnTheCrosshair(8.0f, maxHealth: 10000.0f);
            float damage = _pistol.Profile.Damage;

            yield return Click();
            Assert.AreEqual(1, dummy.TimesHit, "a standing first shot missed the crosshair's target");
            Assert.AreEqual(damage, dummy.TotalDamageTaken, 0.01f, "with no points a shot does the gun's own damage");
            float plainCooldown = _pistol.FireCooldownRemaining;
            float plainKick = _camera.RecoilPitch;
            yield return Wait(1.5f);

            _vitals.Stats[StatId.FirearmDamage] = 25;   // +50%
            _vitals.Stats[StatId.FireRate] = 20;        // +30%
            _vitals.Stats[StatId.RecoilControl] = 25;   // 50% less
            yield return Click();
            Assert.AreEqual(2, dummy.TimesHit, "the second standing shot missed");
            Assert.AreEqual(damage * 2.5f, dummy.TotalDamageTaken, 0.01f, "Firearm Damage +50% should make the second shot do half as much again");
            Assert.Less(_pistol.FireCooldownRemaining, plainCooldown / 1.25f, "Fire Rate +30% should shorten the wait for the next shot");
            Assert.Less(_camera.RecoilPitch, plainKick * 0.7f, "Recoil Control 50% should kick the camera much less");
            yield return Wait(1.5f);

            // Reload Speed: the same reload, timed at +0% and +50%.
            float plainReload = 0.0f;
            yield return TimeAReload(seconds => plainReload = seconds);
            yield return Click();   // a round out again, so there is something to reload
            yield return Wait(0.5f);
            _vitals.Stats[StatId.ReloadSpeed] = 25;   // +50%
            float fastReload = 0.0f;
            yield return TimeAReload(seconds => fastReload = seconds);
            Debug.Log($"CityX gun stats: reload {plainReload:0.00} s plain, {fastReload:0.00} s at +50%");
            Assert.AreEqual(plainReload / 1.5f, fastReload, plainReload * 0.12f, "Reload Speed +50% should reload in two thirds of the time");
        }

        private IEnumerator TimeAReload(System.Action<float> seconds)
        {
            Press(_keyboard.rKey);
            yield return null;
            Release(_keyboard.rKey);
            yield return null;
            Assert.IsTrue(_pistol.IsReloading, "R did not start a reload");
            float elapsed = Time.deltaTime;
            while (_pistol.IsReloading && elapsed < 10.0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            seconds(elapsed);
        }

        [UnityTest]
        public IEnumerator ClickingFiresKicksTheCameraAndTheKickSettlesBack()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();
            SparringDummy target = DummyOnTheCrosshair(6.0f);

            yield return Click();

            Assert.GreaterOrEqual(target.TimesHit, 1, "a click on the crosshair's target missed it");
            Assert.Greater(_camera.RecoilPitch, 0.2f, "the shot did not kick the camera");

            yield return Wait(1.5f);
            Assert.AreEqual(0.0f, _camera.RecoilPitch, 0.02f, "the camera kick never settled");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AHitShowsAMarkerOnTheRealReticleAndAKillShowsARedOne()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();
            SparringDummy tough = DummyOnTheCrosshair(6.0f, maxHealth: 500.0f);

            yield return Click();
            Assert.IsTrue(_reticle.MarkerVisible, "a hit put no marker on the reticle");

            yield return Wait(GunReticleView.KillMarkerSeconds + 0.4f);
            Assert.IsFalse(_reticle.MarkerVisible);
            Object.Destroy(tough.gameObject);
            yield return null;

            SparringDummy fragile = DummyOnTheCrosshair(6.0f, maxHealth: PistolWeapon.Damage);
            yield return Wait(0.3f);   // past the fire cooldown
            yield return Click();

            Assert.IsFalse(fragile.IsAlive, "setup: the fragile dummy survived");
            Assert.IsTrue(_reticle.MarkerVisible);
            Assert.AreEqual(GunReticleView.KillColour.r, _reticle.MarkerColour.r, 0.01f);
            Assert.AreEqual(GunReticleView.KillColour.g, _reticle.MarkerColour.g, 0.01f, "a kill marker was not red");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ClickingOnAnEmptyMagazineReloadsByItself()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();

            for (int i = 0; i < PistolWeapon.MagazineSize; i++)
            {
                yield return Click();
                yield return Wait(PistolWeapon.FireCooldownSeconds + 0.05f);
            }
            Assert.AreEqual(0, _pistol.MagazineAmmo, "setup: the magazine was not emptied");
            Assert.IsFalse(_pistol.IsReloading);

            yield return Click();

            Assert.IsTrue(_pistol.IsReloading, "the empty click did not start a reload");
            yield return Wait(PistolWeapon.ReloadSeconds + 0.4f);
            Assert.AreEqual(PistolWeapon.MagazineSize, _pistol.MagazineAmmo, "the auto-reload never refilled the magazine");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AimingEndsASprintAndSlowsTheWalk()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();

            // Open ground, far from any building: sprinting into a wall
            // engages WallStick (which takes over the body and stops the
            // ordinary movement this test is measuring). A private slab high
            // above the city is the one place guaranteed to have nothing ahead.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.position = new Vector3(0.0f, 999.5f, 0.0f);
            slab.transform.localScale = new Vector3(300.0f, 1.0f, 300.0f);
            _made.Add(slab);
            CharacterController controller = _vitals.GetComponent<CharacterController>();
            controller.enabled = false;
            _vitals.transform.position = new Vector3(0.0f, 1000.2f, 0.0f);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return Wait(0.5f);

            // W FIRST, THEN SHIFT A FRAME LATER -- not a style choice. Two keys
            // pressed in the SAME frame through InputTestFixture lose the first
            // (Keyboard keys are bits in shared bytes, and the second queued delta
            // overwrites the first: W reads false while Shift reads true). Real
            // keyboards deliver both fine; this is a harness limitation.
            Press(_keyboard.wKey);
            yield return Wait(0.6f);
            Assert.Greater(_locomotion.CurrentSpeed, 0.5f, "setup: holding W alone did not walk");
            Press(_keyboard.leftShiftKey);
            yield return Wait(0.8f);
            Assert.IsTrue(_locomotion.IsSprinting, "setup: holding Shift+W did not sprint");
            Assert.Greater(_locomotion.CurrentSpeed, _locomotion.WalkSpeed + 0.5f, "setup: not actually moving at sprint speed");

            Press(_mouse.rightButton);
            yield return Wait(0.8f);

            Assert.IsFalse(_locomotion.IsSprinting, "aiming did not end the sprint");
            Assert.AreEqual(_locomotion.WalkSpeed * _locomotion.AimMoveSpeedMultiplier, _locomotion.CurrentSpeed, 0.2f,
                "aiming did not slow the walk to the aim speed");

            Release(_mouse.rightButton);
            Release(_keyboard.leftShiftKey);
            Release(_keyboard.wKey);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator QSwapsTheCameraToTheOtherShoulderAndBack()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();
            Assert.AreEqual(1.0f, _camera.ShoulderSide);

            Press(_keyboard.qKey);
            yield return null;
            Release(_keyboard.qKey);
            yield return Wait(0.4f);
            Assert.AreEqual(-1.0f, _camera.ShoulderSide, "Q did not swap shoulders");

            Press(_keyboard.qKey);
            yield return null;
            Release(_keyboard.qKey);
            yield return Wait(0.4f);
            Assert.AreEqual(1.0f, _camera.ShoulderSide, "a second Q did not swap back");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AimingMakesTheMouseLookFiner()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithAPistol();

            float hipBefore = _camera.Yaw;
            Set(_mouse.delta, new Vector2(40.0f, 0.0f));
            yield return null;
            yield return null;   // LateUpdate reads the delta AFTER a coroutine resumes, so wait one more frame
            float hipTurn = Mathf.DeltaAngle(hipBefore, _camera.Yaw);
            yield return Wait(0.2f);

            Press(_mouse.rightButton);
            yield return Wait(0.2f);
            float aimBefore = _camera.Yaw;
            Set(_mouse.delta, new Vector2(40.0f, 0.0f));
            yield return null;
            yield return null;
            float aimTurn = Mathf.DeltaAngle(aimBefore, _camera.Yaw);
            Release(_mouse.rightButton);

            Assert.Greater(hipTurn, 1.0f, $"setup: the hip mouse turn was only {hipTurn}");
            Assert.AreEqual(_camera.AimSensitivityMultiplier, aimTurn / hipTurn, 0.08f,
                $"aiming turned {aimTurn:0.00} for the same mouse movement that turned {hipTurn:0.00} at the hip");
#else
            yield break;
#endif
        }
    }
}
