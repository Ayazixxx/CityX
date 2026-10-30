using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How cold a body is (docs/FROST.md): what the Frost Staff's spray does to whoever it is held on (owner, 2026-10-29: "it does a low amount
    /// of consecutive damage and slows them down then eventually freezes them"). Every frost blow that lands on this body adds to its chill
    /// (<see cref="DamageContext.Chill"/>, read where the blow is taken: the body's own machine); the chill wears off out of the spray and slows
    /// the body the colder it is (<see cref="FrostRules"/>); full, the body freezes solid for a moment: its animation stops where it is, it
    /// cannot move, an enemy's mind stops (its <see cref="IHostile"/> parts), and it takes an icy blue tinge (<see cref="FrozenLook"/>); then
    /// it thaws, and cannot freeze again straight away. On every body (Player.prefab, the civilians), so other players see a freeze: decided on
    /// the body's owner's machine, told to every copy (<see cref="IReplicatedAbility"/>: frozen, thawed). Players are frozen too, for now
    /// thawing by themselves (breaking out comes later).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Chill : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "chill";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): Froze (A, how long), Thawed.</summary>
        public const int Froze = 0;

        public const int Thawed = 1;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private readonly System.Collections.Generic.List<Behaviour> _stoppedMinds = new System.Collections.Generic.List<Behaviour>();
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private FrozenLook _look;
        private float _frozenLeft;
        private float _immune;
        private float _sinceChilled = float.MaxValue;
        private float _animatorSpeed = 1.0f;
        private bool _shownFrozen;

        /// <summary>0 to 1, how cold the body is.</summary>
        public float Level { get; private set; }

        public bool IsFrozen => _frozenLeft > 0.0f;

        /// <summary>How fast the body moves, as a share of its own pace (0 while frozen).</summary>
        public float SpeedShare => IsFrozen ? 0.0f : FrostRules.SpeedShare(Level);

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>A frost blow landed on this body: <paramref name="amount"/> colder (nothing while frozen or just thawed).</summary>
        public void Add(float amount)
        {
            if (amount <= 0.0f || float.IsNaN(amount) || IsFrozen || _immune > 0.0f)
            {
                return;
            }
            _sinceChilled = 0.0f;
            Level = Mathf.Min(1.0f, Level + amount);
            if (Level >= 1.0f)
            {
                Freeze(FrostRules.FreezeSeconds);
                Fired?.Invoke(new AbilityEvent(Froze, transform.position, Vector3.zero, FrostRules.FreezeSeconds));
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The chill wearing off, a freeze running out; public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            if (IsFrozen)
            {
                _frozenLeft -= deltaTime;
                if (_animator != null && _animator.speed > 0.0f)
                {
                    _animator.speed = 0.0f;   // something set it running again (a reaction played into the freeze): held still all the same
                }
                if (_frozenLeft <= 0.0f)
                {
                    bool own = !_shownFrozen;   // a copy thaws on its own time but tells no one
                    Thaw();
                    if (own)
                    {
                        Fired?.Invoke(new AbilityEvent(Thawed, transform.position, Vector3.zero));
                    }
                }
            }
            else
            {
                _immune -= deltaTime;
                _sinceChilled += deltaTime;
                if (_sinceChilled > FrostRules.TickSeconds * 1.5f)
                {
                    Level = Mathf.Max(0.0f, Level - FrostRules.ThawPerSecond * deltaTime);
                }
            }
            if (_locomotion != null)
            {
                _locomotion.SlowFactor = SpeedShare;
            }
        }

        /// <summary>Another player's body, frozen or thawed on their machine: this copy shows it (its own movement is theirs).</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind == Froze)
            {
                _shownFrozen = true;
                Freeze(Mathf.Clamp(evt.A, 0.1f, 10.0f));
            }
            else if (evt.Kind == Thawed && IsFrozen)
            {
                Thaw();
            }
        }

        private void Freeze(float seconds)
        {
            _frozenLeft = seconds;
            Level = 1.0f;
            if (_animator != null)
            {
                // A blow's instant of impact may have the Animator stopped already: its own pace is never 0.
                _animatorSpeed = _animator.speed > 0.0f ? _animator.speed : 1.0f;
                _animator.speed = 0.0f;
            }
            if (_locomotion != null)
            {
                _locomotion.FrozenSolid = true;
            }
            SetMinds(false);
            if (_look == null && !TryGetComponent(out _look))
            {
                _look = gameObject.AddComponent<FrozenLook>();
            }
            _look.Show(true);
        }

        private void Thaw()
        {
            _frozenLeft = 0.0f;
            Level = 0.0f;
            _immune = FrostRules.FreezeImmunitySeconds;
            _shownFrozen = false;
            if (_locomotion != null)
            {
                _locomotion.FrozenSolid = false;
            }
            if (_animator != null)
            {
                _animator.speed = _animatorSpeed;
            }
            SetMinds(true);
            if (_look != null)
            {
                _look.Show(false);
            }
        }

        /// <summary>
        /// An enemy's mind (whatever makes it an enemy) stopped while frozen, and on the thaw started again: only the ones the freeze stopped
        /// (another player's copy of an enemy has its mind off for good, NetworkedEnemy).
        /// </summary>
        private void SetMinds(bool on)
        {
            if (on)
            {
                foreach (Behaviour stopped in _stoppedMinds)
                {
                    if (stopped != null)
                    {
                        stopped.enabled = true;
                    }
                }
                _stoppedMinds.Clear();
                return;
            }
            foreach (IHostile hostile in GetComponents<IHostile>())
            {
                if (hostile is Behaviour behaviour && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    _stoppedMinds.Add(behaviour);
                }
            }
        }
    }
}
