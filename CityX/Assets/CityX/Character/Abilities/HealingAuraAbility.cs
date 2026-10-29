using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE HEALING AURA, the Cleric's first spell (owner, 2026-10-29: "add a healing spell that makes a circle on the floor around you that
    /// moves with you and heals yourself and others that go in the circle slowly", "make the healing aura green"; docs/HEALING.md). Cast, a
    /// green circle is laid on the ground round the caster (<see cref="HealingAuraCircle"/>) and goes where they go for
    /// <see cref="HealingAuraRules.DurationSeconds"/>; every tick it heals the caster and every player standing in it a little
    /// (<see cref="HealingAuraRules"/>). Then it waits out its cooldown.
    ///
    /// Decided on the caster's machine: whom it heals is healed on their own machine (PlayerVitals.Heal sends a heal on another player's copy to
    /// its owner). Other players see the circle on their copy of the caster (<see cref="IReplicatedAbility"/>: started, with how long it has
    /// left, and ended).
    /// </summary>
    public sealed class HealingAuraAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "healing-aura";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Started (A, seconds it lasts), Ended.</summary>
        public const int Started = 0;

        public const int Ended = 1;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private PlayerVitals _vitals;
        private HealingAuraCircle _circle;
        private float _remaining;
        private float _cooldown;
        private float _sinceTick;
        private bool _shownOnly;

        /// <summary>The aura is on (on a copy: shown).</summary>
        public bool IsActive => _remaining > 0.0f;

        public float Remaining => Mathf.Max(0.0f, _remaining);

        /// <summary>Seconds until it can be cast again.</summary>
        public float CooldownLeft => Mathf.Max(0.0f, _cooldown);

        /// <summary>The circle on the ground (null until the first cast).</summary>
        public HealingAuraCircle Circle => _circle;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        private void OnDestroy()
        {
            if (_circle != null)
            {
                Destroy(_circle.gameObject);
                _circle = null;
            }
        }

        /// <summary>Casts the aura. False while it is on, cooling down, or the caster is dead.</summary>
        public bool TryCast()
        {
            if (IsActive || _cooldown > 0.0f || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }
            Begin(HealingAuraRules.DurationSeconds, false);
            _cooldown = HealingAuraRules.CooldownSeconds;
            Fired?.Invoke(new AbilityEvent(Started, transform.position, Vector3.zero, HealingAuraRules.DurationSeconds));
            return true;
        }

        /// <summary>Another player's aura, on this copy of them: the circle for as long as theirs lasts. It heals nobody here.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind == Ended)
            {
                End(false);
                return;
            }
            if (evt.Kind == Started)
            {
                Begin(Mathf.Clamp(evt.A, 0.1f, HealingAuraRules.DurationSeconds), true);
            }
        }

        private void Begin(float seconds, bool shownOnly)
        {
            _remaining = seconds;
            _shownOnly = shownOnly;
            _sinceTick = HealingAuraRules.TickSeconds;   // the first heal lands at once
            if (_circle == null)
            {
                _circle = HealingAuraCircle.Make(transform);
            }
            _circle.Show(true);
        }

        private void End(bool tell)
        {
            if (!IsActive)
            {
                return;
            }
            _remaining = 0.0f;
            if (_circle != null)
            {
                _circle.Show(false);
            }
            if (tell)
            {
                Fired?.Invoke(new AbilityEvent(Ended, transform.position, Vector3.zero));
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Counts the aura and its cooldown down and heals what is in it; public so a test can step it without frames.</summary>
        public void Tick(float deltaTime)
        {
            _cooldown -= deltaTime;
            if (!IsActive)
            {
                return;
            }
            if (!_shownOnly && _vitals != null && _vitals.IsDead)
            {
                End(true);
                return;
            }
            _remaining -= deltaTime;
            if (!_shownOnly)
            {
                _sinceTick += deltaTime;
                while (_sinceTick >= HealingAuraRules.TickSeconds - 1e-4f)
                {
                    _sinceTick -= HealingAuraRules.TickSeconds;
                    HealInside();
                }
            }
            if (_remaining <= 0.0f)
            {
                _remaining = 0.01f;   // so End sees it on
                End(!_shownOnly);
            }
        }

        /// <summary>One tick: the caster and every living player standing in the circle healed a little.</summary>
        private void HealInside()
        {
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float heal = CombatTuning.DamageFor(HealingAuraRules.HealPerTick, spellPower);
            float scale = transform.lossyScale.y;
            if (_vitals != null)
            {
                _vitals.Heal(heal);
            }
            foreach (PlayerInputReader reader in Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
            {
                if (reader.gameObject == gameObject || !PlayerBodies.IsPlayer(reader.gameObject) || !reader.TryGetComponent(out PlayerVitals other)
                    || other.IsDead || !HealingAuraRules.Inside(transform.position, other.transform.position, scale))
                {
                    continue;
                }
                other.Heal(heal);
            }
        }
    }
}
