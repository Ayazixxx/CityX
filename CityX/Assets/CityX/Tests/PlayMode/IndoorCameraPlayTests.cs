using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// THE CAMERA INDOORS (CameraRoom), on the real body and camera in Old Guy's house: in his den the shot comes in to the room framing and the
    /// camera is in the room, not in a wall; out in the open yard it goes back to the full distance, easing, not jumping.
    /// </summary>
    public sealed class IndoorCameraPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

#if UNITY_EDITOR
        internal static ThirdPersonCamera Rig(List<GameObject> made, GameObject player, float yaw)
        {
            var rig = new GameObject("FollowCamera");
            made.Add(rig);
            Camera camera = rig.AddComponent<Camera>();
            camera.fieldOfView = ThirdPersonCamera.DefaultFieldOfView;
            ThirdPersonCamera follow = rig.AddComponent<ThirdPersonCamera>();
            follow.Target = player.transform;
            follow.Yaw = yaw;
            follow.Pitch = ThirdPersonCamera.DefaultPitch;
            return follow;
        }

        internal static GameObject Body(List<GameObject> made, Vector3 at)
        {
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at + Vector3.up * 0.1f, Quaternion.identity);
            made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
        }

        private static float Arm(ThirdPersonCamera follow, GameObject player)
        {
            Vector3 pivot = player.transform.position + Vector3.up * (player.GetComponent<PlayerLocomotion>().StandingHeight * follow.PivotHeightFraction);
            return Vector3.Distance(pivot, follow.transform.position);
        }
#endif

        [UnityTest]
        public IEnumerator InTheDenTheShotTightensAndOutsideItEasesBackOut()
        {
#if UNITY_EDITOR
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            GameObject player = Body(_made, House.OldGuyInTheDen);
            ThirdPersonCamera follow = Rig(_made, player, House.OldGuyInTheDenYaw);
            for (int i = 0; i < 90; i++) yield return null;

            Assert.Greater(follow.IndoorBlend, 0.95f, "Old Guy's den is not taken for a room");
            float inside = Arm(follow, player);
            Assert.Less(inside, follow.IndoorDistance + follow.ShoulderOffset + 0.5f, $"the camera hangs {inside:0.00} m back in a room");
            Assert.IsFalse(Physics.CheckSphere(follow.transform.position, 0.1f, ~0, QueryTriggerInteraction.Ignore), "the camera is inside something");

            // Out into the open yard, far from the house: the full distance again, and on the way there no jump.
            // In hops of 5 m (more in one frame is a teleport, which starts the camera again where it stands).
            var yard = new Vector3(-40.0f, 0.1f, -40.0f);
            float largestStep = 0.0f, last = Arm(follow, player);
            for (int i = 0; i < 200; i++)
            {
                player.transform.position = Vector3.MoveTowards(player.transform.position, yard, 5.0f);
                yield return null;
                float now = Arm(follow, player);
                largestStep = Mathf.Max(largestStep, now - last);
                last = now;
            }
            Assert.Less(follow.IndoorBlend, 0.05f, "the open yard is taken for a room");
            Assert.AreEqual(follow.Distance, last, follow.ShoulderOffset + 0.3f, "the camera does not go back to its distance outside");
            Assert.Less(largestStep, 0.3f, $"the camera jumped {largestStep:0.00} m in a frame going back out");
#else
            yield break;
#endif
        }
    }
}
