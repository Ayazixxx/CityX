using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The water in the running game: every <see cref="WaterBody"/> there is and every other <see cref="IWaterSource"/> (the sewers'
    /// channels), asked by point. Character and Weapons read it without knowing who made the water (View draws it), the same static bridge
    /// shape as <see cref="CurrentWeather"/>. With no water the world is dry.
    ///
    /// THE GAMEPLAY QUESTION GAMES ASK OF WATER (docs/WATER.md): "how deep is it here, and which way does it flow". Answered from a short list
    /// of boxes and shapes, nothing simulated.
    /// </summary>
    public static class CurrentWater
    {
        /// <summary>How far under its floor a body may be and still be in a water body, metres: a foot resting on the floor sits a hair below it.</summary>
        public const float FloorAllowance = 0.15f;

        private static readonly List<WaterBody> Bodies = new List<WaterBody>();
        private static readonly List<IWaterSource> Sources = new List<IWaterSource>();

        public static IReadOnlyList<WaterBody> All => Bodies;

        public static bool Any => Bodies.Count > 0 || Sources.Count > 0;

        public static void Add(WaterBody body)
        {
            if (body != null && !Bodies.Contains(body))
            {
                Bodies.Add(body);
            }
        }

        public static void Remove(WaterBody body)
        {
            Bodies.Remove(body);
        }

        public static void AddSource(IWaterSource source)
        {
            if (source != null && !Sources.Contains(source))
            {
                Sources.Add(source);
            }
        }

        public static void RemoveSource(IWaterSource source)
        {
            Sources.Remove(source);
        }

        /// <summary>Takes every body of water away. The sources stay: they belong to the world (the sewers), not to whoever made a pool.</summary>
        public static void Clear()
        {
            Bodies.Clear();
        }

        /// <summary>
        /// The water over a point (a body's feet): how deep it is (0 where dry), its surface and its flow, from the deepest of any that
        /// overlap there.
        /// </summary>
        public static WaterHere At(Vector3 point)
        {
            var found = new WaterHere();
            for (int i = 0; i < Bodies.Count; i++)
            {
                WaterBody body = Bodies[i];
                float depth = body.DepthOver(point);
                if (depth > found.Depth)
                {
                    found = new WaterHere { Depth = depth, SurfaceY = body.SurfaceY, Flow = body.Flow };
                }
            }
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].TryWaterAt(point, out WaterHere water) && water.Depth > found.Depth)
                {
                    found = water;
                }
            }
            return found;
        }

        public static float DepthAt(Vector3 point) => At(point).Depth;

        /// <summary>The surface over a point, or false where there is no water over it.</summary>
        public static bool TrySurfaceAt(Vector3 point, out float surfaceY)
        {
            WaterHere water = At(point);
            surfaceY = water.SurfaceY;
            return water.Wet;
        }
    }
}
