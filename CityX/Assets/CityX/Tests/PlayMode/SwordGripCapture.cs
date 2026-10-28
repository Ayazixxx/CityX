using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the two-handed hold on the spike sword up close (docs/SOULSLIKE_COMBAT.md) to Logs/CityShots/grip2h-*.png -- the
    /// carry and the guard, each from the thumb's side of the right hand, from the back of that hand, from above, and from the front with both
    /// hands in view. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordGripCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordGripCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheTwoHandedHold()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.03f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab"),
                new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            sword.OnEquip(player, ItemInstanceData.None);
            var combat = player.GetComponent<SoulsSwordCombat>();
            var idle = new LocomotionInput { SwordEquipped = true };
            MeshFilter blade = sword.GetComponentInChildren<MeshFilter>();
            Bounds bounds = blade.sharedMesh.bounds;
            float bottom = sword.transform.InverseTransformPoint(blade.transform.TransformPoint(bounds.min)).y;
            Debug.Log($"CityX grip: the spike sword's handle ends {-bottom:0.00} m below the guard; the fists sit {SwordWeapon.DefaultPrimaryGripDown:0.00} and {SwordWeapon.DefaultSupportGripDown:0.00} below it");

            for (int i = 0; i < 40; i++)
            {
                combat.Tick(Step);
                locomotion.Tick(idle, Step);
                yield return null;
            }
            yield return Views(camera, player, sword, "grip2h-carry");

            for (int i = 0; i < 40; i++)
            {
                combat.SetBlockHeld(true);
                combat.Tick(Step);
                locomotion.Tick(idle, Step);
                yield return null;
            }
            yield return Views(camera, player, sword, "grip2h-guard");
            combat.SetBlockHeld(false);

            Object.Destroy(sword.gameObject);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Views(Camera camera, GameObject player, SwordWeapon sword, string name)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 grip = sword.transform.position;
            Vector3 thumbSide = hand.TransformDirection(Vector3.forward);
            Vector3 back = hand.TransformDirection(Vector3.up);
            Vector3 mid = (grip + leftHand.position) * 0.5f;
            yield return Shoot(camera, name + "-thumb", grip + thumbSide * 0.6f + Vector3.up * 0.15f, grip);
            yield return Shoot(camera, name + "-back", grip + back * 0.6f + Vector3.up * 0.1f, grip);
            yield return Shoot(camera, name + "-above", grip + Vector3.up * 0.7f + player.transform.forward * 0.2f, grip);
            yield return Shoot(camera, name + "-front", mid + player.transform.forward * 1.3f + Vector3.up * 0.1f, mid);
            yield return Shoot(camera, name + "-whole", player.transform.position + new Vector3(2.5f, 1.6f, 2.5f), player.transform.position + Vector3.up * 1.2f);
            yield return Shoot(camera, name + "-left", player.transform.position + new Vector3(-2.8f, 1.5f, 1.8f), player.transform.position + Vector3.up * 1.2f);
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
