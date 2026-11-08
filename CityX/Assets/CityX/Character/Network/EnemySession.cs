using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// An enemy's place in a session, asked of its body without the network types (the brains' assembly does not know FishNet): whether this
    /// machine only shows it, whether its drops are shared, and taking it out of the world. A body with no NetworkedEnemy -- every enemy offline
    /// -- is this machine's own and is simply destroyed.
    /// </summary>
    public static class EnemySession
    {
        /// <summary>True when <paramref name="body"/> is a networked enemy's copy here: it shows the enemy and decides nothing about it.</summary>
        public static bool IsCopy(GameObject body)
        {
            var enemy = body != null ? body.GetComponent<NetworkedEnemy>() : null;
            return enemy != null && enemy.IsCopyHere;
        }

        /// <summary>True when <paramref name="body"/> is a networked enemy in a running session (its drops are shared with everyone).</summary>
        public static bool IsShared(GameObject body)
        {
            var enemy = body != null ? body.GetComponent<NetworkedEnemy>() : null;
            return enemy != null && enemy.IsSharedHere;
        }

        /// <summary>
        /// Asks the session to bring <paramref name="kind"/> in here, run by this machine (EnemyNetwork; the Wraith Staff's summon). False with
        /// no session to ask: the caller makes it here itself.
        /// </summary>
        public static bool TrySpawn(string kind, Vector3 position, Quaternion rotation)
        {
            return EnemyNetwork.Current != null && EnemyNetwork.Current.TrySpawn(kind, position, rotation);
        }

        /// <summary>
        /// Takes a body out of the world: a networked enemy is despawned by the server (on every machine; asked by its owner, a copy waits for
        /// it), anything else is simply destroyed here.
        /// </summary>
        public static void Remove(GameObject body)
        {
            var enemy = body != null ? body.GetComponent<NetworkedEnemy>() : null;
            if (enemy == null || !enemy.IsSharedHere)
            {
                Object.Destroy(body);
                return;
            }
            enemy.RemoveFromSession();
        }
    }
}
