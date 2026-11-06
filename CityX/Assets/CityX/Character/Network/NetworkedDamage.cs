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
    ///
    /// ENEMIES TOO (2026-10-27): a networked enemy's body (NetworkedEnemy) is relayed the same way, to the machine that runs it; when a relayed
    /// hit kills one, the player whose hit it was is paid its XP on their own machine (EnemyNetwork.CreditKill).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedDamage : NetworkBehaviour, IRemoteDamageRelay
    {
        /// <summary>The most one forwarded hit may do. A real one is under this (a grenade does 110, and Melee Damage and Bloodrage raise a sword's); a forged huge one is cut down to it.</summary>
        public const float MaxHitDamage = 320.0f;

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
        private float _lastSentMaxHealth = -1.0f;
        private int _nextHitId;

        // Server-side rate limit, per sender.
        private readonly SenderRate _rate = new SenderRate(MaxHitsPerSecond);

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
            if (_vitals != null && _vitals.IsDead && !Dismemberer.TakesCorpseHits(_vitals))
            {
                return true;   // a corpse with nothing left to take off is not hit again, and nothing is sent
            }
            LastRelayedHitId = ++_nextHitId;
            LastRelayWasSent = true;
            Vector3? contact = DamageContext.ContactPoint;
            SendHitToServer(amount, fromPosition, hitstunSeconds, poiseDamage, LastRelayedHitId, (int)DamageContext.Kind, DamageContext.Penetration, DamageContext.BreaksGuard,
                Flags(DamageContext.Edged, DamageContext.Bloodless, contact.HasValue, DamageContext.Chill > 0.0f, DamageContext.HeavyBlow, DamageContext.ByPlayer),
                contact ?? Vector3.zero);
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

        /// <summary>The most one forwarded heal may restore (a beam's tick is a few; the server refuses more).</summary>
        public const float MaxHeal = 40.0f;

        public bool TryRelayHeal(float amount)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_vitals == null || _vitals.IsDead || amount <= 0.0f)
            {
                return true;   // nothing to heal, and nothing is sent
            }
            SendHealToServer(Mathf.Min(amount, MaxHeal));
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendHealToServer(float amount, NetworkConnection sender = null)
        {
            if (!Allowed(sender, amount) || amount > MaxHeal)
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyHealOnOwner(Owner, amount);
            }
        }

        [TargetRpc]
        private void ApplyHealOnOwner(NetworkConnection target, float amount)
        {
            if (_vitals != null)
            {
                _vitals.Heal(Mathf.Clamp(amount, 0.0f, MaxHeal));
            }
        }

        /// <summary>The fastest a forwarded knockdown may throw a body, metres per second.</summary>
        public const float MaxKnockdownSpeed = 30.0f;

        public bool TryRelayKnockdown(Vector3 velocity)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_vitals != null && _vitals.IsDead)
            {
                return true;
            }
            SendKnockdownToServer(velocity);
            return true;
        }

        /// <summary>A knockdown's throw as the owner takes it: finite, and no faster than <see cref="MaxKnockdownSpeed"/>.</summary>
        public static Vector3 SaneKnockdown(Vector3 velocity)
        {
            return WireChecks.Finite(velocity) ? Vector3.ClampMagnitude(velocity, MaxKnockdownSpeed) : Vector3.zero;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendKnockdownToServer(Vector3 velocity, NetworkConnection sender = null)
        {
            if (!Allowed(sender, 1.0f) || !WireChecks.Finite(velocity))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyKnockdownOnOwner(Owner, SaneKnockdown(velocity));
            }
        }

        [TargetRpc]
        private void ApplyKnockdownOnOwner(NetworkConnection target, Vector3 velocity)
        {
            if (_vitals != null)
            {
                _vitals.KnockDown(SaneKnockdown(velocity));
            }
        }

        /// <summary>The farthest, longest and highest a forwarded pull may draw a body: metres, seconds, metres a second.</summary>
        public const float MaxPullMetres = 4.0f, MaxPullSeconds = 1.0f, MaxPullLift = 12.0f;

        public bool TryRelayPull(Vector3 direction, float metres, float seconds, float lift)
        {
            if (!IsCopy)
            {
                return false;
            }
            if (_vitals != null && _vitals.IsDead)
            {
                return true;
            }
            SendPullToServer(direction, metres, seconds, lift);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendPullToServer(Vector3 direction, float metres, float seconds, float lift, NetworkConnection sender = null)
        {
            if (!Allowed(sender, 1.0f) || !WireChecks.Finite(direction) || !WireChecks.Finite(new Vector3(metres, seconds, lift)))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyPullOnOwner(Owner, direction, metres, seconds, lift);
            }
        }

        [TargetRpc]
        private void ApplyPullOnOwner(NetworkConnection target, Vector3 direction, float metres, float seconds, float lift)
        {
            if (_vitals != null && WireChecks.Finite(direction))
            {
                _vitals.Pull(direction, Mathf.Clamp(metres, 0.0f, MaxPullMetres), Mathf.Clamp(seconds, 0.05f, MaxPullSeconds), Mathf.Clamp(lift, 0.0f, MaxPullLift));
            }
        }

        /// <summary>The farthest a forwarded hit's contact point may be from the body it struck, metres; one further is dropped (the wound goes on the near side).</summary>
        public const float MaxContactMetres = 6.0f;

        private const int EdgedFlag = 1;
        private const int BloodlessFlag = 2;
        private const int ContactFlag = 4;
        private const int ChillFlag = 8;
        private const int HeavyBlowFlag = 16;
        private const int ByPlayerFlag = 32;

        /// <summary>
        /// What a forwarded hit says about itself beyond its numbers: struck with an edge, drawing no blood, whether it says where it landed,
        /// whether it chills (a frost blow: the body takes one tick's chill, docs/FROST.md), whether it lands as a heavy blow (DamageContext.HeavyBlow),
        /// and whether a player struck it (DamageContext.ByPlayer: armour stops less of it, docs/ARMOUR.md).
        /// </summary>
        public static int Flags(bool edged, bool bloodless, bool hasContact, bool chilled = false, bool heavyBlow = false, bool byPlayer = false)
        {
            return (edged ? EdgedFlag : 0) | (bloodless ? BloodlessFlag : 0) | (hasContact ? ContactFlag : 0) | (chilled ? ChillFlag : 0) | (heavyBlow ? HeavyBlowFlag : 0)
                | (byPlayer ? ByPlayerFlag : 0);
        }

        /// <summary>A forwarded hit's contact point as the owner takes it: only one that is finite and on (near) the body it struck.</summary>
        public static Vector3? SaneContact(int flags, Vector3 contact, Vector3 body)
        {
            if ((flags & ContactFlag) == 0 || !WireChecks.Finite(contact) || Vector3.Distance(contact, body) > MaxContactMetres)
            {
                return null;
            }
            return contact;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendHitToServer(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage, int hitId, int kind, float penetration,
            bool breaksGuard, int flags, Vector3 contact, NetworkConnection sender = null)
        {
            if (!Allowed(sender, amount) || !WireChecks.Finite(fromPosition) || float.IsNaN(hitstunSeconds) || float.IsNaN(poiseDamage) || float.IsNaN(penetration))
            {
                return;
            }
            if (Owner.IsValid)
            {
                ApplyHitOnOwner(Owner, Mathf.Min(amount, MaxHitDamage), fromPosition, Mathf.Clamp(hitstunSeconds, 0.0f, 3.0f),
                    Mathf.Clamp(poiseDamage, -1.0f, MaxPoiseDamage), hitId, sender.ClientId, Mathf.Clamp(kind, 0, (int)DamageKind.Magic), Mathf.Clamp01(penetration), breaksGuard,
                    flags, WireChecks.Finite(contact) ? contact : Vector3.zero);
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
        private void ApplyHitOnOwner(NetworkConnection target, float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage, int hitId, int attackerClientId,
            int kind, float penetration, bool breaksGuard, int flags, Vector3 contact)
        {
            bool wasAlive = _vitals != null && _vitals.IsAlive;
            HitResponse response = ApplyHitHere(amount, fromPosition, hitstunSeconds, poiseDamage, (DamageKind)kind, penetration, breaksGuard,
                (flags & EdgedFlag) != 0, (flags & BloodlessFlag) != 0, SaneContact(flags, contact, transform.position), (flags & ChillFlag) != 0, (flags & HeavyBlowFlag) != 0,
                (flags & ByPlayerFlag) != 0);
            if (wasAlive && _vitals.IsDead && _vitals.XpRewardOnDeath > 0)
            {
                EnemyNetwork.CreditKill(NetworkManager, attackerClientId, _vitals.XpRewardOnDeath);   // an enemy this machine runs, killed by another player
            }
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

        /// <summary>
        /// Applies a forwarded hit to THIS body as if it had been struck here: its kind, the attacker's armour penetration, whether it was struck
        /// with an edge or drew no blood, and where it landed came with it, so this body's own armour, magic resistance and block chance answer
        /// it (2026-10-14) and a killing blow takes it apart as it would have here (docs/DISMEMBERMENT.md). Public so a test can drive it
        /// without a network.
        /// </summary>
        public HitResponse ApplyHitHere(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage = -1.0f,
            DamageKind kind = DamageKind.Physical, float penetration = 0.0f, bool breaksGuard = false, bool edged = false, bool bloodless = false,
            Vector3? contact = null, bool chilled = false, bool heavyBlow = false, bool byPlayer = false)
        {
            if (_vitals == null)
            {
                return HitResponse.Ignored;
            }
            if (poiseDamage >= 0.0f)
            {
                _vitals.TakePoise(poiseDamage);
            }
            using (DamageContext.GuardBreaking(breaksGuard))
            using (DamageContext.Cutting(edged))
            using (new DamageContext.BloodlessScope(bloodless))
            using (DamageContext.Chilling(chilled ? FrostRules.ChillPerTick : 0.0f))
            using (DamageContext.LandingHeavily(heavyBlow))
            using (DamageContext.StruckByPlayer(byPlayer))
            {
                return DamageContext.Strike(_vitals, amount, fromPosition, hitstunSeconds, kind, penetration, contact);
            }
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
            if (!_rate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return false;
            }
            return AttackerNear(sender, transform.position);
        }

        /// <summary>Whether a body the attacker owns (theirs, or an enemy they run) is within <see cref="MaxRelayMetres"/> of the victim.</summary>
        public static bool AttackerNear(NetworkConnection sender, Vector3 victim)
        {
            foreach (NetworkObject owned in sender.Objects)
            {
                if (owned != null && Vector3.Distance(owned.transform.position, victim) <= MaxRelayMetres)
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
            float maxHealth = _vitals.MaxHealth;
            if (Mathf.Approximately(health, _lastSentHealth) && Mathf.Approximately(maxHealth, _lastSentMaxHealth))
            {
                return;
            }
            _lastSentHealth = health;
            _lastSentMaxHealth = maxHealth;
            SendHealthToServer(health, maxHealth);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendHealthToServer(float health, float maxHealth)
        {
            if (float.IsNaN(health) || float.IsInfinity(health) || float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0.0f)
            {
                return;
            }
            BroadcastHealth(health, maxHealth);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastHealth(float health, float maxHealth)
        {
            if (_vitals != null)
            {
                _vitals.SetHealthFromNetwork(health, maxHealth);
            }
        }
    }
}
