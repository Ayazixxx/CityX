using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Crimson Claw in a session (owner, 2026-11-14: "monsters should appear in the same location between players when online"): its
    /// owner's machine runs it (where it goes, whom it swipes at, what its claws cut); every other machine's copy is moved by the owner
    /// (NetworkedBodyMotion) and decides nothing (<see cref="CrimsonClaw.IsCopy"/>). A hit on a copy goes owner-ward the way a hit on another
    /// player's copy does (the same server checks as NetworkedDamage); its health comes back to every copy, which dies when the owner's
    /// does; and each swipe the owner starts is played on every copy. Its grab is only ever of its own machine's player (the body it
    /// holds and throws is that machine's to move): the thrown body is seen on the others as any fallen body is.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedCrimsonClaw : NetworkBehaviour, IRemoteDamageRelay
    {
        private readonly SenderRate _rate = new SenderRate(NetworkedDamage.MaxHitsPerSecond);
        private CrimsonClaw _claw;
        private float _lastSentHealth = -1.0f;

        private void Awake()
        {
            _claw = GetComponent<CrimsonClaw>();
            if (_claw != null)
            {
                _claw.Relay = this;
                _claw.AttackStarted += OnAttackStarted;
            }
        }

        private void OnDestroy()
        {
            if (_claw != null)
            {
                _claw.AttackStarted -= OnAttackStarted;
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_claw != null)
            {
                _claw.IsCopy = !Owner.IsLocalClient;   // IsOwner is not settled yet inside OnStartNetwork (FishNet)
            }
        }

        private bool IsCopy => NetworkObject != null && IsSpawned && !IsOwner;

        // ---- the owner's swipes, played on every copy ----

        private void OnAttackStarted(bool leftHand)
        {
            if (NetworkObject != null && IsSpawned && IsOwner)
            {
                SendAttackToServer(leftHand);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendAttackToServer(bool leftHand)
        {
            BroadcastAttack(leftHand);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastAttack(bool leftHand)
        {
            if (_claw != null)
            {
                _claw.ShowAttackFromNetwork(leftHand);
            }
        }

        // ---- a hit on a copy lands on the owner's ----

        public bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_claw != null && _claw.IsAlive)
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
                ApplyHitOnOwner(Owner, Mathf.Min(amount, NetworkedDamage.MaxHitDamage), fromPosition);
            }
        }

        [TargetRpc]
        private void ApplyHitOnOwner(NetworkConnection target, float amount, Vector3 fromPosition)
        {
            if (_claw != null && _claw.IsAlive)
            {
                ((IDamageable)_claw).ApplyHit(amount, fromPosition, 0.0f);
            }
        }

        // ---- the owner shares its health: a copy dies when it reaches nothing ----

        private void Update()
        {
            if (_claw == null || NetworkObject == null || !IsSpawned || !IsOwner || Mathf.Approximately(_claw.Health, _lastSentHealth))
            {
                return;
            }
            _lastSentHealth = _claw.Health;
            SendHealthToServer(_claw.Health);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendHealthToServer(float health)
        {
            if (!float.IsNaN(health) && !float.IsInfinity(health))
            {
                BroadcastHealth(Mathf.Clamp(health, 0.0f, CrimsonClaw.MaxHealth));
            }
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastHealth(float health)
        {
            if (_claw != null)
            {
                _claw.SetHealthFromNetwork(health);
            }
        }
    }
}
