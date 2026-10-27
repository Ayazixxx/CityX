using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The water in the running game: every <see cref="WaterBody"/> there is, asked by point. Character and Weapons read it without knowing
    /// who made the water (View draws it), the same static bridge shape as <see cref="CurrentWeather"/>. With no water the world is dry.
    ///
    /// THE GAMEPLAY QUESTION GAMES ASK OF WATER (docs/WATER.md): "how deep is it here, and which way does it flow". Answered from a short list
    /// of boxes, nothing simulated.
    /// </summary>
    public static class CurrentWater
    {
        /// <summary>How far under its floor a body may be and still be in a water body, metres: a foot resting on the floor sits a hair below it.</summary>
        public const float FloorAllowance = 0.15f;

        private static readonly List<WaterBody> Bodies = new List<WaterBody>();

        public static IReadOnlyList<WaterBody> All => Bodies;

        public static bool Any => Bodies.Count > 0;

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

        public static void Clear()
        {
            Bodies.Clear();
        }

        /// <summary>
        /// The water over a point (a body's feet): how deep it is (0 where dry) and the body of water it is in, the deepest of any that
        /// overlap there.
        /// </summary>
        public static float DepthAt(Vector3 point, out WaterBody body)
        {
            body = null;
            float deepest = 0.0f;
            for (int i = 0; i < Bodies.Count; i++)
            {
                float depth = Bodies[i].DepthOver(point);
                if (depth > deepest)
                {
                    deepest = depth;
                    body = Bodies[i];
                }
            }
            return deepest;
        }

        public static float DepthAt(Vector3 point) => DepthAt(point, out _);

        /// <summary>The surface over a point, or false where there is no water over it.</summary>
        public static bool TrySurfaceAt(Vector3 point, out float surfaceY)
        {
            float depth = DepthAt(point, out WaterBody body);
            surfaceY = body != null ? body.SurfaceY : 0.0f;
            return depth > 0.0f;
        }
    }
}
