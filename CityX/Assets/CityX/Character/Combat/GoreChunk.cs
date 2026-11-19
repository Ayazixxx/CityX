using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A piece of gore in flight (docs/DISMEMBERMENT.md): the first time it comes down hard it stops trailing blood and, while its burst has
    /// smears left (<see cref="Smears"/>), leaves one where it landed; once it has lain still a moment it stops being simulated (kinematic, its
    /// collider off), as Ultrakill freezes its gibs, so a street full of them costs nothing; and wakes again if what it lay on goes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GoreChunk : MonoBehaviour
    {
        /// <summary>How hard a landing must be to count (the piece falling, not grazing the body it left), m/s.</summary>
        public const float LandingSpeed = 1.2f;

        /// <summary>Slower than this, moving and turning, for <see cref="RestSeconds"/>, and it lies where it is.</summary>
        public const float RestSpeed = 0.08f;

        public const float RestTurn = 0.5f;
        public const float RestSeconds = 0.8f;

        /// <summary>How big a landing smear is, m at the body's scale.</summary>
        public const float SmearSize = 0.3f;

        /// <summary>How many of a burst's pieces may leave a smear: the floor would be all smears, and the splats have a limit of their own.</summary>
        public sealed class Smears
        {
            private int _left;

            public Smears(int count)
            {
                _left = count;
            }

            public int Left => _left;

            public bool Take()
            {
                if (_left <= 0)
                {
                    return false;
                }
                _left--;
                return true;
            }
        }

        private Rigidbody _body;
        private TrailRenderer _trail;
        private Smears _smears;
        private float _scale = 1.0f;
        private float _still;

        /// <summary>It has come down hard once.</summary>
        public bool Landed { get; private set; }

        /// <summary>It lies still and is no longer simulated.</summary>
        public bool Resting { get; private set; }

        /// <summary>The burst's smears it may take one of, and the scale of the body it came out of.</summary>
        public void Bind(Smears smears, float scale)
        {
            _smears = smears;
            _scale = Mathf.Max(0.2f, scale);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _trail = GetComponent<TrailRenderer>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Landed || collision.relativeVelocity.magnitude < LandingSpeed)
            {
                return;
            }
            Landed = true;
            if (_trail == null)
            {
                _trail = GetComponent<TrailRenderer>();
            }
            if (_trail != null)
            {
                _trail.emitting = false;
            }
            if (_smears != null && collision.contactCount > 0 && _smears.Take())
            {
                ContactPoint contact = collision.GetContact(0);
                Vector3 along = _body != null ? _body.linearVelocity : Vector3.forward;
                BloodSplats.Drop(contact.point, contact.normal, SmearSize * _scale, along);
            }
        }

        /// <summary>
        /// A piece lying still looks this often (seconds) for something under it, and wakes and falls if nothing is (owner, 2026-11-19: "some
        /// of the dismemberment parts freeze in mid air ... they should have physics"). What it lay on can go: another corpse cut up, revived
        /// or cleared away, a piece that shrank away, glass that broke.
        /// </summary>
        public const float SupportCheckSeconds = 0.4f;

        /// <summary>How far below its own bottom it looks for what it lies on, metres.</summary>
        public const float SupportReach = 0.12f;

        private float _supportCheckIn;
        private Bounds _restBounds;
        private readonly System.Collections.Generic.List<Collider> _switchedOff = new System.Collections.Generic.List<Collider>();
        private static readonly RaycastHit[] Under = new RaycastHit[16];

        private void FixedUpdate()
        {
            if (Resting)
            {
                _supportCheckIn -= Time.fixedDeltaTime;
                if (_supportCheckIn <= 0.0f)
                {
                    _supportCheckIn = SupportCheckSeconds;
                    if (!Supported())
                    {
                        Wake();
                    }
                }
                return;
            }
            if (_body == null || _body.isKinematic)
            {
                return;
            }
            if (_body.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed && _body.angularVelocity.sqrMagnitude < RestTurn * RestTurn)
            {
                _still += Time.fixedDeltaTime;
                if (_still >= RestSeconds)
                {
                    Rest();
                }
            }
            else
            {
                _still = 0.0f;
            }
        }

        private void Rest()
        {
            Resting = true;
            if (_trail != null)
            {
                _trail.emitting = false;
            }
            _restBounds = new Bounds(transform.position, Vector3.zero);
            _switchedOff.Clear();
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (collider.enabled && !collider.isTrigger)
                {
                    _restBounds.Encapsulate(collider.bounds);
                }
                collider.enabled = false;
                _switchedOff.Add(collider);
            }
            _body.isKinematic = true;
            // Staggered, so a burst's pieces do not all look on the same step.
            _supportCheckIn = SupportCheckSeconds * Random.value;
        }

        /// <summary>Something solid lies under its footprint, within <see cref="SupportReach"/> of its bottom.</summary>
        private bool Supported()
        {
            Vector3 half = new Vector3(_restBounds.extents.x * 0.8f, 0.01f, _restBounds.extents.z * 0.8f);
            float reach = _restBounds.extents.y + SupportReach * _scale;
            int count = Physics.BoxCastNonAlloc(_restBounds.center, half, Vector3.down, Under, Quaternion.identity, reach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Under[i].collider;
                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What it lay on has gone: simulated again, to fall and come to rest on whatever is under it now.</summary>
        private void Wake()
        {
            Resting = false;
            _still = 0.0f;
            foreach (Collider collider in _switchedOff)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
            _switchedOff.Clear();
            _body.isKinematic = false;
            _body.WakeUp();
        }
    }
}
