using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BODY'S BOOMERANG, HERE AND AS OTHER PLAYERS SEE IT (docs/BOOMERANG.md, docs/MULTIPLAYER_STATE.md). It knows whether the boomerang is out, since
    /// only one can be and it outlives a swap to another weapon. It tells other players of the throw, of what turned it back and of the catch: their
    /// copy flies the same way, hurting no one, and their copy of this body has an empty hand until then.
    ///
    /// THE GIANT BOOMERANG is thrown through its moves (<see cref="GiantBoomerangPoseDriver"/>): right-click draws it off the back into the aim
    /// (<see cref="SetGiantAiming"/>), a click starts the throw (<see cref="StartGiantThrow"/>), and it leaves the hand when the move lets it go. Other
    /// players are told of the aim, of the throw starting and of the release, so their copy of this body draws, aims and throws it too.
    /// </summary>
    public sealed class BoomerangThrower : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "boomerang";

        /// <summary>
        /// The events (<see cref="AbilityEvent.Kind"/>): Thrown (Origin the hand, Direction its way, A its speed, N 1 for the giant one), TurnedBack (A 1 if a
        /// person did it), Caught, GiantAim (A 1 drawn into the aim, 0 put away) and GiantThrowStarted.
        /// </summary>
        public const int Thrown = 0;

        public const int TurnedBack = 1;

        public const int Caught = 2;

        public const int GiantAim = 3;

        public const int GiantThrowStarted = 4;

        /// <summary>The giant one spins about its middle, held near its end: it leaves the hand this far ahead of it, metres.</summary>
        public const float GiantMiddleFromHand = 1.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>Raised when it leaves or comes back, for the hand to show it or not.</summary>
        public event System.Action OutChanged;

        [Tooltip("The boomerang in the air, here and for another player's throw. Set by PlayerAbilityWiring.")]
        public GameObject ThrownPrefab;

        [Tooltip("The Giant Boomerang in the air, here and for another player's throw. Set by PlayerAbilityWiring.")]
        public GameObject GiantThrownPrefab;

        private ThrownBoomerang _flying;
        private float _thrownAt = float.NegativeInfinity;
        private GiantBoomerangPoseDriver _moves;
        private bool _giantThrowPending;
        private Vector3 _pendingWay;
        private float _pendingDamage;
        private float _pendingPenetration;

        /// <summary>A boomerang of this body's is in the air.</summary>
        public bool IsOut => _flying != null;

        public ThrownBoomerang Flying => _flying;

        /// <summary>It can be thrown now: the last one is home, and its use time is over.</summary>
        public bool CanThrow => !IsOut && Time.time - _thrownAt >= BoomerangRules.ThrowEverySeconds;

        /// <summary>The Giant Boomerang's moves on this body (made the first time they are wanted).</summary>
        public GiantBoomerangPoseDriver GiantMoves
        {
            get
            {
                if (_moves == null)
                {
                    _moves = GetComponent<GiantBoomerangPoseDriver>();
                    if (_moves == null)
                    {
                        _moves = gameObject.AddComponent<GiantBoomerangPoseDriver>();
                    }
                    _moves.Released += GiantReleased;
                }
                return _moves;
            }
        }

        /// <summary>The Giant Boomerang is drawn into the aim, or being drawn.</summary>
        public bool GiantAiming => _moves != null && _moves.IsAiming;

        /// <summary>This body throws its boomerang: the arm swings, it flies, and other players are told. Null if one is out already.</summary>
        public ThrownBoomerang Throw(Vector3 from, Vector3 way, float damage, float penetration)
        {
            if (!CanThrow)
            {
                return null;
            }
            return LaunchAndTell(from, way, damage, penetration, false);
        }

        /// <summary>Right-click held or let go with the Giant Boomerang: it is drawn into the aim, or put back.</summary>
        public void SetGiantAiming(bool aiming)
        {
            if (aiming == GiantAiming || (aiming && IsOut))
            {
                return;
            }
            GiantMoves.Aim(aiming);
            Fired?.Invoke(new AbilityEvent(GiantAim, transform.position, Vector3.zero, aiming ? 1.0f : 0.0f));
        }

        /// <summary>A click with the Giant Boomerang: the throw starts (from the aim, or straight off the back), and it leaves the hand when the move lets it go.</summary>
        public bool StartGiantThrow(Vector3 way, float damage, float penetration)
        {
            if (!CanThrow || GiantMoves.IsThrowing || _giantThrowPending)
            {
                return false;
            }
            if (!GiantMoves.Throw())
            {
                return false;
            }
            _giantThrowPending = true;
            _pendingWay = way.normalized;
            _pendingDamage = damage;
            _pendingPenetration = penetration;
            Fired?.Invoke(new AbilityEvent(GiantThrowStarted, transform.position, _pendingWay));
            return true;
        }

        /// <summary>Puts the Giant Boomerang's moves away at once (another weapon taken out): a throw not yet let go is not thrown.</summary>
        public void StopGiantMoves()
        {
            _giantThrowPending = false;
            if (_moves != null)
            {
                _moves.Stop();
            }
        }

        private void GiantReleased()
        {
            if (!_giantThrowPending)
            {
                return;   // another player's copy: their own word of the throw brings it out
            }
            _giantThrowPending = false;
            LaunchAndTell(GiantLeavesFrom(_pendingWay), _pendingWay, _pendingDamage, _pendingPenetration, true);
        }

        /// <summary>Where the giant one's middle is as it leaves the hand.</summary>
        private Vector3 GiantLeavesFrom(Vector3 way)
        {
            Animator animator = GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Vector3 at = hand != null ? hand.position : transform.position + Vector3.up * (BodyRig.StandingHeight * 0.7f);
            return at + way * GiantMiddleFromHand;
        }

        private ThrownBoomerang LaunchAndTell(Vector3 from, Vector3 way, float damage, float penetration, bool giant)
        {
            float speed = giant ? GiantBoomerangRules.OutSpeed : BoomerangRules.OutSpeed;
            ThrownBoomerang boomerang = Launch(from, way, speed, damage, penetration, false, giant);
            if (boomerang == null)
            {
                return null;
            }
            boomerang.TurnedBack += person => Fired?.Invoke(new AbilityEvent(TurnedBack, boomerang.transform.position, Vector3.zero, person ? 1.0f : 0.0f));
            boomerang.Gone += () => Fired?.Invoke(new AbilityEvent(Caught, transform.position, Vector3.zero));
            Fired?.Invoke(new AbilityEvent(Thrown, from, way.normalized, speed, 0.0f, giant ? 1 : 0));
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
                        Launch(evt.Origin, evt.Direction, Mathf.Min(evt.A, BoomerangRules.OutSpeed * 2.0f), 0.0f, 0.0f, true, evt.N == 1);
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
                case GiantAim:
                    GiantMoves.Aim(evt.A > 0.5f);
                    break;
                case GiantThrowStarted:
                    GiantMoves.Throw();
                    break;
            }
        }

        private ThrownBoomerang Launch(Vector3 from, Vector3 way, float speed, float damage, float penetration, bool copy, bool giant)
        {
            GameObject prefab = giant && GiantThrownPrefab != null ? GiantThrownPrefab : ThrownPrefab;
            ThrownBoomerang boomerang = ThrownBoomerang.Spawn(prefab, from, way, speed, gameObject, damage, penetration, copy);
            if (boomerang == null)
            {
                return null;
            }
            _flying = boomerang;
            _thrownAt = Time.time;
            boomerang.Gone += () => Landed(boomerang);
            if (!giant)
            {
                GetComponent<PlayerLocomotion>()?.PlayThrowSwing(way);   // the giant one's throw is its own move
            }
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
