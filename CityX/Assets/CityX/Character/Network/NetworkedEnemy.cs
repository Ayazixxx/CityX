using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// An enemy every player in a session sees and fights (docs/MULTIPLAYER_STATE.md): the machine that brought it into the world OWNS it and
    /// runs its brain, as each player's machine runs their own body; every other machine holds a copy that only shows it. The copy is moved by
    /// the owner (NetworkTransform), plays what the owner's body plays (NetworkedAnimatorSync: walk, swings, guard, a shot, a reload, a tell),
    /// and a hit on it goes to the owner's real body (NetworkedDamage, or the Wraith's own relay), where it is decided.
    ///
    /// This component is the part every networked enemy shares: the brain is switched off on a copy (<see cref="OwnerOnly"/>); the owner's
    /// body dying is told to everyone, so each copy falls as it does (BufferLast: a player arriving later finds a corpse, not a man standing at
    /// health 0); and a body done with -- a corpse that has lain its time, a ghost faded away -- is taken out of the session by the server
    /// (EnemySession.Remove), on every machine at once. An enemy whose owner leaves goes with them (FishNet despawns what a connection owned).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedEnemy : NetworkBehaviour
    {
        [Tooltip("What only the owner runs: the brain. Switched off on every other machine's copy (set by the builder).")]
        public Behaviour[] OwnerOnly = System.Array.Empty<Behaviour>();

        private PlayerVitals _vitals;
        private bool _diedHere;

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            if (_vitals != null)
            {
                _vitals.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_vitals != null)
            {
                _vitals.Died -= OnDied;
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (Owner.IsLocalClient)   // IsOwner is not settled yet inside OnStartNetwork (FishNet)
            {
                return;
            }
            foreach (Behaviour behaviour in OwnerOnly)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        /// <summary>A copy here: it shows the enemy and decides nothing about it.</summary>
        public bool IsCopyHere => NetworkObject != null && IsSpawned && !IsOwner;

        /// <summary>In a running session (its drops are shared with everyone).</summary>
        public bool IsSharedHere => NetworkObject != null && IsSpawned;

        /// <summary>Has the server take it out of the session, on every machine (its owner asks; a copy waits for that).</summary>
        public void RemoveFromSession()
        {
            if (IsServerInitialized)
            {
                Despawn();
            }
            else if (IsOwner)
            {
                AskToBeRemoved();
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void AskToBeRemoved()
        {
            Despawn();
        }

        // ---- its death, shown on every copy ----

        private void OnDied()
        {
            if (_diedHere || NetworkObject == null || !IsSpawned || !IsOwner)
            {
                return;
            }
            _diedHere = true;
            var locomotion = GetComponent<PlayerLocomotion>();
            Vector3 fling = (locomotion != null ? locomotion.Velocity : Vector3.zero) + _vitals.DeathImpulse;
            SendDeathToServer(fling);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendDeathToServer(Vector3 fling)
        {
            BroadcastDeath(NetworkedDamage.SaneKnockdown(fling));
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastDeath(Vector3 fling)
        {
            if (_vitals == null || _diedHere)
            {
                return;
            }
            _diedHere = true;
            // The copy lies where it falls here: the owner's corpse no longer carries it.
            var networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }
            _vitals.DieFromNetwork(NetworkedDamage.SaneKnockdown(fling));
        }
    }
}
