using System.Collections.Generic;
using CityX.Items;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a player is HOLDING (sword, pistol, bottle, flamethrower, grappling hook) or WEARING ON THE BACK (jetpack, parachute), seen on every other
    /// player's copy of the body. Before this the animation said "sword equipped" but nothing was in anyone's hand: equipment was not networked at all.
    ///
    /// The owner sends the list of item ids that currently exist as objects, whenever it changes (<see cref="Inventory.EquipmentChanged"/>) and once
    /// when the body appears; the server relays it with BufferLast so a late joiner is told too. A copy reconciles: it builds a COSMETIC model of any item
    /// it is missing and removes any that are gone. A cosmetic model is the item's own prefab with every behaviour switched off after
    /// <see cref="ICosmeticMountable.MountCosmetic"/> put it on the right bone, so nothing about it touches input, the camera, the HUD, ammo or combat.
    ///
    /// The server only forwards a few ids from the registry; an unknown id is ignored by the copy.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedEquipmentSync : NetworkBehaviour
    {
        /// <summary>More ids than this in one announcement is not a real loadout.</summary>
        public const int MaxItems = 8;

        private Inventory _inventory;
        private Animator _animator;
        private readonly Dictionary<string, GameObject> _shown = new Dictionary<string, GameObject>();
        private bool _subscribed;

        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");

        /// <summary>The ids of the cosmetic models this copy currently shows. For tests.</summary>
        public IEnumerable<string> ShownItemIds => _shown.Keys;

        public GameObject ShownItem(string itemId) => _shown.TryGetValue(itemId, out GameObject go) ? go : null;

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (_subscribed && _inventory != null)
            {
                _inventory.EquipmentChanged -= OnEquipmentChanged;
            }
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory>();
            }
            if (_inventory != null)
            {
                _inventory.EquipmentChanged += OnEquipmentChanged;
                _subscribed = true;
            }
        }

        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private bool IsRemoteCopy => NetworkObject != null && IsSpawned && !IsOwner;

        public override void OnStartClient()
        {
            base.OnStartClient();
            Subscribe();
            if (IsOwner)
            {
                Announce();   // a late joiner is also covered by BufferLast, but the owner's first state must go out too
            }
        }

        public override void OnOwnershipClient(FishNet.Connection.NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
            {
                Announce();
            }
        }

        private void OnEquipmentChanged()
        {
            if (IsLiveOwner)
            {
                Announce();
            }
        }

        private void Announce()
        {
            if (_inventory == null || !IsLiveOwner)
            {
                return;
            }
            SendEquipmentToServer(_inventory.VisibleItemIds().ToArray());
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendEquipmentToServer(string[] itemIds)
        {
            if (itemIds == null || itemIds.Length > MaxItems)
            {
                return;
            }
            var clean = new List<string>();
            foreach (string id in itemIds)
            {
                if (!string.IsNullOrEmpty(id) && ItemRegistry.Has(id) && !clean.Contains(id))
                {
                    clean.Add(id);
                }
            }
            BroadcastEquipment(clean.ToArray());
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastEquipment(string[] itemIds)
        {
            Show(itemIds);
        }

        /// <summary>Makes this copy show exactly these items. Public so a test can drive it without a network.</summary>
        public void Show(IEnumerable<string> itemIds)
        {
            var wanted = new HashSet<string>(itemIds ?? new string[0]);
            var gone = new List<string>();
            foreach (KeyValuePair<string, GameObject> pair in _shown)
            {
                if (!wanted.Contains(pair.Key) || pair.Value == null)
                {
                    gone.Add(pair.Key);
                }
            }
            foreach (string id in gone)
            {
                Remove(id);
            }
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory>();
            }
            foreach (string id in wanted)
            {
                if (_shown.ContainsKey(id) || _inventory == null)
                {
                    continue;
                }
                GameObject prefab = _inventory.PrefabForItem(id);
                GameObject model = prefab != null ? Build(prefab) : null;
                if (model != null)
                {
                    _shown[id] = model;
                }
            }
        }

        private GameObject Build(GameObject prefab)
        {
            GameObject model = Instantiate(prefab);
            model.name = prefab.name + " (copy)";
            var mount = model.GetComponent<ICosmeticMountable>();
            if (mount == null)
            {
                Destroy(model);
                return null;
            }
            model.transform.SetParent(transform, false);
            mount.MountCosmetic(gameObject);

            // Nothing about a copy's item may act: no pickup, no collision, no physics, no scripts.
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;   // off at once (Destroy only takes effect at the end of the frame)
                Destroy(c);
            }
            foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
            }
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
            }
            foreach (AudioSource a in model.GetComponentsInChildren<AudioSource>(true))
            {
                a.Stop();
                a.enabled = false;
            }
            return model;
        }

        private void Remove(string itemId)
        {
            if (_shown.TryGetValue(itemId, out GameObject model) && model != null)
            {
                var mount = model.GetComponent<ICosmeticMountable>();
                mount?.UnmountCosmetic(gameObject);
                Destroy(model);
            }
            _shown.Remove(itemId);
        }

        private void OnDestroy()
        {
            foreach (GameObject model in _shown.Values)
            {
                if (model != null)
                {
                    Destroy(model);
                }
            }
            _shown.Clear();
        }

        private void Update()
        {
            // Things the wearer's state drives (the jetpack's flame while it thrusts).
            if (_shown.Count == 0 || !IsRemoteCopy || _animator == null)
            {
                return;
            }
            bool thrusting = _animator.GetBool(JetpackThrustingParameter);
            foreach (GameObject model in _shown.Values)
            {
                if (model != null)
                {
                    model.GetComponent<ICosmeticMountable>()?.SetCosmeticActive(thrusting);
                }
            }
        }
    }
}
