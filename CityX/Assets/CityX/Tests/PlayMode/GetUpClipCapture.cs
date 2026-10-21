using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player's body through the GettingUp clip, a dozen moments from start to end, side on, so how the stand-up reads can be
    /// looked at (Logs/CityShots/getup-NN.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class GetUpClipCapture
    {
        private const int Shots = 12;

        [UnityTest]
        public IEnumerator FilmTheStandUp()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 40.0f;
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerLocomotion>().enabled = false;
            yield return null;
            Animator animator = player.GetComponentInChildren<Animator>();
            for (int i = 0; i < Shots; i++)
            {
                animator.Play("GettingUp", 0, i / (Shots - 1.0f) * 0.999f);
                animator.Update(0.0f);
                yield return null;
                LimbFloorClamp.Apply(animator, 0.0f);
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 look = new Vector3(hips.x, 1.0f, hips.z);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"getup-{i:00}", look + new Vector3(6.0f, 1.0f, 3.0f), look);
            }
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }
    }
}
