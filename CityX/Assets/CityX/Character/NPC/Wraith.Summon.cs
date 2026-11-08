using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SUMMONED WRAITH (the Wraith Staff; owner, 2026-11-08: "it summons 1 wraith that follows you around and fights for you, when he dies
    /// you can summon another", then "have the enemies view the summon as an opponent as well"). The same Wraith, on its summoner's side: it
    /// drifts along at their shoulder, hunts the enemies near them with its own sneak and bite (a shorter rest between bites), is sturdier
    /// than a wild one, and is fought by the enemies as a player is (<see cref="Opponents"/>). No player's blow hurts it and its kills are its
    /// summoner's (DamageContext.Strike). In a session its summoner's machine runs it (the networked "wraith_summon", every copy knowing it
    /// for a summon from its prefab, <see cref="StartsSummoned"/>).
    ///
    /// A WILD WRAITH'S DROP: killed, one in <see cref="StaffDropChance"/> leaves a Wraith Staff ("a rare drop from the wraiths like a 3% drop
    /// chance"), rolled by the machine that runs it (a copy rolls nothing), shared in a session as a swordsman's sword is.
    /// </summary>
    public sealed partial class Wraith : IOpponent
    {
        /// <summary>A summon's health: a wild one dies to one pistol round; a summon lasts a fight.</summary>
        public const float SummonedMaxHealth = 120.0f;

        /// <summary>A summon hunts the enemies within this of its summoner, metres.</summary>
        public const float GuardMetres = WraithHunt.NoticeMetres;

        /// <summary>It rests this long between bites, seconds (a wild one, WraithHunt.CooldownSeconds).</summary>
        public const float SummonedRestSeconds = 2.0f;

        /// <summary>Where it keeps to, from its summoner: behind and to the right, over the shoulder, metres.</summary>
        public const float FollowBehindMetres = 1.6f;

        public const float FollowSideMetres = 1.4f;

        /// <summary>How fast it keeps up, metres per second (a player runs at about 6).</summary>
        public const float FollowSpeed = 8.0f;

        /// <summary>Left further behind than this it is with its summoner again at once, metres.</summary>
        public const float CatchUpMetres = 40.0f;

        /// <summary>The share of wild Wraiths that leave a Wraith Staff.</summary>
        public const float StaffDropChance = 0.03f;

        [Tooltip("A summon from the start: the networked summon's prefab, so every machine's copy knows it is on the players' side.")]
        public bool StartsSummoned;

        [Tooltip("The Wraith Staff it can leave (WraithBuilder).")]
        public GameObject StaffPrefab;

        private PlayerVitals _master;
        private bool _summoned;

        /// <summary>On the players' side: summoned by a Wraith Staff.</summary>
        public bool IsSummoned => _summoned || StartsSummoned;

        /// <summary>Who summoned it, on the machine that runs it; null on a copy and for a wild one.</summary>
        public PlayerVitals Master => _master;

        /// <summary>
        /// Summoned by <paramref name="master"/>: on their side from now on, at a summon's health. On the machine that runs it; a copy knows
        /// it is a summon from its prefab.
        /// </summary>
        public void Summon(PlayerVitals master)
        {
            Bind();
            _master = master;
            _summoned = true;
            _health = SummonedMaxHealth;
            _hunt.RestSeconds = SummonedRestSeconds;
            _hunt.Bold = true;
            JoinIfSummoned();
        }

        private float MaxHealthNow => IsSummoned ? SummonedMaxHealth : MaxHealth;

        private void JoinIfSummoned()
        {
            if (IsSummoned && !_dead && isActiveAndEnabled)
            {
                Opponents.Join(this);
            }
        }

        private void OnEnable()
        {
            if (!_all.Contains(this))
            {
                _all.Add(this);
            }
            JoinIfSummoned();
        }

        private void OnDisable()
        {
            _all.Remove(this);
            Opponents.Leave(this);
        }

        /// <summary>The summon's own tick when it is not hunting: with its summoner, or gone with them.</summary>
        private void TickSummoned(float deltaTime)
        {
            if (_master == null || !_master.IsAlive)
            {
                Die();   // its summoner gone, it goes too
                return;
            }
            Transform master = _master.transform;
            Vector3 spot = master.position - Flat(master.forward).normalized * FollowBehindMetres + Flat(master.right).normalized * FollowSideMetres;
            Vector3 to = Flat(spot - transform.position);
            float distance = to.magnitude;
            if (distance > CatchUpMetres)
            {
                transform.position = new Vector3(spot.x, transform.position.y, spot.z);
                distance = 0.0f;
            }
            float speed = Mathf.Min(FollowSpeed, distance * 2.0f);
            if (speed > 0.05f)
            {
                Vector3 direction = _steering.Steer(transform, to / distance, deltaTime, master);
                transform.position += direction * (speed * deltaTime);
            }
            _stalkSpeed = speed;
            TurnToward(distance > 1.0f ? to : Flat(master.forward), deltaTime);
            Hover(deltaTime);
        }

        /// <summary>
        /// What a summon goes for: the nearest living enemy (an enemy pack's member, or a wild Wraith) within <see cref="GuardMetres"/> of its
        /// summoner; null for none.
        /// </summary>
        private Component NearestHostile()
        {
            Vector3 from = _master != null ? _master.transform.position : transform.position;
            Component best = null;
            float nearest = GuardMetres;
            foreach (Transform member in EnemyPack.Members)
            {
                if (member != null && member.TryGetComponent(out IDamageable body) && body.IsAlive)
                {
                    Consider(member, body as Component, ref best, ref nearest, from);
                }
            }
            foreach (Wraith wraith in All)
            {
                if (wraith != this && !wraith.IsSummoned && wraith.IsAlive)
                {
                    Consider(wraith.transform, wraith, ref best, ref nearest, from);
                }
            }
            return best;
        }

        private static void Consider(Transform body, Component hit, ref Component best, ref float nearest, Vector3 from)
        {
            float distance = Vector3.Distance(from, body.position);
            if (hit != null && distance < nearest)
            {
                nearest = distance;
                best = hit;
            }
        }

        /// <summary>Killed: a summon is fought no more; a wild one's machine rolls for its staff.</summary>
        private void OnKilled()
        {
            Opponents.Leave(this);
            if (IsSummoned || IsCopy || !Application.isPlaying || Random.value >= StaffDropChance)
            {
                return;
            }
            DropStaff();
        }

        /// <summary>A Wraith Staff falls out of it.</summary>
        public void DropStaff()
        {
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            var pose = new Pose(chest, Quaternion.LookRotation(transform.right, Vector3.up));
            DroppedItems.Shared.Drop(DroppedItem.LetGo(ItemRegistry.WraithStaff, ItemInstanceData.None, pose, transform, Vector3.zero, Vector3.up),
                shared: EnemySession.IsShared(gameObject), gameObject, StaffPrefab);
        }

        // --- IOpponent: what an enemy reads of a summon ---

        bool IOpponent.IsInvulnerable => false;
        bool IOpponent.IsBlocking => false;
        bool IOpponent.IsStunned => _hurt > 0.5f;
        float IOpponent.HitstunRemaining => 0.0f;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
