using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The players' bodies an enemy can go for: this machine's own player, and in a session every other player's copy here too (an enemy is
    /// run by one machine and fights everyone near it; a hit on a copy goes to its owner, NetworkedDamage). A player's body is the one with an
    /// input reader -- switched on for this machine's own, switched off on a copy, which a network identity marks. An enemy built on the
    /// player's body has no reader, and a test's body with its reader switched off and no network is no one's.
    /// </summary>
    public static class PlayerBodies
    {
        /// <summary>The nearest living player's body within <paramref name="reach"/> metres of <paramref name="from"/>, or null.</summary>
        public static PlayerVitals NearestAlive(Vector3 from, float reach)
        {
            PlayerVitals best = null;
            float nearest = reach;
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                if (!IsPlayer(reader))
                {
                    continue;
                }
                var vitals = reader.GetComponent<PlayerVitals>();
                if (vitals == null || !vitals.IsAlive)
                {
                    continue;
                }
                float distance = Vector3.Distance(from, vitals.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = vitals;
                }
            }
            return best;
        }

        /// <summary>Whether <paramref name="body"/> is a player's, this machine's own or another player's copy.</summary>
        public static bool IsPlayer(GameObject body)
        {
            var reader = body != null ? body.GetComponent<PlayerInputReader>() : null;
            return reader != null && IsPlayer(reader);
        }

        private static bool IsPlayer(PlayerInputReader reader)
        {
            if (!reader.gameObject.activeInHierarchy)
            {
                return false;
            }
            if (reader.enabled)
            {
                return true;
            }
            var identity = reader.GetComponent<NetworkedPlayerIdentity>();
            return identity != null && identity.NetworkObject != null && identity.IsSpawned;
        }
    }
}
