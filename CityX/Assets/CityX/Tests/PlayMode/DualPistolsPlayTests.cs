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
    /// DUAL PISTOLS ON THE REAL BODY (owner, 2026-11-19: "make dual wield pistols a thing", "Each click fires a gun": docs/DUAL_PISTOLS.md): a
    /// Glock in each hand, the left held as the right in a mirror; a shot from either raises both arms with both barrels on the crosshair's
    /// point; R reloads both with the pair's own motion, each from its own reserve; another player's copy holds the pair and shows each gun's
    /// own shots; and in the game, left-click fires the right hand's gun, right-click the left hand's, and nothing aims down the sights.
    /// </summary>
    public sealed class DualPistolsPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Dt = 1.0f / 60.0f;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Scene _scene;
        private Mouse _mouse;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _spawned.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

#if UNITY_EDITOR
        private GameObject Spawn(string path, Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        private IEnumerator World()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            _spawned.Add(ground);
            yield return null;
        }

        private (GameObject body, PistolWeapon right, PistolWeapon left) Pair(Vector3 at)
        {
            GameObject body = Spawn("Assets/CityX/Character/Player.prefab", at);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var right = Spawn("Assets/CityX/Weapons/Pistol.prefab", new Vector3(40, 1, 40)).GetComponent<PistolWeapon>();
            var left = Spawn("Assets/CityX/Weapons/Pistol.prefab", new Vector3(42, 1, 40)).GetComponent<PistolWeapon>();
            right.OnEquip(body, ItemInstanceData.None);
            left.OnEquipOffHand(body, ItemInstanceData.None);
            return (body, right, left);
        }
