using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ARCANE ORB (owner, 2026-10-27; docs/ARCANE_ORB.md): a ball of magenta aura that leaves cursed orbs circling whoever it hits, to strike
    /// four seconds later, Sephiroth's Shadow Flare (ArcaneOrbRules has its timing). Cast two ways, both from here on the body:
    /// <list type="bullet">
    /// <item>from the bar (the Arcanist tree's spell): one cast, its rank's orbs (one a rank, up to three) and a tenth more damage a rank, for
    /// <see cref="ArcaneOrbRules.SpellManaCost"/> mana and a <see cref="ArcaneOrbRules.SpellCooldownSeconds"/> cooldown;</item>
    /// <item>from the Arcane Staff's click (<see cref="IStaffCaster"/>): a click one orb, a hold charging to two and three on Shadow Flare's
    /// timing, mana paid by the orb, no cooldown but the staff's pace. The charge stops growing at what the mana can pay for.</item>
    /// </list>
    /// Other players see all of it (<see cref="IReplicatedAbility"/>): the ball, the orbs on whoever it hit (by where they stood: each machine
    /// finds its own copy of them there), the charge glowing at the staff's head and the staff thrust out. Only this machine's orbs hurt.
    /// </summary>
    public sealed class ArcaneOrbCaster : MonoBehaviour, IArcaneOrbCaster, IStaffCaster, IReplicatedAbility
    {
        public const string Id = "arcane-orb";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>).</summary>
        public const int BallFired = 0;     // Origin, Direction; A speed, B 1 from a staff, N the orbs it carries
        public const int OrbsPlaced = 1;    // Origin where the target stood, Direction from there to where the ball landed; A seconds left, N orbs in all
        public const int ChargeStarted = 2;
        public const int ChargeStopped = 3;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("How long the raised hand holds after a cast from the bar, seconds.")]
        public float CastPoseSeconds = 0.4f;

        [Tooltip("The light the glows are made from (SelfSmiteAbility's: soft particles, shipped in a build). Set by PlayerAbilityWiring.")]
        public Material LightMaterial;

        /// <summary>How far in front of the chest a cast from the bar appears, so it does not start inside the caster.</summary>
        public const float SpawnForwardOffset = 0.6f;

        /// <summary>How far round where a target stood another machine looks for its own copy of them, metres.</summary>
        public const float FindTargetRadius = 2.5f;

        private readonly StaffCharge _staff = new StaffCharge(ArcaneOrbRules.StaffShotSeconds);
        private float _cooldownRemaining;
        private float _castPoseRemaining;
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private AudioSource _audio;
        private Transform _tip;
        private Vector3 _eye;
        private Vector3 _look = Vector3.forward;
        private bool _remoteCharging;
        private float _remoteChargeSeconds;
        private float _remoteFollowThrough;
        private int _chimed;

        public bool IsCasting => _castPoseRemaining > 0.0f;

        public float CastFraction => CastPoseSeconds > 0.0f ? Mathf.Clamp01(_castPoseRemaining / CastPoseSeconds) : 0.0f;

        /// <summary>Off cooldown and alive (mana is asked when it is cast).</summary>
        public bool CanCast => _cooldownRemaining <= 0.0f && (_vitals == null || !_vitals.IsDead);

        /// <summary>The bar spell's rank: this body's skills', 1 for a body with none.</summary>
        public int Rank
        {
            get
            {
                PlayerSkills skills = GetComponent<PlayerSkills>();
                return skills != null ? Mathf.Max(1, skills.RankOf(SkillCatalog.ArcaneOrb)) : 1;
            }
        }

        /// <summary>The staff's charge going on now, if any.</summary>
        public StaffCharge Staff => _staff;

        /// <summary>The orbs a staff shot let go now would carry: the charge's, as many as the mana pays for.</summary>
        public int ChargedOrbs => Mathf.Max(1, ArcaneOrbRules.StaffOrbsAffordable(Mana, ArcaneOrbRules.OrbsForCharge(_staff.Seconds)));

        private float Mana => _vitals != null ? _vitals.Mana : float.MaxValue;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs (the casters' idiom).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
            ArcaneVisuals.UseTemplate(LightMaterial);
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0.0f)
            {
                _cooldownRemaining -= deltaTime;
            }
            if (_castPoseRemaining > 0.0f)
            {
                _castPoseRemaining -= deltaTime;
            }
        }

        public bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            if (_vitals != null && !_vitals.TrySpendMana(ArcaneOrbRules.SpellManaCost))
            {
                return false;
            }
            _cooldownRemaining = ArcaneOrbRules.SpellCooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            _castPoseRemaining = CastPoseSeconds;
            int rank = Rank;
            Vector3 forward = direction.normalized;
            Launch(origin + forward * SpawnForwardOffset, forward, ArcaneOrbRules.OrbsForRank(rank), ArcaneOrbRules.DamageFactor(rank),
                ignoreColliders, false);
            return true;
        }

        // --- the staff ---

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            _tip = tip;
            _eye = eye;
            _look = look.sqrMagnitude > 1e-6f ? look.normalized : _look;
            bool alive = _vitals == null || !_vitals.IsDead;
            if (!alive && _staff.Cancel())
            {
                Fired?.Invoke(new AbilityEvent(ChargeStopped, transform.position, Vector3.zero));
            }
            bool canStart = alive && ArcaneOrbRules.StaffOrbsAffordable(Mana, 1) > 0;
            switch (_staff.Tick(held, canStart, deltaTime))
            {
                case StaffCharge.Step.Started:
                    _chimed = 1;
                    Fired?.Invoke(new AbilityEvent(ChargeStarted, transform.position, Vector3.zero));
                    break;
                case StaffCharge.Step.Fire:
                    FireStaff();
                    break;
            }
            if (_staff.Charging && ChargedOrbs > _chimed)
            {
                _chimed = ChargedOrbs;
                Chime(_chimed);
            }
        }

        public void CancelStaffCharge()
        {
            if (_staff.Cancel())
            {
                Fired?.Invoke(new AbilityEvent(ChargeStopped, transform.position, Vector3.zero));
            }
        }

        private void FireStaff()
        {
            int orbs = ArcaneOrbRules.StaffOrbsAffordable(Mana, ArcaneOrbRules.OrbsForCharge(_staff.ReleasedSeconds));
            if (orbs <= 0 || (_vitals != null && !_vitals.TrySpendMana(ArcaneOrbRules.StaffManaFor(orbs))))
            {
                Fired?.Invoke(new AbilityEvent(ChargeStopped, transform.position, Vector3.zero));
                return;
            }
            Vector3 from = _tip != null ? _tip.position : transform.position + Vector3.up * 1.8f;
            Launch(from, PlayerInputReader.CrosshairDirection(from, _eye, _look, transform), orbs, 1.0f, GetComponentsInChildren<Collider>(), true);
        }

        // --- shooting ---

        private void Launch(Vector3 from, Vector3 forward, int orbs, float damageFactor, Collider[] ignoreColliders, bool fromStaff)
        {
            // A spell: Spell Power makes it hurt more (2% a point), and the caster's Armor Penetration goes with it.
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            float ballDamage = CombatTuning.DamageFor(ArcaneOrbRules.BallDamagePerOrb * orbs * damageFactor, spellPower);
            float orbDamage = CombatTuning.DamageFor(ArcaneOrbRules.OrbDamage * damageFactor, spellPower);
            ArcaneOrbProjectile.Spawn(from, forward * ArcaneOrbRules.BallSpeed, orbs, ballDamage, orbDamage, penetration, gameObject,
                ignoreColliders, placed: Announce);
            PlayCast(from);
            Fired?.Invoke(new AbilityEvent(BallFired, from, forward, ArcaneOrbRules.BallSpeed, fromStaff ? 1.0f : 0.0f, orbs));
        }

        /// <summary>Tells other players of the orbs a ball left: where the target stood, and the curse as this machine has it now.</summary>
        private void Announce(CursedOrbs curse)
        {
            if (curse == null || curse.Target == null)
            {
                return;
            }
            Vector3 stood = curse.Target.position;
            Vector3 landed = curse.transform.position - stood;
            Fired?.Invoke(new AbilityEvent(OrbsPlaced, stood, Vector3.ClampMagnitude(landed, 1.4f), curse.Curse.SecondsLeft, 0.0f, curse.Curse.Orbs));
        }

        // --- what everyone sees ---

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_remoteFollowThrough > 0.0f)
            {
                _remoteFollowThrough -= deltaTime;
            }
            if (_remoteCharging)
            {
                _remoteChargeSeconds += deltaTime;
                int orbs = ArcaneOrbRules.OrbsForCharge(_remoteChargeSeconds);
                if (orbs > _chimed)
                {
                    _chimed = orbs;
                    Chime(orbs);
                }
            }
            bool charging = _staff.Charging || _remoteCharging;
            if ((_staff.Busy || _remoteCharging || _remoteFollowThrough > 0.0f) && _locomotion != null && _locomotion.Staff != null)
            {
                _locomotion.HoldStaffOut();
            }
            if (charging)
            {
                Transform tip = StaffItem.TipOf(_locomotion);
                StaffChargeGlow glow = StaffChargeGlow.On(tip, ArcaneVisuals.Glow, ArcaneVisuals.HotGlow, ArcaneVisuals.Magenta, ArcaneOrbRules.MaxOrbsPerShot);
                if (glow != null)
                {
                    float seconds = _staff.Charging ? _staff.Seconds : _remoteChargeSeconds;
                    int orbs = _staff.Charging ? ChargedOrbs : ArcaneOrbRules.OrbsForCharge(seconds);
                    glow.Show(Mathf.Clamp01(seconds / ArcaneOrbRules.ThreeOrbChargeSeconds), orbs);
                }
            }
        }

        /// <summary>
        /// Another player's arcane orb, on this copy of their body: their ball flying (and bursting where it lands, hurting nothing), their orbs on
        /// this machine's copy of whoever they hit, their staff charging and thrust out.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case BallFired:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    _remoteCharging = false;
                    float speed = evt.A > 0.0f ? Mathf.Min(evt.A, 100.0f) : ArcaneOrbRules.BallSpeed;
                    ArcaneOrbProjectile.Spawn(evt.Origin, evt.Direction.normalized * speed, Mathf.Clamp(evt.N, 1, ArcaneOrbRules.MaxOrbsPerShot), 0.0f, 0.0f,
                        0.0f, gameObject, GetComponentsInChildren<Collider>(), cosmetic: true);
                    if (evt.B > 0.5f)
                    {
                        _remoteFollowThrough = StaffCharge.FollowThroughSeconds;
                    }
                    else
                    {
                        RemoteAbilityPose.Show(gameObject, 0.0f, CastPoseSeconds);
                    }
                    PlayCast(evt.Origin);
                    break;
                case OrbsPlaced:
                    IDamageable target = NearestTarget(evt.Origin);
                    if (target != null)
                    {
                        CursedOrbs.Show(target, gameObject, evt.N, evt.A, evt.Origin + Vector3.ClampMagnitude(evt.Direction, 1.4f));
                    }
                    break;
                case ChargeStarted:
                    _remoteCharging = true;
                    _remoteChargeSeconds = 0.0f;
                    _chimed = 1;
                    break;
                case ChargeStopped:
                    _remoteCharging = false;
                    break;
            }
        }

        /// <summary>This machine's copy of whoever stood at <paramref name="stood"/> on the caster's: the nearest thing that takes hits there, not this body.</summary>
        private IDamageable NearestTarget(Vector3 stood)
        {
            IDamageable best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider collider in Physics.OverlapSphere(stood, FindTargetRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                IDamageable candidate = collider.GetComponentInParent<IDamageable>();
                if (!(candidate is Component component) || component == null || component.transform.IsChildOf(transform))
                {
                    continue;
                }
                float distance = (component.transform.position - stood).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private void PlayCast(Vector3 at)
        {
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(ArcaneOrbSounds.Cast, at, 0.8f);
            }
        }

        /// <summary>The charge reaching <paramref name="orbs"/>: a chime, higher for each.</summary>
        private void Chime(int orbs)
        {
            if (!Application.isPlaying)
            {
                return;
            }
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1.0f;
            }
            _audio.pitch = 1.0f + 0.25f * (orbs - 1);
            _audio.PlayOneShot(ArcaneOrbSounds.Charge, 0.7f);
        }
    }
}
