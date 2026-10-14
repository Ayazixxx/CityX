using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE GUNS ON THE REAL BODY (2026-10-14): the Glock's and the AK-47's reloads played from their Blender motions (the magazine dropped,
    /// the ammo in at the click, an empty gun racked, a shot after the click cutting the rest short), the AK firing while the trigger is held
    /// and shouldered at the crosshair, the gun on screen and not behind the head while aiming (the owner: "i cant even see this pistol when
    /// im aiming in with it"), and a copy of the body -- what another player sees -- reloading and firing when told by name.
    /// </summary>
    public sealed class FirearmPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _spawned.Clear();
            Time.captureFramerate = 0;
        }

#if UNITY_EDITOR
        private GameObject Spawn(GameObject prefab, Vector3 at)
        {
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        private GameObject Player(Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab);
            GameObject player = Spawn(prefab, at);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
        }

        private PistolWeapon Gun(string path)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- CityX > Rebuild Firearms");
            return Spawn(prefab, new Vector3(40.0f, 1.0f, 40.0f)).GetComponent<PistolWeapon>();
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

        private static IEnumerator Frames(PlayerLocomotion locomotion, float seconds, LocomotionInput input)
        {
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                locomotion.Tick(input, Dt);
                yield return null;
            }
        }
#endif

        [UnityTest]
        public IEnumerator AnEmptyGlockPlaysTheEmptyReloadTheAmmoIsInAtTheClickAndTheOldMagazineIsDropped()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/Pistol.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            var driver = player.GetComponent<PlayerAnimationDriver>();
            var announced = new List<string>();
            driver.TimedStatePlayed += (name, speed, start) => announced.Add(name);
            yield return Frames(locomotion, 0.5f, new LocomotionInput { GunEquipped = true });

            gun.SetAmmo(0, 30);
            int magazinesBefore = FirearmEffects.ActiveMagazines;
            gun.TryReload();
            GunReloadDriver reloader = driver.GunReloader;
            Assert.IsTrue(gun.IsReloading);
            Assert.IsTrue(reloader.IsPlaying, "the reload motion did not play");
            Assert.IsTrue(reloader.IsEmptyReload, "an empty gun gets the empty reload (with the rack)");
            Assert.IsTrue(driver.IsPlaying(GunReload.EmptyName));
            Assert.Contains(GunReload.EmptyName, announced, "other players were not told about the reload");

            float seat = -1.0f, rack = -1.0f, elapsed = 0.0f;
            bool magazineHidden = false;
            while (gun.IsReloading && elapsed < 5.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
                magazineHidden |= !gun.MagazinePart.gameObject.activeSelf;
                if (seat < 0.0f && gun.ReloadAmmoIn)
                {
                    seat = elapsed;
                    Assert.AreEqual(15, gun.MagazineAmmo, "the magazine was not refilled at the click");
                    Assert.IsTrue(gun.IsReloading, "an empty reload goes on to the rack after the click");
                }
                if (rack < 0.0f && reloader.PassedMarker("rack2"))
                {
                    rack = elapsed;
                }
            }
            Assert.IsFalse(gun.IsReloading, "the reload never ended");
            Assert.Greater(seat, 0.4f, "the ammo went in before the new magazine could have been fetched");
            Assert.Greater(rack, seat, "the slide was not racked after the magazine went in");
            Assert.IsTrue(magazineHidden, "the old magazine never left the gun");
            Assert.Greater(FirearmEffects.ActiveMagazines, magazinesBefore, "no dropped magazine was left behind");
            Assert.AreEqual(15, gun.MagazineAmmo);
            Assert.AreEqual(15, gun.ReserveAmmo);
            yield return Frames(locomotion, 0.3f, new LocomotionInput { GunEquipped = true });
            Assert.IsTrue(gun.MagazinePart.gameObject.activeSelf, "the fresh magazine is not in the gun");
            Assert.AreEqual(0.0f, locomotion.BakedPoseWeight, "the reload pose never let go of the body");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AShotOnceTheFreshMagazineIsInCutsTheTacticalReloadShort()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/Pistol.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.4f, new LocomotionInput { GunEquipped = true });
            gun.SetAmmo(4, 30);
            gun.TryReload();
            GunReloadDriver reloader = player.GetComponent<PlayerAnimationDriver>().GunReloader;
            Assert.IsFalse(reloader.IsEmptyReload, "a magazine with rounds left gets the quicker tactical reload");
            Vector3 origin = player.transform.position + Vector3.up * 2.0f - Vector3.forward * 2.0f;
            Assert.IsFalse(gun.Fire(origin, Vector3.forward, false, player), "fired before the new magazine was in");
            float elapsed = 0.0f;
            while (!gun.ReloadAmmoIn && elapsed < 3.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
            }
            Assert.IsTrue(gun.ReloadAmmoIn, "the new magazine never went in");
            Assert.IsTrue(reloader.IsPlaying, "setup: the motion should still have its last part to play");
            Assert.IsTrue(gun.Fire(origin, Vector3.forward, false, player), "a shot after the click should cut the reload short");
            Assert.IsFalse(gun.IsReloading);
            Assert.IsFalse(reloader.IsPlaying, "the rest of the reload motion still played over the shot");
            Assert.AreEqual(14, gun.MagazineAmmo);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheAK47FiresWhileTheTriggerIsHeldAtItsRateAndItsKickFollowsThePattern()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/AK47.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            Assert.IsTrue(gun.IsAutomatic);
            var shots = new List<GunShot>();
            gun.ShotResolved += shot => { if (shot.Fired) shots.Add(shot); };
            yield return Frames(locomotion, 0.4f, new LocomotionInput { GunEquipped = true, Aiming = true });
            Vector3 origin = player.transform.position + Vector3.up * 2.0f - Vector3.forward * 2.0f;
            for (float t = 0.0f; t < 1.0f - 1e-4f; t += Dt)
            {
                gun.Fire(origin, Vector3.forward, true, player);   // the trigger held: asked every frame, the gun keeps its own rate
                locomotion.Tick(new LocomotionInput { GunEquipped = true, Aiming = true }, Dt);
                yield return null;
            }
            Assert.That(shots.Count, Is.InRange(9, 11), $"{shots.Count} shots in a second: an AK fires about ten");
            FirearmProfile profile = gun.Profile;
            for (int i = 0; i < shots.Count; i++)
            {
                Assert.AreEqual(profile.RecoilFor(i).y * profile.AdsRecoilMultiplier, shots[i].RecoilPitchDegrees, 1e-4f, $"shot {i + 1}'s climb");
                Assert.AreEqual(profile.RecoilFor(i).x * profile.AdsRecoilMultiplier, shots[i].RecoilYawDegrees, 1e-4f, $"shot {i + 1}'s drift");
                Assert.Greater(shots[i].RecoilHoldSeconds, Dt * 6.0f - 1e-4f, "an automatic holds its kick through the burst");
            }
            Assert.AreEqual(30 - shots.Count, gun.MagazineAmmo);
            Assert.Greater(FirearmEffects.ActiveCasings, 0, "no brass came out of the port");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheAK47IsShoulderedAndItsBarrelIsOnTheCrosshairsPoint()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/AK47.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            // Carried: low ready, the muzzle down.
            yield return Frames(locomotion, 0.8f, new LocomotionInput { GunEquipped = true });
            Assert.IsTrue(locomotion.HoldsRifle);
            Assert.Less(locomotion.RifleShoulderBlend, 0.05f, "the rifle is shouldered while only carried");
            Vector3 muzzleForward = gun.MuzzleFlashObject.transform.forward;
            Assert.Less(muzzleForward.y, -0.25f, $"at low ready the muzzle points down (forward {muzzleForward.ToString("0.00")})");

            foreach (float pitch in new[] { 0.0f, 20.0f, -20.0f })
            {
                Vector3 forward = Quaternion.Euler(-pitch, 0.0f, 0.0f) * Vector3.forward;
                Vector3 camera = player.transform.position + Vector3.up * 2.1f + Vector3.right * 1.0f - forward * 2.3f;
                yield return Frames(locomotion, 0.8f, new LocomotionInput
                {
                    GunEquipped = true, Aiming = true, AimCameraPosition = camera, AimCameraForward = forward,
                });
                Assert.Greater(locomotion.RifleShoulderBlend, 0.95f, "the rifle did not come up to the shoulder");
                Vector3 muzzle = gun.MuzzleFlashObject.transform.position;
                // Where the barrel should point: the crosshair's point (15 m down the camera's ray with nothing hit).
                Vector3 aimPoint = camera + forward * PlayerLocomotion.GunAimConvergenceDistance;
                float error = Vector3.Angle(gun.MuzzleFlashObject.transform.forward, aimPoint - muzzle);
                Vector3 butt = gun.transform.TransformPoint(gun.Profile.ButtLocal);
                Debug.Log($"CityX rifle @pitch {pitch}: barrel {error:0.0} deg off the aim point, butt {Vector3.Distance(butt, rightShoulder.position) * 100:0} cm from the shoulder joint, " +
                          $"support palm {locomotion.SupportHandError * 100:0.0} cm off the handguard");
                Assert.Less(error, 4.0f, $"pitch {pitch}: the barrel is {error:0.0} degrees off the crosshair's point");
                Assert.Less(Vector3.Distance(butt, rightShoulder.position), 0.3f, $"pitch {pitch}: the stock is not in the shoulder");
                Assert.Less(locomotion.SupportHandError, 0.05f, $"pitch {pitch}: the support hand is off the handguard");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheGunIsOnScreenAndNotBehindTheHeadWhileAiming()
        {
#if UNITY_EDITOR
            foreach (string path in new[] { "Assets/CityX/Weapons/Pistol.prefab", "Assets/CityX/Weapons/AK47.prefab" })
            {
                yield return World();
                GameObject player = Player(new Vector3(0, 0.05f, 0));
                var locomotion = player.GetComponent<PlayerLocomotion>();
                PistolWeapon gun = Gun(path);
                gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
                var cameraObject = new GameObject("FollowCamera");
                _spawned.Add(cameraObject);
                Camera view = cameraObject.AddComponent<Camera>();
                view.fieldOfView = 75.0f;
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                follow.Target = player.transform;
                yield return null;
                follow.Yaw = 0.0f;
                follow.Pitch = 3.0f;
                for (float t = 0.0f; t < 1.0f; t += Dt)
                {
                    follow.IsAiming = true;
                    locomotion.Tick(new LocomotionInput
                    {
                        GunEquipped = true, Aiming = true, YawDegrees = 0.0f,
                        AimCameraPosition = cameraObject.transform.position, AimCameraForward = cameraObject.transform.forward,
                    }, Dt);
                    yield return null;
                }
                Bounds gunBounds = gun.GetComponentInChildren<Renderer>().bounds;
                foreach (Renderer r in gun.GetComponentsInChildren<Renderer>())
                {
                    gunBounds.Encapsulate(r.bounds);
                }
                Vector3 onScreen = view.WorldToViewportPoint(gunBounds.center);
                Debug.Log($"CityX {gun.Profile.DisplayName} while aiming: centre at viewport {onScreen.ToString("0.00")}, camera {view.fieldOfView:0} deg");
                Assert.Greater(onScreen.z, 0.5f, "the gun is behind the camera");
                Assert.That(onScreen.x, Is.InRange(0.15f, 0.85f), "the gun is off the side of the screen");
                Assert.That(onScreen.y, Is.InRange(0.15f, 0.85f), "the gun is off the top or bottom of the screen");
                // The head is a sphere: the line from the lens to the gun must pass outside it.
                Transform head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Vector3 a = view.transform.position, b = gunBounds.center;
                Vector3 c = head.position;
                float along = Mathf.Clamp01(Vector3.Dot(c - a, b - a) / (b - a).sqrMagnitude);
                float clearance = Vector3.Distance(c, a + (b - a) * along) - FusedBodyShape.HeadRadius;
                Assert.Greater(clearance, 0.03f, $"{gun.Profile.DisplayName}: the view of the gun passes {-clearance * 100:0} cm into the head -- it is hidden behind it");
                Object.Destroy(player);
                Object.Destroy(cameraObject);
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyReloadsAndShootsWhenToldByName()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject copy = Player(new Vector3(3, 0.05f, 0));
            var locomotion = copy.GetComponent<PlayerLocomotion>();
            PistolWeapon model = Gun("Assets/CityX/Weapons/AK47.prefab");
            // As NetworkedEquipmentSync builds a copy's item: mounted, then every script on it switched off.
            model.MountCosmetic(copy);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            Assert.IsTrue(locomotion.HoldsRifle, "the copy does not hold the rifle as a rifle");
            var driver = copy.GetComponent<PlayerAnimationDriver>();
            yield return Frames(locomotion, 0.3f, new LocomotionInput { GunEquipped = true });

            int casings = FirearmEffects.ActiveCasings;
            driver.PlayRemoteState(GunReload.FireName, 1.0f, 0.0f);
            Assert.IsTrue(model.MuzzleFlashObject.activeSelf, "the copy's gun did not flash");
            yield return Frames(locomotion, 0.2f, new LocomotionInput { GunEquipped = true });
            Assert.IsFalse(model.MuzzleFlashObject.activeSelf, "the copy's flash never went out");
            Assert.Greater(FirearmEffects.ActiveCasings, casings, "the copy's gun threw no brass");

            driver.PlayRemoteState(GunReload.TacticalName, 1.0f, 0.0f);
            GunReloadDriver reloader = driver.GunReloader;
            Assert.IsTrue(reloader.IsPlaying, "the copy did not play the reload");
            bool hidden = false;
            for (float t = 0.0f; t < reloader.Duration + 0.3f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                hidden |= !model.MagazinePart.gameObject.activeSelf;
            }
            Assert.IsTrue(hidden, "the copy's magazine never came out");
            Assert.IsTrue(model.MagazinePart.gameObject.activeSelf, "the copy's gun is left without its magazine");
            model.UnmountCosmetic(copy);
            Assert.IsFalse(locomotion.HoldsRifle);
#else
            yield break;
#endif
        }
    }
}