#endif

        private static IEnumerator Frames(PlayerLocomotion locomotion, float seconds, LocomotionInput input)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThePairIsHeldOneInEachHandTheLeftAsTheRightSeenInAMirror()
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon right, PistolWeapon left) = Pair(new Vector3(0, 0.05f, 0));
            Animator animator = body.GetComponentInChildren<Animator>();
            var locomotion = body.GetComponent<PlayerLocomotion>();
            var reader = body.GetComponent<PlayerInputReader>();
            yield return Frames(locomotion, 0.3f, new LocomotionInput { GunEquipped = true });

            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.RightHand), right.transform.parent);
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.LeftHand), left.transform.parent, "the second pistol is not in the left hand");
            Assert.Less(Vector3.Distance(DualPistolHold.Mirror(right.transform.localPosition), left.transform.localPosition), 1e-4f);
            Assert.Less(Quaternion.Angle(DualPistolHold.Mirror(right.transform.localRotation), left.transform.localRotation), 0.1f,
                "the left hand does not hold its gun as the right holds its own");
            Assert.AreSame(right, reader.EquippedGun);
            Assert.AreSame(left, reader.EquippedLeftGun);
            Assert.IsTrue(locomotion.HoldsPistolPair);
            Assert.AreSame(right, left.Pair);
            Assert.AreSame(left, right.Pair);
            Assert.AreSame(right.PairMotion, body.GetComponent<PlayerAnimationDriver>().GunReloader.Motion, "the body does not have the pair's reload");

            left.OnUnequip(body);
            yield return null;
            Assert.IsFalse(locomotion.HoldsPistolPair);
            Assert.IsNull(right.Pair);
            Assert.IsNull(reader.EquippedLeftGun);
            Assert.AreSame(right.Motion, body.GetComponent<PlayerAnimationDriver>().GunReloader.Motion, "alone again, the gun has its own reload back");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AShotFromEitherRaisesBothArmsWithBothBarrelsOnTheCrosshairsPoint([Values(false, true)] bool leftShoots)
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon right, PistolWeapon left) = Pair(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            Vector3 forward = Quaternion.Euler(-8.0f, 0.0f, 0.0f) * Vector3.forward;
            Vector3 camera = body.transform.position + Vector3.up * 2.4f + Vector3.right * 0.9f - forward * 4.5f;
            var input = new LocomotionInput { GunEquipped = true, AimCameraPosition = camera, AimCameraForward = forward };
            yield return Frames(locomotion, 0.5f, input);

            PistolWeapon shooter = leftShoots ? left : right, other = leftShoots ? right : left;
            int otherRounds = other.MagazineAmmo;
            Assert.IsTrue(shooter.Fire(camera, forward, false, body), "the gun did not fire");
            Assert.AreEqual(otherRounds, other.MagazineAmmo, "the other gun fired too");
            yield return Frames(locomotion, 0.25f, input);

            Vector3 aimPoint = camera + forward * PlayerLocomotion.GunAimConvergenceDistance;
            Animator animator = body.GetComponentInChildren<Animator>();
            foreach ((PistolWeapon gun, HumanBodyBones shoulder) in new[] { (right, HumanBodyBones.RightUpperArm), (left, HumanBodyBones.LeftUpperArm) })
            {
                Transform muzzle = gun.MuzzleFlashObject.transform;
                float error = Vector3.Angle(muzzle.forward, aimPoint - muzzle.position);
                Vector3 inBody = body.transform.InverseTransformPoint(gun.transform.position);
                Vector3 shoulderInBody = body.transform.InverseTransformPoint(animator.GetBoneTransform(shoulder).position);
                TestContext.WriteLine($"{(gun.InLeftHand ? "left" : "right")}: barrel {error:0.0} deg off, gun at {inBody.ToString("0.00")}, shoulder {shoulderInBody.ToString("0.00")}");
                Assert.Less(error, 4.0f, $"the {(gun.InLeftHand ? "left" : "right")} barrel is off the crosshair's point");
                Assert.Greater(inBody.z, 0.5f, "the gun is not out before the body");
                Assert.Greater(inBody.y, shoulderInBody.y - 0.45f, "the arm is not raised");
                Assert.AreEqual(gun.InLeftHand, inBody.x < 0.0f, "each gun before its own shoulder");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RReloadsBothTogetherEachFromItsOwnReserve()
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon right, PistolWeapon left) = Pair(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            GunReloadDriver reloader = body.GetComponent<PlayerAnimationDriver>().GunReloader;
            yield return Frames(locomotion, 0.3f, new LocomotionInput { GunEquipped = true });
            right.SetAmmo(9, 30);
            left.SetAmmo(0, 4);
            int magazines = FirearmEffects.ActiveMagazines;

            left.TryReload();   // either gun's R reloads the pair
            Assert.IsTrue(right.IsReloading && left.IsReloading, "both guns are not reloading");
            Assert.IsTrue(reloader.IsPlaying, "the pair's motion did not play");
            Assert.AreSame(right.PairMotion, reloader.Motion);
            Assert.IsTrue(reloader.IsEmptyReload, "a pair with an empty gun gets the empty reload");
            bool rightOut = false, leftOut = false;
            for (float t = 0.0f; (right.IsReloading || left.IsReloading) && t < 5.0f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                rightOut |= !right.MagazinePart.gameObject.activeSelf;
                leftOut |= !left.MagazinePart.gameObject.activeSelf;
            }
            Assert.IsFalse(right.IsReloading || left.IsReloading, "the reload never finished");
            Assert.IsTrue(rightOut && leftOut, "both magazines did not come out");
            Assert.GreaterOrEqual(FirearmEffects.ActiveMagazines, magazines + 2, "two magazines did not drop");
            Assert.IsTrue(reloader.PassedMarker("rack2"), "the empty pair's slides were not racked");
            Assert.AreEqual(15, right.MagazineAmmo);
            Assert.AreEqual(24, right.ReserveAmmo, "the right gun was not filled from its own reserve");
            Assert.AreEqual(4, left.MagazineAmmo, "the left gun was not filled from its own (short) reserve");
            Assert.AreEqual(0, left.ReserveAmmo);
            Assert.IsTrue(right.MagazinePart.gameObject.activeSelf && left.MagazinePart.gameObject.activeSelf, "a gun is left without its magazine");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyHoldsThePairAndShowsEachGunsOwnShots()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject copy = Spawn("Assets/CityX/Character/Player.prefab", new Vector3(3, 0.05f, 0));
            copy.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = copy.GetComponent<PlayerLocomotion>();
            var left = Spawn("Assets/CityX/Weapons/Pistol.prefab", new Vector3(40, 1, 40)).GetComponent<PistolWeapon>();
            var right = Spawn("Assets/CityX/Weapons/Pistol.prefab", new Vector3(42, 1, 40)).GetComponent<PistolWeapon>();
            // As NetworkedEquipmentSync builds them (in whichever order the ids come): mounted, then every script on them switched off.
            left.MountCosmeticOffHand(copy);
            right.MountCosmetic(copy);
            foreach (PistolWeapon model in new[] { left, right })
            {
                foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    b.enabled = false;
                }
            }
            Animator animator = copy.GetComponentInChildren<Animator>();
            Assert.AreEqual(animator.GetBoneTransform(HumanBodyBones.LeftHand), left.transform.parent);
            Assert.IsTrue(locomotion.HoldsPistolPair);
            var driver = copy.GetComponent<PlayerAnimationDriver>();
            Assert.AreSame(right.PairMotion, driver.GunReloader.Motion, "the copy does not have the pair's reload");
            yield return Frames(locomotion, 0.3f, new LocomotionInput { GunEquipped = true });

            driver.PlayRemoteState(GunReload.FireLeftName, 1.0f, 0.0f);
            Assert.IsTrue(left.MuzzleFlashObject.activeSelf, "the left gun's shot did not flash it");
            Assert.IsFalse(right.MuzzleFlashObject.activeSelf, "the left gun's shot flashed the right one");
            yield return Frames(locomotion, 0.2f, new LocomotionInput { GunEquipped = true });
            driver.PlayRemoteState(GunReload.FireName, 1.0f, 0.0f);
            Assert.IsTrue(right.MuzzleFlashObject.activeSelf, "the right gun's shot did not flash it");
            Assert.IsFalse(left.MuzzleFlashObject.activeSelf, "the right gun's shot flashed the left one");
            yield return Frames(locomotion, 0.2f, new LocomotionInput { GunEquipped = true });

            driver.PlayRemoteState(GunReload.TacticalName, 1.0f, 0.0f);
            bool rightOut = false, leftOut = false;
            for (float t = 0.0f; t < driver.GunReloader.Duration + 0.3f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                rightOut |= !right.MagazinePart.gameObject.activeSelf;
                leftOut |= !left.MagazinePart.gameObject.activeSelf;
            }
            Assert.IsTrue(rightOut && leftOut, "the copy's magazines did not both come out");
            left.UnmountCosmetic(copy);
            right.UnmountCosmetic(copy);
            Assert.IsFalse(locomotion.HoldsPistolPair);
#else
            yield break;
#endif
        }

        /// <summary>The owner's controls, with the real mouse in the game: "Each click fires a gun", no aiming in.</summary>
        [UnityTest]
        public IEnumerator InTheGameLeftClickFiresTheRightHandsGunRightClickTheLeftsAndNothingAims()
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
            for (float t = 0.0f; t < 6.0f && !locomotion.IsGrounded; t += Time.deltaTime)
            {
                yield return null;
            }
            PlayerSkills.On(vitals.gameObject).LearnEverything();
            var inventory = vitals.GetComponent<Inventory>();
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Pistol, vitals.gameObject), $"pistol {i + 1} would not go on");
            }
            var reader = vitals.GetComponent<PlayerInputReader>();
            var right = reader.EquippedGun as PistolWeapon;
            var left = reader.EquippedLeftGun as PistolWeapon;
            Assert.IsNotNull(right, "no pistol in the right hand");
            Assert.IsNotNull(left, "the second pistol did not go in the left hand");
            int rightShots = 0, leftShots = 0;
            right.ShotResolved += shot => rightShots += shot.Fired ? 1 : 0;
            left.ShotResolved += shot => leftShots += shot.Fired ? 1 : 0;
            yield return null;

            Press(_mouse.rightButton);
            yield return null;
            yield return null;
            Assert.AreEqual(1, leftShots, "right-click did not fire the left hand's gun");
            Assert.AreEqual(0, rightShots, "right-click fired the right hand's gun");
            Assert.IsFalse(vitals.GetComponentInChildren<Animator>().GetBool("Aiming"), "right-click aimed down the sights");
            Release(_mouse.rightButton);
            yield return null;

            Press(_mouse.leftButton);
            yield return null;
            yield return null;
            Assert.AreEqual(1, rightShots, "left-click did not fire the right hand's gun");
            Assert.AreEqual(1, leftShots, "left-click fired the left hand's gun");
            Release(_mouse.leftButton);
            yield return null;

            Press(_keyboard.rKey);
            yield return null;
            yield return null;
            Release(_keyboard.rKey);
            Assert.IsTrue(right.IsReloading && left.IsReloading, "R did not reload both guns");
#else
            yield break;
#endif
        }
    }
}
