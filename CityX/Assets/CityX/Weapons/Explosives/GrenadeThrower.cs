using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BODY'S GRENADES AS OTHER PLAYERS SEE THEM (docs/GRENADE.md, docs/MULTIPLAYER_STATE.md): on the player body, the grenade thrown (where
    /// from and how fast: their copy of it flies the same way) and where it went off (their copy goes off there, showing the explosion and
    /// hurting no one: the thrower's machine dealt the blast, and it reaches their bodies through their copies, as every blow does).
    /// </summary>
    public sealed class GrenadeThrower : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "grenade";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): Thrown (Origin the hand, Direction its way, A its speed), and Exploded (Origin where).</summary>
        public const int Thrown = 0;

        public const int Exploded = 1;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        [Tooltip("What another player's thrown grenade is shown as here. Set by PlayerAbilityWiring.")]
        public GameObject ThrownPrefab;

        private ThrownGrenade _copy;

        /// <summary>This body throws a grenade: it flies, and other players are told.</summary>
        public ThrownGrenade Throw(GameObject prefab, Vector3 from, Vector3 velocity)
        {
            ThrownGrenade grenade = ThrownGrenade.Spawn(prefab, from, velocity, gameObject, false);
            if (grenade == null)
            {
                return null;
            }
            grenade.Exploded += at => Fired?.Invoke(new AbilityEvent(Exploded, at, Vector3.zero));
            // The velocity travels as its way and its speed (an event's direction is no longer than 1.5).
            Fired?.Invoke(new AbilityEvent(Thrown, from, velocity.normalized, velocity.magnitude));
            return grenade;
        }

        /// <summary>Another player's grenade, on this copy of them: thrown the same way, gone off where theirs did.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Thrown:
                    if (float.IsNaN(evt.A) || evt.A <= 0.0f)
                    {
                        return;
                    }
                    Vector3 velocity = evt.Direction.normalized * Mathf.Min(evt.A, GrenadeRules.ThrowSpeed * 2.0f);
                    _copy = ThrownGrenade.Spawn(ThrownPrefab, evt.Origin, velocity, gameObject, true);
                    break;
                case Exploded:
                    if (_copy != null && !_copy.HasExploded)
                    {
                        _copy.GoOff(evt.Origin);
                    }
                    else
                    {
                        Explosion.Show(evt.Origin);
                    }
                    _copy = null;
                    break;
            }
        }
    }
}
