using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE MAGIC WAND'S AND THE MAGIC STAFF'S SHOT (owner, 2026-10-30: "clicking with it will do some basic projectile attack like it just shoots a
    /// little speck of dust at a low firerate like 1 per second"; the staff's "does the same spec of dust attack but maybe it does slightly more
    /// damage at a slightly lower firerate"; docs/MAGIC_WAND.md). On the body, as every staff's spell is (IStaffCaster): the wand shoots through
    /// <see cref="Wand"/>, the staff through <see cref="Staff"/>, each at its own pace and damage (MagicSpeckRules). A click shoots, or, a moment
    /// before the next is ready, shoots the moment it is; held, it keeps shooting at that pace. From the wand the speck leaves its tip, the arm
    /// snapping out to point it at what the crosshair is on; from the staff, its head, thrust out. Free: a basic attack pays no mana (Hogwarts
    /// Legacy's basic cast is free too), so it is what is left when the mana runs out.
    /// </summary>
    public sealed class MagicSpeckCaster : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "magic-speck";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy as a speck leaves (kind: the wand's or the staff's; A its speed). See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        /// <summary>How far ahead along another player's shot their copy's wand points, metres.</summary>
        private const float RemoteAimMetres = 20.0f;

        [Tooltip("The light the specks' glows are made from (SelfSmiteAbility's: soft particles, shipped in a build). Set by PlayerAbilityWiring.")]
        public Material LightMaterial;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private Shooter _wand;
        private Shooter _staff;
        private float _ready;
        private float _buffered;
        private bool _wasHeld;
        private float _out;
        private Vector3 _aimPoint;
        private int _shots;

        /// <summary>The Magic Wand's click.</summary>
        public IStaffCaster Wand => _wand ??= new Shooter(this, MagicSpeckRules.Wand);

        /// <summary>The Magic Staff's click.</summary>
        public IStaffCaster Staff => _staff ??= new Shooter(this, MagicSpeckRules.Staff);

        /// <summary>Specks this body has shot.</summary>
        public int Shots => _shots;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, callable by hand where Awake never runs (the same idiom the other casters use).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
            MagicSpeck.Palette.UseTemplate(LightMaterial);
        }

        private void Tick(SpeckShot shot, bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            _ready = Mathf.Max(0.0f, _ready - deltaTime);
            _buffered = Mathf.Max(0.0f, _buffered - deltaTime);
            if (held && !_wasHeld)
            {
                _buffered = MagicSpeckRules.ClickBufferSeconds;
            }
            _wasHeld = held;
            if (_vitals != null && _vitals.IsDead)
            {
                return;
            }
            Vector3 head = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            bool wand = _locomotion != null && _locomotion.Wand != null;
            if (held && wand)
            {
                // Held, the wand stays pointed between shots, following the crosshair.
                _locomotion.HoldWandOut(PlayerInputReader.CrosshairPoint(head, eye, look, transform));
            }
            if (_ready > 0.0f || (!held && _buffered <= 0.0f))
            {
                return;
            }

            Vector3 aimPoint = PlayerInputReader.CrosshairPoint(head, eye, look, transform);
            Vector3 from = wand ? _locomotion.PointWandNow(aimPoint) : head;
            Vector3 direction = aimPoint - from;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : look.normalized;
            // Cast Speed shoots it faster, as it does every staff's shots.
            _ready = shot.Seconds / PlayerVitals.SpeedOf(_vitals, StatId.CastSpeed);
            _buffered = 0.0f;
            _out = StaffCharge.FollowThroughSeconds;
            _aimPoint = aimPoint;

            // Spell Power and Armor Penetration, as every magic attack's: the stat's alone, the wand's own boost being for the spells cast through it.
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            MagicSpeck.Spawn(from, direction * MagicSpeckRules.Speed, shot.Size, CombatTuning.DamageFor(shot.Damage, spellPower), penetration,
                gameObject, GetComponentsInChildren<Collider>());
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(MagicSpeckSounds.Cast, from, 0.6f);
            }
            Fired?.Invoke(new AbilityEvent(shot.Kind, from, direction, MagicSpeckRules.Speed));
            _shots++;
        }

        private void Cancel()
        {
            _buffered = 0.0f;
            _wasHeld = false;
        }

        /// <summary>Another player's speck, shown on this copy of their body: the speck itself (hurting nobody), its sound, the wand or staff out.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Direction.sqrMagnitude < 1e-4f)
            {
                return;
            }
            SpeckShot shot = MagicSpeckRules.For(evt.Kind);
            Vector3 direction = evt.Direction.normalized;
            float speed = evt.A > 0.0f ? evt.A : MagicSpeckRules.Speed;
            MagicSpeck.Spawn(evt.Origin, direction * speed, shot.Size, 0.0f, 0.0f, gameObject, GetComponentsInChildren<Collider>(), cosmetic: true);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(MagicSpeckSounds.Cast, evt.Origin, 0.6f);
            }
            _out = StaffCharge.FollowThroughSeconds;
            _aimPoint = evt.Origin + direction * RemoteAimMetres;
        }

        /// <summary>The wand pointed, or the staff thrust out, a moment after each shot, on this body and on other players' copies of it.</summary>
        private void Update()
        {
            if (_out <= 0.0f)
            {
                return;
            }
            _out -= Time.deltaTime;
            if (_locomotion != null && _locomotion.Wand != null)
            {
                _locomotion.HoldWandOut(_aimPoint);
            }
            else if (_locomotion != null && _locomotion.Staff != null)
            {
                _locomotion.HoldStaffOut();
            }
        }

        /// <summary>The wand's or the staff's click: the same speck, at its own pace and damage.</summary>
        private sealed class Shooter : IStaffCaster
        {
            private readonly MagicSpeckCaster _caster;
            private readonly SpeckShot _shot;

            public Shooter(MagicSpeckCaster caster, SpeckShot shot)
            {
                _caster = caster;
                _shot = shot;
            }

            public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime) =>
                _caster.Tick(_shot, held, tip, eye, look, deltaTime);

            public void CancelStaffCharge() => _caster.Cancel();
        }
    }
}
