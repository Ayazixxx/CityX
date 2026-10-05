using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Makes the player's abilities visible to every other player. It finds every <see cref="IReplicatedAbility"/> on the body, and when the
    /// OWNER's copy raises one's <c>Fired</c> it sends the event to the server, which relays it to every other player who can see this body
    /// (FishNet's observers: the 100 m view distance applies, so a fireball cast across the city is not sent to the far side of it); each of
    /// those copies is then told to show it (<see cref="IReplicatedAbility.PlayRemote"/>).
    ///
    /// ONE PAIR OF RPCs FOR ALL ABILITIES, keyed by a hash of the ability's id, so a new ability costs no networking code at all: implement
    /// IReplicatedAbility and be on the body. Same shape as NetworkedAnimatorSync's swings: reliable, ExcludeOwner (the owner already did it),
    /// visual only -- the receiving copies never run the ability's damage or cooldown.
    ///
    /// THE SERVER DOES NOT TRUST WHAT IT RELAYS: numbers must be finite and sane and the origin must be near where the server has this body,
    /// so one client cannot make everyone's screen fill with fireballs spawned across the map or crash a peer with a NaN.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedAbilitySync : NetworkBehaviour
    {
        /// <summary>How far from where the server has the caster an event's origin may be, in metres (a jetpack is fast and the transform lags).</summary>
        public const float MaxOriginDistanceMetres = 60.0f;

        public const float MaxSpeedOrDuration = 1000.0f;

        public const int MaxCount = 64;

        private readonly Dictionary<int, IReplicatedAbility> _byHash = new Dictionary<int, IReplicatedAbility>();
        private readonly List<IReplicatedAbility> _abilities = new List<IReplicatedAbility>();
        private readonly List<System.Action<AbilityEvent>> _handlers = new List<System.Action<AbilityEvent>>();
        private bool _subscribed;

        private void Awake()
        {
            Gather();
        }

        private void OnEnable()
        {
            Gather();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>Finds the abilities on this body. Idempotent.</summary>
        public void Gather()
        {
            _byHash.Clear();
            _abilities.Clear();
            foreach (IReplicatedAbility ability in GetComponents<IReplicatedAbility>())
            {
                _abilities.Add(ability);
                _byHash[AbilityIds.Hash(ability.AbilityId)] = ability;
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }
            _subscribed = true;
            _handlers.Clear();
            foreach (IReplicatedAbility ability in _abilities)
            {
                IReplicatedAbility captured = ability;
                System.Action<AbilityEvent> handler = evt => OnFired(captured, evt);
                _handlers.Add(handler);
                ability.Fired += handler;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }
            _subscribed = false;
            for (int i = 0; i < _abilities.Count && i < _handlers.Count; i++)
            {
                _abilities[i].Fired -= _handlers[i];
            }
            _handlers.Clear();
        }

        /// <summary>
        /// True only for the live owner of a genuinely spawned body: the NetworkObject null check is load-bearing (a body with no session --
        /// singleplayer, the baked player before one starts -- has no initialised NetworkObject and FishNet's own IsSpawned/IsOwner throw).
        /// Same guard NetworkedAnimatorSync uses, for the same reason: these handlers run inside the abilities' own ticks.
        /// </summary>
        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private void OnFired(IReplicatedAbility ability, AbilityEvent evt)
        {
            if (!IsLiveOwner)
            {
                return;
            }
            SendAbilityToServer(AbilityIds.Hash(ability.AbilityId), evt.Kind, evt.Origin, evt.Direction, evt.A, evt.B, evt.N);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendAbilityToServer(int abilityHash, int kind, Vector3 origin, Vector3 direction, float a, float b, int n)
        {
            if (!IsPlausible(origin, direction, a, b, n, transform.position))
            {
                return;
            }
            BroadcastAbility(abilityHash, kind, origin, direction, a, b, n);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastAbility(int abilityHash, int kind, Vector3 origin, Vector3 direction, float a, float b, int n)
        {
            // Checked again here: a client does not trust the server's relay any further than the server trusted the sender.
            if (!IsPlausible(origin, direction, a, b, n, transform.position))
            {
                return;
            }
            Replay(abilityHash, new AbilityEvent(kind, origin, direction, a, b, n));
        }

        /// <summary>Shows an event on this body, as if it had just arrived over the network. False for an ability this body does not have.</summary>
        public bool Replay(int abilityHash, AbilityEvent evt)
        {
            if (_byHash.Count == 0)
            {
                Gather();
            }
            if (!_byHash.TryGetValue(abilityHash, out IReplicatedAbility ability))
            {
                return false;
            }
            ability.PlayRemote(evt);
            return true;
        }

        /// <summary>
        /// Whether an event off the wire is fit to show: every number finite and in range, the direction a direction (or nothing), and the origin
        /// near the caster's own position. Static so it can be tested without a network.
        /// </summary>
        public static bool IsPlausible(Vector3 origin, Vector3 direction, float a, float b, int n, Vector3 casterPosition)
        {
            if (!IsFinite(origin) || !IsFinite(direction) || !IsFinite(a) || !IsFinite(b))
            {
                return false;
            }
            if (Mathf.Abs(a) > MaxSpeedOrDuration || Mathf.Abs(b) > MaxSpeedOrDuration || n < 0 || n > MaxCount)
            {
                return false;
            }
            float length = direction.magnitude;
            if (length > 1.5f)
            {
                return false;
            }
            return Vector3.Distance(origin, casterPosition) <= MaxOriginDistanceMetres;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }
}
