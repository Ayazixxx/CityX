using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Where the tutorial house stands (docs/OLD_GUYS_HOUSE.md): builds it under this transform (OldGuysHouseBuilder), sets the five weapons
    /// down where the plan puts them, and puts the one swordsman in Old Guy's bedroom. The weapons are this machine's own (the tutorial is
    /// played alone), held exactly where they were set down until picked up with E.
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

        public GameObject Built { get; private set; }

        public GameObject Swordsman { get; private set; }

        /// <summary>The weapons set down, by item id.</summary>
        public readonly Dictionary<string, DroppedItem> Items = new Dictionary<string, DroppedItem>();

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            if (Built != null)
            {
                return;
            }
            Built = OldGuysHouseBuilder.Build(transform, new OldGuysHouseBuilder.Materials { Structure = Structure, Glass = Glass });
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
            if (SpawnSwordsman && SwordsmanPrefab != null)
            {
                Swordsman = Instantiate(SwordsmanPrefab, transform.TransformPoint(OldGuysHouse.SwordsmanSpot),
                    transform.rotation * Quaternion.Euler(0, OldGuysHouse.SwordsmanYaw, 0));
            }
        }
    }
}
