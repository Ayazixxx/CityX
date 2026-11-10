using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the whole ragdoll-to-standing recovery as it plays (Ragdoll.BeginStandingUp), from lying on the back, the front and the
    /// side, on level ground, a slope and stairs, a picture every tenth of a second, side on (Logs/CityShots/standup-{ground}-{pose}-NN.png).
    /// Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class StandUpCapture
    {
        private const int Shots = 20;
        private const int FramesBetweenShots = 6;

        /// <summary>Which way the chest faces as the body is laid down, its head toward +X (up the slope and the stairs).</summary>
        private static readonly (string Name, Vector3 ChestFaces)[] Poses =
        {
            ("back", Vector3.up),
            ("front", Vector3.down),
            ("side", Vector3.forward),
        };

        private static readonly string[] Grounds = { "level", "slope", "stairs" };

        [UnityTest]
        public IEnumerator FilmTheRecovery()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 40.0f;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");

            foreach (string ground in Grounds)
            {
                foreach ((string name, Vector3 chestFaces) in Poses)
                {
                    GameObject floor = LyingBody.BuildGround(ground);
                    GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
                    player.GetComponent<PlayerInputReader>().enabled = false;
                    Ragdoll ragdoll = player.GetComponent<Ragdoll>();
                    Animator animator = player.GetComponentInChildren<Animator>();
                    yield return null;
                    ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
                    yield return new WaitForFixedUpdate();
                    LyingBody.LayDown(ragdoll, animator, chestFaces);
                    yield return new WaitForSeconds(2.5f);

                    // Side on to the body as it lies.
                    Vector3 length = animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    length.y = 0.0f;
                    Vector3 side = Vector3.Cross(Vector3.up, length.normalized);
                    if (side.z < 0.0f)
                    {
                        side = -side;
                    }
                    ragdoll.BeginStandingUp(force: true);
                    yield return null;   // the first pose of the get-up is on the bones after its LateUpdate
                    for (int shot = 0; shot < Shots; shot++)
                    {
                        Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                        Vector3 look = new Vector3(hips.x, hips.y + 0.4f, hips.z);
                        BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"standup-{ground}-{name}-{shot:00}",
                            look + side * 6.5f + Vector3.up * 1.0f, look);
                        for (int frame = 0; frame < FramesBetweenShots; frame++)
                        {
                            ragdoll.Tick(Time.deltaTime);
                            yield return null;
                        }
                    }
                    Object.Destroy(player);
                    Object.Destroy(floor);
                    yield return null;
                }
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
#else
            yield break;
#endif
        }
    }
}
