using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Damage between players. Every hit (a punch, a swing, a bullet, a fireball, a flame, a poison cloud) is worked out on the ATTACKER'S machine
    /// against whatever bodies exist there; another player's body there is only a COPY, and a copy's health is meaningless. So when the thing hit is
    /// a copy, the hit is forwarded: attacker -> server -> the body's owner, whose real <see cref="PlayerVitals"/> takes it (block, parry, poise,
    /// stun, health, hit reaction and all, exactly as a hit on their own body does). The owner also tells everyone their health a few times a
    /// second, so a copy that has died stops being a target and anything reading a copy's health sees the real number.
    ///
    /// THE SERVER DOES NOT TRUST THE RELAY. It checks the hit is a sane number, that the sender is not the victim, that the sender's own body is near
    /// the victim (a projectile can come from far: <see cref="MaxRelayMetres"/>), and that the sender is not firing hits faster than any weapon could.
    ///
    /// THE VERDICT COMES BACK (2026-10-13, for PvP feel): the attacker is told "landed" at once (a hit-stop must not wait for a round trip), and when
    /// the victim's own vitals decide otherwise -- parried, soaked by a held guard, rolled through -- that answer is sent back to the attacker's
    /// machine (<see cref="Verdict"/>), where the attacker's combat takes it: a parried swing leaves the attacker staggered just as it does offline.
    /// The DEFENDER decides what happened to them (the research's rule: favour the defender's view of a dodge or a parry).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedDamage : NetworkBehaviour, IRemoteDamageRelay
    {
        /// <summary>The most one forwarded hit may do. A real hit is well under this; a forged huge one is refused.</summary>
        public const float MaxHitDamage = 80.0f;

        /// <summary>The farthest the attacker's body may be from the victim's, in metres: a bullet's range plus slack.</summary>
        public const float MaxRelayMetres = 90.0f;

        /// <summary>The most hits one attacker may send per second (flames and poison tick often; nothing real exceeds this).</summary>
        public const int MaxHitsPerSecond = 40;

        /// <summary>How often the owner shares their health, in seconds.</summary>
        public const float HealthIntervalSeconds = 0.25f;

        /// <summary>The most poise damage a forwarded hit may claim.</summary>
        public const float MaxPoiseDamage = 200.0f;

        private PlayerVitals _vitals;
        private float _sinceHealth;
        private float _lastSentHealth = -1.0f;
        private int _nextHitId;

        // Server-side rate limit, per sender.
        private readonly System.Collections.Generic.Dictionary<int, (float windowStart, int count)> _rate =
            new System.Collections.Generic.Dictionary<int, (float, int)>();

        /// <summary>Raised on the ATTACKER's machine (on this, the victim's copy) with a hit's id and what the victim's own body did with it, when that was not simply "landed".</summary>
        public event System.Action<int, HitResponse> Verdict;

        /// <summary>The id the last forwarded hit was sent with, so the attacker can match a verdict to its swing.</summary>
        public int LastRelayedHitId { get; private set; }

        /// <summary>True when the last <see cref="TryRelayHit"/> actually sent a hit on (false on the owner's own body, or when nothing was sent).</summary>
        public bool LastRelayWasSent { get; private set; }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            if (_vitals != null)
            {
                _vitals.Relay = this;
            }
        }

        /// <summary>True when this body is another player's copy on this machine.</summary>
        private bool IsCopy => NetworkObject != null && IsSpawned && !IsOwner;

        public bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage)
        {
            LastRelayWasSent = false;
            if (!IsCopy)
            {
                return false;
            }
            if (_vitals != null && _vitals.IsDead)
            {
                return true;   // a corpse is not hit again, and nothing is sent
            }
            LastRelayedHitId = ++_nextHitId;
            LastRelayWasSent = true;
            SendHitToServer(amount, fromPosition, hitstunSeconds, poiseDamage, LastRelayedHitId);
            return true;
        }

        public bool TryRelayPoison(float amount)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_vitals != null && _vitals.IsDead)
            {
                return true;
            }
            SendPoisonToServer(amount);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendHitToServer(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage, int hitId, NetworkConnection sender = null)
        {
            if (!Allowed(sender, amount) || !Finite(fromPosition) || float.IsNaN(hitstunSeconds) || float.IsNaN(poiseDamage))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyHitOnOwner(Owner, Mathf.Min(amount, MaxHitDamage), fromPosition, Mathf.Clamp(hitstunSeconds, 0.0f, 3.0f),
                    Mathf.Clamp(poiseDamage, -1.0f, MaxPoiseDamage), hitId, sender.ClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendPoisonToServer(float amount, NetworkConnection sender = null)
        {
            if (!Allowed(sender, amount))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyPoisonOnOwner(Owner, Mathf.Min(amount, MaxHitDamage));
            }
        }

        [TargetRpc]
        private void ApplyHitOnOwner(NetworkConnection target, float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage, int hitId, int attackerClientId)
        {
            HitResponse response = ApplyHitHere(amount, fromPosition, hitstunSeconds, poiseDamage);
            if (response != HitResponse.Landed)
            {
                SendVerdictToServer(attackerClientId, hitId, (int)response);   // the attacker assumed "landed"; only a different answer needs carrying back
            }
        }

        [TargetRpc]
        private void ApplyPoisonOnOwner(NetworkConnection target, float amount)
        {
            if (_vitals != null)
            {
                _vitals.TakePoison(amount);
            }
        }

        /// <summary>Applies a forwarded hit to THIS body as if it had been struck here. Public so a test can drive it without a network.</summary>
        public HitResponse ApplyHitHere(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage = -1.0f)
        {
            if (_vitals == null)
            {
                return HitResponse.Ignored;
            }
            if (poiseDamage >= 0.0f)
            {
                _vitals.TakePoise(poiseDamage);
            }
            return ((IDamageable)_vitals).ApplyHit(amount, fromPosition, hitstunSeconds);
        }

        // ---- the verdict back to the attacker ----

        [ServerRpc(RequireOwnership = true)]
        private void SendVerdictToServer(int attackerClientId, int hitId, int response)
        {
            if (ServerManager != null && ServerManager.Clients.TryGetValue(attackerClientId, out NetworkConnection attacker) && attacker != null && attacker.IsValid)
            {
                DeliverVerdict(attacker, hitId, response);
            }
        }

        [TargetRpc]
        private void DeliverVerdict(NetworkConnection target, int hitId, int response)
        {
            ReceiveVerdict(hitId, response);
        }

        /// <summary>A verdict as it arrives on the attacker's machine. Public so a test can deliver one without a network.</summary>
        public void ReceiveVerdict(int hitId, int response)
        {
            if (response < (int)HitResponse.Ignored || response > (int)HitResponse.Guarded)
            {
                return;
            }
            Verdict?.Invoke(hitId, (HitResponse)response);
        }

        /// <summary>The server's checks on a forwarded hit. Public static pieces are tested directly.</summary>
        private bool Allowed(NetworkConnection sender, float amount)
        {
            if (sender == null || !sender.IsValid || sender == Owner)
            {
                return false;   // not from a client, or the victim hitting themselves by the relay
            }
            if (!Acceptable(amount))
            {
                return false;
            }
            if (!WithinRate(sender.ClientId, Time.unscaledTime))
            {
                return false;
            }
            // The attacker's own body (any body they own) must be within range of the victim's.
            foreach (NetworkObject owned in sender.Objects)
            {
                if (owned != null && Vector3.Distance(owned.transform.position, transform.position) <= MaxRelayMetres)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A forwarded hit must be a real, positive number no bigger than <see cref="MaxHitDamage"/> allows through (bigger is clamped by the caller; absurd is refused).</summary>
        public static bool Acceptable(float amount)
        {
            return !float.IsNaN(amount) && !float.IsInfinity(amount) && amount > 0.0f && amount <= MaxHitDamage * 4.0f;
        }

        private bool WithinRate(int clientId, float now)
        {
            _rate.TryGetValue(clientId, out (float windowStart, int count) entry);
            if (now - entry.windowStart >= 1.0f)
            {
                entry = (now, 0);
            }
            entry.count++;
            _rate[clientId] = entry;
            return entry.count <= MaxHitsPerSecond;
        }

        private static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        // ---- the owner shares their health ---------------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_vitals == null || NetworkObject == null || !IsSpawned || !IsOwner)
            {
                return;
            }
            _sinceHealth += Time.deltaTime;
            if (_sinceHealth < HealthIntervalSeconds)
            {
                return;
            }
            _sinceHealth = 0.0f;
            float health = _vitals.Health;
            if (Mathf.Approximately(health, _lastSentHealth))
            {
                return;
            }
            _lastSentHealth = health;
            SendHealthToServer(health);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendHealthToServer(float health)
        {
            if (float.IsNaN(health) || float.IsInfinity(health))
            {
                return;
            }
            BroadcastHealth(health);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastHealth(float health)
        {
            if (_vitals != null)
            {
                _vitals.SetHealthFromNetwork(health);
            }
        }
    }
}
