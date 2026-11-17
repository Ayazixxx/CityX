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
    /// Not a test: the follow camera in Old Guy's house, at a few places and facings, as it was (no room framing) and as it is (CameraRoom), wearing the
    /// IDK hair. Written to Logs/CityShots/indoorcam-{spot}-{old,new}.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class IndoorCameraCapture
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator RenderTheCameraInTheHouse()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            OldGuysHousePlayTests.MakeHouse(_made, true, false);
            var sun = new GameObject("Sun").AddComponent<Light>();
            _made.Add(sun.gameObject);
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35.0f, -60.0f, 0.0f);
            var spots = new (string Name, Vector3 At, float Yaw)[]
            {
                ("den", House.OldGuyInTheDen, House.OldGuyInTheDenYaw),
                ("den-back", House.OldGuyInTheDen, House.OldGuyInTheDenYaw + 180.0f),
                ("hall", new Vector3(20.0f, House.LevelY(1), 10.5f), 0.0f),
                ("upstairs", House.SwordsmanSpot, 90.0f),
                ("upstairs-back", House.SwordsmanSpot, 270.0f),
            };
            foreach ((string name, Vector3 at, float yaw) in spots)
            {
                foreach (bool roomFraming in new[] { false, true })
                {
                    GameObject player = IndoorCameraPlayTests.Body(_made, at);
                    player.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
                    HairPushPlayTests.PutHairOn(player).SetStyle(CharacterHair.IdkNeedsWork);
                    ThirdPersonCamera follow = IndoorCameraPlayTests.Rig(_made, player, yaw);
                    var lamp = new GameObject("Lamp").AddComponent<Light>();
                    _made.Add(lamp.gameObject);
                    lamp.type = LightType.Point;
                    lamp.range = 16.0f;
                    lamp.intensity = 2.5f;
                    lamp.transform.position = at + Vector3.up * 3.6f;
                    if (!roomFraming)
                    {
                        follow.IndoorDistance = follow.Distance;
                        follow.IndoorFramingHeight = follow.FramingHeight;
                        follow.IndoorFieldOfViewBonus = 0.0f;
                    }
                    for (int i = 0; i < 90; i++) yield return null;
                    Texture2D image = BuildingShapeCapture.Render(follow.GetComponent<Camera>());
                    BuildingShapeCapture.Save(image, BuildingShapeCapture.ShotsFolder, $"indoorcam-{name}-{(roomFraming ? "new" : "old")}");
                    Object.Destroy(image);
                    Debug.Log($"CityX indoorcam {name} {(roomFraming ? "new" : "old")}: room {follow.IndoorBlend:0.00}, camera {Vector3.Distance(follow.transform.position, player.transform.position + Vector3.up * 1.9f):0.00} m from the pivot");
                    Object.Destroy(player);
                    Object.Destroy(follow.gameObject);
                    Object.Destroy(lamp.gameObject);
                    yield return null;
                }
            }
#else
            yield break;
#endif
        }
    }
}
