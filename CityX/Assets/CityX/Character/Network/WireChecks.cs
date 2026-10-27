using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How often each player may ask the server for something (a hit, a drop, a door, a spawn): no more than a set number in any one-second
    /// window, counted per client. The server keeps one per kind of message.
    /// </summary>
    public sealed class SenderRate
    {
        private readonly int _perSecond;
        private readonly Dictionary<int, (float windowStart, int count)> _windows = new Dictionary<int, (float, int)>();

        public SenderRate(int perSecond)
        {
            _perSecond = perSecond;
        }

        /// <summary>Counts one more from <paramref name="clientId"/> at <paramref name="now"/> (unscaled seconds): true while within the rate.</summary>
        public bool Allow(int clientId, float now)
        {
            _windows.TryGetValue(clientId, out (float windowStart, int count) window);
            if (now - window.windowStart >= 1.0f)
            {
                window = (now, 0);
            }
            window.count++;
            _windows[clientId] = window;
            return window.count <= _perSecond;
        }
    }

    /// <summary>The checks every server-side handler makes of numbers that came off the wire.</summary>
    public static class WireChecks
    {
        public static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>A real rotation: finite and of unit length.</summary>
        public static bool IsRotation(Quaternion q) =>
            !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w))
            && Mathf.Abs(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w - 1.0f) < 0.01f;
    }
}
