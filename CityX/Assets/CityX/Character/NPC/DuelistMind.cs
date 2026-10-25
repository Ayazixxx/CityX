using UnityEngine;

namespace CityX.Character
{
    /// <summary>What the duelist is doing (for the debug readout and the tests).</summary>
    public enum DuelistState
    {
        /// <summary>Nobody to fight.</summary>
        Idle,

        /// <summary>Walking (or running) in to fighting distance.</summary>
        Approach,

        /// <summary>At fighting distance, circling with the guard down, watching.</summary>
        Circle,

        /// <summary>Circling behind his raised guard: up before any blow comes, never thrown up against one.</summary>
        Guard,

        /// <summary>Dashed out of the way (a dodge, or getting out after being hit).</summary>
        Evade,

        /// <summary>On the attack: a chain of cuts, or the heavy.</summary>
        Attack,

        /// <summary>The target is staggered (it swung into his guard, or he parried it): straight in with cuts while it is open.</summary>
        Punish,

        /// <summary>Short of stamina: backed off, circling, until it has come back.</summary>
        Recover,
    }

    /// <summary>
    /// THE DUELIST'S MIND (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him. make him utilize
    /// dashes as well as blocking and make him take advantage of the stagger if i attack their block"). One of the six reusable brains the
    /// design plans (docs/GAME_DESIGN.md: brawler, duelist, caster, gunner, summoner, ambusher). Pure: it reads what the body can tell it
    /// (<see cref="Senses"/>) and says what a player would press (<see cref="Intent"/>); SwordsmanBrain hands that to the same sword combat,
    /// dash and locomotion the player uses.
    ///
    /// How a duelist fights, by priority each frame:
    ///  1. PUNISH: the target is OPEN -- it swung into his guard and its blade rebounded (SoulsSwordCombat.GuardReboundSeconds), he parried
    ///     it, or its guard broke -- so he goes straight in with cuts while it is open (dashing in if it is too far to walk in time). A mere
    ///     flinch from his own last cut is not an opening: pressing every one was a stun-lock, not a punish.
    ///  2. DASH OUT OF A SLOW BLOW: the target starts a swing in reach; after a person's reaction (0.3-0.45 s, longer than a light cut takes to
    ///     land) he sometimes dashes out of it if it is still winding up -- only a heavy is that slow. He NEVER throws his guard up against a
    ///     blow already on its way (owner, 2026-10-24: "the enemies get a lot of really bullshit blocks where the second you swing they just
    ///     throw up guard and you get punished ... find another way"): his guard is a STANCE, chosen for each spell of circling and up before
    ///     any swing comes, so it can be seen (Dark Souls' shield-bearers: break it with a heavy, wait it out, or go round it).
    ///  3. RECOVER: short of stamina, he backs off and circles until it is back (no attacking or blocking on fumes).
    ///  4. ATTACK: in reach, the target not swinging and not dashing: a chain of one or two cuts, or now and then the heavy; then he gets
    ///     back out, often with a dash. A raised guard he mostly does not swing into (he would rebound off it, as the player does) -- he waits
    ///     it out, and only sometimes drives a heavy into it to drain it.
    ///  5. APPROACH -- closing the last stretch with a dash now and then -- then CIRCLE at fighting distance, changing direction every second
    ///     or three, keeping his distance; some spells of circling behind his raised guard (the stance above).
    /// IN A PACK (EnemyPack) he punishes and attacks only on his turn (<see cref="Senses.Waiting"/>); waiting, he circles out at
    /// <see cref="WaitMetres"/>, the other way from his nearest packmate, still guarding and dashing as ever.
    /// After being hit he sometimes dashes away. Every dash is the player's own (double-tap Shift with a sword out), with its stamina, so he
    /// keeps a dash's worth in hand. Nothing is instant except the punish (the stagger already gave him time).
    /// </summary>
    public sealed class DuelistMind
    {
        // ---- distances (metres) ----

        /// <summary>He stops closing and circles at about this distance.</summary>
        public const float EngageMetres = 3.4f;

