using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Wraith in a session (docs/WRAITH.md): its owner's machine drifts it and decides its hits; every other machine's copy is moved by the
    /// owner (NetworkTransform) and poses itself from how it moves (<see cref="Wraith.IsCopy"/>). A hit on a copy goes owner-ward the way a hit
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
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_wraith != null)
            {
                _wraith.IsCopy = !Owner.IsLocalClient;   // IsOwner is not settled yet inside OnStartNetwork (FishNet)
            }
        }

        private bool IsCopy => NetworkObject != null && IsSpawned && !IsOwner;

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
