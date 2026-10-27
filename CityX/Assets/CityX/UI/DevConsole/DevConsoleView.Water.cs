using System.Globalization;
using CityX.View;
using CityX.World;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// /water: water to try the water groundwork in (docs/WATER.md), on this machine only, like /weather. A pool round you to wade in, or a
    /// flood rising over the streets round you, flowing the way you face, to float in (hold Space to rise).
    /// </summary>
    public sealed partial class DevConsoleView
    {
        /// <summary>A pool's size across, metres, and its depth unless one is given (about a 2.6 m body's knees).</summary>
        public const float PoolSize = 16.0f;

        public const float PoolDepth = 0.7f;

        /// <summary>A flood's size across, metres, its height unless one is given, and its timeline.</summary>
        public const float FloodSize = 120.0f;

        public const float FloodHeight = 4.0f;

        private static readonly FloodTimeline FloodTest = new FloodTimeline(15.0f, 25.0f, 15.0f, FloodHeight);

        /// <summary>How fast the test flood flows, metres a second.</summary>
        public const float FloodFlow = 1.0f;

        /// <summary>A flood starts this far under the feet, so the low ground round them fills too.</summary>
        private const float FloodFloorBelow = 40.0f;

        private void WaterCommand(string which, string amount)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            WaterSplashes kit = WaterSplashes.Current;
            if (kit == null || kit.WaterMaterial == null)
            {
                Log("no water in this scene (rebuild it: CityMassingSceneBuilder)");
                return;
            }
            Vector3 feet = Vitals.transform.position;
            switch (which)
            {
                case "off":
                    foreach (WaterSurface surface in FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
                    {
                        if (surface.name.StartsWith("DevWater"))
                        {
                            Destroy(surface.gameObject);
                        }
                    }
                    Log("the test water is gone");
                    return;
                case "flood":
                {
                    float height = FloodHeight;
                    if (!string.IsNullOrEmpty(amount) && !float.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out height))
                    {
                        Log(DevCommands.UsageFor("water"));
                        return;
                    }
                    var timeline = new FloodTimeline(FloodTest.RiseSeconds, FloodTest.HoldSeconds, FloodTest.RecedeSeconds, Mathf.Max(0.5f, height));
                    float half = FloodSize * 0.5f;
                    var body = new WaterBody(feet.x - half, feet.z - half, feet.x + half, feet.z + half, feet.y - FloodFloorBelow, feet.y);
                    Vector3 facing = Vector3.ProjectOnPlane(Vitals.transform.forward, Vector3.up).normalized;
                    body.Flow = new Vector2(facing.x, facing.z) * FloodFlow;
                    WaterSurface flood = WaterSurface.Create(body, kit.WaterMaterial, "DevWaterFlood");
                    RisingWater.Begin(flood, feet.y, timeline, Time.time);
                    Log($"a flood rising {timeline.Height:0.#} m over {timeline.RiseSeconds:0} s, holding {timeline.HoldSeconds:0} s, " +
                        $"receding over {timeline.RecedeSeconds:0} s, flowing the way you face -- hold Space to rise");
                    return;
                }
                default:
                {
                    float depth = PoolDepth;
                    if (!string.IsNullOrEmpty(which) && !float.TryParse(which, NumberStyles.Float, CultureInfo.InvariantCulture, out depth))
                    {
                        Log(DevCommands.UsageFor("water"));
                        return;
                    }
                    depth = Mathf.Clamp(depth, 0.05f, 20.0f);
                    WaterSurface.Create(WaterBody.Square(feet, PoolSize, depth), kit.WaterMaterial, "DevWaterPool");
                    Log($"a pool {PoolSize:0} m across, {depth:0.##} m deep, round you (/water off takes it away)");
                    return;
                }
            }
        }
    }
}
