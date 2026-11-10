using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    // The wet streets' mirror (docs/WET_STREETS.md; owner, 2026-11-06, of the graphics offered: "lets try 1 2 3 and 5", the wet reflections
    // being 2), each view shot from the same eye with the mirror off and on: Logs/CityShots/wet-<view>-off.png and -on.png.
    public sealed partial class TrafficCapture
    {
        /// <summary>The real city in heavy rain: down a lit street, along its sidewalk and at the traffic lights, by night, at dusk and at noon.</summary>
        [UnityTest]
        public IEnumerator RenderTheWetStreets()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Open();
            var lamps = new List<StreetLamp>();
            EdgeGeometry street = NearestStreet(_plan, _spawn, lamps, powered: true);
            Assert.IsNotNull(street, "no street near the spawn with power for its lamps");
            yield return Go(_console, _player, _camera, street.A + street.Dir * (street.Length * 0.5));
            _console.Submit("/weather rain heavy");
            RainEffect rain = Object.FindFirstObjectByType<RainEffect>();
            Assert.IsNotNull(rain, "no rain in the scene");
            rain.Soak(1.0f);
            for (int i = 0; i < 6; i++) yield return null;

            Vec2 across = street.Dir.Perp;
            Vec2 driver = street.A + street.Dir * (street.TrimA + 3.0) + across * (street.HalfWidth * 0.5);
            Vector3 driverEye = World(_plan, driver, 2.3), down = World(_plan, street.A + street.Dir * (street.Length * 0.7), 3.0);
            Vec2 walker = street.A + street.Dir * (street.TrimA + 12.0) + across * (street.HalfWidth + 2.2);
            Vector3 walkerEye = World(_plan, walker, 2.3), ahead = World(_plan, walker + street.Dir * 16.0 - across * 1.2, 0.4);
            foreach (string time in new[] { "midnight", "dusk", "noon" })
            {
                _console.Submit("/time " + time);
                for (int i = 0; i < 4; i++) yield return null;
                yield return MirrorOffAndOn($"wet-street-{time}", driverEye, down);
                yield return MirrorOffAndOn($"wet-sidewalk-{time}", walkerEye, ahead);
            }

            // The traffic lights on a main road, red and green on the wet road, by night.
            TrafficJunction lights = Nearest(_plan, _spawn, j => j.Control == JunctionControl.Signal && j.ClassOf(true) >= RoadClass.Arterial);
            if (lights != null)
            {
                yield return Go(_console, _player, _camera, lights.Node.P);
                rain.Soak(1.0f);
                _console.Submit("/time midnight");
                int main = FirstLeg(lights, true);
                Vector3 eye = World(_plan, lights.Node.P + lights.Dirs[main] * 32.0 + lights.Dirs[main].Perp * (lights.HalfWidths[main] * 0.5), 2.2);
                yield return At(lights.Timing, 2.0);
                yield return MirrorOffAndOn("wet-lights-midnight", eye, World(_plan, lights.Node.P, 4.0));
            }
            _console.Submit("/weather dry");
            _console.Submit("/time noon");
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>The same picture by lamplight with the wet mirror switched off, then on (left on).</summary>
        private IEnumerator MirrorOffAndOn(string name, Vector3 eye, Vector3 lookAt)
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            ScriptableRendererFeature mirror = data.rendererFeatures.Find(f => f != null && f.name == GraphicsQuality.ReflectionsFeature);
            Assert.IsNotNull(mirror, $"no {GraphicsQuality.ReflectionsFeature} on {RendererPath}: rebuild the scene");
            mirror.SetActive(false);
            WetMirrorFeature.Clear();
            yield return ShootByLamplight(_camera, name + "-off", eye, lookAt);
            mirror.SetActive(true);
            yield return null;
            // The mirror shows the frame before: the picture is drawn twice from the same eye, and the second is kept.
            BuildingShapeCapture.Shoot(_camera, BuildingShapeCapture.ShotsFolder, name + "-on", eye, lookAt);
            BuildingShapeCapture.Shoot(_camera, BuildingShapeCapture.ShotsFolder, name + "-on", eye, lookAt);
        }
#endif
    }
}
