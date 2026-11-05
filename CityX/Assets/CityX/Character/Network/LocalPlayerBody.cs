using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Which body this machine plays, when it is not the one the scene was built around. A friend who joins plays a clone spawned at runtime, while the scene's HUD and menus were wired at
    /// build time to the baked player, which on the friend's machine is the host's body. So their bars showed the host's numbers, and when they died no death screen came up.
    /// </summary>
    public static class LocalPlayerBody
    {
        /// <summary>The body claimed last; null until one is (the host and a solo player keep the baked body, which everything is already wired to).</summary>
        public static GameObject Current { get; private set; }

        public static event Action<GameObject> Changed;

        public static void Claim(GameObject body)
        {
            Current = body;
            Changed?.Invoke(body);
        }
    }
}
