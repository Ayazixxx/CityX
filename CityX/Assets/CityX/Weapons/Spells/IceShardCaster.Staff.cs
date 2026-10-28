using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE ICE SHARD STAFF'S SHOTS (owner, 2026-10-27: "when you click it shoots 1 iceshard but when you hold click it just shoots consecutively
    /// like an automatic gun", and "dont make it too OP"; docs/STAFFS.md). A shard leaves the staff's head at the press, then one every
    /// <see cref="StaffShotSeconds"/> while it is held, each paying <see cref="StaffManaPerShard"/> mana and wandering a little more than a
    /// volley's (<see cref="StaffSpreadDegrees"/>). The shards are the spell's own (8 damage each, before Spell Power): about 32 a second, eight
    /// seconds of fire from a full bar of mana, well under a gun's.
    /// </summary>
    public sealed partial class IceShardCaster : IStaffCaster
    {
        /// <summary>The event other players are sent for a staff's shard (kind 0 is a volley's).</summary>
        public const int StaffShardFired = 1;

        public const float StaffShotSeconds = 0.25f;

        public const float StaffManaPerShard = 4.0f;

        public const float StaffSpreadDegrees = 1.5f;

        private PlayerLocomotion _locomotion;
        private float _staffReady;
        private float _staffOut;
        private int _staffShots;
        private float _remoteStaffOut;

        /// <summary>The shards the staff has fired since its button went down.</summary>
        public int StaffShotsThisHold => _staffShots;

        public void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime)
        {
            if (_staffReady > 0.0f)
            {
                _staffReady -= deltaTime;
            }
            if (_staffOut > 0.0f)
            {
                _staffOut -= deltaTime;
            }
            if (!held)
            {
                _staffShots = 0;
                return;
            }
            if (_staffReady > 0.0f || (_vitals != null && _vitals.IsDead))
            {
                return;
            }
            if (_vitals != null && !_vitals.TrySpendMana(StaffManaPerShard))
            {
                return;
            }
            // Cast Speed shoots them faster, as it looses a volley's.
            _staffReady = StaffShotSeconds / PlayerVitals.SpeedOf(_vitals, StatId.CastSpeed);
            _staffOut = StaffCharge.FollowThroughSeconds;
            Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 1.8f;
            Vector3 aimed = Nudged(PlayerInputReader.ConvergedDirection(from, eye, look), _staffShots, StaffSpreadDegrees);
            int spellPower = _vitals != null ? _vitals.Stats[StatId.SpellPower] : 0;
            float penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
            IceShardProjectile.Spawn(ShardPrefab, from, aimed * ShardSpeed, spellPower, GetComponentsInChildren<Collider>(),
                penetration: penetration, striker: gameObject);
            PlayCastSound();
            Fired?.Invoke(new AbilityEvent(StaffShardFired, from, aimed, ShardSpeed, 0.0f, _staffShots));
            _staffShots++;
        }

        /// <summary>A staff that shoots as it is held has no charge to drop.</summary>
        public void CancelStaffCharge()
        {
            _staffShots = 0;
        }

        private void PlayRemoteStaffShard(AbilityEvent evt)
        {
            float speed = evt.A > 0.0f ? evt.A : ShardSpeed;
            IceShardProjectile.Spawn(ShardPrefab, evt.Origin, evt.Direction.normalized * speed, 0, GetComponentsInChildren<Collider>(), cosmetic: true);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(IceShardSounds.Cast, evt.Origin, 0.55f);
            }
            _remoteStaffOut = StaffCharge.FollowThroughSeconds;
        }

        /// <summary>The staff held out while it shoots, on this body and on other players' copies of it.</summary>
        private void Update()
        {
            if (_remoteStaffOut > 0.0f)
            {
                _remoteStaffOut -= Time.deltaTime;
            }
            if ((_staffOut > 0.0f || _remoteStaffOut > 0.0f) && _locomotion != null && _locomotion.Staff != null)
            {
                _locomotion.HoldStaffOut();
            }
        }
    }
}
