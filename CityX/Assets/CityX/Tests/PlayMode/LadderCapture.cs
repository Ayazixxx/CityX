using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the ladder climb (docs/SEWERS.md) on the real body in a real room under a manhole, to Logs/CityShots/ladder-*.png: one
    /// step of the climb from the side and from behind, the rest, the slide, the pull-up out onto the street and the backing down from it.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class LadderCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheLadderClimb()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            var world = new CityWorld(streamer.WorldSeed);
            PlanWorld plan = world.Plan;
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var cameraObject = new GameObject("LadderCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var lampObject = new GameObject("LadderLamp");
            lampObject.transform.SetParent(cameraObject.transform, false);
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 40.0f;
            lamp.intensity = 70.0f;
            lamp.color = new Color(1.0f, 0.94f, 0.82f);

            plan.TryFindSpawn(out double sx, out double sz);
            SewerRoom room = null;
            foreach (SewerRoom candidate in SewerPlan.For(plan).Rooms)
            {
                if (candidate.Shaft == null || !candidate.Shaft.Open) continue;
                if (room == null || (candidate.Node.P - new Vec2(sx, sz)).Length < (room.Node.P - new Vec2(sx, sz)).Length) room = candidate;
            }
            Assert.IsNotNull(room, "no open manhole in this city");
            Ladder ladder = Ladders.Of(room.Shaft);
            var viewer = new GameObject("LadderViewer");
            viewer.transform.position = ladder.Top;
            streamer.Viewer = viewer.transform;
            streamer.Rebuild();
            streamer.StreamAround(ladder.Top, int.MaxValue);
            yield return new WaitForSecondsRealtime(2.0f);

            Vector3 back = ladder.Normal, side = Vector3.Cross(Vector3.up, back);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                PlayerLadder.HangPoint(ladder, ladder.Foot.y + 0.05f) + back * 0.2f, Quaternion.LookRotation(-back));
            PlayerLadder climber = player.GetComponent<PlayerLadder>();
            if (climber == null) climber = player.AddComponent<PlayerLadder>();
            PlayerClimbing climbing = player.GetComponent<PlayerClimbing>();
            yield return null;
            yield return null;

            // Where the body's middle is, for the camera to look at.
            Vector3 Chest() => player.transform.position + Vector3.up * 1.4f;
            void ShootBody(string name, Vector3 from) => BuildingShapeCapture.Shoot(camera, directory, name, Chest() + from, Chest());

            climber.GetOn(ladder, false);
            for (int i = 0; i < 40; i++)
            {
                climber.Tick(false, false, false, false, false, Dt);
                yield return null;
            }
            ShootBody("ladder-1-foot-side", side * 4.2f + back * 0.6f + Vector3.up * 0.2f);
            // One whole step of the climb (two rungs: 0.4 s), eight pictures of it from the side and from behind.
            for (int i = 0; i < 60; i++)
            {
                climber.Tick(true, false, false, false, false, Dt);
                yield return null;
            }
            for (int shot = 0; shot < 8; shot++)
            {
                for (int i = 0; i < 3; i++)
                {
                    climber.Tick(true, false, false, false, false, Dt);
                    yield return null;
                }
                ShootBody($"ladder-2-climb-side-{shot}", side * 4.2f + back * 0.6f + Vector3.up * 0.2f);
                ShootBody($"ladder-3-climb-back-{shot}", back * 4.4f + side * 0.5f + Vector3.up * 0.4f);
            }
            for (int i = 0; i < 60; i++)
            {
                climber.Tick(false, false, false, false, false, Dt);
                yield return null;
            }
            ShootBody("ladder-4-rest-side", side * 4.2f + back * 0.6f + Vector3.up * 0.2f);
            ShootBody("ladder-4-rest-back", back * 4.4f + side * 0.5f + Vector3.up * 0.4f);
            ShootBody("ladder-4-rest-hands", side * 1.3f + back * 1.0f + Vector3.up * 1.3f);
            ShootBody("ladder-4-rest-feet", side * 1.4f + back * 0.9f + Vector3.down * 0.9f);

            // Up to the neck, and the pull-up out, from the street.
            for (int i = 0; i < 900 && climber.IsOn; i++)
            {
                climber.Tick(true, false, false, false, false, Dt);
                yield return null;
            }
            lamp.enabled = false;
            Vector3 street = ladder.Top;
            for (int shot = 0; shot < 5 && climbing.IsToppingOut; shot++)
            {
                BuildingShapeCapture.Shoot(camera, directory, $"ladder-5-out-{shot}", street - back * 4.5f + side * 3.0f + Vector3.up * 2.2f, street + Vector3.up * 0.9f);
                for (int i = 0; i < 16 && climbing.IsToppingOut; i++)
                {
                    climbing.TickMantle(Dt);
                    yield return null;
                }
            }
            for (int i = 0; i < 80 && climbing.IsToppingOut; i++)
            {
                climbing.TickMantle(Dt);
                yield return null;
            }
            yield return null;
            BuildingShapeCapture.Shoot(camera, directory, "ladder-5-out-stood", street - back * 4.5f + side * 3.0f + Vector3.up * 2.2f, street + Vector3.up * 0.9f);

            // And back down it from the top.
            Assert.IsTrue(climber.Nearby(out Ladder found, out bool fromTop) && fromTop, "standing where the pull-up ended, the ladder is not offered from its top");
            climber.GetOn(found, true);
            for (int shot = 0; shot < 5; shot++)
            {
                for (int i = 0; i < 13; i++)
                {
                    climber.Tick(false, false, false, false, false, Dt);
                    yield return null;
                }
                BuildingShapeCapture.Shoot(camera, directory, $"ladder-6-down-{shot}", street - back * 4.5f + side * 3.0f + Vector3.up * 2.2f, street + Vector3.up * 0.9f);
            }
            lamp.enabled = true;
            for (int i = 0; i < 40; i++)
            {
                climber.Tick(false, true, true, false, false, Dt);
                yield return null;
            }
            ShootBody("ladder-7-slide-side", side * 4.2f + back * 0.6f + Vector3.up * 0.2f);
            ShootBody("ladder-7-slide-back", back * 4.4f + side * 0.5f + Vector3.up * 0.4f);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
