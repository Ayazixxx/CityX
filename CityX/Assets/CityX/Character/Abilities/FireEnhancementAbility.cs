using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FIRE ENHANCEMENT, the first "enhance weapon" spell (owner, 2026-10-29: "it just makes your next swing deal fire damage, same with
    /// your next bullet, next arrow, etc. and for now we'll just make it have no effect on staffs", "and sets your sword ablaze too";
    /// docs/FIRE_ENHANCEMENT.md). Cast, the body's next attack carries fire: the next swing (every blow it lands, a held heavy too), the next
    /// bullet, the next arrow or bolt. Every blow it lands adds <see cref="FireEnhancementRules.FireDamage"/> and sets its target alight
    /// (<see cref="FireScene.SetAlight"/>). A staff neither takes it nor shows it. While it waits, and through the swing that takes it, the
    /// weapon burns (<see cref="WeaponAblaze"/>): a sword along its blade, anything else round the hand.
    ///
    /// Decided on the caster's machine. Other players see it (<see cref="IReplicatedAbility"/>): Lit and Out (the flames on their copy's
    /// weapon), and every body it Burned (alight on their screens too, for show: the burning is dealt here and reaches that body as every
    /// blow does).
    /// </summary>
    public sealed class FireEnhancementAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "fire-enhancement";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>): the weapon Lit, Out, and a body Burned (Origin its chest).</summary>
        public const int Lit = 0;

        public const int Out = 1;
        public const int Burned = 2;

        /// <summary>A copy sets alight the body nearest the chest it was sent, within this, metres.</summary>
        public const float RemoteBodyReach = 2.5f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private PlayerVitals _vitals;
        private MeleeAttacker _melee;
        private SoulsSwordCombat _sword;
        private WeaponAblaze _ablaze;
        private float _armedLeft;
        private float _cooldown;
        private bool _burningSwing;
        private float _swingSeconds;
        private bool _shownOnly;
        private float _igniteDue;

        /// <summary>The fire waits for the body's next attack (on a copy: is shown waiting).</summary>
        public bool IsArmed => _armedLeft > 0.0f;

        /// <summary>The swing that took the fire is still going: every blow it lands burns.</summary>
        public bool IsSwingBurning => _burningSwing;

        /// <summary>The weapon shows the fire: while it waits, and through the swing that takes it.</summary>
        public bool IsAblaze => IsArmed || _burningSwing;

        public float CooldownLeft => Mathf.Max(0.0f, _cooldown);

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            if (_melee == null && TryGetComponent(out _melee))
            {
                _melee.SwingStarted += OnSwingStarted;
                _melee.Landed += OnLanded;
            }
            if (_sword == null && TryGetComponent(out _sword))
            {
                _sword.AttackStarted += OnSwordAttackStarted;
            }
        }

        private void OnDestroy()
        {
            if (_melee != null)
            {
                _melee.SwingStarted -= OnSwingStarted;
                _melee.Landed -= OnLanded;
            }
            if (_sword != null)
            {
                _sword.AttackStarted -= OnSwordAttackStarted;
            }
            if (_ablaze != null)
            {
                Destroy(_ablaze.gameObject);
            }
        }

        /// <summary>Lights the weapon. False while the fire still waits, cooling down, short of mana, or dead.</summary>
        public bool TryCast()
        {
            if (IsArmed || _cooldown > 0.0f || _shownOnly || (_vitals != null && (_vitals.IsDead || !_vitals.TrySpendMana(FireEnhancementRules.ManaCost))))
            {
                return false;
            }
            _cooldown = FireEnhancementRules.CooldownSeconds;
            _armedLeft = FireEnhancementRules.ArmedSeconds;
            Fired?.Invoke(new AbilityEvent(Lit, transform.position, Vector3.zero, _armedLeft));
            return true;
        }

        /// <summary>
        /// The gun or bow of <paramref name="wielder"/> fires: true when this shot carries the fire, which it takes. A staff never asks.
        /// </summary>
        public static bool TakeShot(GameObject wielder)
        {
            var ability = wielder != null ? wielder.GetComponent<FireEnhancementAbility>() : null;
            if (ability == null || !ability.Take())
            {
                return false;
            }
            ability.GoOut();
            return true;
        }

        /// <summary>
        /// A blow of the enhanced attack landed on <paramref name="target"/> at <paramref name="point"/>: the fire's damage (a spell's, no
        /// stagger, no blood, credited to <paramref name="striker"/>), the target alight, and other players told.
        /// </summary>
        public static void Burn(IDamageable target, Vector3 point, GameObject striker)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }
            PlayerVitals vitals = striker != null ? striker.GetComponent<PlayerVitals>() : null;
            float damage = CombatTuning.DamageFor(FireEnhancementRules.FireDamage, PlayerVitals.SpellPowerOf(vitals));
            using (DamageContext.WithoutBlood())
            {
                DamageContext.Strike(target, damage, point, 0.0f, DamageKind.Magic, 0.0f, point, striker);
            }
            FireScene.SetAlight(target, striker, FireEnhancementRules.BurnSeconds);
            var ability = striker != null ? striker.GetComponent<FireEnhancementAbility>() : null;
            ability?.Fired?.Invoke(new AbilityEvent(Burned, point, Vector3.zero));
        }

        /// <summary>Another player's fire, on this copy of them: their weapon's flames, and the bodies their blows set alight (for show).</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Lit:
                    _shownOnly = true;
                    _armedLeft = Mathf.Clamp(evt.A, 0.1f, FireEnhancementRules.ArmedSeconds);
                    break;
                case Out:
                    _armedLeft = 0.0f;
                    break;
                case Burned:
                    IDamageable body = NearestBody(evt.Origin);
                    if (body != null)
                    {
                        FireScene.SetAlight(body, null, FireEnhancementRules.BurnSeconds, true);
                    }
                    break;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Counts the fire, its cooldown and the burning swing down, and keeps the flames on the weapon; public for tests.</summary>
        public void Tick(float deltaTime)
        {
            _cooldown -= deltaTime;
            if (IsArmed && !_shownOnly)
            {
                Bind();   // the sword's combat is added to the body when a sword is first taken in hand: hear its swings too
            }
            if (!_shownOnly && (IsArmed || _burningSwing) && _vitals != null && _vitals.IsDead)
            {
                _armedLeft = 0.0f;
                _burningSwing = false;
                GoOut();
            }
            if (IsArmed)
            {
                _armedLeft -= deltaTime;
                if (_armedLeft <= 0.0f && !_shownOnly)
                {
                    GoOut();
                }
            }
            if (_burningSwing)
            {
                _swingSeconds += deltaTime;
                bool swinging = (_melee != null && _melee.IsSwinging) || (_sword != null && _sword.IsCommitted);
                if ((!swinging && _swingSeconds > FireEnhancementRules.SwingGraceSeconds) || _swingSeconds > FireEnhancementRules.MaxSwingSeconds)
                {
                    _burningSwing = false;
                    GoOut();
                }
            }
            if (IsAblaze && _ablaze == null)
            {
                _ablaze = WeaponAblaze.Make(transform);
            }
            if (_ablaze != null)
            {
                _ablaze.Show(IsAblaze);
                LightGasolineNearTheFlames(deltaTime);
            }
        }

        /// <summary>
        /// A weapon ablaze lights gasoline it comes near (owner, 2026-11-08: "make the gasoline ignite from gunshot and fire spells"), at both ends
        /// of its flames. Decided on the caster's machine; other players are told by the fire itself (FireSync).
        /// </summary>
        private void LightGasolineNearTheFlames(float deltaTime)
        {
            _igniteDue -= deltaTime;
            if (_shownOnly || !_ablaze.Burning || _igniteDue > 0.0f)
            {
                return;
            }
            _igniteDue = FireRules.IgniteTickSeconds;
            FireScene.Ignite(_ablaze.From, FireRules.SparkIgniteRadius, true, gameObject);
            FireScene.Ignite(_ablaze.To, FireRules.SparkIgniteRadius, true, gameObject);
        }

        private void OnSwingStarted(ComboStage stage)
        {
            StartSwing();
        }

        private void OnSwordAttackStarted(SoulsAttack attack)
        {
            StartSwing();
        }

        /// <summary>A swing starts: it takes the waiting fire; one that does not ends the burning one before it.</summary>
        private void StartSwing()
        {
            if (_shownOnly)
            {
                return;
            }
            _burningSwing = Take();
            _swingSeconds = 0.0f;
        }

        private void OnLanded(IDamageable target, float damage)
        {
            if (_burningSwing && target is Component body)
            {
                Burn(target, ChestOf(body), gameObject);
            }
        }

        /// <summary>The waiting fire goes to this attack, unless a staff is in hand (it has no effect on staffs).</summary>
        private bool Take()
        {
            if (!IsArmed || _shownOnly || GetComponentInChildren<IEquippableStaff>() != null)
            {
                return false;
            }
            _armedLeft = 0.0f;
            return true;
        }

        /// <summary>The flames on the weapon go out here, and on every other player's copy once nothing here burns.</summary>
        private void GoOut()
        {
            if (!IsAblaze)
            {
                Fired?.Invoke(new AbilityEvent(Out, transform.position, Vector3.zero));
            }
        }

        private static Vector3 ChestOf(Component body)
        {
            return body.transform.position + Vector3.up * 1.6f * body.transform.lossyScale.y;
        }

        private IDamageable NearestBody(Vector3 chest)
        {
            IDamageable best = null;
            float nearest = RemoteBodyReach;
            foreach (Collider collider in Physics.OverlapSphere(chest, RemoteBodyReach, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !(target is Component body) || body.transform.IsChildOf(transform))
                {
                    continue;
                }
                float distance = Vector3.Distance(ChestOf(body), chest);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = target;
                }
            }
            return best;
        }
    }
}
