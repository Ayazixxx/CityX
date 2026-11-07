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

        /// <summary>
        /// The body this machine plays: the one claimed, else the scene's own player (a body with an input reader that is not another player's
        /// copy). Not "the first body with a reader": on a joiner's machine that can be the host's copy (a screen that latched onto it showed
        /// the host's loot and items). Null with no player in the scene.
        /// </summary>
        public static GameObject Find()
        {
            if (Current != null)
            {
                return Current;
            }
            foreach (PlayerInputReader reader in UnityEngine.Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                var identity = reader.GetComponent<NetworkedPlayerIdentity>();
                if (identity != null && identity.NetworkObject != null && identity.IsSpawned && !identity.IsOwner)
                {
                    continue;   // another player's copy
                }
                return reader.gameObject;
            }
            return null;
        }

        /// <summary>A component of the body this machine plays (<see cref="Find"/>), or null.</summary>
        public static T Find<T>() where T : Component
        {
            GameObject body = Find();
            return body != null ? body.GetComponent<T>() : null;
        }
    }
}
