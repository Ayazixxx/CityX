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
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;   // back to the steady frames every test runs on (0 left later tests on real batch frames)
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

        /// <summary>
        /// Owner, 2026-11-19, with photos of soldiers holding AKs: "the left hand is clipping through. fix that ... something like this (fix the left
        /// hand only right looks fine)". The left hand cradles the lower handguard: palm up under it, the wrist below it, every joint of the hand a
        /// finger's thickness out of it, none over the top of the gun. Carried and aimed.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAK47sLeftHandCradlesTheHandguardPalmUpWithNoFingerThroughIt([Values(false, true)] bool aiming)
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/AK47.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.8f, new LocomotionInput { GunEquipped = true, Aiming = aiming });
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform gunFrame = gun.transform;
            Vector3 palmFaces = gunFrame.InverseTransformDirection(-hand.up);
            TestContext.WriteLine($"aiming {aiming}: palm faces {palmFaces.ToString("0.00")}, wrist at {gunFrame.InverseTransformPoint(hand.position).ToString("0.000")}");
            Assert.Greater(palmFaces.y, 0.7f, "the palm is not turned up under the handguard");

            // The lower handguard (ak47_model.py: 46 to 52 mm thick round the barrel, times 1.45), and the gun's top over it (the upper handguard).
            const float halfWidth = 0.0334f, bottom = 0.0073f, top = 0.0827f, front = 0.587f, back = 0.370f, gunTop = 0.109f;
            const float fingerRadius = 0.012f;
            Assert.Less(gunFrame.InverseTransformPoint(hand.position).y, bottom, "the wrist is not below the handguard");
            var handguard = new Bounds(new Vector3(0.0f, (bottom + top) * 0.5f, (front + back) * 0.5f), new Vector3(2.0f * halfWidth, top - bottom, front - back));
            handguard.Expand(2.0f * fingerRadius);
            var through = new List<string>();
            foreach (Transform joint in hand.GetComponentsInChildren<Transform>())
            {
                Vector3 local = gunFrame.InverseTransformPoint(joint.position);
                TestContext.WriteLine($"  {joint.name,-28} {local.ToString("0.000")}");
                bool overTheTop = local.y > gunTop && Mathf.Abs(local.x) < 0.03f && local.z > back && local.z < front;
                if (handguard.Contains(local) || overTheTop)
                {
                    through.Add($"{joint.name} at {local.ToString("0.000")}");
                }
            }
            Assert.IsEmpty(through, "the left hand goes into the handguard or over the top of the gun");
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
                for (float t = 0.0f; t < 2.0f; t += Dt)
                {
                    follow.IsAiming = true;
                    follow.PistolAim = locomotion.HoldsPistol;
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
                if (locomotion.HoldsPistol)
                {
                    // The centre clear is not enough (half the Glock was behind the head with this passing): every corner of it, and it must be big enough to read.
                    float least = float.MaxValue;
                    float left = 1.0f;
                    float right = 0.0f;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = gunBounds.center + Vector3.Scale(gunBounds.extents, new Vector3((corner & 1) * 2 - 1, (corner & 2) - 1, (corner & 4) / 2 - 1));
                        float share = Mathf.Clamp01(Vector3.Dot(c - a, point - a) / (point - a).sqrMagnitude);
                        least = Mathf.Min(least, Vector3.Distance(c, a + (point - a) * share) - FusedBodyShape.HeadRadius);
                        float x = view.WorldToViewportPoint(point).x;
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                    }
                    Debug.Log($"CityX pistol while aiming: {least * 100:0} cm clear of the head at its nearest, {(right - left) * 100:0.0}% of the screen wide");
                    Assert.Greater(least, 0.0f, $"part of the pistol is behind the head (by {-least * 100:0} cm)");
                    Assert.Greater(right - left, 0.06f, "the pistol is too small on the screen to read");
                }
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

        /// <summary>
        /// THE AK-47'S OWN RELOAD RECORDING (owner, 2026-10-14: "replace the ak47 reload sound with this"): it plays from the start of each reload, the
        /// one for rounds left or the empty one, for the shooter and on another player's copy of them (multiplayer-first), and stops when a shot
        /// cuts the reload short.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAK47ReloadPlaysItsRecordingForTheShooterAndOnOtherPlayersCopies()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun("Assets/CityX/Weapons/AK47.prefab");
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.4f, new LocomotionInput { GunEquipped = true });
            Assert.IsNotNull(gun.Profile.ReloadSound, "setup: the AK-47 has no reload recording -- CityX > Rebuild Firearms");

            // Rounds left: the shorter recording, from its start.
            gun.SetAmmo(10, 60);
            gun.TryReload();
            GunReloadDriver reloader = player.GetComponent<PlayerAnimationDriver>().GunReloader;
            Assert.IsTrue(reloader.IsPlaying && !reloader.IsEmptyReload, "setup: the tactical reload did not start");
            Assert.AreSame(gun.Profile.ReloadSound, gun.ReloadPlayer.clip, "the reload with rounds left did not play its recording");
            Assert.IsTrue(gun.ReloadPlayer.isPlaying);
            yield return Frames(locomotion, 0.8f, new LocomotionInput { GunEquipped = true });
            Assert.IsTrue(gun.ReloadPlayer.isPlaying && gun.ReloadPlayer.clip == gun.Profile.ReloadSound, "the recording stopped or was replaced part-way");

            // A shot once the fresh magazine is in cuts the reload short, and its recording with it.
            float elapsed = 0.0f;
            while (!gun.ReloadAmmoIn && elapsed < 3.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
            }
            Assert.IsTrue(gun.ReloadAmmoIn, "setup: the new magazine never went in");
            Vector3 origin = player.transform.position + Vector3.up * 2.0f - Vector3.forward * 2.0f;
            Assert.IsTrue(gun.Fire(origin, Vector3.forward, false, player), "setup: the shot after the click did not fire");
            Assert.IsFalse(gun.ReloadPlayer.isPlaying, "the reload recording played on over the shot that cut the reload short");
            yield return Frames(locomotion, 0.5f, new LocomotionInput { GunEquipped = true });

            // Empty: the longer recording, with the action racked.
            gun.SetAmmo(0, 60);
            gun.TryReload();
            Assert.IsTrue(reloader.IsPlaying && reloader.IsEmptyReload, "setup: the empty reload did not start");
            Assert.AreSame(gun.Profile.EmptyReloadSound, gun.ReloadPlayer.clip, "the empty reload did not play its recording");
            Assert.IsTrue(gun.ReloadPlayer.isPlaying);

            // Another player's copy, told the reload by name, plays the same recording on its own gun.
            GameObject copy = Player(new Vector3(4, 0.05f, 0));
            var copyLocomotion = copy.GetComponent<PlayerLocomotion>();
            PistolWeapon model = Gun("Assets/CityX/Weapons/AK47.prefab");
            model.MountCosmetic(copy);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;   // as NetworkedEquipmentSync leaves a copy's item
            }
            yield return Frames(copyLocomotion, 0.3f, new LocomotionInput { GunEquipped = true });
            copy.GetComponent<PlayerAnimationDriver>().PlayRemoteState(GunReload.EmptyName, 1.0f, 0.0f);
            Assert.AreSame(model.Profile.EmptyReloadSound, model.ReloadPlayer.clip, "another player's copy did not play the empty reload's recording");
            Assert.IsTrue(model.ReloadPlayer.isPlaying);
            model.UnmountCosmetic(copy);
#else
            yield break;
#endif
        }
    }
}
