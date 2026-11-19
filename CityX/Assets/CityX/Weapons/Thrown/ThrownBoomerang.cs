using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOOMERANG IN THE AIR (docs/BOOMERANG.md). It flies its <see cref="BoomerangFlight"/> and sweeps its path each frame. Going out, the first thing
    /// it meets, a person or a wall, turns it back. Coming back, it goes through walls. One throw hits as many people as its <see cref="BoomerangBlows"/>
    /// allow, one unless more are unlocked, the nearest first; after that it flies home through people, hurting no one. Every hit pushes away from the
    /// thrower, whichever way it is flying. Only the thrower's hurts; another player's copy flies the same way and hurts no one.
    ///
    /// THE GIANT ONE (<see cref="Giant"/>, BoomerangProfile.Heavy) flies flat. Going out it goes on through the people it may pass through and turns
    /// back off the last it may hit, off a wall, or off a guard that stops it. Every body it hits is thrown down limp, away from the thrower, unless a
    /// guard, a block, a shield or a barrier stopped the blow.
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
        private BoomerangBlows _blows;
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

        public static ThrownBoomerang Spawn(GameObject prefab, Vector3 from, Vector3 way, float speed, GameObject thrower, float damage, float penetration, bool copy,
            int passesThrough = 0)
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
            boomerang._blows = new BoomerangBlows(passesThrough);
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

        /// <summary>
        /// The owner says something turned it back on their screen: it turns back here too, and sounds it. A wall, the floor or a thing struck at
        /// <paramref name="point"/> facing <paramref name="normal"/> bangs and bursts here as there (a zero normal: where it is, facing back).
        /// </summary>
        public void TurnBackFromNetwork(bool hitSomeone, Vector3 point, Vector3 normal)
        {
            if (_flight == null || _flight.Returning)
            {
                return;
            }
            Vector3 way = _flight.Velocity;
            _flight.TurnBack();
            if (hitSomeone)
            {
                Clack(transform.position, true);
                return;
            }
            bool told = normal.sqrMagnitude > 1e-6f;
            Bang(told ? point : transform.position, told ? normal : -way);
        }

        /// <summary>Where the last wall, floor or thing it struck was met, and which way that faced (the thrower's boomerang; for the network and tests).</summary>
        public Vector3 LastImpactPoint { get; private set; }

        public Vector3 LastImpactNormal { get; private set; }

        /// <summary>The boomerang's own size of <see cref="ImpactBurst"/>, and the giant one's.</summary>
        public const float BurstSize = 1.0f, GiantBurstSize = 1.8f;

        private const float BangVolume = 0.9f, BangReachMetres = 60.0f;

        /// <summary>A wall, the floor or anything hard struck (owner, 2026-11-17: "some sort of bang noise and some impact fx"): the bang, and the burst on it.</summary>
        private void Bang(Vector3 point, Vector3 normal)
        {
            bool giant = _profile.Heavy;
            ImpactBurst.At(point, normal, giant ? GiantBurstSize : BurstSize);
            if (Application.isPlaying)
            {
                CityX.View.PositionalSound.Play(BoomerangSounds.Bang, point, BangVolume, giant ? BoomerangSounds.GiantBangPitch : 1.0f, 0.0f, BangReachMetres);
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
            System.Array.Sort(_hits, 0, count, NearestFirst.Instance);   // the blows it has left go to the nearest
            for (int i = 0; i < count; i++)
            {
                Collider touched = _hits[i].collider;
                if (touched == null || touched.transform.IsChildOf(_thrower.transform) || touched.transform.IsChildOf(transform))
                {
                    continue;
                }
                IDamageable body = touched.GetComponentInParent<IDamageable>();
                if (body == null)
                {
                    if (returning || heavy)
                    {
                        continue;   // home through walls; the giant one going out meets a wall by its thin blade's own sweep (below), not by its span
                    }
                    StrikeTheWall(_hits[i], from, way);
                    return;
                }
                if (!body.IsAlive || _blows.Spent)
                {
                    continue;
                }
                bool struck = Hit(body, PointOf(_hits[i], from), out bool stopped);
                bool last = struck && _blows.Strike();
                // Going out, the wooden one turns back off anyone; the giant one off a guard, as off a wall, or off the last body it may hit.
                if (!returning && (!heavy || stopped || last))
                {
                    _flight.TurnBack();
                    TurnedBack?.Invoke(true);
                    return;
                }
            }
            if (heavy && !returning)
            {
                int wall = FirstWall(from, way, length);
                if (wall >= 0)
                {
                    StrikeTheWall(_hits[wall], from, way);
                }
            }
        }

        /// <summary>Going out it met a wall, the floor or a thing: a blow on it if it breaks, the bang and burst, and it turns back.</summary>
        private void StrikeTheWall(in RaycastHit hit, Vector3 from, Vector3 way)
        {
            Vector3 point = PointOf(hit, from);
            var breakable = hit.collider.GetComponentInParent<CityX.World.IBreakableSurface>();
            breakable?.TakeBlow(point, way, _profile.Heavy ? CityX.World.BlowStrength.Heavy : CityX.World.BlowStrength.Light);
            // (A sweep that began already touching says nothing of the surface's facing: straight back along the way it came.)
            Vector3 normal = hit.distance > 0.0f ? hit.normal : -way;
            LastImpactPoint = point;
            LastImpactNormal = normal;
            Bang(point, normal);
            _flight.TurnBack();
            TurnedBack?.Invoke(false);
        }

        private sealed class NearestFirst : IComparer<RaycastHit>
        {
            public static readonly NearestFirst Instance = new NearestFirst();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
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

        /// <summary>
        /// Strikes a body once a pass: false if it struck this one too lately to strike it again. <paramref name="stopped"/> if a guard, a block, a shield or
        /// a barrier stopped it (owner, 2026-11-12: "make it so the boomerang is blockable and when you block it, it doesnt ragdoll you"): only a blow
        /// that landed throws the body down, and a giant one going out turns back off it.
        /// </summary>
        private bool Hit(IDamageable body, Vector3 point, out bool stopped)
        {
            stopped = false;
            if (_struckAt.TryGetValue(body, out float at) && _flight.Age - at < _profile.HitAgainSeconds)
            {
                return false;
            }
            _struckAt[body] = _flight.Age;
            HitResponse response = DamageContext.Strike(body, _damage, _thrower.transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, _penetration, point, _thrower);
            stopped = response == HitResponse.Blocked || response == HitResponse.Guarded;
            if (_profile.Heavy && response == HitResponse.Landed && body.IsAlive)
            {
                Vector3 away = point - _thrower.transform.position;
                away.y = 0.0f;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : transform.forward;
                Throws.Limp(body, away * GiantBoomerangRules.KnockAway + Vector3.up * GiantBoomerangRules.KnockUp);
            }
            Clack(point, !stopped);
            return true;
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
