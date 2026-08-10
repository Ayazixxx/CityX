using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace CityX.Spike
{
    /// <summary>
    /// Populates the spike world with server-simulated entities.
    ///
    /// The agent "prefab" is built in code rather than authored as an asset.
    /// That keeps the whole spike self-contained and reviewable as text, and
    /// means the entity count is a number in a test rather than a scene to
    /// maintain.
    ///
    /// KNOWN LIMIT, stated rather than discovered later: runtime-registered
    /// prefabs only line up when server and client share one process (host
    /// mode), because both read the same collection instance. The moment the
    /// spike grows to a separate headless server and real client processes,
    /// this has to become a real prefab asset so both sides resolve the same
    /// prefab id. That is a known step, not a surprise.
    /// </summary>
    public class SpikeWorld : MonoBehaviour
    {
        /// <summary>
        /// PORT_PLAN's Phase 0 target. 200 is not arbitrary: it is roughly
        /// what dozens of players plus faction NPCs plus summons implies once
        /// every player is a source of extra entities.
        /// </summary>
        public const int DefaultAgentCount = 200;

        /// <summary>Spread agents over a disc rather than a point, so interest management has something to cull.</summary>
        private const float SpawnRadius = 100.0f;

        private NetworkManager _networkManager;
        private NetworkObject _agentPrefab;
        private readonly List<NetworkObject> _spawned = new List<NetworkObject>();

        /// <summary>How many agents are currently spawned by this world.</summary>
        public int SpawnedCount => _spawned.Count;

        public void Initialize(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        /// <summary>
        /// Spawns <paramref name="count"/> agents. Server only -- calling
        /// this on a client is a programming error, not a runtime condition,
        /// so it complains rather than silently doing nothing.
        /// </summary>
        public void SpawnAgents(int count)
        {
            if (_networkManager == null || !_networkManager.IsServerStarted)
            {
                Debug.LogError("[CityX.Spike] SpawnAgents called with no server running.");
                return;
            }

            EnsurePrefab();

            for (int i = 0; i < count; i++)
            {
                // Deterministic placement: a golden-angle spiral gives an even
                // disc without random clumping, so density is comparable
                // between runs with different entity counts.
                float t = (i + 0.5f) / count;
                float radius = SpawnRadius * Mathf.Sqrt(t);
                float angle = i * 2.39996323f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);

                NetworkObject agent = Object.Instantiate(_agentPrefab, position, Quaternion.identity);
                agent.gameObject.SetActive(true);
                _networkManager.ServerManager.Spawn(agent);
                _spawned.Add(agent);
            }
        }

        /// <summary>Despawns everything this world spawned. Safe to call twice.</summary>
        public void DespawnAll()
        {
            foreach (NetworkObject agent in _spawned)
            {
                if (agent != null && agent.IsSpawned && _networkManager != null && _networkManager.IsServerStarted)
                {
                    _networkManager.ServerManager.Despawn(agent);
                }
                else if (agent != null)
                {
                    Object.Destroy(agent.gameObject);
                }
            }
            _spawned.Clear();

            if (_agentPrefab != null)
            {
                Object.Destroy(_agentPrefab.gameObject);
                _agentPrefab = null;
            }
        }

        private void EnsurePrefab()
        {
            if (_agentPrefab != null)
            {
                return;
            }

            // Inactive, so it behaves as a template: Instantiate copies it
            // without the original ever running as a live entity.
            GameObject go = new GameObject("SpikeAgent (prefab)");
            go.SetActive(false);

            NetworkObject nob = go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            go.AddComponent<SpikeAgent>();

            _networkManager.SpawnablePrefabs.AddObject(nob, true, true);
            _agentPrefab = nob;
        }
    }
}
