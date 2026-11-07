using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    // The lighting effects (docs/LIGHTING.md; owner, 2026-11-06: "lets try 1 2 3 and 5"), each shot twice from the same eye with its renderer
    // feature off and on: Logs/CityShots/gfx-<spot>-off.png and -on.png.
    public sealed partial class TrafficCapture
    {
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string LightShaftsFeature = "CityXLightShafts";

        /// <summary>The real city: down a street toward a low sun, and over the roofs from up high.</summary>
        [UnityTest]
        public IEnumerator RenderTheLightEffects()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Open();
            var lamps = new List<StreetLamp>();
            EdgeGeometry street = NearestStreet(_plan, _spawn, lamps, powered: true);
            Assert.IsNotNull(street, "no street near the spawn");
            Vec2 middle = street.A + street.Dir * (street.Length * 0.5);
            yield return Go(_console, _player, _camera, middle);
            foreach (string time in new[] { "morning", "afternoon" })
            {
                _console.Submit("/time " + time);
                for (int i = 0; i < 4; i++) yield return null;
                Vector3 towardSun = TowardSun();
                Vector3 eye = World(_plan, middle, 2.3);
                yield return BeforeAndAfter(LightShaftsFeature, $"gfx-shafts-street-{time}", eye, eye + Flat(towardSun) * 30.0f + Vector3.up * 9.0f);
                Vector3 high = World(_plan, middle, 60.0);
                yield return BeforeAndAfter(LightShaftsFeature, $"gfx-shafts-high-{time}", high, high + Flat(towardSun) * 60.0f - Vector3.up * 8.0f);
            }
#else
            yield break;
#endif
        }

        /// <summary>Plain ground and a low sun behind a row of pillars, with no city's haze: the beams between them.</summary>
        [UnityTest]
        public IEnumerator RenderTheLightEffectsInTheSun()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var made = new List<GameObject>();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(14.0f, 180.0f, 0.0f);   // low, shining toward -z: the camera looks into it
            made.Add(sun.gameObject);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(120.0f, 1.0f, 120.0f);
            ground.GetComponent<Renderer>().material = new Material(CityX.Core.LitShader.Find()) { color = new Color(0.62f, 0.62f, 0.64f) };
            made.Add(ground);
            for (int i = -5; i <= 5; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.transform.position = new Vector3(i * 4.0f, 6.0f, 30.0f);
                pillar.transform.localScale = new Vector3(2.0f, 12.0f, 2.0f);
                pillar.GetComponent<Renderer>().material = new Material(CityX.Core.LitShader.Find()) { color = new Color(0.5f, 0.5f, 0.55f) };
                made.Add(pillar);
            }
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.88f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400.0f;
            made.Add(camera.gameObject);
            for (int i = 0; i < 4; i++) yield return null;
            _camera = camera;
            yield return BeforeAndAfter(LightShaftsFeature, "gfx-sun-pillars", new Vector3(3.0f, 2.2f, 0.0f), new Vector3(0.0f, 5.0f, 30.0f));
            foreach (GameObject go in made) if (go != null) Object.Destroy(go);
#else
            yield break;
#endif
        }

        /// <summary>The way to the sun (the scene's main directional light).</summary>
        private static Vector3 TowardSun()
        {
            Light best = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.isActiveAndEnabled && (best == null || light.intensity > best.intensity)) best = light;
            }
            return best != null ? -best.transform.forward : Vector3.up;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0.0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

#if UNITY_EDITOR
        /// <summary>The same picture with the renderer feature <paramref name="feature"/> switched off, then on (left on).</summary>
        private IEnumerator BeforeAndAfter(string feature, string name, Vector3 eye, Vector3 lookAt)
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            ScriptableRendererFeature found = data.rendererFeatures.Find(f => f != null && f.name == feature);
            Assert.IsNotNull(found, $"no {feature} on {RendererPath}: rebuild the scene");
            found.SetActive(false);
            yield return Shoot(_camera, name + "-off", eye, lookAt);
            found.SetActive(true);
            yield return Shoot(_camera, name + "-on", eye, lookAt);
        }
#endif
    }
}
