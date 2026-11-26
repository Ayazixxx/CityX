using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A piece of gore (docs/DISMEMBERMENT.md): a cut-off part or a chunk of a burst. The first time it comes down hard it stops trailing blood
    /// and, while its burst has smears left (<see cref="Smears"/>), leaves one where it landed, on firm ground only (BloodSplats.IsFirm: not on
    /// another piece, a body or a car, which would carry it off and leave it hanging in the air).
    ///
    /// IT KEEPS ITS PHYSICS UNTIL IT IS TAKEN AWAY (owner, 2026-11-26: "keep physics working for each dismembered limb"). Lying still, the
    /// physics engine puts it to sleep, which costs next to nothing, and whatever knocks it, or takes away what it lies on, wakes it; a sleeping
    /// piece also looks under its middle now and then and wakes itself when nothing holds it up (<see cref="Supported"/>). It used to be frozen
    /// once still (kinematic, its collider off), woken only when nothing at all lay under its whole footprint: a limb lying with one end on a
    /// corpse kept finding the ground under its other end when the corpse went, and hung in the air at its angle (owner, 2026-11-19: "some of
    /// the dismemberment parts freeze in mid air", and 2026-11-26: "arms at angles where its obvious the physics have stopped working").
    ///
    /// People walk through the pieces (each piece ignores the capsules of the bodies about when it is made), as games' gibs do: nobody trips on
    /// a street of them, and nobody is pushed up onto one.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GoreChunk : MonoBehaviour
    {
        /// <summary>How hard a landing must be to count (the piece falling, not grazing the body it left), m/s.</summary>
        public const float LandingSpeed = 1.2f;

        /// <summary>
        /// The physics engine's rest: moving with less energy than this (half its speed squared, per kilogram) for a moment, it sleeps. Above
        /// Unity's 0.005, so a piece settles quickly.
        /// </summary>
        public const float SleepEnergy = 0.02f;

        /// <summary>How big a landing smear is, m at the body's scale.</summary>
        public const float SmearSize = 0.3f;

        /// <summary>A sleeping piece looks under its middle this often for what holds it up, seconds.</summary>
        public const float SupportCheckSeconds = 0.4f;

        /// <summary>How far below its own underside, under its middle, it looks for what it lies on, metres at the body's scale.</summary>
        public const float SupportReach = 0.12f;

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

        private static readonly RaycastHit[] Under = new RaycastHit[16];
        private static CharacterController[] _people = new CharacterController[0];
        private static int _peopleFoundOnFrame = -1;

        private Rigidbody _body;
        private Collider _collider;
        private TrailRenderer _trail;
        private Smears _smears;
        private float _scale = 1.0f;
        private float _supportCheckIn;
        private bool _smeared;

        /// <summary>It has come down hard once.</summary>
        public bool Landed { get; private set; }

        /// <summary>It lies still: the physics engine has put it to sleep (it is still simulated, and wakes when knocked or let fall).</summary>
        public bool Resting => _body != null && _body.IsSleeping();

        /// <summary>How many times it woke itself, finding nothing under it (for tests).</summary>
        public int WokenByNothingUnder { get; private set; }

        /// <summary>The burst's smears it may take one of, and the scale of the body it came out of.</summary>
        public void Bind(Smears smears, float scale)
        {
            _smears = smears;
            _scale = Mathf.Max(0.2f, scale);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.sleepThreshold = SleepEnergy;
            _collider = GetComponent<Collider>();
            _trail = GetComponent<TrailRenderer>();
            // Staggered, so a burst's pieces do not all look on the same step.
            _supportCheckIn = SupportCheckSeconds * Random.value;
            IgnorePeople();
        }

        /// <summary>The capsules people move with pass through this piece (found once a frame, however many pieces a burst makes).</summary>
        private void IgnorePeople()
        {
            if (_peopleFoundOnFrame != Time.frameCount)
            {
                _peopleFoundOnFrame = Time.frameCount;
                _people = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
            }
            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                foreach (CharacterController person in _people)
                {
                    if (person != null)
                    {
                        Physics.IgnoreCollision(own, person, true);
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < LandingSpeed)
            {
                return;
            }
            if (!Landed)
            {
                Landed = true;
                if (_trail == null)
                {
                    _trail = GetComponent<TrailRenderer>();
                }
                if (_trail != null)
                {
                    _trail.emitting = false;
                }
            }
            // Its one smear goes where it first comes down hard on firm ground: knocking into another piece in the air first does not use it up.
            if (!_smeared && _smears != null && collision.contactCount > 0 && BloodSplats.IsFirm(collision.collider, null) && _smears.Take())
            {
                _smeared = true;
                ContactPoint contact = collision.GetContact(0);
                Vector3 along = _body != null ? _body.linearVelocity : Vector3.forward;
                BloodSplats.Drop(contact.point, contact.normal, SmearSize * _scale, along, collision.collider);
            }
        }

        private void FixedUpdate()
        {
            if (!Resting)
            {
                return;
            }
            if (_trail != null && _trail.emitting)
            {
                _trail.emitting = false;
            }
            _supportCheckIn -= Time.fixedDeltaTime;
            if (_supportCheckIn > 0.0f)
            {
                return;
            }
            _supportCheckIn = SupportCheckSeconds;
            if (!Supported())
            {
                WokenByNothingUnder++;
                _body.WakeUp();   // what it lay on went without the physics noticing (moved away, switched off): it falls
            }
        }

        /// <summary>
        /// Something solid lies just under its MIDDLE (where its weight is), within <see cref="SupportReach"/> of its own underside there. Under
        /// its middle, not anywhere under its footprint: a limb left leaning with its far end on the ground has nothing under its middle once
        /// what held up its near end goes, and falls flat.
        /// </summary>
        private bool Supported()
        {
            if (_collider == null || !_collider.enabled)
            {
                return true;
            }
            Vector3 middle = _body.worldCenterOfMass;
            Bounds bounds = _collider.bounds;
            // Its own underside straight under the middle: a ray up from below finds this piece's surface there.
            var upFromBelow = new Ray(new Vector3(middle.x, bounds.min.y - 1.0f, middle.z), Vector3.up);
            float underside = _collider.Raycast(upFromBelow, out RaycastHit own, bounds.size.y + 2.0f) ? own.point.y : bounds.min.y;
            const float above = 0.02f;
            float reach = SupportReach * _scale + above;
            int count = Physics.RaycastNonAlloc(new Vector3(middle.x, underside + above, middle.z), Vector3.down, Under, reach, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Under[i].collider;
                if (hit != null && hit.attachedRigidbody != _body)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
