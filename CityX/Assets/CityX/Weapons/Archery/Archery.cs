using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// WHAT OTHER PLAYERS SEE OF A BOW (docs/ARCHERY.md; the body-side half of <see cref="BowWeapon"/>, as WaterJetCaster is of the water staff):
    /// the bow drawn on this body's copy -- the arms up at the aim, the nock and the pull by the draw, told ten times a second -- the arrow
    /// loosed (their copy of the arrow flies the same flat range and falls the same way, hurting nothing: the archer's machine decided the hit;
    /// their bow twangs and the hand flicks), and the bow lowered. The crossbow needs none of this: it is a gun (PistolWeapon), and its shots
    /// reach copies as every gun's do.
    /// </summary>
    public sealed class Archery : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "archery";

        /// <summary>The events other players are sent (<see cref="AbilityEvent.Kind"/>).</summary>
        public const int Drawing = 0;   // Direction the aim; A the draw, 0..1
        public const int Loosed = 1;    // Origin the nock, Direction the arrow's; A the draw
        public const int Lowered = 2;

        /// <summary>How often a draw is told, seconds, and how long a copy keeps the bow up past the last word of it.</summary>
        public const float DrawSendSeconds = 0.1f;

        public const float RemoteGraceSeconds = 0.3f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("The arrow a copy's bow looses (the bow prefab's own; set by PlayerAbilityWiring).")]
        public GameObject ArrowPrefab;

        private PlayerLocomotion _locomotion;
        private float _remoteLeft;
        private float _remoteDraw;
        private Vector3 _remoteAim = Vector3.forward;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs (the casters' idiom).</summary>
        public void Bind()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        public void TellDrawing(Vector3 aim, float draw) => Fired?.Invoke(new AbilityEvent(Drawing, transform.position, aim, draw));

        public void TellLoosed(Vector3 nock, Vector3 direction, float draw) => Fired?.Invoke(new AbilityEvent(Loosed, nock, direction, draw));

        public void TellLowered() => Fired?.Invoke(new AbilityEvent(Lowered, transform.position, Vector3.zero));

        private void Update()
        {
            if (_remoteLeft <= 0.0f)
            {
                return;
            }
            _remoteLeft -= Time.deltaTime;
            _locomotion?.HoldBow(_remoteAim, _remoteDraw);
        }

        /// <summary>Another player's bow, on this copy of their body.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Drawing:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    _remoteAim = evt.Direction.normalized;
                    _remoteDraw = Mathf.Clamp01(evt.A);
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case Loosed:
                    if (evt.Direction.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    float draw = ArcheryRules.Clamp(evt.A);
                    ArrowProjectile.Loose(ArrowPrefab, evt.Origin, evt.Origin, evt.Direction.normalized, ArcheryRules.FlatRangeFor(draw),
                        ArcheryRules.FallSpeedFor(draw), 0.0f, 0.0f, gameObject, GetComponentsInChildren<Collider>(), cosmetic: true, out _, out _);
                    if (Application.isPlaying)
                    {
                        AudioSource.PlayClipAtPoint(ArcherySounds.Twang, evt.Origin, 0.7f);
                    }
                    _locomotion?.LooseBow();
                    GetComponentInChildren<BowWeapon>()?.Twang();
                    _remoteAim = evt.Direction.normalized;
                    _remoteDraw = 0.0f;
                    _remoteLeft = RemoteGraceSeconds;
                    break;
                case Lowered:
                    _remoteLeft = 0.0f;
                    break;
            }
        }
    }
}
