using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE PUMP SHOTGUN ON THE REAL BODY (owner, 2026-11-18; docs/SHOTGUN.md): a shot up close lands its whole pattern as one blow and a far one
    /// fewer, softer pellets; the pump is worked after the shot with the support hand on it and the shell flies as it comes back; the tube is
    /// loaded a shell at a time, each piece announced to other players, the arms staying on the job between shells; a shot breaks the loading
    /// off; a gun run dry is pumped at the end; and another player's copy pumps and loads when told by name.
    /// </summary>
    public sealed class ShotgunPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const string PrefabPath = "Assets/CityX/Weapons/Shotgun.prefab";

        /// <summary>A finger's half-thickness on this 2.6 m body, metres: a finger bone's line is this far inside its skin.</summary>
        private const float FingerRadius = 0.012f;
        private readonly List<GameObject> _spawned = new List<GameObject>();

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
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            _spawned.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

#if UNITY_EDITOR
        private GameObject Player(Vector3 at)
        {
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _spawned.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
        }

        private PistolWeapon Gun()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " is missing -- CityX > Rebuild Firearms");
            GameObject gun = Object.Instantiate(prefab, new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity);
            _spawned.Add(gun);
            return gun.GetComponent<PistolWeapon>();
        }

        private Dummy Target(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = at;
            _spawned.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Dummy>();
        }

        private IEnumerator World()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(120, 1, 120);
            _spawned.Add(ground);
            yield return null;
        }

        private static IEnumerator Frames(PlayerLocomotion locomotion, float seconds, LocomotionInput input = default)
        {
            input.GunEquipped = true;
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        /// <summary>Where the camera would be: behind the body, looking past it.</summary>
        private static Vector3 Eye(GameObject player) => player.transform.position + Vector3.up * 1.8f - Vector3.forward * 2.0f;
#endif

        [UnityTest]
        public IEnumerator UpCloseTheWholePatternLandsAsOneBlowAndFarOffFewerSofterPellets()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun();
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.4f);
            FirearmProfile profile = gun.Profile;
            Vector3 eye = Eye(player);

            Dummy near = Target(eye + Vector3.forward * 6.0f);
            Assert.IsTrue(gun.Fire(eye, Vector3.forward, false, player), "it did not fire");
            Assert.AreEqual(1, near.Hits, "nine pellets were nine blows: the body takes the blast as one");
            Assert.AreEqual(profile.Damage * profile.Pellets, near.Taken, 0.5f, "up close not every pellet landed whole");
            Assert.AreEqual(profile.MagazineSize - 1, gun.MagazineAmmo, "one shot is one shell, however many pellets");
            Assert.IsFalse(gun.Fire(eye, Vector3.forward, false, player), "a second shot went before the pump was worked");
            Object.Destroy(near.gameObject);
            yield return Frames(locomotion, profile.FireCooldownSeconds + 0.1f);

            Dummy far = Target(eye + Vector3.forward * 27.0f);
            Assert.IsTrue(gun.Fire(eye, Vector3.forward, false, player));
            Assert.AreEqual(1, far.Hits, "the middle pellet missed what the crosshair was on");
            Assert.Greater(far.Taken, profile.Damage * 0.3f);
            Assert.Less(far.Taken, profile.Damage * profile.Pellets * 0.5f, "at 25 m the pattern still lands most of its damage");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-18, with a photo of a man holding a shotgun: "fix the shotgun hold, the left hand is clipping through, it should be holding it
        /// like the other image". The left hand cradles the forend: palm up under it, the wrist below it, no finger up past the barrel. Carried and aimed.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLeftHandCradlesTheForendPalmUpUnderItWithNoFingerOverTheTop([Values(false, true)] bool aiming)
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun();
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.8f, new LocomotionInput { Aiming = aiming });
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform gunFrame = gun.transform;
            Vector3 palmFaces = gunFrame.InverseTransformDirection(-hand.up);
            TestContext.WriteLine($"aiming {aiming}: palm faces {palmFaces.ToString("0.00")}, wrist at {gunFrame.InverseTransformPoint(hand.position).ToString("0.000")} in the gun's frame");
            Assert.Greater(palmFaces.y, 0.7f, "the palm is not turned up under the forend");
            const float forendBottom = -0.0058f, boreLine = 0.068f;   // Shotgun.points.json: the forend's underside, the barrel's middle
            Assert.Less(gunFrame.InverseTransformPoint(hand.position).y, forendBottom, "the wrist is not below the forend");
            // Owner (the same evening, a close picture): "not quite it still clipping through a bit". No joint of the hand, a finger's thickness
            // round it, inside the forend (shotgun_model.py's FOREND with its ribs, times 1.45, where the pump has it) or the barrel above it.
            Vector3 forendCentre = gun.ActionPart.localPosition + new Vector3(0.0f, -0.0058f, 0.0f);
            var forend = new Bounds(new Vector3(0.0f, (-0.0058f + 0.052f) * 0.5f, forendCentre.z), new Vector3(2.0f * 0.0428f, 0.0578f, 0.2175f));
            forend.Expand(2.0f * FingerRadius);
            var hits = new List<string>();
            foreach (Transform joint in hand.GetComponentsInChildren<Transform>())
            {
                Vector3 local = gunFrame.InverseTransformPoint(joint.position);
                TestContext.WriteLine($"  {joint.name,-28} {local.ToString("0.000")}");
                bool overTheTop = local.y > forend.max.y && Mathf.Abs(local.x) < 0.03f && local.z > forend.min.z && local.z < forend.max.z;
                if (forend.Contains(local) || overTheTop)
                {
                    hits.Add($"{joint.name} at {local.ToString("0.000")}");
                }
            }
            Assert.IsEmpty(hits, "the hand goes into the forend or over the top of the gun");
            foreach (HumanBodyBones bone in new[]
            {
                HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
                HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            })
            {
                Transform joint = animator.GetBoneTransform(bone);
                if (joint == null)
                {
                    continue;
                }
                Assert.Less(gunFrame.InverseTransformPoint(joint.position).y, boreLine, $"{bone} is up over the top of the gun");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePumpIsWorkedAfterTheShotTheSupportHandOnItAndTheShellFliesAsItComesBack()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun();
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            yield return Frames(locomotion, 0.5f);
            FirearmProfile profile = gun.Profile;
            Transform leftHand = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
            float pumpRest = gun.ActionPart.localPosition.z;
            float handRest = gun.transform.InverseTransformPoint(leftHand.position).z;
            Assert.Less(Mathf.Abs(handRest - pumpRest), 0.25f, "setup: the support hand is not at the forend");
            int casings = FirearmEffects.ActiveCasings;

            Assert.IsTrue(gun.Fire(Eye(player), Vector3.forward, false, player));
            float furthestPump = 0.0f, furthestHand = 0.0f, casingAt = -1.0f, pumpBackAt = -1.0f;
            for (float t = Dt; t < profile.FireCooldownSeconds; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                float pump = pumpRest - gun.ActionPart.localPosition.z;
                if (t < profile.ActionDelaySeconds - 2.0f * Dt)
                {
                    Assert.Less(pump, 0.001f, "the pump moved with the shot, before a hand could have worked it");
                    Assert.AreEqual(casings, FirearmEffects.ActiveCasings, "the shell flew with the shot: a pump gun holds it until it is worked");
                }
                if (pump > furthestPump)
                {
                    furthestPump = pump;
                    pumpBackAt = t;
                }
                furthestHand = Mathf.Max(furthestHand, handRest - gun.transform.InverseTransformPoint(leftHand.position).z);
                if (casingAt < 0.0f && FirearmEffects.ActiveCasings > casings)
                {
                    casingAt = t;
                }
            }
            TestContext.WriteLine($"pump back {furthestPump:0.000} m at {pumpBackAt:0.00} s, hand back {furthestHand:0.000} m, shell out at {casingAt:0.00} s");
            Assert.AreEqual(profile.ActionTravel, furthestPump, 0.02f, "the pump was not pulled its whole travel");
            Assert.Greater(furthestHand, profile.ActionTravel * 0.7f, "the support hand did not go back with the forend");
            Assert.AreEqual(profile.EjectDelaySeconds, casingAt, 0.06f, "the shell did not fly as the pump reached the rear");
            Assert.AreSame(profile.RackSound, gun.ReloadPlayer.clip, "the pump did not play its recording");
            Assert.Less(pumpRest - gun.ActionPart.localPosition.z, 0.001f, "the pump never came home");
            Assert.Less(Mathf.Abs(handRest - gun.transform.InverseTransformPoint(leftHand.position).z), 0.02f, "the hand did not come home with it");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheTubeIsLoadedAShellAtATimeEachPieceAnnouncedAndTheArmsStayOnTheJobBetweenShells()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun();
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            var driver = player.GetComponent<PlayerAnimationDriver>();
            var announced = new List<string>();
            driver.TimedStatePlayed += (name, speed, start) => announced.Add(name);
            yield return Frames(locomotion, 0.4f);

            gun.SetAmmo(2, 10);
            gun.TryReload();
            Assert.IsTrue(gun.IsReloading);
            Assert.IsTrue(driver.GunReloader.IsPlaying, "the loading's motion did not play");
            var counts = new List<int> { gun.MagazineAmmo };
            float elapsed = 0.0f, leastWeightMidway = 1.0f;
            bool shellSeen = false;
            while (gun.IsReloading && elapsed < 8.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
                if (gun.MagazineAmmo != counts[counts.Count - 1])
                {
                    counts.Add(gun.MagazineAmmo);
                }
                if (announced.Contains(GunReload.ShellName) && !announced.Contains(GunReload.ShellsEndName))
                {
                    leastWeightMidway = Mathf.Min(leastWeightMidway, locomotion.BakedPoseWeight);
                    shellSeen |= gun.MagazinePart.gameObject.activeSelf && gun.MagazinePart.localPosition.y < -0.05f;
                }
            }
            Assert.IsFalse(gun.IsReloading, "the loading never ended");
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6 }, counts, "the shells did not go in one at a time");
            Assert.AreEqual(6, gun.ReserveAmmo);
            CollectionAssert.AreEqual(
                new[] { GunReload.ShellsStartName, GunReload.ShellName, GunReload.ShellName, GunReload.ShellName, GunReload.ShellName, GunReload.ShellsEndName },
                announced.Where(GunReload.Has).ToArray(), "other players were not told each piece, in order");
            FirearmProfile profile = gun.Profile;
            Assert.AreEqual(profile.ShellStartSeconds + 4.0f * profile.ShellSeconds + profile.ShellEndSeconds, elapsed, 0.2f, "four shells did not take four shells' time");
            Assert.Greater(leastWeightMidway, 0.98f, "between shells the arms let go of the loading and went back toward the aim");
            Assert.IsTrue(shellSeen, "no shell was ever seen in the hand under the gun");
            yield return Frames(locomotion, 0.4f);
            Assert.AreEqual(0.0f, locomotion.BakedPoseWeight, "the loading never let go of the body");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AShotBreaksTheLoadingOffAndAGunRunDryIsPumpedAtTheEnd()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject player = Player(new Vector3(0, 0.05f, 0));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            PistolWeapon gun = Gun();
            gun.OnEquip(player, CityX.Items.ItemInstanceData.None);
            var driver = player.GetComponent<PlayerAnimationDriver>();
            var announced = new List<string>();
            driver.TimedStatePlayed += (name, speed, start) => announced.Add(name);
            yield return Frames(locomotion, 0.4f);

            // Dry: nothing to shoot until the first shell is in; the pump is worked at the end.
            gun.SetAmmo(0, 2);
            gun.TryReload();
            Assert.IsTrue(gun.IsReloading, "a dry gun with shells in reserve did not start loading");
            Assert.IsFalse(gun.Fire(Eye(player), Vector3.forward, false, player), "a dry gun fired");
            float pumpRest = gun.ActionPart.localPosition.z, furthestPump = 0.0f, elapsed = 0.0f;
            while (gun.IsReloading && elapsed < 6.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
                furthestPump = Mathf.Max(furthestPump, pumpRest - gun.ActionPart.localPosition.z);
            }
            Assert.AreEqual(2, gun.MagazineAmmo);
            Assert.AreEqual(0, gun.ReserveAmmo, "it took more shells than there were");
            Assert.Contains(GunReload.ShellsEndEmptyName, announced, "a gun run dry was not pumped at the end");
            Assert.AreEqual(gun.Profile.ActionTravel, furthestPump, 0.02f, "the pump was not worked");
            Assert.AreEqual(2, announced.Count(n => n == GunReload.ShellName), "two shells in reserve were not two shells loaded");

            // With rounds in it, a shot breaks the loading off where it is.
            yield return Frames(locomotion, 0.4f);
            gun.SetAmmo(1, 10);
            gun.TryReload();
            elapsed = 0.0f;
            while (gun.MagazineAmmo < 3 && elapsed < 4.0f)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                elapsed += Dt;
            }
            Assert.AreEqual(3, gun.MagazineAmmo, "setup: two shells should be in by now");
            Assert.IsTrue(gun.IsReloading, "setup: there is room for three more");
            Assert.IsTrue(gun.Fire(Eye(player), Vector3.forward, false, player), "a shot did not break the loading off");
            Assert.IsFalse(gun.IsReloading);
            Assert.IsFalse(driver.GunReloader.IsPlaying, "the loading went on over the shot");
            Assert.AreEqual(2, gun.MagazineAmmo);
            yield return Frames(locomotion, 0.5f);
            Assert.AreEqual(0.0f, locomotion.BakedPoseWeight, "the broken-off loading never let go of the body");
            Assert.AreEqual(2, gun.MagazineAmmo, "the loading went on after the shot");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyPumpsAfterAShotAndLoadsShellsWhenToldByName()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject copy = Player(new Vector3(3, 0.05f, 0));
            var locomotion = copy.GetComponent<PlayerLocomotion>();
            PistolWeapon model = Gun();
            // As NetworkedEquipmentSync builds a copy's item: mounted, then every script on it switched off.
            model.MountCosmetic(copy);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            Assert.IsTrue(locomotion.HoldsRifle, "the copy does not hold the shotgun in both hands");
            var driver = copy.GetComponent<PlayerAnimationDriver>();
            yield return Frames(locomotion, 0.3f);
            FirearmProfile profile = model.Profile;
            Transform leftHand = copy.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
            float pumpRest = model.ActionPart.localPosition.z;
            float handRest = model.transform.InverseTransformPoint(leftHand.position).z;

            int casings = FirearmEffects.ActiveCasings;
            driver.PlayRemoteState(GunReload.FireName, 1.0f, 0.0f);
            Assert.IsTrue(model.MuzzleFlashObject.activeSelf, "the copy's gun did not flash");
            Assert.AreEqual(casings, FirearmEffects.ActiveCasings, "the copy's shell flew with the shot");
            float furthestPump = 0.0f, furthestHand = 0.0f;
            for (float t = 0.0f; t < profile.FireCooldownSeconds; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
                furthestPump = Mathf.Max(furthestPump, pumpRest - model.ActionPart.localPosition.z);
                furthestHand = Mathf.Max(furthestHand, handRest - model.transform.InverseTransformPoint(leftHand.position).z);
            }
            Assert.AreEqual(profile.ActionTravel, furthestPump, 0.02f, "the copy's pump was not worked");
            Assert.Greater(furthestHand, profile.ActionTravel * 0.7f, "the copy's hand did not go back with the forend");
            Assert.Less(pumpRest - model.ActionPart.localPosition.z, 0.001f, "the copy's pump never came home");
            Assert.Greater(FirearmEffects.ActiveCasings, casings, "the copy's gun threw no shell");

            GunReloadDriver reloader = driver.GunReloader;
            bool shellSeen = false;
            float leastWeight = 1.0f;
            foreach (string piece in new[] { GunReload.ShellsStartName, GunReload.ShellName, GunReload.ShellName, GunReload.ShellsEndName })
            {
                driver.PlayRemoteState(piece, 1.0f, 0.0f);
                Assert.IsTrue(reloader.IsPlaying, $"the copy did not play {piece}");
                // A few frames more than the piece: on a copy the next one's name arrives a moment late.
                for (float t = 0.0f; t < reloader.Duration + 3.0f * Dt; t += Dt)
                {
                    locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                    yield return null;
                    shellSeen |= piece == GunReload.ShellName && model.MagazinePart.gameObject.activeSelf && model.MagazinePart.localPosition.y < -0.05f;
                    if (piece == GunReload.ShellName)
                    {
                        leastWeight = Mathf.Min(leastWeight, locomotion.BakedPoseWeight);
                    }
                }
            }
            Assert.IsTrue(shellSeen, "the copy never had a shell in its hand");
            Assert.Greater(leastWeight, 0.98f, "the copy's arms let go between shells while the next piece's name was on its way");
            yield return Frames(locomotion, 0.5f);
            Assert.AreEqual(0.0f, locomotion.BakedPoseWeight, "the copy never came back out of the loading");
            model.UnmountCosmetic(copy);
#else
            yield break;
#endif
        }
    }
}
