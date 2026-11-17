using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the sprint with nothing in hand and with the baseball bat (the two-handed run), at six moments of a stride, from straight behind and from
    /// where the game's camera sits (5 m back, a little right, above the head), wearing the IDK hair (a bare head is a ball, its tilt unseen); then the face
    /// from in front. Written to Logs/CityShots/run2h-{plain,bat}[-game]-N.png and run2h-{plain,bat}-front.png, each run's head tip and turn logged.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TwoHandedRunCapture
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Moments = 6;
        private const int FramesBetween = 5;

        [UnityTest]
        public IEnumerator RenderTheRunsFromBehind()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            foreach (string run in new[] { "plain", "bat" })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, -150.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                HairPushPlayTests.PutHairOn(player).SetStyle(CharacterHair.IdkNeedsWork);   // a bare head is a ball: hair shows how it is held
                var locomotion = player.GetComponent<PlayerLocomotion>();
                Animator animator = player.GetComponentInChildren<Animator>();
                GameObject bat = null;
                if (run == "bat")
                {
                    bat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/BaseballBat.prefab"), new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
                    bat.GetComponent<SwordWeapon>().OnEquip(player, CityX.Items.ItemInstanceData.None);
                }
                var input = new LocomotionInput { Move = Vector2.up, Sprint = true, SwordEquipped = run == "bat" };
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform body = player.transform;
                float tip = 0.0f, turn = 0.0f;
                int samples = 0;
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(input, Dt);
                    yield return null;
                }
                for (int moment = 0; moment < Moments; moment++)
                {
                    for (int i = 0; i < FramesBetween; i++)
                    {
                        locomotion.Tick(input, Dt);
                        yield return null;
                        tip += Mathf.Atan2(Vector3.Dot(head.up, body.right), Vector3.Dot(head.up, body.up)) * Mathf.Rad2Deg;
                        turn += Vector3.SignedAngle(body.forward, Vector3.ProjectOnPlane(head.forward, body.up), body.up);
                        samples++;
                    }
                    Vector3 chest = body.position + Vector3.up * 1.3f;
                    camera.fieldOfView = 30.0f;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"run2h-{run}-{moment}", chest - body.forward * 7.0f + Vector3.up * 0.3f, chest);
                    camera.fieldOfView = 50.0f;
                    Vector3 eye = body.position + Vector3.up * 3.2f - body.forward * 5.0f + body.right * 0.87f;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"run2h-{run}-game-{moment}", eye, eye + body.forward * 10.0f - Vector3.up * 2.4f);
                }
                // The face and the hair from in front and a little below.
                camera.fieldOfView = 30.0f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"run2h-{run}-front", head.position + body.forward * 4.0f - Vector3.up * 0.6f, head.position);
                Debug.Log($"CityX run2h {run}: head tipped {tip / samples:0.0} deg right, turned {turn / samples:0.0} deg right of the run");
                Object.Destroy(player);
                if (bat != null) Object.Destroy(bat);
                yield return null;
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
