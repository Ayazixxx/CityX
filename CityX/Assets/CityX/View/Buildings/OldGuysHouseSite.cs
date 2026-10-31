using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Where the tutorial house stands (docs/OLD_GUYS_HOUSE.md): builds it under this transform (OldGuysHouseBuilder), sets the weapons down where the
    /// plan puts them, and puts the one swordsman in Old Guy's bedroom once this machine's player comes near. In the city the streamer moves it onto
    /// the land the plan keeps for it beside the spawn (<see cref="PlaceOn"/>, Plan.TutorialLot). The weapons are this machine's own (the tutorial
    /// is played alone), held exactly where they were set down until picked up with E.
    /// </summary>
    public sealed class OldGuysHouseSite : MonoBehaviour
    {
        [Tooltip("The city's wall material (CityConcrete): every surface of the house is tinted in it.")]
        public Material Structure;

        [Tooltip("See-through window glass.")]
        public Material Glass;

        [Tooltip("The swordsman upstairs (owner: \"maybe just one swordsman upstairs\").")]
        public GameObject SwordsmanPrefab;

        public bool PlaceItems = true;

        public bool SpawnSwordsman = true;

        [Tooltip("How near this machine's player comes before the swordsman is put down. In a session he is spawned for everyone, run by that " +
                 "player's machine (EnemyNetwork), which lets a player put an enemy down only near themselves.")]
        public float SwordsmanWakeMetres = 30.0f;

        [Tooltip("How far below the floor the footing under the lot reaches (set from the ground under it when placed in the city).")]
        public float FootingDepth;

        public GameObject Built { get; private set; }

        /// <summary>The swordsman put down on this machine alone (null in a session, where the server spawns him, or before he is due).</summary>
        public GameObject Swordsman { get; private set; }

        /// <summary>The weapons set down, by item id.</summary>
        public readonly Dictionary<string, DroppedItem> Items = new Dictionary<string, DroppedItem>();

        private bool _swordsmanDue;
        private Transform _player;

        /// <summary>The footing reaches this far under the lowest ground, so its foot is never seen.</summary>
        private const float FootingIntoGround = 0.3f;

        private void Start()
        {
            Build();
        }

        /// <summary>
        /// Stands the house on its land in the city: the frame's origin at the floor's height, turned so its front faces the street. Built afresh if it
        /// was already built somewhere else (a new world seed).
        /// </summary>
        public void PlaceOn(in TutorialLot lot)
        {
            bool wasBuilt = Built != null;
            Clear();
            transform.SetPositionAndRotation(new Vector3((float)lot.OriginX, (float)lot.FloorHeight, (float)lot.OriginZ),
                Quaternion.Euler(0.0f, Mathf.Atan2((float)-lot.AxisZ, (float)lot.AxisX) * Mathf.Rad2Deg, 0.0f));
            FootingDepth = (float)(lot.FloorHeight - lot.LowestGround) + FootingIntoGround;
            if (wasBuilt)
            {
                Build();
            }
        }

        public void Build()
        {
            if (Built != null)
            {
                return;
            }
            Built = OldGuysHouseBuilder.Build(transform, new OldGuysHouseBuilder.Materials { Structure = Structure, Glass = Glass }, FootingDepth);
            Physics.SyncTransforms();
            if (PlaceItems)
            {
                foreach (OldGuysHouse.ItemSpot spot in OldGuysHouse.Items)
                {
                    var drop = new ItemDrop
                    {
                        ItemId = spot.ItemId,
                        Data = ItemInstanceData.None,
                        Position = transform.TransformPoint(spot.Position),
                        Rotation = transform.rotation * Quaternion.Euler(spot.Euler),
                        Placed = true,
                    };
                    DroppedItem item = DroppedItems.Shared.Drop(drop, false);
                    if (item != null)
                    {
                        Items[spot.ItemId] = item;
                    }
                }
            }
            _swordsmanDue = SpawnSwordsman && SwordsmanPrefab != null;
        }

        private void Update()
        {
            if (!_swordsmanDue)
            {
                return;
            }
            if (_player == null)
            {
                PlayerInputReader reader = FindFirstObjectByType<PlayerInputReader>();
                if (reader == null)
                {
                    return;
                }
                _player = reader.transform;
            }
            if ((_player.position - transform.TransformPoint(OldGuysHouse.SwordsmanSpot)).sqrMagnitude <= SwordsmanWakeMetres * SwordsmanWakeMetres)
            {
                PutDownSwordsman();
            }
        }

        /// <summary>The swordsman in Old Guy's bedroom: in a session spawned for everyone and run by this machine, otherwise here alone.</summary>
        public void PutDownSwordsman()
        {
            _swordsmanDue = false;
            Vector3 where = transform.TransformPoint(OldGuysHouse.SwordsmanSpot);
            Quaternion facing = transform.rotation * Quaternion.Euler(0, OldGuysHouse.SwordsmanYaw, 0);
            EnemyNetwork network = EnemyNetwork.Current;
            if (network == null || !network.TrySpawn("swordsman", where, facing))
            {
                Swordsman = Instantiate(SwordsmanPrefab, where, facing);
            }
        }

        private void Clear()
        {
            foreach (DroppedItem item in Items.Values)
            {
                if (item != null)
                {
                    DroppedItems.Shared.Remove(item.Id);
                }
            }
            Items.Clear();
            if (Swordsman != null)
            {
                Destroy(Swordsman);
                Swordsman = null;
            }
            if (Built != null)
            {
                Destroy(Built);
                Built = null;
            }
            _swordsmanDue = false;
        }
    }
}