        /// <summary>A light cut reaches this far (the reach box's 2.2 m and the lunge).</summary>
        public const float StrikeMetres = 2.5f;

        /// <summary>A swing started this close can reach him: he answers it.</summary>
        public const float ThreatMetres = 3.8f;

        /// <summary>Further off than this he runs in.</summary>
        public const float SprintBeyondMetres = 8.0f;

        /// <summary>A staggered target further off than this is closed on with a dash in (walking would arrive too late).</summary>
        public const float DashInBeyondMetres = 3.6f;

        /// <summary>When recovering stamina he backs off to about here.</summary>
        public const float RecoverMetres = 5.5f;

        /// <summary>
        /// Waiting his turn (another of the pack is attacking: EnemyPack, AttackTurns) he circles out here instead, just out of a cut's reach:
        /// close enough to step in the moment the turn is his, far enough not to crowd the one attacking.
        /// </summary>
        public const float WaitMetres = 5.2f;

        // ---- timing (seconds) ----

        /// <summary>
        /// How long he takes to answer a swing he sees start: a person's reaction (seeing it, deciding, moving), and longer than a light cut takes
        /// to land (0.26-0.34 s), so a quick cut already on its way lands; only a slow swing (a heavy) leaves him time to dash out of it.
        /// </summary>
        public const float DodgeReactionMin = 0.3f;

        public const float DodgeReactionMax = 0.45f;

        /// <summary>How long he circles one way before switching.</summary>
        public const float CircleMin = 1.2f;

        public const float CircleMax = 2.8f;

        /// <summary>The pause between his attacks.</summary>
        public const float AttackCooldownMin = 1.0f;

        public const float AttackCooldownMax = 2.4f;

        /// <summary>He steps back out for this long after a chain.</summary>
        public const float BackOffSeconds = 0.9f;

        /// <summary>He presses the next cut this often in a chain or a punish (the combat holds a press until the chain window opens).</summary>
        public const float PressSeconds = 0.3f;

        /// <summary>A dash takes this long before he decides anything else.</summary>
        public const float DashSeconds = 0.5f;

        // ---- choices (chances, 0 to 1) ----

        /// <summary>A spell of circling is spent behind his raised guard this often, decided as it begins.</summary>
        public const float GuardStanceChance = 0.45f;

        /// <summary>A slow swing still winding up once he has reacted, he dashes out of this often (never from behind his guard: he trusts it).</summary>
        public const float DodgeChance = 0.35f;

        /// <summary>He raises his guard only within this much of his ring: further off he comes in with it down.</summary>
        public const float GuardWithinMetres = 1.5f;

        /// <summary>An attack is the heavy this often.</summary>
        public const float HeavyChance = 0.15f;

        /// <summary>He swings into a raised guard only this often (a heavy, to drain it): mostly he waits it out.</summary>
        public const float IntoGuardChance = 0.12f;

        /// <summary>Hit, he dashes away this often.</summary>
        public const float DashWhenHitChance = 0.35f;

        /// <summary>After a chain he dashes back out this often (else he walks back out): hit, then gone.</summary>
        public const float DashOutChance = 0.4f;

        /// <summary>Coming in from <see cref="DashInFromMetres"/> or more, he closes with a dash about this often a second.</summary>
        public const float DashInPerSecond = 0.35f;

        /// <summary>He closes with a dash only from this far (any nearer he walks in to circle).</summary>
        public const float DashInFromMetres = 5.0f;

        /// <summary>A dash costs this share of the bar or more: he keeps it in hand for one.</summary>
        public const float DashStamina = 0.2f;

        // ---- stamina (shares of the bar) ----

        /// <summary>Below this he breaks off to recover...</summary>
        public const float LowStamina = 0.25f;

        /// <summary>...until it is back to this.</summary>
        public const float RecoveredStamina = 0.6f;

        /// <summary>He starts an attack only with this much.</summary>
        public const float AttackStamina = 0.3f;

        /// <summary>He takes his guard stance only with this much (a guard costs stamina for every blow it soaks).</summary>
        public const float GuardStamina = 0.2f;

        /// <summary>What his body can tell him this frame.</summary>
        public struct Senses
        {
            public bool Dead;
            public bool HasTarget;

