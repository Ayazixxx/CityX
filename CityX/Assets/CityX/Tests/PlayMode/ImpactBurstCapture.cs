using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: a boomerang's burst on a wall and the giant one's on the floor, at moments after the strike (owner, 2026-11-17: "some sort of bang
    /// noise and some impact fx"). Written to Logs/CityShots/impact-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ImpactBurstCapture
    {
        [UnityTest]
        public IEnumerator RenderTheBursts()
        {
            CaptureGuard.NeedsGraphics();
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 3.0f, 5.2f);
            wall.transform.localScale = new Vector3(12.0f, 6.0f, 0.4f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 40.0f;
            string folder = BuildingShapeCapture.ShotsFolder;

            foreach ((string name, Vector3 point, Vector3 normal, float size, Vector3 eye) in new[]
            {
                ("wall", new Vector3(-1.5f, 1.8f, 5.0f), Vector3.back, ThrownBoomerang.BurstSize, new Vector3(1.5f, 2.4f, -1.0f)),
                ("giant-floor", new Vector3(2.5f, 0.0f, 2.0f), Vector3.up, ThrownBoomerang.GiantBurstSize, new Vector3(5.5f, 3.2f, -3.5f)),
            })
            {
                ImpactBurst.At(point, normal, size);
                float clock = 0.0f;
                int shot = 0;
                foreach (float moment in new[] { 0.03f, 0.12f, 0.3f, 0.7f })
                {
                    for (; clock < moment; clock += 1.0f / 60.0f)
                    {
                        yield return null;
                    }
                    BuildingShapeCapture.Shoot(camera, folder, $"impact-{name}-{shot++}", eye, point);
                }
                yield return new WaitForSeconds(ImpactBurst.LifeSeconds);
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(wall);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }
    }
}
