using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The ice shards spell: press 3 and four shards leave the hand one after another down the crosshair, each sticking where it lands (30 s
    /// in a wall, 10 s in a character). An innate ability like the fireball, no equipping, working whatever is in the hands.
    ///
    /// THE VOLLEY FOLLOWS THE CROSSHAIR: the first shard leaves at the press, and the rest at fixed intervals toward wherever the aim is at
    /// their own moment (PlayerInputReader hands the aim in every frame, see <see cref="Aim"/>), so swinging the view across a target spreads
    /// the four across it. Each is nudged off the exact line by a fraction of a degree in a fixed pattern, so a volley stuck in a wall is a
    /// little cluster rather than one shard over another.
    ///
    /// Everything is a function of a delta (Tick), like the other casters.
    /// </summary>
    public sealed class IceShardCaster : MonoBehaviour, IIceShardCaster, IReplicatedAbility
    {
        public const string Id = "ice-shards";

        string IReplicatedAbility.AbilityId => Id;

        /// <summary>Raised on the owner's copy as each shard leaves (N is which of the volley it is). See IReplicatedAbility.</summary>
        public event System.Action<AbilityEvent> Fired;

        [Tooltip("Shards in a volley.")]
        public int ShardCount = 4;

        [Tooltip("Seconds between one shard and the next.")]
        public float ShardIntervalSeconds = 0.11f;

        [Tooltip("Seconds from the start of a volley before another can begin.")]
        public float CooldownSeconds = 2.5f;

        [Tooltip("How long the raised-hand pose holds after the last shard.")]
        public float FollowThroughSeconds = 0.4f;

        [Tooltip("How far each shard is nudged off the aim, at most, in degrees.")]
        public float SpreadDegrees = 0.8f;

        [Tooltip("Metres per second.")]
        public float ShardSpeed = 50.0f;

        /// <summary>How far in front of the origin a shard appears, so it does not start inside the caster.</summary>
        public const float SpawnForwardOffset = 0.6f;

        public GameObject ShardPrefab;

        private float _cooldown;
        private int _shotsLeft;
        private int _shotsFired;
        private float _nextShotIn;
        private float _castPoseRemaining;
        private Vector3 _origin;
        private Vector3 _direction = Vector3.forward;
        private Collider[] _ignore;
        private PlayerVitals _vitals;
        private AudioSource _audio;

        // The nudges, in units of SpreadDegrees: dead centre first, then a ring.
        private static readonly Vector2[] Pattern =
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.6f),
            new Vector2(-1.0f, 0.6f),
            new Vector2(0.0f, -1.0f),
        };

        public bool IsVolleying => _shotsLeft > 0;

        public bool IsCasting => _castPoseRemaining > 0.0f;

        public float CastFraction =>
            FollowThroughSeconds > 0.0f ? Mathf.Clamp01(_castPoseRemaining / FollowThroughSeconds) : (IsCasting ? 1.0f : 0.0f);

        public bool CanCast => _cooldown <= 0.0f && _shotsLeft <= 0 && (_vitals == null || !_vitals.IsDead);

        /// <summary>How many shards the current (or last) volley has fired.</summary>
        public int ShotsFired => _shotsFired;

        private readonly System.Collections.Generic.List<IceShardProjectile> _volley = new System.Collections.Generic.List<IceShardProjectile>();

        /// <summary>The shards of the current (or last) volley, in the order they left (a shard that has since melted away reads as null).</summary>
        public System.Collections.Generic.IReadOnlyList<IceShardProjectile> LastVolley => _volley;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, callable by hand where Awake never runs (the same idiom the other casters use).</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0.0f)
            {
                _cooldown -= deltaTime;
            }
            if (_castPoseRemaining > 0.0f)
            {
                _castPoseRemaining -= deltaTime;
            }

            if (_shotsLeft <= 0)
            {
                return;
            }
            if (_vitals != null && _vitals.IsDead)
            {
                _shotsLeft = 0;
                return;
            }
            _nextShotIn -= deltaTime;
            while (_shotsLeft > 0 && _nextShotIn <= 0.0f)
            {
                FireOne();
                _nextShotIn += ShardIntervalSeconds;
            }
        }

        public bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders)
        {
            if (!CanCast || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            _cooldown = CooldownSeconds;
            _shotsLeft = Mathf.Max(1, ShardCount);
            _shotsFired = 0;
            _volley.Clear();
            _origin = origin;
            _direction = direction.normalized;
            _ignore = ignoreColliders;
            // The pose holds for the whole volley and then eases out over the follow-through.
            _castPoseRemaining = FollowThroughSeconds + (_shotsLeft - 1) * ShardIntervalSeconds;
            _nextShotIn = 0.0f;
            FireOne();
            _nextShotIn = ShardIntervalSeconds;
            return true;
        }

        public void Aim(Vector3 origin, Vector3 direction)
        {
            if (_shotsLeft > 0 && direction.sqrMagnitude > 0.0001f)
            {
                _origin = origin;
                _direction = direction.normalized;
            }
        }

        private void FireOne()
        {
            Vector2 nudge = Pattern[_shotsFired % Pattern.Length] * (SpreadDegrees * Mathf.Deg2Rad);
            Vector3 right = Vector3.Cross(Vector3.up, _direction);
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.right;   // aiming straight up or down
            }
            right.Normalize();
            Vector3 up = Vector3.Cross(_direction, right);
            Vector3 aimed = (_direction + right * Mathf.Tan(nudge.x) + up * Mathf.Tan(nudge.y)).normalized;

            int strength = _vitals != null ? _vitals.Strength : 0;
            Vector3 spawnAt = _origin + aimed * SpawnForwardOffset;
            _volley.Add(IceShardProjectile.Spawn(ShardPrefab, spawnAt, aimed * ShardSpeed, strength, _ignore));
            PlayCastSound();
            // B is how long the raised-hand pose should be held in all (the whole volley and its follow-through): sent with the first shard.
            float poseSeconds = FollowThroughSeconds + (Mathf.Max(1, ShardCount) - 1) * ShardIntervalSeconds;
            Fired?.Invoke(new AbilityEvent(0, spawnAt, aimed, ShardSpeed, poseSeconds, _shotsFired));

            _shotsLeft--;
            _shotsFired++;
        }

        /// <summary>
        /// Another player's shard, shown on this copy of their body: the shard itself (flying, sticking in the wall or the character it
        /// meets and melting away on the same clock, but hurting nothing), its "shing", and -- with the first shard of a volley -- the raised hand.
        /// </summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float speed = evt.A > 0.0f ? evt.A : ShardSpeed;
            IceShardProjectile.Spawn(ShardPrefab, evt.Origin, evt.Direction.normalized * speed, 0, GetComponentsInChildren<Collider>(), cosmetic: true);
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(IceShardSounds.Cast, evt.Origin, 0.55f);
            }
            if (evt.N == 0)
            {
                float total = evt.B > 0.0f ? evt.B : FollowThroughSeconds;
                RemoteAbilityPose.Show(gameObject, Mathf.Max(0.0f, total - FollowThroughSeconds), FollowThroughSeconds);
            }
        }

        private void PlayCastSound()
        {
            if (_audio == null)
            {
                var holder = new GameObject("IceShardAudio");
                holder.transform.SetParent(transform, false);
                _audio = holder.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0.0f;   // the sound of THIS body casting, heard from inside it
                _audio.volume = 0.55f;
            }
            _audio.PlayOneShot(IceShardSounds.Cast);
        }
    }
}