            /// <summary>Metres to the target, flat.</summary>
            public float Distance;

            /// <summary>The target's swing is winding up or live (it can still hurt).</summary>
            public bool TargetAttacking;

            /// <summary>The target's swing is still winding up (the blade not yet live).</summary>
            public bool TargetWindingUp;

            /// <summary>The target holds a guard.</summary>
            public bool TargetBlocking;

            /// <summary>
            /// The target is OPEN: its blade rebounded off his guard (or he parried it) or its guard broke, and it is still stunned. Not merely
            /// flinching from his own last cut: pressing that would be a stun-lock, not a punish.
            /// </summary>
            public bool TargetStaggered;

            /// <summary>The target is dashing (it cannot be hit).</summary>
            public bool TargetDashing;

            /// <summary>His own stamina, as a share of the bar.</summary>
            public float Stamina;

            /// <summary>His own swing is under way (he cannot start another, block or walk freely).</summary>
            public bool Committed;

            /// <summary>His combat allows a dash now (idle, or the tail of a swing) and he is not already dashing.</summary>
            public bool CanDash;

            /// <summary>He is stunned himself (his blade rebounded, he was hit hard).</summary>
            public bool Stunned;

            /// <summary>He was hurt since the last frame.</summary>
            public bool TookHit;

            /// <summary>
            /// Another of the pack holds the turn to attack the target, or a packmate stands in the way: he keeps his distance and does not swing
            /// (EnemyPack). False when he fights alone.
            /// </summary>
            public bool Waiting;

            /// <summary>Which side of him his nearest packmate is, as he faces the target: +1 right, -1 left, 0 none near. He circles the other way.</summary>
            public float CrowdSide;
        }

        /// <summary>What he presses this frame. Move is in the target's frame: x to his right as he faces it, y toward it.</summary>
        public struct Intent
        {
            public Vector2 Move;
            public bool Sprint;

            /// <summary>Hold the guard this frame.</summary>
            public bool Guard;

            /// <summary>Press a light attack (this frame only).</summary>
            public bool Light;

            /// <summary>Press the heavy (this frame only).</summary>
            public bool Heavy;

            /// <summary>Dash (this frame only), toward <see cref="DashDirection"/> in the target's frame.</summary>
            public bool Dash;

            public Vector2 DashDirection;
        }

        private readonly System.Random _random;
        private float _stateTime;
        private bool _threatSeen;
        private float _reaction;
        private bool _dodge;
        private bool _guardStance;
        private bool _guardUp;
        private float _circleSign = 1.0f;
        private float _circleLeft;
        private float _attackCooldown;
        private int _chainLeft;
        private float _pressTimer;
        private float _backOff;
        private float _dashBusy;
        private bool _recovering;

        public DuelistMind(int seed)
        {
            _random = new System.Random(seed);
            _circleLeft = Range(CircleMin, CircleMax);
            _guardStance = Chance() < GuardStanceChance;
        }

        public DuelistState State { get; private set; } = DuelistState.Idle;

        /// <summary>His guard is up (the stance).</summary>
        public bool GuardUp => _guardUp;

        private float Chance() => (float)_random.NextDouble();

        private float Range(float min, float max) => min + (max - min) * Chance();

        private void Become(DuelistState state)
        {
            if (state != State)
            {
                State = state;
                _stateTime = 0.0f;
            }
        }

