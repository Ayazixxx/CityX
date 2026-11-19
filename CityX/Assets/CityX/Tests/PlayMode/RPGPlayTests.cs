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
    /// THE RPG ON THE REAL BODY (owner, 2026-11-19: "make a basic rpg rocket launcher that is animated"; docs/RPG.md): shouldered with the tube on
    /// the shoulder beside the head and the round at the crosshair; fired, its rocket flies to a wall and bursts there, hurting who stands by it,
    /// the round gone from the muzzle until the reload (which starts at once) seats the next; the back-blast hurts who stands close behind, not
    /// beside; and another player's copy fires the same rocket, hurting no one.
    /// </summary>
    public sealed class RPGPlayTests
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
            foreach (FlyingRocket rocket in Object.FindObjectsByType<FlyingRocket>(FindObjectsSortMode.None))
            {
                Object.Destroy(rocket.gameObject);
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

#if UNITY_EDITOR
        private GameObject Spawn(string path, Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- rebuild the scene (RPGBuilder)");
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            _spawned.Add(go);
            return go;
        }

        private GameObject Body(Vector3 at)
        {
            GameObject body = Spawn("Assets/CityX/Character/Player.prefab", at);
            body.GetComponent<PlayerInputReader>().enabled = false;
            if (body.GetComponent<RocketLauncher>() == null)
            {
                body.AddComponent<RocketLauncher>();   // as PlayerAbilityWiring puts it on the game's bodies (this prefab carries no abilities)
            }
            return body;
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

        private (GameObject body, PistolWeapon rpg) Armed(Vector3 at)
        {
            GameObject body = Body(at);
            var rpg = Spawn("Assets/CityX/Weapons/RPG.prefab", new Vector3(40, 1, 40)).GetComponent<PistolWeapon>();
            rpg.OnEquip(body, ItemInstanceData.None);
            return (body, rpg);
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
        public IEnumerator ShoulderedTheTubeLiesOnTheShoulderBesideTheHeadAndTheRoundIsOnTheCrosshair()
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon rpg) = Armed(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            Vector3 forward = Vector3.forward;
            Vector3 camera = body.transform.position + Vector3.up * 2.4f + Vector3.right * 0.9f - forward * 4.0f;
            yield return Frames(locomotion, 1.0f, new LocomotionInput { GunEquipped = true, Aiming = true, AimCameraPosition = camera, AimCameraForward = forward });
            Assert.IsTrue(locomotion.HoldsRifle, "the RPG is not held as a rifle");
            Animator animator = body.GetComponentInChildren<Animator>();
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 rest = rpg.transform.TransformPoint(new Vector3(0.0f, 0.0377f, -0.2175f));   // RPG.points.json: where the tube lies on the shoulder
            Vector3 tubeAxis = rpg.transform.TransformPoint(new Vector3(0.0f, 0.0899f, -0.2175f));
            float headRadius = 0.35f * body.transform.lossyScale.y;
            TestContext.WriteLine($"rest {rest - shoulder.position} from the shoulder joint, tube {Vector3.Distance(tubeAxis, head.position):0.00} m from the head's middle");
            Assert.Greater(rest.y, shoulder.position.y - 0.05f, "the tube is not up on the shoulder");
            Assert.Less(rest.y, shoulder.position.y + 0.35f, "the tube floats over the shoulder");
            Assert.Greater(Vector3.Distance(tubeAxis, head.position), headRadius, "the tube goes through the head");
            Vector3 aimPoint = camera + forward * PlayerLocomotion.GunAimConvergenceDistance;
            float error = Vector3.Angle(rpg.MuzzleFlashObject.transform.forward, aimPoint - rpg.MuzzleFlashObject.transform.position);
            Assert.Less(error, 5.0f, "the tube does not point at the crosshair's point");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FiredTheRocketBurstsAtTheWallHurtingWhoStandsByItAndTheNextRoundIsLoaded()
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon rpg) = Armed(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 3, 30);
            wall.transform.localScale = new Vector3(20, 6, 1);
            _spawned.Add(wall);
            GameObject target = Body(new Vector3(2.0f, 0.05f, 28.0f));
            var victim = target.GetComponent<PlayerVitals>();
            float before = victim.Health;
            var launcher = body.GetComponent<RocketLauncher>();
            var events = new List<AbilityEvent>();
            ((IReplicatedAbility)launcher).Fired += e => events.Add(e);
            yield return Frames(locomotion, 0.5f, new LocomotionInput { GunEquipped = true });

            Assert.IsTrue(rpg.MagazinePart.gameObject.activeSelf, "the loaded round does not show at the muzzle");
            Vector3 eye = body.transform.position + Vector3.up * 2.2f;
            Assert.IsTrue(rpg.Fire(eye, Vector3.forward, false, body), "it did not fire");
            Assert.AreEqual(0, rpg.MagazineAmmo);
            Assert.IsFalse(rpg.MagazinePart.gameObject.activeSelf, "the round is still in the muzzle after it flew");
            Assert.IsTrue(rpg.IsReloading, "a single shot did not start its reload");
            FlyingRocket rocket = Object.FindFirstObjectByType<FlyingRocket>();
            Assert.IsNotNull(rocket, "no rocket flies");
            for (float t = 0.0f; rocket != null && !rocket.HasExploded && t < 3.0f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
            }
            Assert.IsTrue(rocket == null || rocket.HasExploded, "the rocket never burst");
            Assert.Less(victim.Health, before, "the one standing by the wall was not hurt");
            Assert.AreEqual(RocketLauncher.Launched, events[0].Kind);
            Assert.IsTrue(events.Exists(e => e.Kind == RocketLauncher.Exploded && Mathf.Abs(e.Origin.z - 29.5f) < 1.0f),
                "other players were not told it burst at the wall");

            for (float t = 0.0f; rpg.IsReloading && t < 5.0f; t += Dt)
            {
                locomotion.Tick(new LocomotionInput { GunEquipped = true }, Dt);
                yield return null;
            }
            Assert.IsFalse(rpg.IsReloading);
            Assert.AreEqual(1, rpg.MagazineAmmo, "the next round was not loaded");
            Assert.AreEqual(FirearmProfile.RPG().StartingReserveAmmo - 1, rpg.ReserveAmmo);
            yield return Frames(locomotion, 0.2f, new LocomotionInput { GunEquipped = true });
            Assert.IsTrue(rpg.MagazinePart.gameObject.activeSelf, "the next round does not show at the muzzle");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheBackBlastHurtsWhoStandsCloseBehindNotBeside()
        {
#if UNITY_EDITOR
            yield return World();
            (GameObject body, PistolWeapon rpg) = Armed(new Vector3(0, 0.05f, 0));
            var locomotion = body.GetComponent<PlayerLocomotion>();
            GameObject behind = Body(new Vector3(0.3f, 0.05f, -2.5f));
            GameObject beside = Body(new Vector3(3.5f, 0.05f, 0.0f));
            float behindBefore = behind.GetComponent<PlayerVitals>().Health, besideBefore = beside.GetComponent<PlayerVitals>().Health;
            yield return Frames(locomotion, 1.0f, new LocomotionInput
            {
                GunEquipped = true, Aiming = true, AimCameraPosition = body.transform.position + Vector3.up * 2.4f - Vector3.forward * 4.0f, AimCameraForward = Vector3.forward,
            });
            Assert.IsTrue(rpg.Fire(body.transform.position + Vector3.up * 2.4f, Vector3.forward, true, body));
            yield return null;
            Assert.Less(behind.GetComponent<PlayerVitals>().Health, behindBefore, "the one close behind was not hurt by the back-blast");
            Assert.AreEqual(besideBefore, beside.GetComponent<PlayerVitals>().Health, 0.01f, "the one beside was hurt by the back-blast");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyFiresTheSameRocketAndItHurtsNoOne()
        {
#if UNITY_EDITOR
            yield return World();
            GameObject copy = Body(new Vector3(0, 0.05f, 0));
            var model = Spawn("Assets/CityX/Weapons/RPG.prefab", new Vector3(40, 1, 40)).GetComponent<PistolWeapon>();
            model.MountCosmetic(copy);
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            GameObject bystander = Body(new Vector3(1.0f, 0.05f, 12.0f));
            float before = bystander.GetComponent<PlayerVitals>().Health;
            var launcher = copy.GetComponent<RocketLauncher>();
            var told = new List<AbilityEvent>();
            ((IReplicatedAbility)launcher).Fired += e => told.Add(e);
            yield return null;
            copy.GetComponent<PlayerAnimationDriver>().PlayRemoteState(GunReload.FireName, 1.0f, 0.0f);
            Assert.IsFalse(model.MagazinePart.gameObject.activeSelf, "the copy's round did not leave its muzzle");
            launcher.PlayRemote(new AbilityEvent(RocketLauncher.Launched, model.MagazinePart.position, Vector3.forward));
            FlyingRocket rocket = Object.FindFirstObjectByType<FlyingRocket>();
            Assert.IsNotNull(rocket, "the copy fired no rocket");
            Assert.IsTrue(rocket.IsCopy);
            for (int i = 0; i < 6; i++) yield return null;
            launcher.PlayRemote(new AbilityEvent(RocketLauncher.Exploded, new Vector3(1.0f, 1.2f, 12.0f), Vector3.zero));
            yield return null;
            Assert.IsTrue(rocket == null || rocket.HasExploded, "the copy's rocket did not burst where its owner's did");
            Assert.AreEqual(before, bystander.GetComponent<PlayerVitals>().Health, 0.01f, "another player's rocket hurt someone on this machine");
            Assert.IsEmpty(told, "a copy told other players about a rocket");
#else
            yield break;
#endif
        }
    }
}
