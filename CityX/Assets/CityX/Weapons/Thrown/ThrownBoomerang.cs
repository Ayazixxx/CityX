using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOOMERANG IN THE AIR (docs/BOOMERANG.md). It flies its <see cref="BoomerangFlight"/> and sweeps its path each frame. Going out, the first thing
    /// it meets, a person or a wall, turns it back. Coming back, it goes through walls and hits everyone it passes. Every hit pushes away from the
    /// thrower, whichever way it is flying. Only the thrower's hurts; another player's copy flies the same way and hurts no one.
    ///
    /// THE GIANT ONE (<see cref="Giant"/>, BoomerangProfile.Heavy) flies flat and goes on through the people it meets going out: only a wall turns it
    /// back. Every body it hits is thrown down limp, away from the thrower.
    /// </summary>
    public sealed class ThrownBoomerang : MonoBehaviour
    {
        /// <summary>How far the spinning disc is tipped from flat, degrees, up toward its way and over to its side, so that its spin reads from behind, the side and above.</summary>
        public const float TiltDegrees = 55.0f;

        public const float RollDegrees = 30.0f;

        /// <summary>The giant one's tip: nearly flat, a blade cutting the air at chest height.</summary>
        public const float GiantTiltDegrees = 8.0f;

        /// <summary>The giant one going away again this near the catching hand has gone past it: caught, metres.</summary>
        public const float PassedTheHandMetres = 2.5f;

        private const float WhirrVolume = 0.55f;
        private const float HitVolume = 0.8f;

        [Tooltip("What spins: the model, about its own flat axis. Set by BoomerangBuilder.")]
        public Transform Spinner;

        [Tooltip("The Giant Boomerang: its own numbers (BoomerangProfile.Giant). Set by BoomerangBuilder.")]
        public bool Giant;

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Dictionary<IDamageable, float> _struckAt = new Dictionary<IDamageable, float>();

        private BoomerangFlight _flight;
        private BoomerangProfile _profile;
        private GameObject _thrower;
        private float _damage;
        private float _penetration;
        private float _spin;
        private bool _done;

        /// <summary>Another player's boomerang, shown here: it hurts no one.</summary>
        public bool IsCopy { get; private set; }

        public bool Returning => _flight != null && _flight.Returning;

        /// <summary>How it flies and hits.</summary>
        public BoomerangProfile Profile => _profile;

        /// <summary>Raised on the thrower's boomerang when something turned it back on its way out: whether that was a person.</summary>
        public event System.Action<bool> TurnedBack;

        /// <summary>Raised as it goes, caught or lost.</summary>
        public event System.Action Gone;

        /// <summary>The giant one, a moment (GiantBoomerangMoves.CatchSeconds) before it is home: the hand goes out for it.</summary>
        public event System.Action ComingIn;

        /// <summary>It got home (not lost).</summary>
        public bool Caught { get; private set; }

        private Transform _catchingHand;
        private bool _comingIn;

        public static ThrownBoomerang Spawn(GameObject prefab, Vector3 from, Vector3 way, float speed, GameObject thrower, float damage, float penetration, bool copy)
        {
            if (prefab == null)
            {
                return null;
            }
            var template = prefab.GetComponent<ThrownBoomerang>();
            bool giant = template != null && template.Giant;
            Quaternion facing = Quaternion.LookRotation(way.sqrMagnitude > 1e-6f ? way : Vector3.forward, Vector3.up)
                * (giant ? Quaternion.Euler(GiantTiltDegrees, 0.0f, 0.0f) : Quaternion.Euler(TiltDegrees, 0.0f, RollDegrees));
            GameObject made = Instantiate(prefab, from, facing);
            var boomerang = made.GetComponent<ThrownBoomerang>();
            boomerang._profile = giant ? BoomerangProfile.Giant : BoomerangProfile.Wooden;
            boomerang._flight = new BoomerangFlight(from, way, speed, boomerang._profile);
            boomerang._thrower = thrower;
            boomerang._damage = damage;
            boomerang._penetration = penetration;
            boomerang.IsCopy = copy;
            boomerang.StartWhirr();
            return boomerang;
        }

        private void Update()
        {
            if (_flight == null || _done)
            {
                return;
            }
            if (_thrower == null)
            {
                Finish();
                return;
            }
            float deltaTime = Time.deltaTime;
            Vector3 home = HomeOf(_thrower);
            if (_profile.Heavy && _flight.Returning && !_comingIn
                && Vector3.Distance(home, _flight.Position) - _profile.CatchMetres <= _flight.Velocity.magnitude * GiantBoomerangMoves.CatchSeconds)
            {
                _comingIn = true;
                ComingIn?.Invoke();
            }
            Vector3 before = _flight.Position;
            _flight.Step(deltaTime, home);
            if (!IsCopy)
            {
                Sweep(before, _flight.Position);
            }
            transform.position = _flight.Position;
            _spin = Mathf.Repeat(_spin + _profile.SpinDegreesPerSecond * deltaTime, 360.0f);
            if (Spinner != null)
            {
                Spinner.localRotation = Quaternion.AngleAxis(_spin, Vector3.up);
            }
            // The giant one steers for a hand that moves out to meet it: one that comes close and starts away again has passed the hand, and is caught.
            bool caught = _flight.Arrived(home) || (_profile.Heavy && _flight.Returning && Vector3.Distance(home, _flight.Position) < PassedTheHandMetres
                && Vector3.Dot(_flight.Velocity, home - _flight.Position) < 0.0f);
            if (caught || _flight.Lost(home))
            {
                Caught = caught;
                Finish();
            }
        }

        /// <summary>The owner says something turned it back on their screen: it turns back here too, and sounds it.</summary>
        public void TurnBackFromNetwork(bool hitSomeone)
        {
            if (_flight != null && !_flight.Returning)
            {
                _flight.TurnBack();
                Clack(transform.position, hitSomeone);
            }
        }

        /// <summary>It is gone now, wherever it is: <paramref name="caught"/> if its thrower caught it (an owner's word reaching their copy).</summary>
        public void Finish(bool caught = false)
        {
            if (_done)
            {
                return;
            }
            _done = true;
            Caught |= caught;
            Gone?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>Where it steers for: the thrower's chest, or for the giant one the hand that catches it.</summary>
        private Vector3 HomeOf(GameObject thrower)
        {
            if (_profile.Heavy)
            {
                if (_catchingHand == null)
                {
                    Animator animator = thrower.GetComponentInChildren<Animator>();
                    _catchingHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                }
                if (_catchingHand != null)
                {
                    return _catchingHand.position;
                }
            }
            var locomotion = thrower.GetComponent<PlayerLocomotion>();
            float height = locomotion != null ? locomotion.WorldHeight : BodyRig.StandingHeight;
            return thrower.transform.position + Vector3.up * (height * 0.6f);
        }

        private void Sweep(Vector3 from, Vector3 to)
        {
            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 1e-5f)
            {
                return;
            }
            Vector3 way = path / length;
            bool returning = _flight.Returning;
            bool heavy = _profile.Heavy;
            int count = Physics.SphereCastNonAlloc(from, _profile.HitRadius, way, _hits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            int first = -1;
            IDamageable firstBody = null;
            for (int i = 0; i < count; i++)
            {
                Collider touched = _hits[i].collider;
                if (touched == null || touched.transform.IsChildOf(_thrower.transform) || touched.transform.IsChildOf(transform))
                {
                    continue;
                }
                IDamageable body = touched.GetComponentInParent<IDamageable>();
                if (body != null && !body.IsAlive)
                {
                    continue;
                }
                if (returning || (heavy && body != null))
                {
                    if (body != null)
                    {
                        Hit(body, PointOf(_hits[i], from));
                    }
                }
                else if (body == null && heavy)
                {
                    continue;   // a wall is met by the thin blade's own sweep (below), not by its span
                }
                else if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    first = i;
                    firstBody = body;
                }
            }
            if (heavy && !returning)
            {
                first = FirstWall(from, way, length);
                firstBody = null;
            }
            if (first < 0)
            {
                return;
            }

            Vector3 point = PointOf(_hits[first], from);
            if (firstBody != null)
            {
                Hit(firstBody, point);
            }
            else
            {
                var breakable = _hits[first].collider.GetComponentInParent<CityX.World.IBreakableSurface>();
                breakable?.TakeBlow(point, way, heavy ? CityX.World.BlowStrength.Heavy : CityX.World.BlowStrength.Light);
                Clack(point, false);
            }
            _flight.TurnBack();
            TurnedBack?.Invoke(firstBody != null);
        }

        /// <summary>The nearest wall (not a body) its thin blade meets this frame, as an index into the hits, or -1.</summary>
        private int FirstWall(Vector3 from, Vector3 way, float length)
        {
            int count = Physics.SphereCastNonAlloc(from, _profile.WallRadius, way, _hits, length, ~0, QueryTriggerInteraction.Ignore);
            int first = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider touched = _hits[i].collider;
                if (touched == null || touched.transform.IsChildOf(_thrower.transform) || touched.transform.IsChildOf(transform)
                    || touched.attachedRigidbody != null || touched.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    first = i;
                }
            }
            return first;
        }

        /// <summary>A sweep that began already touching something says nothing of where: this frame's start is near enough.</summary>
        private static Vector3 PointOf(in RaycastHit hit, Vector3 from) => hit.distance > 0.0f ? hit.point : from;

        private void Hit(IDamageable body, Vector3 point)
        {
            if (_struckAt.TryGetValue(body, out float at) && _flight.Age - at < _profile.HitAgainSeconds)
            {
                return;
            }
            _struckAt[body] = _flight.Age;
            DamageContext.Strike(body, _damage, _thrower.transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, _penetration, point, _thrower);
            if (_profile.Heavy && body.IsAlive)
            {
                Vector3 away = point - _thrower.transform.position;
                away.y = 0.0f;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : transform.forward;
                Throws.Limp(body, away * GiantBoomerangRules.KnockAway + Vector3.up * GiantBoomerangRules.KnockUp);
            }
            Clack(point, true);
        }

        private static void Clack(Vector3 at, bool onSomeone)
        {
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(onSomeone ? ArcherySounds.Thud : ArcherySounds.Knock, at, HitVolume);
            }
        }

        private void StartWhirr()
        {
            var whirr = gameObject.AddComponent<AudioSource>();
            whirr.clip = BoomerangSounds.Whirr;
            whirr.loop = true;
            whirr.spatialBlend = 1.0f;
            whirr.volume = WhirrVolume;
            whirr.pitch = _profile.Heavy ? 0.55f : 1.0f;
            whirr.Play();
        }
    }
}
