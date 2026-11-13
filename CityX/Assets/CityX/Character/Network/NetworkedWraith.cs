using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Wraith in a session (docs/WRAITH.md): its owner's machine drifts it and decides its hits; every other machine's copy is moved by the
    /// owner (NetworkedBodyMotion) and poses itself from how it moves (<see cref="Wraith.IsCopy"/>). A hit on a copy goes owner-ward the way a hit
    /// on another player's copy does (NetworkedDamage, with the same server checks); its health comes back to every copy, so each one flinches
    /// and flashes when it is hurt and fades away when it dies, whoever's blow it was. The player whose hit killed it is given its XP.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedWraith : NetworkBehaviour, IRemoteDamageRelay
    {
        private readonly SenderRate _rate = new SenderRate(NetworkedDamage.MaxHitsPerSecond);
        private Wraith _wraith;
        private float _lastSentHealth = -1.0f;

        private void Awake()
        {
            _wraith = GetComponent<Wraith>();
            if (_wraith != null)
            {
                _wraith.Relay = this;
                _wraith.PhaseChanged += OnPhaseChanged;
                _wraith.Bled += OnBled;
            }
        }

        private void OnDestroy()
        {
            if (_wraith != null)
            {
                _wraith.PhaseChanged -= OnPhaseChanged;
                _wraith.Bled -= OnBled;
            }
        }

        // ---- the owner's hunt: what it is doing and whose neck it is at, and each spurt of blood, on every screen ----

        private void OnPhaseChanged(WraithPhase phase, Component prey)
        {
            if (NetworkObject != null && IsSpawned && IsOwner)
            {
                SendPhaseToServer((byte)phase, prey != null ? prey.GetComponentInParent<NetworkObject>() : null);
            }
        }

        private void OnBled(Vector3 at)
        {
            if (NetworkObject != null && IsSpawned && IsOwner)
            {
                SendBloodToServer(at);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendPhaseToServer(byte phase, NetworkObject prey)
        {
            if (phase <= (byte)WraithPhase.Punching)
            {
                BroadcastPhase(phase, prey);
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastPhase(byte phase, NetworkObject prey)
        {
            if (_wraith != null)
            {
                _wraith.ShowPhaseFromNetwork((WraithPhase)phase, prey != null ? prey.GetComponent<IDamageable>() as Component : null);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendBloodToServer(Vector3 at)
        {
            if (WireChecks.Finite(at))
            {
                BroadcastBlood(at);
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastBlood(Vector3 at)
        {
            if (_wraith != null)
            {
                _wraith.ShowBloodFromNetwork(at);
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_wraith != null)
            {
                _wraith.IsCopy = !Owner.IsLocalClient;   // IsOwner is not settled yet inside OnStartNetwork (FishNet)
                if (_wraith.StartsSummoned && Owner.IsLocalClient)
                {
                    _wraith.Summon(OwnersBody());   // a Wraith Staff's summon, come in: its summoner's machine runs it
                }
            }
        }

        private bool IsCopy => NetworkObject != null && IsSpawned && !IsOwner;

        /// <summary>The body of the player who owns it, as this machine holds it: its summoner.</summary>
        private PlayerVitals OwnersBody()
        {
            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity.NetworkManager == NetworkManager && identity.Owner == Owner && identity.TryGetComponent(out PlayerVitals body))
                {
                    return body;
                }
            }
            return PlayerBodies.Local();
        }

        public bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_wraith != null && _wraith.IsAlive)
            {
                SendHitToServer(amount, fromPosition);
            }
            return true;
        }

        public bool TryRelayPoison(float amount) => false;

        public bool TryRelayKnockdown(Vector3 velocity) => false;

        public bool TryRelayPull(Vector3 direction, float metres, float seconds, float lift) => false;

        public bool TryRelayHeal(float amount) => false;

        [ServerRpc(RequireOwnership = false)]
        private void SendHitToServer(float amount, Vector3 fromPosition, NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsValid || sender == Owner || !NetworkedDamage.Acceptable(amount) || !WireChecks.Finite(fromPosition)
                || !_rate.Allow(sender.ClientId, Time.unscaledTime) || !NetworkedDamage.AttackerNear(sender, transform.position))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyHitOnOwner(Owner, Mathf.Min(amount, NetworkedDamage.MaxHitDamage), fromPosition, sender.ClientId);
            }
        }

        [TargetRpc]
        private void ApplyHitOnOwner(NetworkConnection target, float amount, Vector3 fromPosition, int attackerClientId)
        {
            if (_wraith == null || !_wraith.IsAlive)
            {
                return;
            }
            ((IDamageable)_wraith).ApplyHit(amount, fromPosition, 0.0f);
            if (!_wraith.IsAlive)
            {
                EnemyNetwork.CreditKill(NetworkManager, attackerClientId, Wraith.XpForKill);
            }
        }

        // ---- the owner shares its health: a copy flinches at a drop and fades at 0 ----

        private void Update()
        {
            if (_wraith == null || NetworkObject == null || !IsSpawned || !IsOwner || Mathf.Approximately(_wraith.Health, _lastSentHealth))
            {
                return;
            }
            _lastSentHealth = _wraith.Health;
            SendHealthToServer(_wraith.Health);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendHealthToServer(float health)
        {
            if (!float.IsNaN(health) && !float.IsInfinity(health))
            {
                BroadcastHealth(Mathf.Clamp(health, 0.0f, Wraith.MaxHealth));
            }
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastHealth(float health)
        {
            if (_wraith != null)
            {
                _wraith.SetHealthFromNetwork(health);
            }
        }
    }
}
