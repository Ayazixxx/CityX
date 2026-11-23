using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BODY'S BOTTLE AS OTHER PLAYERS SEE IT (docs/GLASS_BOTTLE.md, docs/MULTIPLAYER_STATE.md): on the player body, it plays the bottle's
    /// moves (<see cref="BottlePoseDriver"/>) and tells the other machines of them, of the bottle leaving the hand (their copy of it flies
    /// the same way and shatters where it lands, hurting no one: the thrower's machine dealt the blow) and of the bottle in the hand
    /// breaking on something (their copy of it breaks too, its pieces flying).
    /// </summary>
    public sealed class BottleThrower : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "bottle";

        /// <summary>
        /// The events (<see cref="AbilityEvent.Kind"/>): Thrown (Origin the bottle, Direction its way, A its speed, N which bottle of
        /// GlassBottleKinds.All), Broke (N the state it broke to, Direction the way the blow was going), Aimed (N 1 raised, 0 lowered) and
        /// ThrowStarted (the arm goes).
        /// </summary>
        public const int Thrown = 0;
        public const int Broke = 1;
        public const int Aimed = 2;
        public const int ThrowStarted = 3;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("What each bottle is in the air, in GlassBottleKinds.All's order (PlayerAbilityWiring).")]
        public GameObject[] ThrownPrefabs = new GameObject[0];

        private BottlePoseDriver _driver;

        /// <summary>What plays the bottle's moves on this body, made the first time a bottle is held.</summary>
        public BottlePoseDriver Driver
        {
            get
            {
                if (_driver == null)
                {
                    _driver = GetComponent<BottlePoseDriver>();
                    if (_driver == null)
                    {
                        _driver = gameObject.AddComponent<BottlePoseDriver>();
                    }
                }
                return _driver;
            }
        }

        /// <summary>The bottle this body holds, if it holds one (its own, or the copy's model in the hand).</summary>
        private BottleWeapon Held => GetComponentInChildren<BottleWeapon>();

        /// <summary>Right-click held or let go with a bottle in hand: the arm goes up with it, or comes back down. Other players are told.</summary>
        public void Aim(bool on)
        {
            if (Driver.AimHeld == on)
            {
                return;
            }
            Driver.Aim(on);
            Fired?.Invoke(new AbilityEvent(Aimed, transform.position, Vector3.zero, 0.0f, 0.0f, on ? 1 : 0));
        }

        /// <summary>The throw begins (the bottle leaves the hand at the move's release). False if one is already going.</summary>
        public bool BeginThrow()
        {
            if (!Driver.Throw())
            {
                return false;
            }
            Fired?.Invoke(new AbilityEvent(ThrowStarted, transform.position, Vector3.zero));
            return true;
        }

        /// <summary>This body's bottle leaves its hand: it flies, and other players are told.</summary>
        public ThrownBottle Throw(GameObject prefab, string itemId, Vector3 from, Quaternion rotation, Vector3 velocity)
        {
            var vitals = GetComponent<PlayerVitals>();
            ThrownBottle thrown = ThrownBottle.Spawn(prefab, from, rotation, velocity, vitals != null ? vitals.Strength : 0,
                GetComponentsInChildren<Collider>(), vitals != null ? vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f, gameObject);
            if (thrown != null)
            {
                // The velocity travels as its way and its speed (an event's direction is no longer than 1.5).
                Fired?.Invoke(new AbilityEvent(Thrown, from, velocity.normalized, velocity.magnitude, 0.0f, Mathf.Max(0, GlassBottleKinds.IndexOf(itemId))));
            }
            return thrown;
        }

        /// <summary>This body's bottle broke in its hand, on a blow going <paramref name="way"/>: other players are told.</summary>
        public void TellBroke(BottleWeapon.DurabilityState to, Vector3 way)
        {
            Fired?.Invoke(new AbilityEvent(Broke, transform.position, way.sqrMagnitude > 1e-6f ? way.normalized : Vector3.forward, 0.0f, 0.0f, (int)to));
        }

        /// <summary>Another player's bottle, on this copy of them.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Thrown:
                {
                    if (float.IsNaN(evt.A) || evt.A <= 0.0f || evt.N < 0 || evt.N >= ThrownPrefabs.Length)
                    {
                        return;
                    }
                    Vector3 velocity = evt.Direction.normalized * Mathf.Min(evt.A, BottleWeapon.ThrowSpeedConst * 2.0f);
                    BottleWeapon held = Held;
                    Quaternion rotation = held != null ? held.transform.rotation : Quaternion.LookRotation(velocity.normalized);
                    ThrownBottle.Spawn(ThrownPrefabs[evt.N], evt.Origin, rotation, velocity, 0, GetComponentsInChildren<Collider>(), 0.0f, gameObject, true);
                    break;
                }
                case Broke:
                {
                    BottleWeapon held = Held;
                    if (held != null && evt.N >= (int)BottleWeapon.DurabilityState.Broken && evt.N <= (int)BottleWeapon.DurabilityState.FullyBroken)
                    {
                        held.BreakTo((BottleWeapon.DurabilityState)evt.N, evt.Direction);
                    }
                    break;
                }
                case Aimed:
                    Driver.Aim(evt.N == 1);
                    break;
                case ThrowStarted:
                    Driver.Throw();
                    break;
            }
        }
    }
}
