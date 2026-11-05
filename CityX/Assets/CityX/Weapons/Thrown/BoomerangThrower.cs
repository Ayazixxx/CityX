using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BODY'S BOOMERANG, HERE AND AS OTHER PLAYERS SEE IT (docs/BOOMERANG.md, docs/MULTIPLAYER_STATE.md). It knows whether the boomerang is out, since
    /// only one can be and it outlives a swap to another weapon. It tells other players of the throw, of what turned it back and of the catch: their
    /// copy flies the same way, hurting no one, and their copy of this body has an empty hand until then.
    /// </summary>
    public sealed class BoomerangThrower : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "boomerang";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): Thrown (Origin the hand, Direction its way, A its speed), TurnedBack (A 1 if a person did it), and Caught.</summary>
        public const int Thrown = 0;

        public const int TurnedBack = 1;

        public const int Caught = 2;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>Raised when it leaves or comes back, for the hand to show it or not.</summary>
        public event System.Action OutChanged;

        [Tooltip("The boomerang in the air, here and for another player's throw. Set by PlayerAbilityWiring.")]
        public GameObject ThrownPrefab;

        private ThrownBoomerang _flying;
        private float _thrownAt = float.NegativeInfinity;

        /// <summary>A boomerang of this body's is in the air.</summary>
        public bool IsOut => _flying != null;

        public ThrownBoomerang Flying => _flying;

        /// <summary>It can be thrown now: the last one is home, and its use time is over.</summary>
        public bool CanThrow => !IsOut && Time.time - _thrownAt >= BoomerangRules.ThrowEverySeconds;

        /// <summary>This body throws its boomerang: the arm swings, it flies, and other players are told. Null if one is out already.</summary>
        public ThrownBoomerang Throw(Vector3 from, Vector3 way, float damage, float penetration)
        {
            if (!CanThrow)
            {
                return null;
            }
            ThrownBoomerang boomerang = Launch(from, way, BoomerangRules.OutSpeed, damage, penetration, false);
            if (boomerang == null)
            {
                return null;
            }
            boomerang.TurnedBack += person => Fired?.Invoke(new AbilityEvent(TurnedBack, boomerang.transform.position, Vector3.zero, person ? 1.0f : 0.0f));
            boomerang.Gone += () => Fired?.Invoke(new AbilityEvent(Caught, transform.position, Vector3.zero));
            Fired?.Invoke(new AbilityEvent(Thrown, from, way.normalized, BoomerangRules.OutSpeed));
            return boomerang;
        }

        /// <summary>Another player's boomerang, on this copy of them.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Thrown:
                    if (!float.IsNaN(evt.A) && evt.A > 0.0f)
                    {
                        if (_flying != null)
                        {
                            _flying.Finish();
                        }
                        Launch(evt.Origin, evt.Direction, Mathf.Min(evt.A, BoomerangRules.OutSpeed * 2.0f), 0.0f, 0.0f, true);
                    }
                    break;
                case TurnedBack:
                    if (_flying != null)
                    {
                        _flying.TurnBackFromNetwork(evt.A > 0.5f);
                    }
                    break;
                case Caught:
                    if (_flying != null)
                    {
                        _flying.Finish();
                    }
                    break;
            }
        }

        private ThrownBoomerang Launch(Vector3 from, Vector3 way, float speed, float damage, float penetration, bool copy)
        {
            ThrownBoomerang boomerang = ThrownBoomerang.Spawn(ThrownPrefab, from, way, speed, gameObject, damage, penetration, copy);
            if (boomerang == null)
            {
                return null;
            }
            _flying = boomerang;
            _thrownAt = Time.time;
            boomerang.Gone += () => Landed(boomerang);
            GetComponent<PlayerLocomotion>()?.PlayThrowSwing(way);
            OutChanged?.Invoke();
            return boomerang;
        }

        private void Landed(ThrownBoomerang boomerang)
        {
            if (_flying == boomerang)
            {
                _flying = null;
                OutChanged?.Invoke();
            }
        }
    }
}
