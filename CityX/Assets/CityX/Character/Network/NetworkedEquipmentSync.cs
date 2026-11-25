using System.Collections.Generic;
using CityX.Core;
using CityX.Items;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a player is HOLDING (sword, pistol, bottle, flamethrower, grappling hook), WEARING ON THE BACK (jetpack, parachute) or WEARING (clothes,
    /// hats, helmets; since 2026-10-22), seen on every other player's copy of the body. Before this the animation said "sword equipped" but nothing was in anyone's hand: equipment was not networked at all.
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
        /// <summary>More ids than this in one announcement is not a real loadout (every worn slot, the back, a weapon in each hand, a shield).</summary>
        public const int MaxItems = 12;

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
            List<string> ids = _inventory.VisibleItemIds();
            foreach (string id in ids)
            {
                Inventory.ShownDesign(id, out string designId);
                if (designId.Length > 0)
                {
                    DesignLibrary.Share(designId);   // before the copies are told: they will ask the server for it
                }
            }
            SendEquipmentToServer(ids.ToArray());
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
                string item = Inventory.ShownDesign(id, out string designId);   // a printed shirt's id carries its design's
                Inventory.ShownAsPair(item, out string itemId);   // a left-hand pistol's id is its item's, marked
                bool designOk = designId.Length == 0 || DesignStore.IsValidId(designId);
                if (!string.IsNullOrEmpty(itemId) && ItemRegistry.Has(itemId) && designOk && !clean.Contains(id))
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
                string item = Inventory.ShownDesign(id, out string designId);   // a printed shirt (docs/HEAT_PRESS.md)
                bool leftHand = Inventory.ShownAsPair(item, out string itemId);   // the second of a pair of pistols (docs/DUAL_PISTOLS.md)
                GameObject prefab = _inventory.PrefabForItem(itemId);
                GameObject model = prefab != null ? Build(prefab, leftHand) : null;
                if (model != null && designId.Length > 0)
                {
                    model.GetComponent<IPrintable>()?.ShowDesign(designId);
                }
                if (model != null)
                {
                    _shown[id] = model;
                }
            }
        }

        private GameObject Build(GameObject prefab, bool leftHand)
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
            if (leftHand && model.GetComponent<IOffHandItem>() is IOffHandItem second)
            {
                second.MountCosmeticOffHand(gameObject);
            }
            else
            {
                mount.MountCosmetic(gameObject);
            }

            // Nothing about a copy's item may act: no pickup, no collision, no physics, no scripts -- but clothing's own upkeep of the body.
            InertModel.Strip(model);
            foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>())
            {
                if (behaviour is ICosmeticKeepsRunning)
                {
                    behaviour.enabled = true;
                }
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
