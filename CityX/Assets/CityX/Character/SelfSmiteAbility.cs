using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// SELF SMITE (the Electromancer tree's first spell, cast from the bar): Pikachu's Thunder. The caster throws their hands up and down
    /// (selfsmite.fbx, the owner's), a thundercloud forms 1.5 km overhead, and a giant yellow bolt comes down out of it and strikes the caster,
    /// who bursts with electricity: everyone close by is hurt and thrown, anyone the bolt passed on its way down is hurt too, and the caster is
    /// unharmed -- for the moment of the strike nothing can touch them. Timed frame for frame to Thunder (<see cref="SelfSmiteMove"/>): the bolt
    /// lands at 0.55 s, the caster is free at 1.23 s, and stands still for all of it.
    ///
    /// THE DAMAGE IS THE CASTER'S MACHINE'S, as every hit is: the bolt's column and the burst are tested against whatever bodies exist here, and a
    /// hit on another player's copy is forwarded to its owner (NetworkedDamage), throw and all (PlayerVitals.TriggerLaunchKnockdown). Spell Power
    /// makes it hurt more and the caster's Armor Penetration goes with it, as with every spell.
    ///
    /// MULTIPLAYER (IReplicatedAbility, id "self-smite"): the owner fires once, as it is cast; every other player's copy of the caster plays the
    /// same animation and the same cloud, bolt, burst, light and thunder (SelfSmiteBolt), hurting nothing.
    /// </summary>
    public sealed class SelfSmiteAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "self-smite";

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The thunder as the bolt lands.")]
        public AudioClip StrikeSound;

        [Tooltip("What the bolt and the burst are drawn with: additive, soft where they meet the world (SelfSmiteBolt; built by PlayerAbilityWiring).")]
        public Material LightMaterial;

        [Tooltip("Seconds between casts (Cooldown Reduction shortens it).")]
        public float CooldownSeconds = SelfSmiteMove.CooldownSeconds;

        /// <summary>The move is playing (on this body, or shown on a copy).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Seconds since it was cast.</summary>
        public float Seconds { get; private set; }

        /// <summary>The caster is held still by the move (the owner's body only; a copy moves with the network).</summary>
        public bool HoldsTheBody => IsActive && !_remote;

        /// <summary>The bolt on show (null between casts, and once it has faded).</summary>
        public SelfSmiteBolt Bolt => _bolt;

        /// <summary>Everyone the bolt's fall and the burst have hurt, all told (for tests).</summary>
        public int BoltHits { get; private set; }

        public int BlastHits { get; private set; }

        public float CooldownRemaining => Mathf.Max(0.0f, _cooldown);

        private static readonly Collider[] Overlaps = new Collider[128];

        private readonly HashSet<IDamageable> _boltHit = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _blasted = new HashSet<IDamageable>();

        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private Ragdoll _ragdoll;
        private Animator _animator;
        private SelfSmiteBolt _bolt;
        private bool _remote;
        private bool _struck;
        private bool _blastChecked;
        private float _cooldown;
        private float _tipAbove;
        private int _casts;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _vitals = GetComponent<PlayerVitals>();
            _melee = GetComponent<MeleeAttacker>();
            _ragdoll = GetComponent<Ragdoll>();
            _animator = GetComponentInChildren<Animator>();
        }

        private float Height => _locomotion != null ? _locomotion.StandingHeight : 2.6f;

        public bool CanCast =>
            !IsActive && _cooldown <= 0.0f && (_vitals == null || (!_vitals.IsDead && !_vitals.IsStunned)) && (_ragdoll == null || !_ragdoll.IsLimp)
            && (_melee == null || !_melee.IsSwinging);

        /// <summary>Casts it: the hands go up, the cloud gathers. False on cooldown, mid-swing, stunned, limp or dead.</summary>
        public bool TryCast()
        {
            if (!CanCast)
            {
                return false;
            }
            _cooldown = CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            Begin(remote: false);
            Fired?.Invoke(new AbilityEvent(0, transform.position, Vector3.up));
            return true;
        }

        /// <summary>Another player cast it: the same animation and bolt on their copy here, hurting nothing.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != 0)
            {
                return;
            }
            if (IsActive)
            {
                Finish();
            }
            Begin(remote: true);
        }

        private void Begin(bool remote)
        {
            _remote = remote;
            IsActive = true;
            Seconds = 0.0f;
            _struck = false;
            _blastChecked = false;
            _boltHit.Clear();
            _blasted.Clear();
            _casts++;
            _bolt = SelfSmiteBolt.Spawn(transform, Height, StrikeSound, GetInstanceID() * 17 + _casts * 7919, LightMaterial);
            _tipAbove = _bolt.CloudBase - Head.y;
            PlayAnimation();
            if (!remote && _locomotion != null)
            {
                // Rooted where it stands, as Pikachu is: no walking, and in the air no drifting.
                _locomotion.MovementRooted = true;
            }
        }

        private Vector3 Head => transform.position + Vector3.up * (Height * 0.97f);

        private Vector3 Middle => transform.position + Vector3.up * (Height * 0.5f);

        private void PlayAnimation()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
            {
                return;
            }
            int hash = Animator.StringToHash(SelfSmiteMove.StateName);
            if (!_animator.HasState(0, hash))
            {
                return;
            }
            // The state has its own speed (SelfSmiteMove.ClipSpeed); a swing's stretch left on the Animator would race it.
            _animator.speed = 1.0f;
            _animator.CrossFadeInFixedTime(hash, 0.06f, 0, 0.0f);
        }

        private void Update()
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= Time.deltaTime;
            }
            if (!IsActive)
            {
                return;
            }
            Seconds += Time.deltaTime;
            if (!_remote)
            {
                // Knocked out of it (a blow that stuns, a fall into a ragdoll, death): free at once; before the strike the bolt still comes
                // down where they stood, with no burst.
                bool knockedOut = (_vitals != null && (_vitals.IsDead || _vitals.IsStunned)) || (_ragdoll != null && _ragdoll.IsLimp);
                if (knockedOut)
                {
                    Interrupt();
                    return;
                }
                HurtUnderTheBolt();
                if (!_struck && Seconds >= SelfSmiteMove.StrikeSeconds)
                {
                    _struck = true;
                    // Unhittable from the strike to frame 43, as Pikachu is.
                    if (_vitals != null)
                    {
                        _vitals.GrantInvulnerability(SelfSmiteMove.IntangibleEndSeconds - Seconds);
                    }
                }
                // The burst hurts for its three frames; the first look always happens, however long the frame that crossed the strike was.
                if (_struck && (!_blastChecked || SelfSmiteMove.BlastHurts(Seconds)))
                {
                    _blastChecked = true;
                    Blast();
                }
            }
            if (Seconds >= SelfSmiteMove.FreeSeconds)
            {
                Finish();
            }
        }

        /// <summary>Knocked out of the move before the bolt landed: free at once, and the bolt strikes where they were without a burst.</summary>
        public void Interrupt()
        {
            if (!IsActive)
            {
                return;
            }
            if (_bolt != null && !_struck)
            {
                _bolt.Detach();
            }
            Finish();
        }

        private void Finish()
        {
            IsActive = false;
            if (!_remote && _locomotion != null)
            {
                _locomotion.MovementRooted = false;
            }
            _bolt = null;   // it fades away on its own
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                Interrupt();
            }
        }

        /// <summary>The bolt's fall this frame: whoever stands within <see cref="SelfSmiteMove.BoltRadius"/> of the stretch it just came down.</summary>
        private void HurtUnderTheBolt()
        {
            float cloudAbove = _bolt != null ? _bolt.CloudBase - Head.y : _tipAbove;
            float tip = SelfSmiteMove.TipHeightAbove(Seconds, cloudAbove);
            if (Seconds < SelfSmiteMove.BoltSeconds || tip >= _tipAbove)
            {
                _tipAbove = Mathf.Min(_tipAbove, tip);
                return;
            }
            Vector3 head = Head;
            Vector3 from = head + Vector3.up * _tipAbove;
            Vector3 to = head + Vector3.up * tip;
            _tipAbove = tip;
            int count = Physics.OverlapCapsuleNonAlloc(from, to, SelfSmiteMove.BoltRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IDamageable target = TargetOf(Overlaps[i]);
                if (target == null || _boltHit.Contains(target))
                {
                    continue;
                }
                _boltHit.Add(target);
                if (Hurt(target, SelfSmiteMove.BoltDamage, to, CombatTuning.HitstunSeconds) != HitResponse.Ignored)
                {
                    BoltHits++;
                }
            }
        }

        /// <summary>The burst round the caster: hurt, and thrown away and up.</summary>
        private void Blast()
        {
            Vector3 centre = Middle;
            int count = Physics.OverlapSphereNonAlloc(centre, SelfSmiteMove.BlastRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IDamageable target = TargetOf(Overlaps[i]);
                if (target == null || _blasted.Contains(target))
                {
                    continue;
                }
                _blasted.Add(target);
                HitResponse response = Hurt(target, SelfSmiteMove.BlastDamage, centre, CombatTuning.HitstunSeconds);
                if (response == HitResponse.Ignored)
                {
                    continue;
                }
                BlastHits++;
                if (response == HitResponse.Blocked || !target.IsAlive)
                {
                    continue;
                }
                var body = (Component)target;
                Vector3 launch = SelfSmiteMove.Launch(centre, body.transform.position);
                if (target is PlayerVitals player)
                {
                    player.TriggerLaunchKnockdown(launch);
                }
                else if (target is ILaunchable launchable)
                {
                    launchable.Launch(launch);
                }
            }
        }

        /// <summary>Something damageable under a collider, not the caster and not already dead.</summary>
        private IDamageable TargetOf(Collider collider)
        {
            if (collider == null || collider.transform.IsChildOf(transform))
            {
                return null;
            }
            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || ReferenceEquals(target, _vitals))
            {
                return null;
            }
            return target;
        }

        private HitResponse Hurt(IDamageable target, float baseDamage, Vector3 from, float hitstun)
        {
            // A spell: Spell Power makes it hurt more (2% a point), and the caster's Armor Penetration goes with it.
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            HitResponse response = DamageContext.Strike(target, CombatTuning.DamageFor(baseDamage, spellPower), from, hitstun, DamageKind.Magic,
                penetration);
            if (!target.IsAlive && target is IXpReward reward && _vitals != null)
            {
                _vitals.AddXp(reward.XpReward);
            }
            return response;
        }
    }
}
