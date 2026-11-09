using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Recovery flip filmed side on, frame by frame, so how it reads can be looked at (Logs/CityShots/recovery-*.png): thrown
    /// and flipping in the air, then lying on the ground and springing up into it. Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class RecoveryCapture
    {
        private const int FramesBetweenShots = 3;

        [UnityTest]
        public IEnumerator FilmTheFlip()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 45.0f;
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var ragdoll = player.GetComponent<Ragdoll>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var vitals = player.GetComponent<PlayerVitals>();
            Animator animator = player.GetComponentInChildren<Animator>();
            try
            {
                yield return null;
                vitals.KnockDown(new Vector3(0.0f, 9.0f, 6.0f));
                for (int i = 0; i < 14; i++)
                {
                    ragdoll.Tick(Time.deltaTime);
                    yield return null;
                }
                Film(camera, animator, "recovery-thrown", 0);
                Assert.IsTrue(ragdoll.Recover(Vector3.forward));
                Debug.Log($"CityX recovery capture, thrown: flip {ragdoll.Flip.SweepDegrees:0} deg from {ragdoll.Flip.StartDegrees:0}, {ragdoll.Flip.Seconds:0.00} s");
                yield return FilmUntilLanded(camera, animator, ragdoll, locomotion, "recovery-thrown");

                yield return new WaitForSeconds(1.0f);
                vitals.KnockDown(new Vector3(0.0f, 1.0f, -3.0f));
                for (float t = 0.0f; t < 1.5f; t += Time.deltaTime)
                {
                    ragdoll.Tick(Time.deltaTime);
                    yield return null;
                }
                Film(camera, animator, "recovery-lying", 0);
                Assert.IsTrue(ragdoll.Recover(Vector3.forward));
                Debug.Log($"CityX recovery capture, lying: flip {ragdoll.Flip.SweepDegrees:0} deg from {ragdoll.Flip.StartDegrees:0}, {ragdoll.Flip.Seconds:0.00} s");
                yield return FilmUntilLanded(camera, animator, ragdoll, locomotion, "recovery-lying");
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(camera.gameObject);
                Object.Destroy(sun.gameObject);
                Object.Destroy(ground);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator FilmUntilLanded(Camera camera, Animator animator, Ragdoll ragdoll, PlayerLocomotion locomotion, string name)
        {
            int shot = 1;
            int frames = 0;
            for (float t = 0.0f; t < 3.0f && (ragdoll.IsLimp || !locomotion.IsGrounded); t += Time.deltaTime)
            {
                if (ragdoll.IsLimp)
                {
                    ragdoll.Tick(Time.deltaTime);
                }
                else
                {
                    locomotion.Tick(default, Time.deltaTime);
                }
                yield return null;
                if (++frames % FramesBetweenShots == 0)
                {
                    Film(camera, animator, name, shot++);
                }
            }
            for (int i = 0; i < 2; i++)
            {
                locomotion.Tick(default, Time.deltaTime);
                yield return null;
            }
            Film(camera, animator, name, shot);
        }

        /// <summary>Side on (from the body's right) and a three-quarter view, both looking at the hips; how high the body is, logged.</summary>
        private static void Film(Camera camera, Animator animator, string name, int shot)
        {
            Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            float lowest = float.MaxValue;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand,
                         HumanBodyBones.RightHand, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
            {
                lowest = Mathf.Min(lowest, animator.GetBoneTransform(bone).position.y);
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"CityX recovery capture {name} {shot:00}: feet of the capsule {animator.transform.parent.position.y:0.00} m, hips {hips.y:0.00} m, " +
                      $"lowest part {lowest:0.00} m; jump state {state.IsName("Jump")} at {state.normalizedTime:0.00} of {state.length:0.00} s");
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{name}-side-{shot:00}", hips + new Vector3(8.0f, 0.5f, 0.0f), hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{name}-front-{shot:00}", hips + new Vector3(5.0f, 1.5f, 6.0f), hips);
        }
    }
}