        /// <summary>One frame: what he presses, given what he senses.</summary>
        public Intent Tick(in Senses s, float deltaTime)
        {
            var intent = new Intent();
            _stateTime += deltaTime;
            _attackCooldown -= deltaTime;
            _pressTimer -= deltaTime;
            _backOff -= deltaTime;
            _dashBusy -= deltaTime;
            _circleLeft -= deltaTime;
            if (_circleLeft <= 0.0f)
            {
                _circleSign = -_circleSign;
                _circleLeft = Range(CircleMin, CircleMax);
                _guardStance = Chance() < GuardStanceChance;   // the next spell: behind his guard or not, decided now, not when a blow comes
            }

            if (s.Dead || !s.HasTarget)
            {
                Become(DuelistState.Idle);
                _chainLeft = 0;
                _guardUp = false;
                return intent;
            }
            if (s.Stunned)
            {
                // His own blade rebounded, or he was hit hard: the body is locked; whatever he was about is over.
                _chainLeft = 0;
                _guardUp = false;
                return intent;
            }
            if (_dashBusy > 0.0f)
            {
                return intent;   // mid-dash: the dash carries him
            }

            // A swing the target starts in reach: noticed once. Never answered with the guard; with a dash out, sometimes, once he has had
            // time to react (section 2).
            bool threat = s.TargetAttacking && s.Distance <= ThreatMetres;
            if (threat && !_threatSeen)
            {
                _threatSeen = true;
                _reaction = Range(DodgeReactionMin, DodgeReactionMax);
                _dodge = Chance() < DodgeChance;
            }
            if (!s.TargetAttacking)
            {
                _threatSeen = false;
                _dodge = false;
            }

            // 1. PUNISH: the target is open. No reaction delay: the stagger already gave him the time.
            if (s.TargetStaggered && !s.TargetDashing && !s.Waiting)
            {
                bool opening = State != DuelistState.Punish;
                Become(DuelistState.Punish);
                _recovering = false;
                _guardUp = false;
                if (s.Distance > StrikeMetres)
                {
                    intent.Move = new Vector2(0.0f, 1.0f);
                    intent.Sprint = s.Distance > EngageMetres;
                    if (s.Distance > DashInBeyondMetres && s.CanDash && !s.Committed)
                    {
                        Dash(ref intent, new Vector2(0.0f, 1.0f));
                    }
                    return intent;
                }
                intent.Move = new Vector2(0.0f, 0.4f);
                if (opening || _pressTimer <= 0.0f)
                {
                    intent.Light = true;   // the first the moment the opening shows, then one a press-time on (the chain takes them in turn)
                    _pressTimer = PressSeconds;
                }
                _chainLeft = 0;
                return intent;
            }

            // 2. DASH OUT of a slow blow he saw coming, once he has had time to react: a swing still winding up then (a heavy). Not from behind
            // his guard (he trusts it), and never the guard itself.
            if (_threatSeen && _dodge && !s.Committed)
            {
                _reaction -= deltaTime;
                if (_reaction <= 0.0f)
                {
                    _dodge = false;
                    if (s.TargetWindingUp && !_guardUp && s.CanDash && s.Stamina >= DashStamina)
                    {
                        Dash(ref intent, AwayDirection());
                        return intent;
                    }
                }
            }

            // Hit (and not already swinging): sometimes he dashes away from it.
            if (s.TookHit && !s.Committed && s.CanDash && Chance() < DashWhenHitChance)
            {
                Dash(ref intent, AwayDirection());
                return intent;
            }

            // 3. RECOVER his stamina.
            if (s.Stamina < LowStamina)
            {
                _recovering = true;
            }
            else if (s.Stamina >= RecoveredStamina)
            {
                _recovering = false;
            }
            if (_recovering && !s.Committed)
            {
                Become(DuelistState.Recover);
                _chainLeft = 0;
                _guardUp = false;
                intent.Move = new Vector2(_circleSign * 0.6f, s.Distance < RecoverMetres ? -0.8f : 0.0f);
                return intent;
            }

            // 4. ATTACK: the chain he started, pressed on through its cuts...
            if (State == DuelistState.Attack)
            {
                if (s.Committed)
                {
                    intent.Move = new Vector2(0.0f, 0.3f);
                    if (_chainLeft > 0 && _pressTimer <= 0.0f && !s.TargetBlocking)
                    {
                        intent.Light = true;
                        _chainLeft--;
                        _pressTimer = PressSeconds;
                    }
                    return intent;
                }
                if (_stateTime > 0.2f)
                {
                    // ...and over: he gets back out -- with a dash now and then, else walking.
                    _chainLeft = 0;
                    _backOff = BackOffSeconds;
                    _attackCooldown = Range(AttackCooldownMin, AttackCooldownMax);
                    if (s.CanDash && s.Stamina >= DashStamina && Chance() < DashOutChance)
                    {
                        Dash(ref intent, AwayDirection());
                        return intent;
                    }
                    Become(DuelistState.Circle);
                }
                else
                {
                    return intent;   // the press has not turned into a swing yet
                }
            }

            // ...or a new one.
            bool inReach = s.Distance <= StrikeMetres + 0.3f;
            if (inReach && _backOff <= 0.0f && _attackCooldown <= 0.0f && s.Stamina >= AttackStamina && !s.TargetDashing && !s.Committed
                && !s.TargetAttacking && !s.Waiting)
            {
                if (!s.TargetBlocking || Chance() < IntoGuardChance)
                {
                    StartAttack(ref intent, s, heavy: s.TargetBlocking);
                    return intent;
                }
                _attackCooldown = Range(AttackCooldownMin, AttackCooldownMax);   // a raised guard: wait it out
            }

            // 5. APPROACH, then CIRCLE -- further out while another has the turn, and the other way from the nearest packmate, so the pack
            // spreads round the target.
            float ring = s.Waiting ? WaitMetres : EngageMetres;
            if (s.CrowdSide != 0.0f)
            {
                _circleSign = -Mathf.Sign(s.CrowdSide);
            }
            // His guard: up for this spell of circling if he chose it, once near his ring -- raised only while no blow of the target's is on
            // its way, so it is there before a swing starts or not at all, and never lowered under one, so the guard a swing started against
            // is the guard it meets; down when he attacks, dashes, runs short of stamina, or the spell ends.
            bool guardWanted = _guardStance && s.Stamina >= GuardStamina && s.Distance <= ring + GuardWithinMetres;
            if (!guardWanted)
            {
                if (!threat)
                {
                    _guardUp = false;
                }
            }
            else if (!_guardUp && !threat)
            {
                _guardUp = true;
            }
            intent.Guard = _guardUp;
            if (s.Distance > ring + 0.4f && _backOff <= 0.0f)
            {
                Become(DuelistState.Approach);
                intent.Move = new Vector2(0.0f, 1.0f);
                intent.Sprint = s.Distance > SprintBeyondMetres;
                if (!s.Waiting && s.Distance >= DashInFromMetres && s.CanDash && s.Stamina >= DashStamina * 2.0f
                    && Chance() < DashInPerSecond * deltaTime)
                {
                    Dash(ref intent, new Vector2(0.0f, 1.0f));   // closing the gap
                }
                return intent;
            }
            Become(_guardUp ? DuelistState.Guard : DuelistState.Circle);
            float inOut = 0.0f;
            if (_backOff > 0.0f || s.Distance < ring - 0.7f)
            {
                inOut = -0.8f;
            }
            else if (s.Distance > ring + 0.2f)
            {
                inOut = 0.5f;
            }
            intent.Move = new Vector2(_circleSign * 0.75f, inOut);
            return intent;
        }

        private void StartAttack(ref Intent intent, in Senses s, bool heavy = false)
        {
            Become(DuelistState.Attack);
            _guardUp = false;   // the guard comes down to swing (the sword combat drops it)
            _guardStance = false;
            if (heavy || Chance() < HeavyChance)
            {
                intent.Heavy = true;
                _chainLeft = 0;
            }
            else
            {
                intent.Light = true;
                _chainLeft = _random.Next(0, 2);   // one or two cuts: a duelist, not a berserker
            }
            _pressTimer = PressSeconds;
        }

        private void Dash(ref Intent intent, Vector2 direction)
        {
            Become(DuelistState.Evade);
            _guardUp = false;   // a dash drops the guard
            intent.Dash = true;
            intent.DashDirection = direction.normalized;
            _dashBusy = DashSeconds;
            _chainLeft = 0;
        }

        /// <summary>Back and to one side, away from the target (whichever side he is circling to).</summary>
        private Vector2 AwayDirection()
        {
            return Chance() < 0.3f ? new Vector2(_circleSign, 0.0f) : new Vector2(_circleSign * 0.7f, -0.7f);
        }
    }
}
