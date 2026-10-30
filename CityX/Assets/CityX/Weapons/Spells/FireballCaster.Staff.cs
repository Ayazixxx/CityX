using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FIREBALL STAFF'S SHOTS (owner, 2026-10-27: "when you hold click with that one it does a charged shot the explodes into a bigger ball of
    /// fire the longer you charge it, come up with a reasonable maximum"; docs/STAFFS.md). A click is a plain fireball; held, the shot grows for
    /// up to <see cref="StaffFullChargeSeconds"/> (Mega Man's charged buster takes about as long), to <see cref="StaffMostSize"/> times the ball
    /// and its damage, and its burst burns everyone within up to <see cref="StaffMostBlastRadius"/> of where it lands. A shot pays
    /// <see cref="StaffLeastMana"/> mana uncharged, up to <see cref="StaffMostMana"/> full; the charge stops growing at what the mana pays for.
    /// The numbers are mine.
    /// </summary>
    public sealed partial class FireballCaster : IStaffCaster
    {
        /// <summary>The events other players are sent beside the fireball's own (kind 0: B its size, N <see cref="FromStaff"/> from a staff).</summary>
        public const int StaffChargeStarted = 1;
        public const int StaffChargeStopped = 2;

        public const int FromStaff = 1;

        public const float StaffFullChargeSeconds = 1.5f;

        /// <summary>A full charge's ball, and its damage, as a multiple of a plain fireball's.</summary>
        public const float StaffMostSize = 2.5f;

        /// <summary>How far round its landing a full charge's burst burns, metres (nothing round an uncharged one).</summary>
        public const float StaffMostBlastRadius = 4.0f;

        public const float StaffLeastMana = SpellManaCost;
        public const float StaffMostMana = 30.0f;

        /// <summary>The quickest the staff shoots again (the bar's fireball waits 1.5 s).</summary>
        public const float StaffShotSeconds = 0.5f;

        private static readonly Color FireColour = new Color(1.0f, 0.5f, 0.12f);

        /// <summary>A full charge's glow at the head, metres across: a ball of fire forming, bigger than the arcane staff's.</summary>
        public const float FullChargeGlowSize = 1.4f;
        private static readonly Color FireHotColour = new Color(1.0f, 0.85f, 0.45f);
        private static Material _fireGlow;
        private static Material _fireHot;

        [Tooltip("The light the charge's glow is made from (SelfSmiteAbility's: soft particles, shipped in a build). Set by PlayerAbilityWiring.")]
        public Material LightMaterial;

        private readonly StaffCharge _staff = new StaffCharge(StaffShotSeconds);
        private PlayerLocomotion _locomotion;
        private Transform _tip;
        private Vector3 _eye;
        private Vector3 _look = Vector3.forward;
        private bool _remoteCharging;
        private float _remoteChargeSeconds;
        private float _remoteFollowThrough;

        /// <summary>A charge held <paramref name="seconds"/>, as a share of a full one.</summary>
        public static float StaffChargeFraction(float seconds) => Mathf.Clamp01(seconds / StaffFullChargeSeconds);

        public static float StaffManaFor(float fraction) => Mathf.Lerp(StaffLeastMana, StaffMostMana, Mathf.Clamp01(fraction));

        /// <summary>The most charge <paramref name="mana"/> pays for, as a share of a full one; below 0 when not even a plain shot.</summary>
        public static float StaffAffordableFraction(float mana) =>
            mana < StaffLeastMana ? -1.0f : Mathf.Clamp01((mana - StaffLeastMana) / (StaffMostMana - StaffLeastMana));

        /// <summary>The staff's charge going on now, if any.</summary>
        public StaffCharge Staff => _staff;

        /// <summary>The charge a shot let go now would carry, as a share of a full one (what the mana pays for).</summary>
        public float StaffChargeNow => Mathf.Min(StaffChargeFraction(_staff.Seconds), Mathf.Max(0.0f, StaffAffordableFraction(Mana)));

        private float Mana => _vitals != null ? _vitals.Mana : float.MaxValue;

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            _tip = tip;
            _eye = eye;
            _look = look.sqrMagnitude > 1e-6f ? look.normalized : _look;
            bool alive = _vitals == null || !_vitals.IsDead;
            if (!alive)
            {
                CancelStaffCharge();
            }
            switch (_staff.Tick(held, alive && StaffAffordableFraction(Mana) >= 0.0f, deltaTime))
            {
                case StaffCharge.Step.Started:
                    Fired?.Invoke(new AbilityEvent(StaffChargeStarted, transform.position, Vector3.zero));
                    break;
                case StaffCharge.Step.Fire:
                    FireStaff();
                    break;
            }
        }

        public void CancelStaffCharge()
        {
            if (_staff.Cancel())
            {
                Fired?.Invoke(new AbilityEvent(StaffChargeStopped, transform.position, Vector3.zero));
            }
        }

        private void FireStaff()
        {
            float fraction = Mathf.Min(StaffChargeFraction(_staff.ReleasedSeconds), StaffAffordableFraction(Mana));
            if (fraction < 0.0f || (_vitals != null && !_vitals.TrySpendMana(StaffManaFor(fraction))))
            {
                Fired?.Invoke(new AbilityEvent(StaffChargeStopped, transform.position, Vector3.zero));
                return;
            }
            float size = Mathf.Lerp(1.0f, StaffMostSize, fraction);
            Vector3 from = _tip != null ? _tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 forward = PlayerInputReader.CrosshairDirection(from, _eye, _look, transform);
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            FireballProjectile.Spawn(FireballPrefab, from, forward * FireballSpeed, spellPower, GetComponentsInChildren<Collider>(),
                penetration: penetration, striker: gameObject, size: size, blastRadius: fraction * StaffMostBlastRadius);
            if (CastAudio != null && CastAudio.clip != null)
            {
                CastAudio.PlayOneShot(CastAudio.clip);
            }
            Fired?.Invoke(new AbilityEvent(0, from, forward, FireballSpeed, size, FromStaff));
        }

        private void PlayRemoteCharge(bool started)
        {
            _remoteCharging = started;
            _remoteChargeSeconds = 0.0f;
        }

        private void PlayRemoteStaffShot()
        {
            _remoteCharging = false;
            _remoteFollowThrough = StaffCharge.FollowThroughSeconds;
        }

        /// <summary>The staff held out and its head glowing while it charges, on this body and on other players' copies of it.</summary>
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
            }
            if ((_staff.Busy || _remoteCharging || _remoteFollowThrough > 0.0f) && _locomotion != null && _locomotion.Staff != null)
            {
                _locomotion.HoldStaffOut();
            }
            if (!_staff.Charging && !_remoteCharging)
            {
                return;
            }
            Transform staff = _locomotion != null ? _locomotion.Staff : null;
            var item = staff != null ? staff.GetComponent<StaffItem>() : null;
            Transform tip = item != null && item.Tip != null ? item.Tip : staff;
            StaffChargeGlow glow = StaffChargeGlow.On(tip, FireGlow, FireHot, FireColour, 0, FullChargeGlowSize);
            if (glow != null)
            {
                glow.Show(_staff.Charging ? StaffChargeNow : StaffChargeFraction(_remoteChargeSeconds), 0);
            }
        }

        private Material FireGlow => _fireGlow != null ? _fireGlow : (_fireGlow = SoftGlow.Make("FireStaffGlow", FireColour, true, LightMaterial));

        private Material FireHot => _fireHot != null ? _fireHot : (_fireHot = SoftGlow.Make("FireStaffHot", FireHotColour, true, LightMaterial));
    }
}
