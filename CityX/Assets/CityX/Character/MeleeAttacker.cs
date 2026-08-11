using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Swings the attack chain and works out what it hits.
    ///
    /// Ported from player.gd's punch handling plus character.gd's combo state,
    /// which were split across two files in the original because one owned
    /// animation and the other owned damage. Here the chain lives in
    /// <see cref="ComboChain"/> and this drives it, so there is one place that
    /// knows where in a combo you are.
    ///
    /// EVERY RULE IS A FUNCTION OF A DELTA. No coroutines, no invoke, no
    /// waiting on animation events. The chain is the part that made the Godot
    /// build feel good, and the failure modes -- a window that never closes, a
    /// finisher that loops, a swing that never releases (task #292 in the
    /// Godot build, still open) -- are all timing bugs that only show up after
    /// a specific sequence. Those are exactly the bugs a test can catch and a
    /// playthrough usually will not.
    /// </summary>
    public sealed class MeleeAttacker : MonoBehaviour
    {
        [Header("Reach (ported from player.gd)")]
        [Tooltip("player.gd PUNCH_RANGE")]
        public float Range = 2.5f;

        [Tooltip("player.gd PUNCH_ORIGIN_HEIGHT, as a fraction of standing height.")]
        public float OriginHeightFraction = 0.68f;

        [Tooltip("Half-angle of the arc in front that a swing can reach, in degrees.")]
        public float HalfArcDegrees = 60.0f;

        [Header("Timing (ported from player.gd)")]
        [Tooltip("player.gd PUNCH_COOLDOWN_SECONDS -- how long a swing occupies the body.")]
        public float SwingSeconds = 0.35f;

        [Tooltip("Fraction into the swing at which the blow actually lands.")]
        public float ContactFraction = 0.45f;

        [Header("Targets")]
        public LayerMask TargetLayers = ~0;

        private float _swingRemaining;
        private float _windowRemaining;
        private int _nextStage;
        private bool _contactDone;
        private ComboStage _currentStage;

        /// <summary>Which stage of the chain the next swing would play.</summary>
        public int NextStageIndex => _nextStage;

        /// <summary>True while a swing is occupying the body.</summary>
        public bool IsSwinging => _swingRemaining > 0.0f;

        /// <summary>The stage currently being swung. Meaningless when not swinging.</summary>
        public ComboStage CurrentStage => _currentStage;

        /// <summary>Strength stat, read from vitals when present.</summary>
        private PlayerVitals _vitals;

        private PlayerLocomotion _locomotion;

        /// <summary>Raised when a swing actually connects with something.</summary>
        public event System.Action<IDamageable, float> Landed;

        /// <summary>Raised when a swing starts, with the stage being played.</summary>
        public event System.Action<ComboStage> SwingStarted;

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        /// <summary>
        /// Asks to swing. Returns false when the body is busy.
        ///
        /// Advancing versus restarting is decided here rather than by the
        /// caller: the window is this component's state, and letting anyone
        /// else decide is how the two ends of a combo drift apart.
        /// </summary>
        public bool TryAttack()
        {
            if (IsSwinging || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }

            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            if (chain.Length == 0)
            {
                return false;
            }

            // Out of the window means starting again from the first stage, not
            // continuing where the chain left off.
            if (_windowRemaining <= 0.0f)
            {
                _nextStage = 0;
            }

            _currentStage = chain[_nextStage];
            _swingRemaining = SwingSeconds;
            _contactDone = false;

            // A finisher closes the chain; anything else opens the window for
            // the following stage. Advancing here rather than on contact means
            // a whiffed punch still moves the combo along, which is what the
            // original did and what makes the chain feel continuous.
            if (_currentStage.IsFinisher)
            {
                _nextStage = 0;
                _windowRemaining = 0.0f;
            }
            else
            {
                _nextStage++;
                _windowRemaining = SwingSeconds + _currentStage.WindowSeconds;
            }

            SwingStarted?.Invoke(_currentStage);
            return true;
        }

        /// <summary>
        /// Advances swing and window timers, and lands the blow at the contact
        /// moment. Call once per frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_windowRemaining > 0.0f)
            {
                _windowRemaining -= deltaTime;
                if (_windowRemaining <= 0.0f)
                {
                    _windowRemaining = 0.0f;
                    _nextStage = 0;
                }
            }

            if (_swingRemaining <= 0.0f)
            {
                return;
            }

            float before = _swingRemaining;
            _swingRemaining -= deltaTime;

            // Contact is a moment inside the swing, not its start or end. Fire
            // it when the timer crosses that point, so a long frame cannot skip
            // it -- a dropped frame swallowing a punch is a miserable bug.
            float contactAt = SwingSeconds * (1.0f - ContactFraction);
            if (!_contactDone && before > contactAt && _swingRemaining <= contactAt)
            {
                _contactDone = true;
                LandBlow();
            }

            if (_swingRemaining <= 0.0f)
            {
                _swingRemaining = 0.0f;
                // Belt and braces: if the contact moment was somehow never
                // crossed, land it as the swing ends rather than losing it.
                if (!_contactDone)
                {
                    _contactDone = true;
                    LandBlow();
                }
            }
        }

        /// <summary>
        /// Everything within reach and in front. Public so a test can check
        /// what a swing WOULD hit without swinging.
        /// </summary>
        public List<IDamageable> TargetsInReach()
        {
            var found = new List<IDamageable>();
            Vector3 origin = SwingOrigin();

            Collider[] hits = Physics.OverlapSphere(origin, Range, TargetLayers,
                QueryTriggerInteraction.Ignore);
            float cosLimit = Mathf.Cos(HalfArcDegrees * Mathf.Deg2Rad);

            foreach (Collider collider in hits)
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                // Do not punch yourself.
                if (ReferenceEquals(target, GetComponent<IDamageable>()))
                {
                    continue;
                }
                if (found.Contains(target))
                {
                    continue;
                }

                Vector3 toTarget = collider.transform.position - origin;
                toTarget.y = 0.0f;
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(toTarget.normalized, transform.forward) < cosLimit)
                {
                    continue;   // behind, or too far round the side
                }
                found.Add(target);
            }

            return found;
        }

        private Vector3 SwingOrigin()
        {
            float height = _locomotion != null ? _locomotion.StandingHeight : 1.8f;
            return transform.position + Vector3.up * (height * OriginHeightFraction);
        }

        private void LandBlow()
        {
            int strength = _vitals != null ? _vitals.Strength : 0;
            float damage = CombatTuning.DamageFor(_currentStage, strength);
            float hitstun = CombatTuning.HitstunFor(_currentStage);

            foreach (IDamageable target in TargetsInReach())
            {
                target.ApplyHit(damage, transform.position, hitstun);
                Landed?.Invoke(target, damage);
            }
        }
    }
}
