using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ARCANE BARRIER (owner, 2026-10-27; docs/ARCANE_BARRIER.md): Bartolomeo's Barrier from One Piece: Burning Blood, a magenta wall of aura
    /// before the caster that stops what comes at its face until its health runs out, then fades away. This first version is the one the owner
    /// described to start with: the key raises it (turning the body to where the camera looks) and, for as long as the key is held, the caster
    /// stands still behind it doing nothing else (PlayerInputReader holds the body). Let go, it fades; broken, it fades slower and the spell
    /// waits longer. Raising it costs <see cref="ArcaneBarrierRules.RaiseManaCost"/> mana and holding it
    /// <see cref="ArcaneBarrierRules.UpkeepManaPerSecond"/> a second; it drops when the mana runs out. Each rank adds a tenth to its health.
    ///
    /// The wall (<see cref="ArcaneBarrierWall"/>) is a child of the body with a collider, on this machine and on every copy, so a shot fired at it
    /// on any machine stops on it and goes to the body; the body's PlayerVitals asks this (<see cref="IBarrier"/>) first on the machine that
    /// decides its hits, here, which takes it off the wall's health. Other players are told it rose, each blow it took and how it went
    /// (<see cref="IReplicatedAbility"/>).
    /// </summary>
    public sealed class ArcaneBarrierCaster : MonoBehaviour, IArcaneBarrierCaster, IBarrier, IReplicatedAbility
    {
        public const string Id = "arcane-barrier";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>).</summary>
        public const int Raised = 0;    // Origin the body, Direction its facing
        public const int Struck = 1;    // Origin where on the wall; A its health left, as a share
        public const int Lowered = 2;
        public const int Broken = 3;

        /// <summary>How long the wall stays up without being told its key is held (the body stunned, knocked down, dead), seconds.</summary>
        public const float HoldGraceSeconds = 0.15f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The light the glows are made from (SelfSmiteAbility's: soft particles, shipped in a build). Set by PlayerAbilityWiring.")]
        public Material LightMaterial;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private ArcaneBarrierWall _wall;
        private bool _raisedHere;
        private float _health;
        private float _maxHealth;
        private float _holdLeft;
        private float _cooldownRemaining;

        /// <summary>This body's wall is standing (not fading), raised here or shown for another player.</summary>
        public bool IsUp => _wall != null && !_wall.Fading;

        /// <summary>The wall standing now, or null.</summary>
        public ArcaneBarrierWall Wall => IsUp ? _wall : null;

        /// <summary>Its health left, and at full.</summary>
        public float Health => IsUp ? _health : 0.0f;

        public float MaxHealth => _maxHealth;

        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>The bar spell's rank: this body's skills', 1 for a body with none.</summary>
        public int Rank
        {
            get
            {
                PlayerSkills skills = GetComponent<PlayerSkills>();
                return skills != null ? Mathf.Max(1, skills.RankOf(SkillCatalog.ArcaneBarrier)) : 1;
            }
        }

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
            if (_cooldownRemaining > 0.0f && !IsUp)
            {
                _cooldownRemaining -= deltaTime;
            }
        }

        public bool TryRaise(Vector3 facing)
        {
            facing.y = 0.0f;
            if (IsUp || _cooldownRemaining > 0.0f || facing.sqrMagnitude < 1e-6f || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }
            if (_vitals != null && !_vitals.TrySpendMana(ArcaneBarrierRules.RaiseManaCost))
            {
                return false;
            }
            transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            _maxHealth = ArcaneBarrierRules.HealthFor(Rank);
            _health = _maxHealth;
            _holdLeft = HoldGraceSeconds;
            _raisedHere = true;
            Stand();
            if (_locomotion != null)
            {
                _locomotion.MovementRooted = true;
            }
            Fired?.Invoke(new AbilityEvent(Raised, transform.position, transform.forward));
            return true;
        }

        public void Hold(bool held)
        {
            if (!IsUp || !_raisedHere)
            {
                return;
            }
            if (!held)
            {
                Let(broken: false);
                return;
            }
            _holdLeft = HoldGraceSeconds;
        }

        // --- the wall takes what comes at it (IBarrier: asked by PlayerVitals; on a copy only whether it covers, the blow going to the owner) ---

        public bool Covers(Vector3 fromPosition)
        {
            if (!IsUp)
            {
                return false;
            }
            return ArcaneBarrierRules.Covers(_wall.transform.position, _wall.Facing, _wall.transform.up, _wall.HalfWidth, _wall.HalfHeight,
                fromPosition, Chest);
        }

        public void Absorb(float amount, Vector3 fromPosition)
        {
            if (!IsUp)
            {
                return;
            }
            _health = Mathf.Max(0.0f, _health - Mathf.Max(0.0f, amount));
            float share = _maxHealth > 0.0f ? _health / _maxHealth : 0.0f;
            Vector3 point = ArcaneBarrierRules.StruckPoint(_wall.transform.position, _wall.Facing, _wall.transform.up, _wall.HalfWidth, _wall.HalfHeight,
                fromPosition, Chest);
            _wall.Struck(point, share);
            Fired?.Invoke(new AbilityEvent(Struck, point, Vector3.zero, share));
            if (_health <= 0.0f)
            {
                Let(broken: true);
            }
        }

        private Vector3 Chest => transform.position + Vector3.up * ((_locomotion != null ? _locomotion.WorldHeight : BodyRig.StandingHeight) * ArcaneBarrierRules.ChestShare);

        // --- up, and down ---

        private void Update()
        {
            if (!IsUp)
            {
                return;
            }
            if (_locomotion != null)
            {
                _locomotion.HoldBarrierUp();
            }
            if (!_raisedHere)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            _holdLeft -= deltaTime;
            bool dead = _vitals != null && _vitals.IsDead;
            if (dead || _holdLeft <= 0.0f || (_vitals != null && !_vitals.TrySpendMana(ArcaneBarrierRules.UpkeepManaPerSecond * deltaTime)))
            {
                Let(broken: false);
            }
        }

        /// <summary>The wall goes: let go (or out of mana) it fades quickly; <paramref name="broken"/>, it fades slower and the spell waits longer.</summary>
        private void Let(bool broken)
        {
            if (!IsUp)
            {
                return;
            }
            Fade(broken);
            if (_raisedHere)
            {
                _raisedHere = false;
                _cooldownRemaining = (broken ? ArcaneBarrierRules.BrokenCooldownSeconds : ArcaneBarrierRules.CooldownSeconds) * PlayerVitals.CooldownFactorOf(_vitals);
                if (_locomotion != null)
                {
                    _locomotion.MovementRooted = false;
                }
                Fired?.Invoke(new AbilityEvent(broken ? Broken : Lowered, transform.position, Vector3.zero));
            }
        }

        /// <summary>The wall rises before the body, and the body's hits ask it first (on a copy too: a blade bounces off it on the attacker's machine).</summary>
        private void Stand()
        {
            if (_wall != null)
            {
                _wall.FadeAway(ArcaneBarrierRules.LowerFadeSeconds, false);
            }
            _wall = ArcaneBarrierWall.Raise(transform);
            if (_vitals != null)
            {
                _vitals.Barrier = this;
            }
        }

        private void Fade(bool broken)
        {
            if (_wall != null)
            {
                _wall.FadeAway(broken ? ArcaneBarrierRules.BreakFadeSeconds : ArcaneBarrierRules.LowerFadeSeconds, broken);
            }
            if (_vitals != null && ReferenceEquals(_vitals.Barrier, this))
            {
                _vitals.Barrier = null;
            }
        }

        private void OnDisable()
        {
            Let(broken: false);
        }

        /// <summary>Another player's barrier, on this copy of their body: it rises (its collider here too, so this machine's shots stop on it), shows each blow, and fades.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Raised:
                    Stand();
                    break;
                case Struck:
                    if (_wall != null)
                    {
                        _wall.Struck(evt.Origin, evt.A);
                    }
                    break;
                case Lowered:
                    Fade(false);
                    break;
                case Broken:
                    Fade(true);
                    break;
            }
        }
    }
}
