using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A piece of gore in flight (docs/DISMEMBERMENT.md): the first time it comes down hard it stops trailing blood and, while its burst has
    /// smears left (<see cref="Smears"/>), leaves one where it landed; once it has lain still a moment it stops being simulated (kinematic, its
    /// collider off), as Ultrakill freezes its gibs, so a street full of them costs nothing.
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

        private void FixedUpdate()
        {
            if (Resting || _body == null || _body.isKinematic)
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
            _body.isKinematic = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }
    }
}
